using Lumine.Backend.Contracts;
using Lumine.Backend.Security;
using Lumine.Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Lumine.Backend.Controllers;

/// <summary>
/// A user's own profile, avatar and favorites. Every action is limited to the
/// signed-in account (admins may access any account).
/// </summary>
[ApiController]
[Route("api/profile/{userId:guid}")]
[Authorize]
public sealed class ProfileController(IProfileService profileService) : ControllerBase
{
    /// <summary>Gets the profile. Accounts that never saved one get empty fields.</summary>
    [HttpGet]
    public Task<UserProfileResponse> Get(Guid userId, CancellationToken cancellationToken)
    {
        User.EnsureCanAccess(userId);
        return profileService.GetAsync(userId, cancellationToken);
    }

    /// <summary>Saves the editable profile fields. The avatar is left unchanged.</summary>
    [HttpPut]
    public Task<UserProfileResponse> Update(Guid userId, UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        User.EnsureCanAccess(userId);
        return profileService.UpdateAsync(userId, request, cancellationToken);
    }

    /// <summary>Uploads a new profile photo (JPG, PNG, WebP or HEIC, max 5 MB).</summary>
    [HttpPost("avatar")]
    [RequestSizeLimit(ImageFile.MaxBytes + 1024 * 1024)]
    public Task<AvatarResponse> UploadAvatar(Guid userId, IFormFile? file, CancellationToken cancellationToken)
    {
        User.EnsureCanAccess(userId);
        return profileService.UploadAvatarAsync(userId, file, cancellationToken);
    }

    /// <summary>Ids of the jewelry the user saved.</summary>
    [HttpGet("favorites")]
    public Task<IReadOnlyList<string>> GetFavorites(Guid userId, CancellationToken cancellationToken)
    {
        User.EnsureCanAccess(userId);
        return profileService.GetFavoriteIdsAsync(userId, cancellationToken);
    }

    /// <summary>Saves an item to favorites. Saving it twice is harmless.</summary>
    [HttpPost("favorites/{jewelryId:guid}")]
    public async Task<MessageResponse> AddFavorite(Guid userId, Guid jewelryId, CancellationToken cancellationToken)
    {
        User.EnsureCanAccess(userId);
        await profileService.AddFavoriteAsync(userId, jewelryId, cancellationToken);
        return new MessageResponse("Added to favorites.");
    }

    /// <summary>Removes an item from favorites.</summary>
    [HttpDelete("favorites/{jewelryId:guid}")]
    public async Task<MessageResponse> RemoveFavorite(Guid userId, Guid jewelryId, CancellationToken cancellationToken)
    {
        User.EnsureCanAccess(userId);
        await profileService.RemoveFavoriteAsync(userId, jewelryId, cancellationToken);
        return new MessageResponse("Removed from favorites.");
    }
}
