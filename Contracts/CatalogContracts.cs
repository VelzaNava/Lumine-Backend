using System.ComponentModel.DataAnnotations;
using Lumine.Backend.Data;

namespace Lumine.Backend.Contracts;

/// <summary>A catalog item as the app sees it.</summary>
public sealed class JewelryDto
{
    public Guid Id { get; init; }
    public string CreatedAt { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string Material { get; init; } = string.Empty;
    public decimal Price { get; init; }

    /// <summary>The cover photo (first of <see cref="ImageUrls"/>), kept for older app versions.</summary>
    public string? ImageUrl { get; init; }

    /// <summary>All product photos (up to <see cref="JewelryRequest.MaxPhotos"/>), cover first.</summary>
    public IReadOnlyList<string> ImageUrls { get; init; } = [];

    public string? ModelUrl { get; init; }
    public string? Description { get; init; }
    public bool IsAvailable { get; init; }
    public bool IsArEnabled { get; init; }

    /// <summary>Optional inventory count. <c>null</c> means "not tracked", never "zero".</summary>
    public int? StockQuantity { get; init; }

    public static JewelryDto From(JewelryRow row)
    {
        // Items saved before multi-photo support only have the single image_url.
        var photos = row.ImageUrls?.Where(url => !string.IsNullOrWhiteSpace(url)).ToList() ?? [];
        if (photos.Count == 0 && !string.IsNullOrWhiteSpace(row.ImageUrl))
        {
            photos.Add(row.ImageUrl);
        }

        return new JewelryDto
        {
            Id = row.Id,
            CreatedAt = row.CreatedAt ?? string.Empty,
            Name = row.Name ?? string.Empty,
            Type = row.Type ?? string.Empty,
            Material = row.Material ?? string.Empty,
            Price = row.Price,
            ImageUrl = photos.FirstOrDefault(),
            ImageUrls = photos,
            ModelUrl = row.ModelUrl,
            Description = row.Description,
            IsAvailable = row.IsAvailable,
            IsArEnabled = row.IsArEnabled,
            StockQuantity = row.StockQuantity,
        };
    }
}

/// <summary>
/// Body for creating or updating a catalog item. Server-managed fields
/// (<c>id</c>, <c>createdAt</c>) are ignored if the client sends them.
/// </summary>
public sealed class JewelryRequest
{
    public const int MaxPhotos = 3;

    [Required, StringLength(120)]
    public string Name { get; init; } = string.Empty;

    /// <summary>ring, earring, bracelet or necklace.</summary>
    [Required, StringLength(40)]
    public string Type { get; init; } = string.Empty;

    [StringLength(120)]
    public string? Material { get; init; }

    [Range(typeof(decimal), "0", "100000000", ErrorMessage = "Price must be zero or more.")]
    public decimal Price { get; init; }

    /// <summary>Single cover photo. Only used by older app versions that don't send <see cref="ImageUrls"/>.</summary>
    [StringLength(1000)]
    public string? ImageUrl { get; init; }

    /// <summary>Up to three photos (URLs from the upload endpoint), cover first.</summary>
    [MaxLength(MaxPhotos, ErrorMessage = "You can add up to 3 photos per item.")]
    public List<string>? ImageUrls { get; init; }

    /// <summary>Name of the 3D model in the AR scene.</summary>
    [StringLength(200)]
    public string? ModelUrl { get; init; }

    [StringLength(4000)]
    public string? Description { get; init; }

    public bool IsAvailable { get; init; } = true;

    public bool IsArEnabled { get; init; }

    [Range(0, 1_000_000, ErrorMessage = "Stock must be zero or more.")]
    public int? StockQuantity { get; init; }
}

public sealed record ImageUploadResponse(string ImageUrl);
