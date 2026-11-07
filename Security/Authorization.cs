using System.Security.Claims;
using Lumine.Backend.Errors;

namespace Lumine.Backend.Security;

public static class Roles
{
    public const string Admin = "admin";
}

public static class Policies
{
    /// <summary>Signed in with an email listed in <c>AdminEmails</c>.</summary>
    public const string Admin = "Admin";
}

public static class RateLimitPolicies
{
    /// <summary>Sign-in and other auth calls: generous, but stops password guessing.</summary>
    public const string Auth = "auth";

    /// <summary>Endpoints that send an email: tight, to prevent inbox spam.</summary>
    public const string EmailCode = "email-code";
}

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : throw ApiException.Unauthorized("Please log in to continue.");

    public static string GetEmail(this ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.Email) is { Length: > 0 } email
            ? email
            : throw ApiException.Unauthorized("Please log in to continue.");

    public static bool IsAdmin(this ClaimsPrincipal user) => user.IsInRole(Roles.Admin);

    /// <summary>Only the account owner (or an admin) may read or change an account's data.</summary>
    public static void EnsureCanAccess(this ClaimsPrincipal user, Guid accountId)
    {
        if (user.GetUserId() != accountId && !user.IsAdmin())
        {
            throw ApiException.Forbidden("You can only access your own account.");
        }
    }
}
