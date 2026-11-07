using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Lumine.Backend.Configuration;
using Lumine.Backend.Errors;
using Microsoft.Extensions.Options;

namespace Lumine.Backend.Infrastructure.Supabase;

/// <summary>
/// Typed client for the Supabase Auth (GoTrue) REST API.
/// </summary>
/// <remarks>
/// Stateless: every call carries its own credentials, so one user's session can
/// never end up in another user's request.
/// </remarks>
public sealed class SupabaseAuthApi(
    HttpClient http, IOptions<SupabaseSettings> settings, ILogger<SupabaseAuthApi> logger)
{
    private readonly SupabaseSettings _settings = settings.Value;

    // Sessions

    /// <summary>Email + password sign-in. Throws 401 when the credentials are wrong.</summary>
    public async Task<AuthSession> SignInWithPasswordAsync(string email, string password, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Post, "token?grant_type=password",
            new { email, password }, _settings.AnonKey, bearerToken: null, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return await ReadSessionAsync(response, cancellationToken);
        }

        SupabaseHttp.ThrowIfRateLimited(response);
        if (SupabaseHttp.IsClientError(response))
        {
            throw ApiException.Unauthorized("Invalid email or password.");
        }

        throw await SupabaseHttp.FailureAsync(response, logger, "sign-in",
            "We couldn't sign you in. Please try again.", cancellationToken);
    }

    /// <summary>
    /// Exchanges a refresh token for a new session. Supabase rotates refresh
    /// tokens, so the caller must store the one returned here.
    /// </summary>
    public async Task<AuthSession> RefreshSessionAsync(string refreshToken, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Post, "token?grant_type=refresh_token",
            new { refresh_token = refreshToken }, _settings.AnonKey, bearerToken: null, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return await ReadSessionAsync(response, cancellationToken);
        }

        SupabaseHttp.ThrowIfRateLimited(response);
        if (SupabaseHttp.IsClientError(response))
        {
            throw ApiException.Unauthorized("Your session has expired. Please log in again.");
        }

        throw await SupabaseHttp.FailureAsync(response, logger, "session refresh",
            "We couldn't refresh your session. Please try again.", cancellationToken);
    }

    /// <summary>Resolves the user behind an access token, or <c>null</c> if Supabase rejects the token.</summary>
    public async Task<AuthUser?> GetUserAsync(string accessToken, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Get, "user",
            body: null, _settings.AnonKey, accessToken, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<AuthUser>(cancellationToken);
        }

        if (SupabaseHttp.IsClientError(response))
        {
            return null;
        }

        throw await SupabaseHttp.FailureAsync(response, logger, "token validation",
            "We couldn't verify your session. Please try again.", cancellationToken);
    }

    // One-time codes

    /// <summary>
    /// Emails a one-time code. <paramref name="createUser"/> allows sign-up;
    /// without it the code is only sent to existing accounts.
    /// </summary>
    public async Task SendEmailCodeAsync(string email, bool createUser, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Post, "otp",
            new { email, create_user = createUser }, _settings.AnonKey, bearerToken: null, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return;
        }

        SupabaseHttp.ThrowIfRateLimited(response);
        throw await SupabaseHttp.FailureAsync(response, logger, "send code",
            "We couldn't send the code. Please check the email address and try again.", cancellationToken);
    }

    /// <summary>
    /// Verifies an emailed code and returns the session it unlocks.
    /// <paramref name="type"/> is the Supabase verification type, e.g. <c>magiclink</c>.
    /// </summary>
    public async Task<AuthSession> VerifyEmailCodeAsync(
        string email, string code, string type, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Post, "verify",
            new { type, token = code, email }, _settings.AnonKey, bearerToken: null, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return await ReadSessionAsync(response, cancellationToken);
        }

        SupabaseHttp.ThrowIfRateLimited(response);
        if (SupabaseHttp.IsClientError(response))
        {
            logger.LogInformation("Code verification rejected with {StatusCode}", (int)response.StatusCode);
            throw ApiException.Unauthorized("Invalid or expired code.");
        }

        throw await SupabaseHttp.FailureAsync(response, logger, "code verification",
            "We couldn't verify the code. Please try again.", cancellationToken);
    }

    /// <summary>Sets a new password for the user who owns <paramref name="accessToken"/>.</summary>
    public async Task UpdatePasswordAsync(string accessToken, string newPassword, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Put, "user",
            new { password = newPassword }, _settings.AnonKey, accessToken, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return;
        }

        // 422: rejected by the password policy (too weak, or same as the old one).
        if (response.StatusCode == HttpStatusCode.UnprocessableEntity)
        {
            var reason = await SupabaseHttp.ReadErrorMessageAsync(response, cancellationToken);
            throw ApiException.BadRequest(reason ?? "Please choose a different password.");
        }

        throw await SupabaseHttp.FailureAsync(response, logger, "password update",
            "Your code was accepted, but the password couldn't be saved. Please try again.", cancellationToken);
    }

    // Administration (service_role key)

    /// <summary>All accounts, newest first (first 1000 - far beyond this app's scale).</summary>
    public async Task<IReadOnlyList<AuthUser>> ListUsersAsync(CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Get, "admin/users?page=1&per_page=1000",
            body: null, _settings.Key, bearerToken: null, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw await SupabaseHttp.FailureAsync(response, logger, "list users",
                "We couldn't load the user list. Please try again.", cancellationToken);
        }

        return await ReadUserListAsync(response, cancellationToken);
    }

    /// <summary>The confirmed account registered with <paramref name="email"/>, if any.</summary>
    public async Task<AuthUser?> FindConfirmedUserByEmailAsync(string email, CancellationToken cancellationToken)
    {
        // "filter" is a substring match ("email" isn't a supported parameter), so the
        // exact comparison happens here.
        using var response = await SendAsync(HttpMethod.Get,
            $"admin/users?page=1&per_page=1000&filter={Uri.EscapeDataString(email)}",
            body: null, _settings.Key, bearerToken: null, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw await SupabaseHttp.FailureAsync(response, logger, "find user",
                "We couldn't check that email right now. Please try again.", cancellationToken);
        }

        var users = await ReadUserListAsync(response, cancellationToken);
        return users.FirstOrDefault(user =>
            user.EmailConfirmedAt is not null &&
            string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>The account with <paramref name="userId"/>, or <c>null</c> if it doesn't exist.</summary>
    public async Task<AuthUser?> GetUserByIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Get, $"admin/users/{userId}",
            body: null, _settings.Key, bearerToken: null, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<AuthUser>(cancellationToken);
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        throw await SupabaseHttp.FailureAsync(response, logger, "get user",
            "We couldn't load that user. Please try again.", cancellationToken);
    }

    /// <summary>Permanently deletes an account. Missing accounts are treated as already deleted.</summary>
    public async Task DeleteUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Delete, $"admin/users/{userId}",
            body: null, _settings.Key, bearerToken: null, cancellationToken);

        if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NotFound)
        {
            return;
        }

        throw await SupabaseHttp.FailureAsync(response, logger, "delete user",
            "We couldn't remove that account. Please try again.", cancellationToken);
    }

    // Helpers

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method, string path, object? body, string apiKey, string? bearerToken,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, path);
        SupabaseHttp.Authorize(request, apiKey, bearerToken);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return await SupabaseHttp.SendAsync(http, request, logger, cancellationToken);
    }

    private async Task<AuthSession> ReadSessionAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var session = await response.Content.ReadFromJsonAsync<AuthSession>(cancellationToken);
        if (session is null || string.IsNullOrEmpty(session.AccessToken))
        {
            logger.LogError("Supabase returned a success response without a session");
            throw ApiException.Upstream("We couldn't start your session. Please try again.");
        }

        return session;
    }

    private static async Task<IReadOnlyList<AuthUser>> ReadUserListAsync(
        HttpResponseMessage response, CancellationToken cancellationToken)
    {
        // Current versions wrap the list as { "users": [...] }; tolerate a bare array too.
        using var document = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);

        var list = document.RootElement.ValueKind == JsonValueKind.Array
            ? document.RootElement
            : document.RootElement.TryGetProperty("users", out var users) ? users : default;

        return list.ValueKind == JsonValueKind.Array
            ? list.Deserialize<List<AuthUser>>() ?? []
            : [];
    }
}

/// <summary>A Supabase session as returned by the token and verify endpoints.</summary>
public sealed class AuthSession
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; init; } = string.Empty;

    [JsonPropertyName("refresh_token")]
    public string RefreshToken { get; init; } = string.Empty;

    [JsonPropertyName("user")]
    public AuthUser? User { get; init; }
}

/// <summary>The subset of a Supabase user this API uses.</summary>
public sealed class AuthUser
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("email")]
    public string? Email { get; init; }

    [JsonPropertyName("email_confirmed_at")]
    public DateTimeOffset? EmailConfirmedAt { get; init; }
}
