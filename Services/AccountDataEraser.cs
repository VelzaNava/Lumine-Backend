using Lumine.Backend.Data;
using Lumine.Backend.Infrastructure.Supabase;

namespace Lumine.Backend.Services;

/// <summary>
/// Permanently removes an account and the personal data linked to it. Used by
/// both self-service deletion and the admin panel, so the two always remove the
/// same things - and match what the privacy policy promises.
/// </summary>
internal sealed class AccountDataEraser(SupabaseAuthApi authApi, SupabaseRestApi database, SupabaseStorageApi storage)
{
    public async Task EraseAsync(Guid userId, CancellationToken cancellationToken)
    {
        // Linked rows first, so this works whether or not the database cascades.
        await database.DeleteAsync(Tables.Favorites, SupabaseRestApi.Eq("user_id", userId), cancellationToken);

        // Ratings feed the try-on study, so they stay, but no longer point to anyone.
        await database.UpdateAsync<EvaluationRow>(Tables.Evaluations, SupabaseRestApi.Eq("user_id", userId),
            new Dictionary<string, object?> { ["user_id"] = null }, cancellationToken);

        await database.DeleteAsync(Tables.UserProfiles, SupabaseRestApi.Eq("id", userId), cancellationToken);
        await storage.TryDeleteAsync(Buckets.Avatars, [ProfileService.AvatarObjectPath(userId)], cancellationToken);
        await authApi.DeleteUserAsync(userId, cancellationToken);
    }
}
