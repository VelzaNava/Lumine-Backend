using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Lumine.Backend.Configuration;
using Lumine.Backend.Errors;
using Microsoft.Extensions.Options;

namespace Lumine.Backend.Infrastructure.Supabase;

/// <summary>
/// Typed client for the Supabase database REST API (PostgREST). Every call runs
/// with the service_role key, so authorization is enforced by this API's own
/// policies before a request ever gets here.
/// </summary>
/// <remarks>
/// Queries use PostgREST syntax, e.g. <c>select=*&amp;id=eq.123</c>. Build
/// filter values with <see cref="Eq"/> so they are always URL-escaped.
/// </remarks>
public sealed class SupabaseRestApi(
    HttpClient http, IOptions<SupabaseSettings> settings, ILogger<SupabaseRestApi> logger)
{
    private const string LoadFailed = "We couldn't load the data. Please try again.";
    private const string SaveFailed = "We couldn't save your changes. Please try again.";

    private readonly SupabaseSettings _settings = settings.Value;

    /// <summary><c>column=eq.value</c>, with the value URL-escaped.</summary>
    public static string Eq(string column, object value) =>
        $"{column}=eq.{Uri.EscapeDataString(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty)}";

    public async Task<List<T>> SelectAsync<T>(string table, string query, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Get, $"{table}?{query}", body: null, prefer: null, cancellationToken);
        await EnsureSuccessAsync(response, $"read {table}", LoadFailed, cancellationToken);
        return await response.Content.ReadFromJsonAsync<List<T>>(cancellationToken) ?? [];
    }

    public async Task<T?> SelectSingleAsync<T>(string table, string query, CancellationToken cancellationToken)
    {
        var rows = await SelectAsync<T>(table, $"{query}&limit=1", cancellationToken);
        return rows.Count > 0 ? rows[0] : default;
    }

    /// <summary>Inserts a row and returns it as stored (with generated id and timestamps).</summary>
    public async Task<T> InsertAsync<T>(string table, object row, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Post, table, row, "return=representation", cancellationToken);
        await EnsureSuccessAsync(response, $"insert {table}", SaveFailed, cancellationToken);
        var rows = await response.Content.ReadFromJsonAsync<List<T>>(cancellationToken);
        return rows is { Count: > 0 }
            ? rows[0]
            : throw ApiException.Upstream(SaveFailed);
    }

    /// <summary>
    /// Inserts a row unless it would violate a unique constraint.
    /// Returns <c>false</c> when the row already existed.
    /// </summary>
    public async Task<bool> InsertIfAbsentAsync(string table, object row, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Post, table, row, "return=minimal", cancellationToken);
        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            return false;
        }

        await EnsureSuccessAsync(response, $"insert {table}", SaveFailed, cancellationToken);
        return true;
    }

    /// <summary>
    /// Inserts the row, or merges it into the existing row with the same
    /// <paramref name="conflictColumn"/>. Only the columns present in
    /// <paramref name="row"/> are written, so other columns keep their values.
    /// </summary>
    public async Task<T?> UpsertAsync<T>(string table, string conflictColumn, object row, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Post, $"{table}?on_conflict={conflictColumn}", row,
            "resolution=merge-duplicates,return=representation", cancellationToken);
        await EnsureSuccessAsync(response, $"upsert {table}", SaveFailed, cancellationToken);
        var rows = await response.Content.ReadFromJsonAsync<List<T>>(cancellationToken);
        return rows is { Count: > 0 } ? rows[0] : default;
    }

    /// <summary>Updates the rows matching <paramref name="filter"/> and returns them as stored.</summary>
    public async Task<List<T>> UpdateAsync<T>(string table, string filter, object changes, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Patch, $"{table}?{filter}", changes,
            "return=representation", cancellationToken);
        await EnsureSuccessAsync(response, $"update {table}", SaveFailed, cancellationToken);
        return await response.Content.ReadFromJsonAsync<List<T>>(cancellationToken) ?? [];
    }

    /// <summary>Deletes the rows matching <paramref name="filter"/>.</summary>
    public async Task DeleteAsync(string table, string filter, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Delete, $"{table}?{filter}", body: null,
            "return=minimal", cancellationToken);

        // 409 = still referenced by another table's foreign key.
        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            throw ApiException.Conflict("This item is still in use and can't be deleted.");
        }

        await EnsureSuccessAsync(response, $"delete {table}", SaveFailed, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method, string pathAndQuery, object? body, string? prefer, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, pathAndQuery);
        SupabaseHttp.Authorize(request, _settings.Key);
        if (prefer is not null)
        {
            request.Headers.Add("Prefer", prefer);
        }

        if (body is not null)
        {
            // Database column names are snake_case; row objects declare them explicitly.
            request.Content = JsonContent.Create(body, body.GetType(), options: SerializerOptions);
        }

        return await SupabaseHttp.SendAsync(http, request, logger, cancellationToken);
    }

    private async Task EnsureSuccessAsync(
        HttpResponseMessage response, string operation, string userMessage, CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            throw await SupabaseHttp.FailureAsync(response, logger, operation, userMessage, cancellationToken);
        }
    }

    /// <summary>Writes property names exactly as declared (no camelCase conversion).</summary>
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.General);
}
