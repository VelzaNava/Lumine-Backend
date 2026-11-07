using Lumine.Backend.Contracts;
using Lumine.Backend.Data;
using Lumine.Backend.Infrastructure.Supabase;

namespace Lumine.Backend.Services;

/// <summary>A user's own profile, avatar and favorites.</summary>
public interface IProfileService
{
    Task<UserProfileResponse> GetAsync(Guid userId, CancellationToken cancellationToken);
    Task<UserProfileResponse> UpdateAsync(Guid userId, UpdateProfileRequest request, CancellationToken cancellationToken);
    Task<AvatarResponse> UploadAvatarAsync(Guid userId, IFormFile? file, CancellationToken cancellationToken);
    Task<IReadOnlyList<string>> GetFavoriteIdsAsync(Guid userId, CancellationToken cancellationToken);
    Task AddFavoriteAsync(Guid userId, Guid jewelryId, CancellationToken cancellationToken);
    Task RemoveFavoriteAsync(Guid userId, Guid jewelryId, CancellationToken cancellationToken);
}

internal sealed class ProfileService(SupabaseRestApi database, SupabaseStorageApi storage) : IProfileService
{
    /// <summary>Each user has exactly one avatar object; uploads replace it.</summary>
    public static string AvatarObjectPath(Guid userId) => $"{userId}.jpg";

    public async Task<UserProfileResponse> GetAsync(Guid userId, CancellationToken cancellationToken)
    {
        var id = userId.ToString();
        var row = await database.SelectSingleAsync<ProfileRow>(
            Tables.UserProfiles, $"select=*&{SupabaseRestApi.Eq("id", id)}", cancellationToken);

        // Someone who has never saved a profile simply gets an empty one.
        return UserProfileResponse.From(id, row);
    }

    public async Task<UserProfileResponse> UpdateAsync(
        Guid userId, UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        var id = userId.ToString();

        // An upsert writes only the columns listed, so avatar_url keeps its value.
        var row = await database.UpsertAsync<ProfileRow>(Tables.UserProfiles, "id", new Dictionary<string, object?>
        {
            ["id"] = id,
            ["username"] = request.Username?.Trim() ?? string.Empty,
            ["first_name"] = request.FirstName?.Trim() ?? string.Empty,
            ["last_name"] = request.LastName?.Trim() ?? string.Empty,
            ["mobile_number"] = string.IsNullOrWhiteSpace(request.MobileNumber) ? null : request.MobileNumber.Trim(),
        }, cancellationToken);

        return UserProfileResponse.From(id, row);
    }

    public async Task<AvatarResponse> UploadAvatarAsync(Guid userId, IFormFile? file, CancellationToken cancellationToken)
    {
        var image = await ImageFile.ReadAsync(file, cancellationToken);
        var id = userId.ToString();

        var publicUrl = await storage.UploadAsync(
            Buckets.Avatars, AvatarObjectPath(userId), image.Content, image.ContentType, cancellationToken);

        // The object path never changes, so version the URL - otherwise every
        // client keeps showing its cached copy of the old photo.
        var avatarUrl = $"{publicUrl}?v={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";

        var updated = await database.UpdateAsync<ProfileRow>(Tables.UserProfiles, SupabaseRestApi.Eq("id", id),
            new Dictionary<string, object?> { ["avatar_url"] = avatarUrl }, cancellationToken);

        if (updated.Count == 0)
        {
            // Photo chosen before the profile was ever saved: create the row now,
            // otherwise the new avatar would silently never be stored.
            await database.InsertAsync<ProfileRow>(Tables.UserProfiles, new Dictionary<string, object?>
            {
                ["id"] = id,
                ["username"] = string.Empty,
                ["first_name"] = string.Empty,
                ["last_name"] = string.Empty,
                ["avatar_url"] = avatarUrl,
            }, cancellationToken);
        }

        return new AvatarResponse(avatarUrl);
    }

    public async Task<IReadOnlyList<string>> GetFavoriteIdsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var rows = await database.SelectAsync<FavoriteRow>(
            Tables.Favorites, $"select=jewelry_id&{SupabaseRestApi.Eq("user_id", userId)}", cancellationToken);

        return rows.Select(row => row.JewelryId).ToList();
    }

    public async Task AddFavoriteAsync(Guid userId, Guid jewelryId, CancellationToken cancellationToken)
    {
        // Adding an existing favorite is a no-op, not an error. The explicit check
        // covers databases without a unique (user_id, jewelry_id) index; with one,
        // InsertIfAbsentAsync also absorbs the race between two taps.
        var filter = $"{SupabaseRestApi.Eq("user_id", userId)}&{SupabaseRestApi.Eq("jewelry_id", jewelryId)}";
        if (await database.SelectSingleAsync<FavoriteRow>(Tables.Favorites, $"select=jewelry_id&{filter}", cancellationToken) is not null)
        {
            return;
        }

        await database.InsertIfAbsentAsync(Tables.Favorites, new Dictionary<string, object?>
        {
            ["user_id"] = userId.ToString(),
            ["jewelry_id"] = jewelryId.ToString(),
        }, cancellationToken);
    }

    public Task RemoveFavoriteAsync(Guid userId, Guid jewelryId, CancellationToken cancellationToken) =>
        database.DeleteAsync(Tables.Favorites,
            $"{SupabaseRestApi.Eq("user_id", userId)}&{SupabaseRestApi.Eq("jewelry_id", jewelryId)}",
            cancellationToken);
}
