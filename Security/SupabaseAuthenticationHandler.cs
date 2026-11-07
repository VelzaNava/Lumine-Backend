using System.Security.Claims;
using System.Text.Encodings.Web;
using Lumine.Backend.Configuration;
using Lumine.Backend.Errors;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Lumine.Backend.Security;

/// <summary>
/// Authenticates requests carrying <c>Authorization: Bearer &lt;access token&gt;</c>
/// issued by Supabase, and marks accounts listed in <c>AdminEmails</c> with the
/// admin role.
/// </summary>
internal sealed class SupabaseAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory loggerFactory,
    UrlEncoder encoder,
    IAccessTokenValidator tokenValidator,
    AdminDirectory adminDirectory)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, loggerFactory, encoder)
{
    public const string SchemeName = "Supabase";

    private const string BearerPrefix = "Bearer ";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers.Authorization.ToString();
        if (!header.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticateResult.NoResult();
        }

        var token = header[BearerPrefix.Length..].Trim();
        if (token.Length == 0)
        {
            return AuthenticateResult.NoResult();
        }

        var user = await tokenValidator.ValidateAsync(token, Context.RequestAborted);
        if (user is null)
        {
            return AuthenticateResult.Fail("Invalid or expired access token.");
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Email, user.Email),
        };

        if (adminDirectory.IsAdmin(user.Email))
        {
            claims.Add(new Claim(ClaimTypes.Role, Roles.Admin));
        }

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));
        return AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName));
    }

    /// <summary>401 with the standard error body, so the app can refresh the session.</summary>
    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.Headers.WWWAuthenticate = "Bearer";
        await Response.WriteAsJsonAsync(new ErrorResponse("Please log in to continue."));
    }

    protected override async Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        await Response.WriteAsJsonAsync(new ErrorResponse("You don't have permission to do that."));
    }
}
