namespace Lumine.Backend.Errors;

/// <summary>
/// An expected failure with an HTTP status code and a message that is safe to
/// show to the user. Thrown by services and translated into an
/// <see cref="ErrorResponse"/> by <see cref="GlobalExceptionHandler"/>.
/// </summary>
public sealed class ApiException(int statusCode, string message, TimeSpan? retryAfter = null) : Exception(message)
{
    public int StatusCode { get; } = statusCode;

    /// <summary>How long the client should wait before retrying; sent as the Retry-After header.</summary>
    public TimeSpan? RetryAfter { get; } = retryAfter;

    public static ApiException BadRequest(string message) => new(StatusCodes.Status400BadRequest, message);
    public static ApiException Unauthorized(string message, TimeSpan? retryAfter = null) =>
        new(StatusCodes.Status401Unauthorized, message, retryAfter);
    public static ApiException Forbidden(string message) => new(StatusCodes.Status403Forbidden, message);
    public static ApiException NotFound(string message) => new(StatusCodes.Status404NotFound, message);
    public static ApiException Conflict(string message) => new(StatusCodes.Status409Conflict, message);
    public static ApiException TooManyRequests(string message, TimeSpan? retryAfter = null) =>
        new(StatusCodes.Status429TooManyRequests, message, retryAfter);

    /// <summary>Supabase answered with an error we can't recover from.</summary>
    public static ApiException Upstream(string message) => new(StatusCodes.Status502BadGateway, message);

    /// <summary>Supabase couldn't be reached at all.</summary>
    public static ApiException Unavailable(string message) => new(StatusCodes.Status503ServiceUnavailable, message);
}
