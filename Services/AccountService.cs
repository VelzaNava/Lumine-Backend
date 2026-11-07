using Lumine.Backend.Errors;
using Lumine.Backend.Infrastructure.Supabase;

namespace Lumine.Backend.Services;

/// <summary>Things a signed-in user does to their own account.</summary>
public interface IAccountService
{
    /// <summary>Re-checks the password, then permanently deletes the account and its personal data.</summary>
    Task DeleteOwnAccountAsync(Guid userId, string email, string password, CancellationToken cancellationToken);
}

internal sealed class AccountService(
    SupabaseAuthApi authApi, AccountDataEraser eraser, ILogger<AccountService> logger) : IAccountService
{
    public async Task DeleteOwnAccountAsync(
        Guid userId, string email, string password, CancellationToken cancellationToken)
    {
        // A valid session alone isn't enough for something this permanent.
        try
        {
            await authApi.SignInWithPasswordAsync(email, password, cancellationToken);
        }
        catch (ApiException exception) when (exception.StatusCode == StatusCodes.Status401Unauthorized)
        {
            // Not 401: the app reads 401 as an expired session and would sign the user out.
            throw ApiException.BadRequest("That password is incorrect.");
        }

        await eraser.EraseAsync(userId, cancellationToken);
        logger.LogInformation("Account deleted by its owner: {UserId}", userId);
    }
}
