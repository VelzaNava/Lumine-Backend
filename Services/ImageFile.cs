using Lumine.Backend.Errors;

namespace Lumine.Backend.Services;

/// <summary>
/// An uploaded image, validated by its actual bytes. The declared content type
/// can't be trusted either way - the app labels every upload <c>image/*</c>.
/// </summary>
public sealed record ImageFile(byte[] Content, string ContentType, string Extension)
{
    public const long MaxBytes = 5 * 1024 * 1024;

    public static async Task<ImageFile> ReadAsync(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            throw ApiException.BadRequest("No image was provided.");
        }

        if (file.Length > MaxBytes)
        {
            throw ApiException.BadRequest("That image is too large. The maximum size is 5 MB.");
        }

        using var buffer = new MemoryStream((int)file.Length);
        await file.CopyToAsync(buffer, cancellationToken);
        var content = buffer.ToArray();

        var (contentType, extension) = DetectFormat(content)
            ?? throw ApiException.BadRequest("Please choose a JPG, PNG or WebP image.");

        return new ImageFile(content, contentType, extension);
    }

    /// <summary>
    /// Identifies the format from the file signature ("magic bytes"). Only the
    /// formats the storage buckets accept (JPG, PNG, WebP) pass; the app
    /// converts everything else, e.g. HEIC camera photos, to JPG before upload.
    /// </summary>
    private static (string ContentType, string Extension)? DetectFormat(ReadOnlySpan<byte> bytes)
    {
        if (bytes.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xD8, 0xFF]))
        {
            return ("image/jpeg", "jpg");
        }

        if (bytes.StartsWith((ReadOnlySpan<byte>)[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]))
        {
            return ("image/png", "png");
        }

        if (bytes.Length >= 12 && bytes[..4].SequenceEqual("RIFF"u8) && bytes[8..12].SequenceEqual("WEBP"u8))
        {
            return ("image/webp", "webp");
        }

        return null;
    }
}
