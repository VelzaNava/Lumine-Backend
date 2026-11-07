using Lumine.Backend.Configuration;
using Lumine.Backend.Contracts;
using Lumine.Backend.Errors;
using Lumine.Backend.Infrastructure.Supabase;
using Lumine.Backend.Security;

namespace Lumine.Backend.Services;

/// <summary>Sign-in, sign-up and password reset, on top of Supabase Auth.</summary>
public interface IAuthService
{
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken);
    Task<AuthResponse> RefreshAsync(RefreshSessionRequest request, CancellationToken cancellationToken);
    Task<CodeSentResponse> SendSignUpCodeAsync(OtpRequest request, CancellationToken cancellationToken);
    Task<AuthResponse> VerifyAndRegisterAsync(VerifyAndRegisterRequest request, CancellationToken cancellationToken);
    Task<CodeSentResponse> SendPasswordResetCodeAsync(ForgotPasswordRequest request, CancellationToken cancellationToken);
    Task<AuthResponse> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken);
}

internal sealed class AuthService(
    SupabaseAuthApi authApi, AdminDirectory adminDirectory, LoginCooldown cooldown,
    ILogger<AuthService> logger) : IAuthService
{
    /// <summary>Supabase verification type for the codes sent by its <c>/otp</c> endpoint.</summary>
    private const string EmailCodeType = "magiclink";

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(request.Email);
        cooldown.EnsureAllowed(email);

        AuthSession session;
        try
        {
            session = await authApi.SignInWithPasswordAsync(email, request.Password, cancellationToken);
        }
        catch (ApiException exception) when (exception.StatusCode == StatusCodes.Status401Unauthorized)
        {
            throw cooldown.WrongPassword(email);
        }

        logger.LogInformation("User signed in: {UserId}", session.User?.Id);
        return ToResponse(session, email);
    }

    public async Task<AuthResponse> RefreshAsync(RefreshSessionRequest request, CancellationToken cancellationToken)
    {
        var session = await authApi.RefreshSessionAsync(request.RefreshToken, cancellationToken);
        return ToResponse(session, fallbackEmail: string.Empty);
    }

    public async Task<CodeSentResponse> SendSignUpCodeAsync(OtpRequest request, CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(request.Email);

        // Sign-up ends by setting a password, so a code for an existing account
        // would let this flow overwrite that account's password.
        if (await authApi.FindConfirmedUserByEmailAsync(email, cancellationToken) is not null)
        {
            throw ApiException.Conflict("An account with this email already exists. Please log in instead.");
        }

        await authApi.SendEmailCodeAsync(email, createUser: true, cancellationToken);
        return new CodeSentResponse("Verification code sent.", email);
    }

    public async Task<AuthResponse> VerifyAndRegisterAsync(
        VerifyAndRegisterRequest request, CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(request.Email);
        var session = await authApi.VerifyEmailCodeAsync(email, request.Token, EmailCodeType, cancellationToken);

        // The verified session belongs to the new user, so it can set their password itself.
        await authApi.UpdatePasswordAsync(session.AccessToken, request.Password, cancellationToken);

        logger.LogInformation("User registered: {UserId}", session.User?.Id);
        return ToResponse(session, email);
    }

    public async Task<CodeSentResponse> SendPasswordResetCodeAsync(
        ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(request.Email);
        EnsureSelfServiceReset(email);

        if (await authApi.FindConfirmedUserByEmailAsync(email, cancellationToken) is null)
        {
            throw ApiException.NotFound("No account found with this email.");
        }

        await authApi.SendEmailCodeAsync(email, createUser: false, cancellationToken);
        return new CodeSentResponse("Reset code sent.", email);
    }

    public async Task<AuthResponse> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(request.Email);

        // Checked again here: step 2 must not rely on step 1 having been called.
        EnsureSelfServiceReset(email);

        var session = await authApi.VerifyEmailCodeAsync(email, request.Token, EmailCodeType, cancellationToken);
        await authApi.UpdatePasswordAsync(session.AccessToken, request.NewPassword, cancellationToken);

        logger.LogInformation("Password reset: {UserId}", session.User?.Id);
        return ToResponse(session, email);
    }

    /// <summary>Admin passwords are managed in the Supabase dashboard, never by emailed code.</summary>
    private void EnsureSelfServiceReset(string email)
    {
        if (adminDirectory.IsAdmin(email))
        {
            logger.LogWarning("Self-service password reset refused for an admin account");
            throw ApiException.Forbidden("This account can't use self-service password reset.");
        }
    }

    private AuthResponse ToResponse(AuthSession session, string fallbackEmail)
    {
        var email = session.User?.Email ?? fallbackEmail;
        return new AuthResponse
        {
            AccessToken = session.AccessToken,
            RefreshToken = session.RefreshToken,
            Email = email,
            UserId = session.User?.Id ?? string.Empty,
            IsAdmin = adminDirectory.IsAdmin(email),
        };
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}
