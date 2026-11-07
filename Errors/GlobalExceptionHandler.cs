using Microsoft.AspNetCore.Diagnostics;

namespace Lumine.Backend.Errors;

/// <summary>
/// Turns every exception that escapes a request into a consistent
/// <see cref="ErrorResponse"/>. Expected failures (<see cref="ApiException"/>)
/// keep their status and message; anything else is logged in full and returned
/// as a generic 500, so internals never reach the client.
/// </summary>
internal sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    private const string GenericMessage = "Something went wrong. Please try again.";

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        // The client disconnected; there is nobody left to answer.
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            return true;
        }

        int statusCode;
        string message;

        if (exception is ApiException apiException)
        {
            statusCode = apiException.StatusCode;
            message = apiException.Message;

            if (statusCode >= StatusCodes.Status500InternalServerError)
            {
                logger.LogWarning("{Method} {Path} failed with {StatusCode}: {Message}",
                    httpContext.Request.Method, httpContext.Request.Path, statusCode, message);
            }
        }
        else
        {
            statusCode = StatusCodes.Status500InternalServerError;
            message = GenericMessage;
            logger.LogError(exception, "Unhandled exception on {Method} {Path}",
                httpContext.Request.Method, httpContext.Request.Path);
        }

        httpContext.Response.StatusCode = statusCode;
        if (exception is ApiException { RetryAfter: { } retryAfter })
        {
            httpContext.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();
        }

        await httpContext.Response.WriteAsJsonAsync(new ErrorResponse(message), cancellationToken);
        return true;
    }
}
