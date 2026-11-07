using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Lumine.Backend.Infrastructure.Supabase;
using Microsoft.Extensions.Caching.Memory;

namespace Lumine.Backend.Security;

/// <summary>The account behind a verified access token.</summary>
public sealed record AuthenticatedUser(string Id, string Email);

/// <summary>Verifies bearer tokens sent by the app.</summary>
public interface IAccessTokenValidator
{
    /// <summary>The token's owner, or <c>null</c> if the token is invalid, expired or revoked.</summary>
    Task<AuthenticatedUser?> ValidateAsync(string accessToken, CancellationToken cancellationToken);
}

/// <summary>
/// Validates tokens by asking Supabase who owns them. This works whichever JWT
/// signing scheme the project uses and needs no extra secret. Results are
/// cached briefly (never past the token's own expiry) so a burst of requests
/// costs a single round trip.
/// </summary>
internal sealed class SupabaseAccessTokenValidator(SupabaseAuthApi authApi, IMemoryCache cache) : IAccessTokenValidator
{
    private static readonly TimeSpan MaxCacheDuration = TimeSpan.FromMinutes(2);

    public async Task<AuthenticatedUser?> ValidateAsync(string accessToken, CancellationToken cancellationToken)
    {
        // Key on a hash so raw tokens never sit in memory longer than the request.
        var cacheKey = "access-token:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(accessToken)));
        if (cache.TryGetValue(cacheKey, out AuthenticatedUser? cached))
        {
            return cached;
        }

        var user = await authApi.GetUserAsync(accessToken, cancellationToken);
        if (user is null || string.IsNullOrEmpty(user.Id))
        {
            return null;
        }

        var authenticated = new AuthenticatedUser(user.Id, user.Email ?? string.Empty);
        var lifetime = RemainingLifetime(accessToken);
        var cacheFor = lifetime is { } remaining && remaining < MaxCacheDuration ? remaining : MaxCacheDuration;
        if (cacheFor > TimeSpan.Zero)
        {
            cache.Set(cacheKey, authenticated, cacheFor);
        }

        return authenticated;
    }

    /// <summary>
    /// Time until the JWT's <c>exp</c> claim. Only used to bound the cache -
    /// the token itself was already verified by Supabase.
    /// </summary>
    private static TimeSpan? RemainingLifetime(string jwt)
    {
        try
        {
            var parts = jwt.Split('.');
            if (parts.Length != 3)
            {
                return null;
            }

            var payload = parts[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');

            using var document = JsonDocument.Parse(Convert.FromBase64String(payload));
            return document.RootElement.TryGetProperty("exp", out var exp) && exp.TryGetInt64(out var seconds)
                ? DateTimeOffset.FromUnixTimeSeconds(seconds) - DateTimeOffset.UtcNow
                : null;
        }
        catch (Exception exception) when (exception is FormatException or JsonException)
        {
            return null;
        }
    }
}
