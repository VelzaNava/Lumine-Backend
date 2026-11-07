using System.Net;
using System.Net.Http.Json;

namespace Lumine.Backend.Tests;

/// <summary>Who may call what: public, signed-in, owner-only and admin-only endpoints.</summary>
public sealed class AccessRulesTests(LumineApiFactory factory) : IClassFixture<LumineApiFactory>
{
    private static readonly string UserProfile = $"/api/profile/{TestAccounts.User.Id}";
    private static readonly string OtherUserProfile = $"/api/profile/{TestAccounts.OtherUser.Id}";
    private static readonly string Ring = $"/api/jewelry/{FakeJewelryService.Ring.Id}";

    [Theory]
    [InlineData("/health")]
    [InlineData("/api/jewelry")]
    [InlineData("/api/jewelry/99a38e9f-5573-4c26-a453-98183f78c539")]
    public async Task Public_endpoints_work_without_signing_in(string path)
    {
        var response = await factory.CreateClientFor(account: null).GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("GET", "/api/admin/users")]
    [InlineData("GET", "/api/evaluation/summary")]
    [InlineData("GET", "/api/profile/11111111-1111-4111-8111-111111111111")]
    [InlineData("POST", "/api/jewelry")]
    [InlineData("POST", "/api/evaluation")]
    public async Task Protected_endpoints_reject_anonymous_requests(string method, string path)
    {
        var response = await factory.CreateClientFor(account: null).SendAsync(Request(method, path));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await ApiAssert.HasErrorMessageAsync(response);
    }

    [Fact]
    public async Task Invalid_tokens_are_rejected()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", "not-a-real-token");

        var response = await client.GetAsync(UserProfile);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("GET", "/api/admin/users")]
    [InlineData("GET", "/api/evaluation/summary")]
    [InlineData("GET", "/api/evaluation/all")]
    [InlineData("DELETE", "/api/admin/users/22222222-2222-4222-8222-222222222222")]
    [InlineData("DELETE", "/api/jewelry/99a38e9f-5573-4c26-a453-98183f78c539")]
    [InlineData("PUT", "/api/jewelry/99a38e9f-5573-4c26-a453-98183f78c539")]
    public async Task Admin_endpoints_are_forbidden_for_regular_users(string method, string path)
    {
        var response = await factory.CreateClientFor(TestAccounts.User).SendAsync(Request(method, path));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await ApiAssert.HasErrorMessageAsync(response);
    }

    [Theory]
    [InlineData("GET", "/api/admin/users", HttpStatusCode.OK)]
    [InlineData("GET", "/api/evaluation/summary", HttpStatusCode.OK)]
    [InlineData("DELETE", "/api/admin/users/22222222-2222-4222-8222-222222222222", HttpStatusCode.OK)]
    [InlineData("DELETE", "/api/jewelry/99a38e9f-5573-4c26-a453-98183f78c539", HttpStatusCode.NoContent)]
    public async Task Admin_endpoints_work_for_admins(string method, string path, HttpStatusCode expected)
    {
        var response = await factory.CreateClientFor(TestAccounts.Admin).SendAsync(Request(method, path));

        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    public async Task Admins_can_add_jewelry()
    {
        var response = await factory.CreateClientFor(TestAccounts.Admin)
            .PostAsJsonAsync("/api/jewelry", ValidJewelry());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
    }

    [Fact]
    public async Task Admins_cannot_remove_their_own_account()
    {
        var response = await factory.CreateClientFor(TestAccounts.Admin)
            .DeleteAsync($"/api/admin/users/{TestAccounts.Admin.Id}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await ApiAssert.HasErrorMessageAsync(response);
    }

    [Fact]
    public async Task Users_can_read_their_own_profile()
    {
        var response = await factory.CreateClientFor(TestAccounts.User).GetAsync(UserProfile);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("GET", "")]
    [InlineData("PUT", "")]
    [InlineData("GET", "/favorites")]
    [InlineData("POST", "/favorites/99a38e9f-5573-4c26-a453-98183f78c539")]
    [InlineData("DELETE", "/favorites/99a38e9f-5573-4c26-a453-98183f78c539")]
    public async Task Users_cannot_touch_someone_elses_account(string method, string suffix)
    {
        var response = await factory.CreateClientFor(TestAccounts.User)
            .SendAsync(Request(method, OtherUserProfile + suffix));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await ApiAssert.HasErrorMessageAsync(response);
    }

    [Fact]
    public async Task Admins_can_read_any_profile()
    {
        var response = await factory.CreateClientFor(TestAccounts.Admin).GetAsync(OtherUserProfile);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Ratings_are_recorded_for_the_signed_in_user_not_the_user_in_the_body()
    {
        var response = await factory.CreateClientFor(TestAccounts.User).PostAsJsonAsync("/api/evaluation", new
        {
            userId = TestAccounts.OtherUser.Id,
            jewelryId = FakeJewelryService.Ring.Id,
            jewelryName = "Two Toned Ring",
            rating = 5,
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(TestAccounts.User.Id, factory.Evaluations.LastSubmittedBy);
    }

    [Fact]
    public async Task Catalog_json_uses_the_field_names_the_app_expects()
    {
        var items = await factory.CreateClientFor(account: null)
            .GetFromJsonAsync<List<Dictionary<string, object>>>("/api/jewelry");

        Assert.NotNull(items);
        Assert.Equal(
            ["id", "createdAt", "name", "type", "material", "price", "imageUrl", "imageUrls", "modelUrl",
             "description", "isAvailable", "isArEnabled", "stockQuantity"],
            items[0].Keys);
    }

    private static HttpRequestMessage Request(string method, string path)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (method is "POST" or "PUT")
        {
            request.Content = JsonContent.Create(path.Contains("jewelry") && !path.Contains("favorites")
                ? ValidJewelry()
                : new { });
        }

        return request;
    }

    private static object ValidJewelry() => new
    {
        name = "Test Ring",
        type = "ring",
        material = "Gold",
        price = 1500.0,
        isAvailable = true,
    };
}
