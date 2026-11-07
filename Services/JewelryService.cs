using Lumine.Backend.Contracts;
using Lumine.Backend.Data;
using Lumine.Backend.Errors;
using Lumine.Backend.Infrastructure.Supabase;

namespace Lumine.Backend.Services;

/// <summary>The jewelry catalog.</summary>
public interface IJewelryService
{
    Task<IReadOnlyList<JewelryDto>> GetAllAsync(CancellationToken cancellationToken);
    Task<JewelryDto> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<JewelryDto> CreateAsync(JewelryRequest request, CancellationToken cancellationToken);
    Task<JewelryDto> UpdateAsync(Guid id, JewelryRequest request, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
    Task<ImageUploadResponse> UploadImageAsync(IFormFile? file, CancellationToken cancellationToken);
}

internal sealed class JewelryService(
    SupabaseRestApi database, SupabaseStorageApi storage, ILogger<JewelryService> logger) : IJewelryService
{
    public async Task<IReadOnlyList<JewelryDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        // No ORDER BY on purpose: every seeded item shares one created_at, so the
        // table's natural order is the only stable order - and it's the catalog
        // order the app has always shown.
        var rows = await database.SelectAsync<JewelryRow>(Tables.Jewelry, "select=*", cancellationToken);
        return rows.Select(JewelryDto.From).ToList();
    }

    public async Task<JewelryDto> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var row = await database.SelectSingleAsync<JewelryRow>(
            Tables.Jewelry, $"select=*&{SupabaseRestApi.Eq("id", id)}", cancellationToken);

        return row is null ? throw NotFound() : JewelryDto.From(row);
    }

    public async Task<JewelryDto> CreateAsync(JewelryRequest request, CancellationToken cancellationToken)
    {
        var columns = ToColumns(request, isCreate: true);
        columns["created_at"] = DateTimeOffset.UtcNow;

        var row = await database.InsertAsync<JewelryRow>(Tables.Jewelry, columns, cancellationToken);
        logger.LogInformation("Jewelry created: {JewelryId} ({Name})", row.Id, row.Name);
        return JewelryDto.From(row);
    }

    public async Task<JewelryDto> UpdateAsync(Guid id, JewelryRequest request, CancellationToken cancellationToken)
    {
        var rows = await database.UpdateAsync<JewelryRow>(
            Tables.Jewelry, SupabaseRestApi.Eq("id", id), ToColumns(request, isCreate: false), cancellationToken);

        return rows.Count > 0 ? JewelryDto.From(rows[0]) : throw NotFound();
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        // Clear favorites that point at the item first, so none are left dangling.
        await database.DeleteAsync(Tables.Favorites, SupabaseRestApi.Eq("jewelry_id", id), cancellationToken);
        await database.DeleteAsync(Tables.Jewelry, SupabaseRestApi.Eq("id", id), cancellationToken);
        logger.LogInformation("Jewelry deleted: {JewelryId}", id);
    }

    public async Task<ImageUploadResponse> UploadImageAsync(IFormFile? file, CancellationToken cancellationToken)
    {
        var image = await ImageFile.ReadAsync(file, cancellationToken);
        var objectPath = $"jewelry_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}.{image.Extension}";

        var url = await storage.UploadAsync(Buckets.Jewelry, objectPath, image.Content, image.ContentType, cancellationToken);
        return new ImageUploadResponse(url);
    }

    private static Dictionary<string, object?> ToColumns(JewelryRequest request, bool isCreate)
    {
        var columns = new Dictionary<string, object?>
        {
            ["name"] = request.Name.Trim(),
            // The AR scene matches on lowercase types ("ring", "necklace", ...).
            ["type"] = request.Type.Trim().ToLowerInvariant(),
            ["material"] = request.Material?.Trim() ?? string.Empty,
            ["price"] = request.Price,
            ["model_url"] = request.ModelUrl?.Trim(),
            ["description"] = request.Description?.Trim(),
            ["is_available"] = request.IsAvailable,
            ["is_ar_enabled"] = request.IsArEnabled,
            ["stock_quantity"] = request.StockQuantity,
        };

        if (request.ImageUrls is not null)
        {
            var photos = NormalizePhotos(request.ImageUrls);
            columns["image_urls"] = photos;
            columns["image_url"] = photos.FirstOrDefault();
        }
        else
        {
            // Older app versions send one imageUrl and know nothing about the photo list:
            // set the cover, and on edits leave any extra photos untouched.
            var cover = string.IsNullOrWhiteSpace(request.ImageUrl) ? null : request.ImageUrl.Trim();
            columns["image_url"] = cover;
            if (isCreate)
            {
                columns["image_urls"] = cover is null ? new List<string>() : [cover];
            }
        }

        return columns;
    }

    /// <summary>Drops blanks and duplicates, keeping the admin's order (the first photo is the cover).</summary>
    private static List<string> NormalizePhotos(IEnumerable<string> urls)
    {
        var photos = urls
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .Select(url => url.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (photos.Count > JewelryRequest.MaxPhotos)
        {
            throw ApiException.BadRequest("You can add up to 3 photos per item.");
        }

        if (photos.Any(url => url.Length > 1000))
        {
            throw ApiException.BadRequest("A photo link is too long.");
        }

        return photos;
    }

    private static ApiException NotFound() => ApiException.NotFound("Jewelry not found.");
}
