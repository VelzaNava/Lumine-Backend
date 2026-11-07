using System.Text.Json.Serialization;

namespace Lumine.Backend.Data;

// Rows exactly as stored in Supabase (snake_case columns). They never leave
// the API: controllers return the DTOs in Lumine.Backend.Contracts instead, so
// the database schema and the app's JSON contract can change independently.

/// <summary>Database table names.</summary>
public static class Tables
{
    public const string Jewelry = "jewelry";
    public const string UserProfiles = "user_profiles";
    public const string Favorites = "favorites";
    public const string Evaluations = "evaluations";
}

/// <summary>Public storage buckets.</summary>
public static class Buckets
{
    public const string Avatars = "avatars";
    public const string Jewelry = "jewelry";
}

public sealed class JewelryRow
{
    [JsonPropertyName("id")] public Guid Id { get; init; }
    [JsonPropertyName("created_at")] public string? CreatedAt { get; init; }
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("type")] public string? Type { get; init; }
    [JsonPropertyName("material")] public string? Material { get; init; }
    [JsonPropertyName("price")] public decimal Price { get; init; }
    /// <summary>The cover photo, always equal to the first of <see cref="ImageUrls"/>.</summary>
    [JsonPropertyName("image_url")] public string? ImageUrl { get; init; }
    /// <summary>Up to three product photos, cover first. Null on databases without the column yet.</summary>
    [JsonPropertyName("image_urls")] public List<string>? ImageUrls { get; init; }
    [JsonPropertyName("model_url")] public string? ModelUrl { get; init; }
    [JsonPropertyName("description")] public string? Description { get; init; }
    [JsonPropertyName("is_available")] public bool IsAvailable { get; init; }
    [JsonPropertyName("is_ar_enabled")] public bool IsArEnabled { get; init; }
    [JsonPropertyName("stock_quantity")] public int? StockQuantity { get; init; }
}

public sealed class ProfileRow
{
    [JsonPropertyName("id")] public string Id { get; init; } = string.Empty;
    [JsonPropertyName("username")] public string? Username { get; init; }
    [JsonPropertyName("first_name")] public string? FirstName { get; init; }
    [JsonPropertyName("last_name")] public string? LastName { get; init; }
    [JsonPropertyName("mobile_number")] public string? MobileNumber { get; init; }
    [JsonPropertyName("avatar_url")] public string? AvatarUrl { get; init; }
}

public sealed class FavoriteRow
{
    [JsonPropertyName("user_id")] public string UserId { get; init; } = string.Empty;
    [JsonPropertyName("jewelry_id")] public string JewelryId { get; init; } = string.Empty;
}

public sealed class EvaluationRow
{
    [JsonPropertyName("id")] public Guid Id { get; init; }
    [JsonPropertyName("user_id")] public string UserId { get; init; } = string.Empty;
    [JsonPropertyName("jewelry_id")] public string JewelryId { get; init; } = string.Empty;
    [JsonPropertyName("jewelry_name")] public string? JewelryName { get; init; }
    [JsonPropertyName("rating")] public int Rating { get; init; }
    [JsonPropertyName("comment")] public string? Comment { get; init; }
    [JsonPropertyName("created_at")] public string? CreatedAt { get; init; }
}
