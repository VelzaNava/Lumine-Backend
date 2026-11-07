using Lumine.Backend.Contracts;
using Lumine.Backend.Security;
using Lumine.Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Lumine.Backend.Controllers;

/// <summary>Sign-in, sign-up (email code) and password reset.</summary>
[ApiController]
[Route("api/auth")]
[AllowAnonymous]
[EnableRateLimiting(RateLimitPolicies.Auth)]
public sealed class AuthController(IAuthService authService) : ControllerBase
{
    /// <summary>Signs in with email and password.</summary>
    /// <response code="401">Wrong email or password; that email must then wait 10 seconds (Retry-After).</response>
    /// <response code="429">Still waiting after a wrong password; Retry-After says how long.</response>
    [HttpPost("login")]
    public Task<AuthResponse> Login(LoginRequest request, CancellationToken cancellationToken) =>
        authService.LoginAsync(request, cancellationToken);

    /// <summary>Trades a refresh token for a new session. Refresh tokens are single-use.</summary>
    /// <response code="401">The refresh token is invalid, expired or already used.</response>
    [HttpPost("refresh")]
    public Task<AuthResponse> Refresh(RefreshSessionRequest request, CancellationToken cancellationToken) =>
        authService.RefreshAsync(request, cancellationToken);

    /// <summary>Sign-up step 1: emails a 6-digit verification code.</summary>
    /// <response code="409">An account with this email already exists.</response>
    /// <response code="429">Too many codes requested.</response>
    [HttpPost("send-otp")]
    [EnableRateLimiting(RateLimitPolicies.EmailCode)]
    public Task<CodeSentResponse> SendSignUpCode(OtpRequest request, CancellationToken cancellationToken) =>
        authService.SendSignUpCodeAsync(request, cancellationToken);

    /// <summary>Sign-up step 2: verifies the code, sets the password and signs the user in.</summary>
    /// <response code="401">Invalid or expired code.</response>
    [HttpPost("verify-and-register")]
    public Task<AuthResponse> VerifyAndRegister(VerifyAndRegisterRequest request, CancellationToken cancellationToken) =>
        authService.VerifyAndRegisterAsync(request, cancellationToken);

    /// <summary>Password reset step 1: emails a code to an existing (non-admin) account.</summary>
    /// <response code="403">Admin accounts can't reset their password this way.</response>
    /// <response code="404">No account with this email.</response>
    /// <response code="429">Too many codes requested.</response>
    [HttpPost("forgot-password")]
    [EnableRateLimiting(RateLimitPolicies.EmailCode)]
    public Task<CodeSentResponse> ForgotPassword(ForgotPasswordRequest request, CancellationToken cancellationToken) =>
        authService.SendPasswordResetCodeAsync(request, cancellationToken);

    /// <summary>Password reset step 2: verifies the code, sets the new password and signs the user in.</summary>
    /// <response code="401">Invalid or expired code.</response>
    /// <response code="403">Admin accounts can't reset their password this way.</response>
    [HttpPost("reset-password")]
    public Task<AuthResponse> ResetPassword(ResetPasswordRequest request, CancellationToken cancellationToken) =>
        authService.ResetPasswordAsync(request, cancellationToken);
}
