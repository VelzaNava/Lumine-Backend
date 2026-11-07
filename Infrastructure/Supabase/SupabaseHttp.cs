using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Lumine.Backend.Errors;

namespace Lumine.Backend.Infrastructure.Supabase;

/// <summary>Plumbing shared by the Supabase REST clients.</summary>
internal static class SupabaseHttp
{
    private const int MaxLoggedBodyLength = 500;

    /// <summary>Attaches the API key and bearer token Supabase expects on every call.</summary>
    public static void Authorize(HttpRequestMessage request, string apiKey, string? bearerToken = null)
    {
        request.Headers.Add("apikey", apiKey);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken ?? apiKey);
    }

    /// <summary>
    /// Sends the request, converting "Supabase is unreachable" (network failure
    /// or timeout) into a 503 instead of an unhandled exception.
    /// </summary>
    public static async Task<HttpResponseMessage> SendAsync(
        HttpClient http, HttpRequestMessage request, ILogger logger, CancellationToken cancellationToken)
    {
        try
        {
            return await http.SendAsync(request, cancellationToken);
        }
        catch (Exception exception) when (
            exception is HttpRequestException ||
            (exception is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            logger.LogError(exception, "Supabase unreachable: {Method} {Path}",
                request.Method, request.RequestUri?.AbsolutePath);
            throw ApiException.Unavailable("The service is temporarily unavailable. Please try again.");
        }
    }

    /// <summary>Logs a failed Supabase response and returns a 502 with a user-facing message.</summary>
    public static async Task<ApiException> FailureAsync(
        HttpResponseMessage response, ILogger logger, string operation, string userMessage,
        CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        logger.LogError("Supabase {Operation} failed with {StatusCode}: {Body}",
            operation, (int)response.StatusCode, Truncate(body));
        return ApiException.Upstream(userMessage);
    }

    /// <summary>Best-effort extraction of the human-readable message from a Supabase error body.</summary>
    public static async Task<string?> ReadErrorMessageAsync(
        HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            foreach (var field in (string[])["msg", "message", "error_description"])
            {
                if (document.RootElement.TryGetProperty(field, out var value) &&
                    value.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrWhiteSpace(value.GetString()))
                {
                    return value.GetString();
                }
            }
        }
        catch (JsonException)
        {
            // Not JSON - fall through to "no message".
        }

        return null;
    }

    public static bool IsClientError(HttpResponseMessage response) =>
        (int)response.StatusCode is >= 400 and < 500 && response.StatusCode != HttpStatusCode.TooManyRequests;

    public static void ThrowIfRateLimited(HttpResponseMessage response)
    {
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            throw ApiException.TooManyRequests("Too many requests. Please wait 60 seconds and try again.");
        }
    }

    private static string Truncate(string text) =>
        text.Length <= MaxLoggedBodyLength ? text : text[..MaxLoggedBodyLength] + "…";
}
