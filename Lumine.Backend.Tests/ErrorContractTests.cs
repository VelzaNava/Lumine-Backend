using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Lumine.Backend.Tests;

/// <summary>Every failure comes back as <c>{ "error": "..." }</c> with the status code the app relies on.</summary>
public sealed class ErrorContractTests(LumineApiFactory factory) : IClassFixture<LumineApiFactory>
{
    [Fact]
    public async Task Validation_failures_return_a_readable_message()
    {
        var response = await factory.CreateClientFor(account: null)
            .PostAsJsonAsync("/api/auth/login", new { email = "not-an-email", password = "x" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Email", await ApiAssert.HasErrorMessageAsync(response));
    }

    [Fact]
    public async Task Malformed_json_returns_a_generic_message_without_parser_details()
    {
        var response = await factory.CreateClientFor(account: null).PostAsync("/api/auth/login",
            new StringContent("{ not json", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("The request is invalid.", await ApiAssert.HasErrorMessageAsync(response));
    }

    [Fact]
    public async Task Wrong_password_returns_401_and_when_to_retry()
    {
        var response = await factory.CreateClientFor(account: null)
            .PostAsJsonAsync("/api/auth/login", new { email = "user@lumine.test", password = "wrong" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(TimeSpan.FromSeconds(10), response.Headers.RetryAfter?.Delta);
        Assert.Contains("10 seconds", await ApiAssert.HasErrorMessageAsync(response));
    }

    [Fact]
    public async Task Successful_login_returns_the_session_fields_the_app_reads()
    {
        var response = await factory.CreateClientFor(account: null).PostAsJsonAsync("/api/auth/login",
            new { email = "user@lumine.test", password = FakeAuthService.CorrectPassword });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        foreach (var field in (string[])["accessToken", "refreshToken", "email", "userId", "isAdmin"])
        {
            Assert.True(body.RootElement.TryGetProperty(field, out _), $"missing '{field}'");
        }
    }

    [Fact]
    public async Task Unknown_jewelry_returns_404()
    {
        var response = await factory.CreateClientFor(account: null).GetAsync($"/api/jewelry/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await ApiAssert.HasErrorMessageAsync(response);
    }

    [Fact]
    public async Task Jewelry_without_a_name_is_rejected()
    {
        var response = await factory.CreateClientFor(TestAccounts.Admin)
            .PostAsJsonAsync("/api/jewelry", new { name = "", type = "ring", price = 100 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Name", await ApiAssert.HasErrorMessageAsync(response));
    }

    [Fact]
    public async Task Server_managed_fields_from_older_app_versions_are_ignored()
    {
        // The admin form sends id = "" and createdAt = "" for new items.
        var response = await factory.CreateClientFor(TestAccounts.Admin).PostAsJsonAsync("/api/jewelry",
            new { id = "", createdAt = "", name = "Ring", type = "ring", material = "Gold", price = 100, isAvailable = true });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Items_can_have_at_most_three_photos()
    {
        var response = await factory.CreateClientFor(TestAccounts.Admin).PostAsJsonAsync("/api/jewelry", new
        {
            name = "Ring",
            type = "ring",
            price = 100,
            imageUrls = new[] { "https://a.test/1.jpg", "https://a.test/2.jpg", "https://a.test/3.jpg", "https://a.test/4.jpg" },
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("You can add up to 3 photos per item.", await ApiAssert.HasErrorMessageAsync(response));
    }

    [Fact]
    public async Task Every_response_reports_its_server_time()
    {
        var response = await factory.CreateClientFor(account: null).GetAsync("/api/jewelry");

        Assert.True(response.Headers.TryGetValues("Server-Timing", out var values));
        Assert.StartsWith("app;dur=", values.Single());
    }

    [Fact]
    public async Task Ratings_outside_one_to_five_are_rejected()
    {
        var response = await factory.CreateClientFor(TestAccounts.User)
            .PostAsJsonAsync("/api/evaluation", new { jewelryId = "abc", rating = 9 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Rating", await ApiAssert.HasErrorMessageAsync(response));
    }
}

internal static class ApiAssert
{
    /// <summary>Asserts the body is <c>{ "error": "&lt;non-empty&gt;" }</c> and returns the message.</summary>
    public static async Task<string> HasErrorMessageAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        Assert.True(document.RootElement.TryGetProperty("error", out var error), $"no 'error' field in: {body}");

        var message = error.GetString();
        Assert.False(string.IsNullOrWhiteSpace(message));
        return message!;
    }
}
