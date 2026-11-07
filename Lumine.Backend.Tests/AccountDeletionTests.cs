using System.Net;
using System.Net.Http.Json;

namespace Lumine.Backend.Tests;

/// <summary>Self-service account deletion, and the public pages Google Play links to.</summary>
public sealed class AccountDeletionTests(LumineApiFactory factory) : IClassFixture<LumineApiFactory>
{
    private const string DeletePath = "/api/account/delete";

    [Fact]
    public async Task Deleting_requires_signing_in()
    {
        var response = await factory.CreateClientFor(account: null)
            .PostAsJsonAsync(DeletePath, new { password = "anything" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Users_delete_the_account_their_token_belongs_to()
    {
        var response = await factory.CreateClientFor(TestAccounts.User)
            .PostAsJsonAsync(DeletePath, new { password = FakeAuthService.CorrectPassword });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains((TestAccounts.User.Id, TestAccounts.User.Email), factory.Accounts.Deleted);
    }

    [Fact]
    public async Task Deleting_needs_the_password()
    {
        var response = await factory.CreateClientFor(TestAccounts.OtherUser)
            .PostAsJsonAsync(DeletePath, new { });

        // 400, never 401: the app treats 401 as an expired session.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await ApiAssert.HasErrorMessageAsync(response);
        Assert.DoesNotContain(factory.Accounts.Deleted, deleted => deleted.UserId == TestAccounts.OtherUser.Id);
    }

    [Fact]
    public async Task Admin_accounts_cannot_be_deleted_from_the_app()
    {
        var response = await factory.CreateClientFor(TestAccounts.Admin)
            .PostAsJsonAsync(DeletePath, new { password = FakeAuthService.CorrectPassword });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await ApiAssert.HasErrorMessageAsync(response);
        Assert.DoesNotContain(factory.Accounts.Deleted, deleted => deleted.UserId == TestAccounts.Admin.Id);
    }

    [Theory]
    [InlineData("/privacy", "Privacy Policy")]
    [InlineData("/delete-account", "Delete your Lumine account")]
    public async Task Policy_pages_are_public_html(string path, string expectedText)
    {
        var response = await factory.CreateClientFor(account: null).GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains(expectedText, await response.Content.ReadAsStringAsync());
    }
}
