using Lumine.Backend.Contracts;
using Lumine.Backend.Errors;
using Lumine.Backend.Security;
using Lumine.Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Lumine.Backend.Controllers;

/// <summary>The signed-in user's own account. Acts only on the account the token belongs to.</summary>
[ApiController]
[Route("api/account")]
[Authorize]
public sealed class AccountController(IAccountService accountService) : ControllerBase
{
    /// <summary>
    /// Permanently deletes the signed-in account with its profile, photo and
    /// favorites. Ratings are kept without any link to the account.
    /// </summary>
    /// <response code="400">Wrong password.</response>
    /// <response code="403">Admin accounts can't be deleted this way.</response>
    [HttpPost("delete")]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    public async Task<MessageResponse> Delete(DeleteAccountRequest request, CancellationToken cancellationToken)
    {
        // Losing the admin account would lock everyone out of the admin panel.
        if (User.IsAdmin())
        {
            throw ApiException.Forbidden("Admin accounts can't be deleted from the app.");
        }

        await accountService.DeleteOwnAccountAsync(User.GetUserId(), User.GetEmail(), request.Password, cancellationToken);
        return new MessageResponse("Your account has been deleted.");
    }
}
