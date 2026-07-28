using System.Diagnostics;
using System.Net;
using System.Text.Json;

namespace OrderFlow.Api.Middleware;

public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var requestId = context.TraceIdentifier;
        var stopwatch = Stopwatch.StartNew();

        context.Response.Headers["X-Request-Id"] = requestId;

        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Unhandled exception caught by global middleware. RequestId: {RequestId}, Method: {Method}, Path: {Path}, Duration: {Duration}ms",
                requestId, context.Request.Method, context.Request.Path, stopwatch.ElapsedMilliseconds);

            await HandleExceptionAsync(context, ex, requestId, stopwatch.ElapsedMilliseconds);
        }
        finally
        {
            stopwatch.Stop();
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception, string requestId, long durationMs)
    {
        var (statusCode, message) = exception switch
        {
            KeyNotFoundException ex => (HttpStatusCode.NotFound, ex.Message),
            ArgumentException ex => (HttpStatusCode.BadRequest, ex.Message),
            UnauthorizedAccessException ex => (HttpStatusCode.Forbidden, "Access denied."),
            InvalidOperationException ex => (HttpStatusCode.BadRequest, ex.Message),
            TimeoutException => (HttpStatusCode.RequestTimeout, "The request timed out."),
            OperationCanceledException => (HttpStatusCode.ServiceUnavailable, "The operation was cancelled."),
            _ => (HttpStatusCode.InternalServerError, "An internal server error occurred.")
        };

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;

        var response = new
        {
            error = new
            {
                statusCode = (int)statusCode,
                message,
                requestId,
                timestamp = DateTime.UtcNow,
                durationMs
            }
        };

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(response, jsonOptions));
    }
}
