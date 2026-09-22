using Microsoft.AspNetCore.Diagnostics;
using smartHRMS.Application.Common.Exceptions;
using smartHRMS.Application.Common.Models;

namespace smartHRMS.Api.Middleware;

public class AppExceptionHandler : IExceptionHandler
{
    // Non-standard status (nginx convention) for a client that disconnected before the response was sent.
    private const int ClientClosedRequest = 499;

    private readonly ILogger<AppExceptionHandler> _logger;

    public AppExceptionHandler(ILogger<AppExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            _logger.LogDebug("Request was cancelled by the client.");
            httpContext.Response.StatusCode = ClientClosedRequest;
            return true;
        }

        var (statusCode, message) = exception switch
        {
            NotFoundException => (StatusCodes.Status404NotFound, "Resource not found."),
            ConflictException => (StatusCodes.Status409Conflict, "Conflict."),
            BadRequestException => (StatusCodes.Status400BadRequest, "Bad request."),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred."),
        };

        ApiResponse<object> response;

        if (statusCode == StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(exception, "Unhandled exception occurred");

            // Never leak internal exception details to the client.
            response = ApiResponse.Fail(message);
        }
        else
        {
            response = ApiResponse.Fail(message, new[] { exception.Message });
        }

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(response, cancellationToken);

        return true;
    }
}
