using Microsoft.AspNetCore.Mvc;

namespace Lumine.Backend.Errors;

/// <summary>
/// The body of every error response: <c>{ "error": "..." }</c>. The app reads
/// the <c>error</c> field and shows it as-is, so messages are user-facing and
/// never contain exception details.
/// </summary>
public sealed record ErrorResponse(string Error)
{
    /// <summary>
    /// Replaces the default validation <c>ProblemDetails</c> with the same
    /// <c>{ "error" }</c> shape, carrying the first readable validation message.
    /// </summary>
    public static IActionResult FromInvalidModelState(ActionContext context)
    {
        var message = context.ModelState.Values
            .SelectMany(entry => entry.Errors)
            .Select(error => error.ErrorMessage)
            .FirstOrDefault(text => !string.IsNullOrWhiteSpace(text));

        return new BadRequestObjectResult(new ErrorResponse(message ?? "The request is invalid."));
    }
}
