using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using OpenParking.Api.Middleware;
using Xunit;

namespace OpenParking.Tests;

public class GlobalExceptionMiddlewareTests
{
    [Fact]
    public async Task ConcurrentSlotUpdate_ReturnsStructuredConflict()
    {
        var context = new DefaultHttpContext();
        using var body = new MemoryStream();
        context.Response.Body = body;
        var middleware = new GlobalExceptionMiddleware(
            _ => throw new DbUpdateConcurrencyException("database details"),
            Mock.Of<ILogger<GlobalExceptionMiddleware>>());
        await middleware.InvokeAsync(context);
        Assert.Equal(StatusCodes.Status409Conflict, context.Response.StatusCode);
        body.Position = 0;
        using var response = await JsonDocument.ParseAsync(body);
        Assert.Equal("CONFLICT", response.RootElement.GetProperty("error").GetProperty("code").GetString());
        Assert.DoesNotContain("database details", response.RootElement.ToString());
    }
}
