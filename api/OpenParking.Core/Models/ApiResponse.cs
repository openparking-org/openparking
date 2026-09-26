namespace OpenParking.Core.Models;

/// <summary>
/// Standard API envelope used on EVERY response.
/// Success:  { success: true,  data: T,    error: null }
/// Failure:  { success: false, data: null, error: { code, message, details } }
/// </summary>
public class ApiResponse<T>
{
    public bool Success { get; init; }
    public T? Data { get; init; }
    public ApiError? Error { get; init; }
    public string TraceId { get; init; } = string.Empty;

    public static ApiResponse<T> Ok(T data, string traceId = "") =>
        new() { Success = true, Data = data, TraceId = traceId };

    public static ApiResponse<T> Fail(string code, string message, string traceId = "", object? details = null) =>
        new()
        {
            Success = false,
            Error = new ApiError { Code = code, Message = message, Details = details },
            TraceId = traceId
        };
}

public class ApiError
{
    public string Code { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public object? Details { get; init; }
}
