using System.Net.Http.Headers;
using System.Net.Http.Json;
using Lumine.Backend.Configuration;
using Microsoft.Extensions.Options;

namespace Lumine.Backend.Infrastructure.Supabase;

/// <summary>Typed client for Supabase Storage (public buckets only).</summary>
public sealed class SupabaseStorageApi(
    HttpClient http, IOptions<SupabaseSettings> settings, ILogger<SupabaseStorageApi> logger)
{
    private readonly SupabaseSettings _settings = settings.Value;

    /// <summary>
    /// Uploads (or replaces) an object and returns its public URL.
    /// </summary>
    public async Task<string> UploadAsync(
        string bucket, string objectPath, byte[] content, string contentType, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, $"object/{bucket}/{objectPath}");
        SupabaseHttp.Authorize(request, _settings.Key);
        request.Content = new ByteArrayContent(content);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);

        using var response = await SupabaseHttp.SendAsync(http, request, logger, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw await SupabaseHttp.FailureAsync(response, logger, $"upload to {bucket}",
                "The image couldn't be uploaded. Please try again.", cancellationToken);
        }

        return GetPublicUrl(bucket, objectPath);
    }

    /// <summary>
    /// Deletes objects. Best effort: a failure is logged but doesn't fail the
    /// caller, since a leftover file is harmless compared to a half-finished
    /// account removal.
    /// </summary>
    public async Task TryDeleteAsync(string bucket, IReadOnlyCollection<string> objectPaths, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"object/{bucket}");
        SupabaseHttp.Authorize(request, _settings.Key);
        request.Content = JsonContent.Create(new { prefixes = objectPaths });

        using var response = await SupabaseHttp.SendAsync(http, request, logger, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Deleting {Count} object(s) from {Bucket} failed with {StatusCode}",
                objectPaths.Count, bucket, (int)response.StatusCode);
        }
    }

    public string GetPublicUrl(string bucket, string objectPath) =>
        $"{_settings.BaseUrl}/storage/v1/object/public/{bucket}/{objectPath}";
}
