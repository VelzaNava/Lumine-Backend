using Lumine.Backend.Contracts;
using Lumine.Backend.Data;
using Lumine.Backend.Errors;
using Lumine.Backend.Infrastructure.Supabase;

namespace Lumine.Backend.Services;

/// <summary>Account management for the admin panel.</summary>
public interface IAdminUserService
{
    Task<IReadOnlyList<AdminUserInfo>> ListAsync(CancellationToken cancellationToken);
    Task<AdminUserInfo> GetAsync(Guid userId, CancellationToken cancellationToken);
    Task DeleteAsync(Guid userId, CancellationToken cancellationToken);
}

internal sealed class AdminUserService(
    SupabaseAuthApi authApi,
    SupabaseRestApi database,
    AccountDataEraser eraser,
    ILogger<AdminUserService> logger) : IAdminUserService
{
    public async Task<IReadOnlyList<AdminUserInfo>> ListAsync(CancellationToken cancellationToken)
    {
        // Accounts and profiles live in different Supabase services; load both at once.
        var usersTask = authApi.ListUsersAsync(cancellationToken);
        var profilesTask = database.SelectAsync<ProfileRow>(Tables.UserProfiles, "select=*", cancellationToken);
        await Task.WhenAll(usersTask, profilesTask);

        var profilesById = (await profilesTask).ToDictionary(profile => profile.Id, StringComparer.OrdinalIgnoreCase);

        return (await usersTask)
            .Select(user => AdminUserInfo.From(user.Id, user.Email, profilesById.GetValueOrDefault(user.Id)))
            .ToList();
    }

    public async Task<AdminUserInfo> GetAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await authApi.GetUserByIdAsync(userId, cancellationToken)
            ?? throw ApiException.NotFound("User not found.");

        var profile = await database.SelectSingleAsync<ProfileRow>(
            Tables.UserProfiles, $"select=*&{SupabaseRestApi.Eq("id", userId)}", cancellationToken);

        return AdminUserInfo.From(user.Id, user.Email, profile);
    }

    public async Task DeleteAsync(Guid userId, CancellationToken cancellationToken)
    {
        await eraser.EraseAsync(userId, cancellationToken);
        logger.LogInformation("Account deleted by an admin: {UserId}", userId);
    }
}
