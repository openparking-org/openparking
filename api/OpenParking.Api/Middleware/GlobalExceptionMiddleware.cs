using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OpenParking.Core.Models;
using System.Net;
using System.Text.Json;

namespace OpenParking.Api.Middleware;

/// <summary>
/// Catches ALL unhandled exceptions before they reach the client.
///
/// Known AppExceptions → structured JSON error (e.g. 400 SLOT_UNAVAILABLE).
/// Unknown exceptions  → 500 INTERNAL_ERROR with TraceId for debugging.
///
/// The raw exception details are NEVER sent to the client — they are logged
/// server-side so you can grep by TraceId in production logs.
/// </summary>
public class GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (AppException ex)
        {
            // Known business rule exception — log as Warning (not Error)
            var traceId = context.TraceIdentifier;
            logger.LogWarning(
                "AppException [{TraceId}] {Code}: {Message} | Path={Path}",
                traceId, ex.Code, ex.Message, context.Request.Path);

            context.Response.StatusCode = ex.StatusCode;
            context.Response.ContentType = "application/json";

            var response = ApiResponse<object>.Fail(ex.Code, ex.Message, traceId);
            await context.Response.WriteAsync(JsonSerializer.Serialize(response, _jsonOptions));
        }
        catch (DbUpdateConcurrencyException ex)
        {
            logger.LogWarning(ex, "Concurrent update [{TraceId}] | Path={Path}",
                context.TraceIdentifier, context.Request.Path);
            context.Response.StatusCode = StatusCodes.Status409Conflict;
            context.Response.ContentType = "application/json";
            var response = ApiResponse<object>.Fail(ErrorCodes.Conflict,
                "The parking state changed during this request. Refresh and try again.", context.TraceIdentifier);
            await context.Response.WriteAsync(JsonSerializer.Serialize(response, _jsonOptions));
        }
        catch (Exception ex) when (IsSerializationConflict(ex))
        {
            logger.LogWarning(ex, "Concurrent gate operation [{TraceId}]", context.TraceIdentifier);
            context.Response.StatusCode = StatusCodes.Status409Conflict;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(ApiResponse<object>.Fail(ErrorCodes.Conflict,
                "Another attendant updated this parking session. Refresh and try again.", context.TraceIdentifier), _jsonOptions));
        }
        catch (Exception ex)
        {
            // Unknown exception — log as Error with full stack trace
            var traceId = context.TraceIdentifier;
            logger.LogError(ex,
                "Unhandled exception [{TraceId}] | Path={Path} | Method={Method}",
                traceId, context.Request.Path, context.Request.Method);

            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            context.Response.ContentType = "application/json";

            var response = ApiResponse<object>.Fail(
                ErrorCodes.InternalError,
                "An unexpected error occurred. Please contact support with the TraceId.",
                traceId);

            await context.Response.WriteAsync(JsonSerializer.Serialize(response, _jsonOptions));
        }
    }

    private static bool IsSerializationConflict(Exception error) =>
        error is Npgsql.PostgresException { SqlState: "40001" or "40P01" } ||
        error.InnerException != null && IsSerializationConflict(error.InnerException);
}
