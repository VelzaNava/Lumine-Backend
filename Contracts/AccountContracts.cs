using System.ComponentModel.DataAnnotations;
using Lumine.Backend.Data;

namespace Lumine.Backend.Contracts;

public sealed class UserProfileResponse
{
    public string UserId { get; init; } = string.Empty;
    public string Username { get; init; } = string.Empty;
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string? MobileNumber { get; init; }
    public string? AvatarUrl { get; init; }

    public static UserProfileResponse From(string userId, ProfileRow? row) => new()
    {
        UserId = userId,
        Username = row?.Username ?? string.Empty,
        FirstName = row?.FirstName ?? string.Empty,
        LastName = row?.LastName ?? string.Empty,
        MobileNumber = row?.MobileNumber,
        AvatarUrl = row?.AvatarUrl,
    };
}

/// <summary>Profile fields the user can edit. The avatar has its own upload endpoint.</summary>
public sealed class UpdateProfileRequest
{
    [StringLength(50, ErrorMessage = "Username can be at most 50 characters.")]
    public string? Username { get; init; }

    [StringLength(60, ErrorMessage = "First name can be at most 60 characters.")]
    public string? FirstName { get; init; }

    [StringLength(60, ErrorMessage = "Last name can be at most 60 characters.")]
    public string? LastName { get; init; }

    [StringLength(20, ErrorMessage = "Mobile number can be at most 20 characters.")]
    public string? MobileNumber { get; init; }
}

public sealed record AvatarResponse(string AvatarUrl);

/// <summary>An account as listed in the admin panel: sign-in email plus profile.</summary>
/// <summary>Deleting an account asks for the password again.</summary>
public sealed class DeleteAccountRequest
{
    [Required(ErrorMessage = "Please enter your password.")]
    public string Password { get; init; } = string.Empty;
}

public sealed class AdminUserInfo
{
    public string UserId { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Username { get; init; } = string.Empty;
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string? MobileNumber { get; init; }
    public string? AvatarUrl { get; init; }

    public static AdminUserInfo From(string userId, string? email, ProfileRow? profile) => new()
    {
        UserId = userId,
        Email = email ?? string.Empty,
        Username = profile?.Username ?? string.Empty,
        FirstName = profile?.FirstName ?? string.Empty,
        LastName = profile?.LastName ?? string.Empty,
        MobileNumber = profile?.MobileNumber,
        AvatarUrl = profile?.AvatarUrl,
    };
}
