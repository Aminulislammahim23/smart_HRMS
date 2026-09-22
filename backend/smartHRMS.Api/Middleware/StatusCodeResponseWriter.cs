using Microsoft.AspNetCore.Diagnostics;
using smartHRMS.Application.Common.Models;

namespace smartHRMS.Api.Middleware;

/// <summary>
/// Gives body-less error responses (unknown route, 405, 415, ...) the standard <see cref="ApiResponse{T}"/> envelope.
/// </summary>
public static class StatusCodeResponseWriter
{
    public static Task WriteAsync(StatusCodeContext context)
    {
        var httpContext = context.HttpContext;
        var statusCode = httpContext.Response.StatusCode;

        var message = statusCode switch
        {
            StatusCodes.Status404NotFound => "Resource not found.",
            StatusCodes.Status405MethodNotAllowed => "Method not allowed.",
            StatusCodes.Status415UnsupportedMediaType => "Unsupported media type.",
            StatusCodes.Status401Unauthorized => "Authentication is required.",
            StatusCodes.Status403Forbidden => "You do not have permission to perform this action.",
            >= 500 => "An unexpected error occurred.",
            _ => "The request could not be processed.",
        };

        return httpContext.Response.WriteAsJsonAsync(ApiResponse.Fail(message));
    }
}
