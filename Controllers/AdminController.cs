using Lumine.Backend.Contracts;
using Lumine.Backend.Errors;
using Lumine.Backend.Security;
using Lumine.Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Lumine.Backend.Controllers;

/// <summary>Account management for the admin panel. Admin only.</summary>
[ApiController]
[Route("api/admin/users")]
[Authorize(Policy = Policies.Admin)]
public sealed class AdminController(IAdminUserService adminUserService, IProfileService profileService) : ControllerBase
{
    /// <summary>Lists every account with its profile.</summary>
    [HttpGet]
    public Task<IReadOnlyList<AdminUserInfo>> List(CancellationToken cancellationToken) =>
        adminUserService.ListAsync(cancellationToken);

    /// <summary>Gets one account with its profile.</summary>
    /// <response code="404">No account with this id.</response>
    [HttpGet("{userId:guid}")]
    public Task<AdminUserInfo> Get(Guid userId, CancellationToken cancellationToken) =>
        adminUserService.GetAsync(userId, cancellationToken);

    /// <summary>Permanently removes an account with its profile, favorites and photo.</summary>
    /// <response code="400">Admins can't remove their own account.</response>
    [HttpDelete("{userId:guid}")]
    public async Task<MessageResponse> Delete(Guid userId, CancellationToken cancellationToken)
    {
        if (userId == User.GetUserId())
        {
            throw ApiException.BadRequest("You can't remove your own account.");
        }

        await adminUserService.DeleteAsync(userId, cancellationToken);
        return new MessageResponse("Account removed.");
    }

    /// <summary>Ids of the jewelry an account saved.</summary>
    [HttpGet("{userId:guid}/favorites")]
    public Task<IReadOnlyList<string>> GetFavorites(Guid userId, CancellationToken cancellationToken) =>
        profileService.GetFavoriteIdsAsync(userId, cancellationToken);
}
