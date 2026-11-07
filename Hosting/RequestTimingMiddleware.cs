using System.Diagnostics;
using System.Globalization;

namespace Lumine.Backend.Hosting;

/// <summary>
/// Measures how long the API takes to handle each request. The time is logged
/// ("GET /api/jewelry responded 200 in 143.2 ms") and returned in the standard
/// <c>Server-Timing</c> response header (<c>app;dur=143.2</c>), so it can be
/// read from the server log, curl, Postman or the app's network log.
/// </summary>
/// <remarks>
/// This is the server's share of the response time, including its calls to
/// Supabase. What a phone experiences adds the network round trip on top.
/// </remarks>
internal sealed class RequestTimingMiddleware(RequestDelegate next, ILogger<RequestTimingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var started = Stopwatch.GetTimestamp();

        context.Response.OnStarting(() =>
        {
            var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            context.Response.Headers["Server-Timing"] =
                "app;dur=" + elapsed.ToString("0.0", CultureInfo.InvariantCulture);
            return Task.CompletedTask;
        });

        try
        {
            await next(context);
        }
        finally
        {
            logger.LogInformation("{Method} {Path} responded {StatusCode} in {ElapsedMs:0.0} ms",
                context.Request.Method,
                context.Request.Path,
                context.Response.StatusCode,
                Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        }
    }
}
