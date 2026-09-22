namespace smartHRMS.Application.Common.Models;

/// <summary>
/// Standard envelope returned by every API endpoint.
/// Success: <c>errors</c> is null. Failure: <c>data</c> is null and <c>errors</c> lists the details.
/// </summary>
public class ApiResponse<T>
{
    public bool Success { get; init; }

    public string Message { get; init; } = string.Empty;

    public T? Data { get; init; }

    public IReadOnlyList<string>? Errors { get; init; }

    public static ApiResponse<T> Ok(T data, string message = "Request completed successfully.")
    {
        return new ApiResponse<T>
        {
            Success = true,
            Message = message,
            Data = data,
        };
    }

    public static ApiResponse<T> Fail(string message, IEnumerable<string>? errors = null)
    {
        return new ApiResponse<T>
        {
            Success = false,
            Message = message,
            Errors = (errors ?? Enumerable.Empty<string>()).ToList(),
        };
    }
}

/// <summary>
/// Shortcuts for responses that carry no data payload.
/// </summary>
public static class ApiResponse
{
    public static ApiResponse<object> Ok(string message = "Request completed successfully.")
    {
        return new ApiResponse<object>
        {
            Success = true,
            Message = message,
        };
    }

    public static ApiResponse<object> Fail(string message, IEnumerable<string>? errors = null)
    {
        return ApiResponse<object>.Fail(message, errors);
    }
}
