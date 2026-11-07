using System.ComponentModel.DataAnnotations;

namespace Lumine.Backend.Contracts;

public sealed class LoginRequest
{
    [Required, EmailAddress]
    public string Email { get; init; } = string.Empty;

    [Required]
    public string Password { get; init; } = string.Empty;
}

public sealed class RefreshSessionRequest
{
    [Required]
    public string RefreshToken { get; init; } = string.Empty;
}

/// <summary>Step 1 of sign-up: email a verification code to a new address.</summary>
public sealed class OtpRequest
{
    [Required, EmailAddress]
    public string Email { get; init; } = string.Empty;
}

/// <summary>Step 2 of sign-up: verify the code and choose a password.</summary>
public sealed class VerifyAndRegisterRequest
{
    [Required, EmailAddress]
    public string Email { get; init; } = string.Empty;

    [Required, StringLength(6, MinimumLength = 6, ErrorMessage = "The code must be 6 digits.")]
    public string Token { get; init; } = string.Empty;

    [Required, MinLength(8, ErrorMessage = "Password must be at least 8 characters.")]
    public string Password { get; init; } = string.Empty;
}

/// <summary>Step 1 of a password reset: email a code to an existing account.</summary>
public sealed class ForgotPasswordRequest
{
    [Required, EmailAddress]
    public string Email { get; init; } = string.Empty;
}

/// <summary>Step 2 of a password reset: verify the code and set the new password.</summary>
public sealed class ResetPasswordRequest
{
    [Required, EmailAddress]
    public string Email { get; init; } = string.Empty;

    [Required, StringLength(6, MinimumLength = 6, ErrorMessage = "The code must be 6 digits.")]
    public string Token { get; init; } = string.Empty;

    [Required, MinLength(8, ErrorMessage = "Password must be at least 8 characters.")]
    public string NewPassword { get; init; } = string.Empty;
}

/// <summary>
/// Returned by every endpoint that signs a user in. The app sends
/// <see cref="AccessToken"/> as a bearer token and trades
/// <see cref="RefreshToken"/> for a new pair when it expires.
/// </summary>
public sealed class AuthResponse
{
    public string AccessToken { get; init; } = string.Empty;
    public string RefreshToken { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string UserId { get; init; } = string.Empty;
    public bool IsAdmin { get; init; }
}

public sealed record CodeSentResponse(string Message, string Email);

public sealed record MessageResponse(string Message);
