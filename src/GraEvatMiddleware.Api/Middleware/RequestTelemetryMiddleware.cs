using System.Diagnostics;
using GraEvatMiddleware.Application.Operations;

namespace GraEvatMiddleware.Api.Middleware;

public sealed class RequestTelemetryMiddleware(
    RequestDelegate next,
    IRequestMetrics metrics,
    ILogger<RequestTelemetryMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        metrics.RequestStarted();
        using var scope = logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = context.TraceIdentifier
        });

        try
        {
            await next(context);
        }
        finally
        {
            stopwatch.Stop();
            metrics.RequestCompleted(context.Response.StatusCode, stopwatch.Elapsed);
            logger.LogInformation(
                "HTTP request completed: {Method} {Path} returned {StatusCode} in {ElapsedMilliseconds} ms.",
                context.Request.Method,
                context.Request.Path.Value,
                context.Response.StatusCode,
                Math.Round(stopwatch.Elapsed.TotalMilliseconds, 2));
        }
    }
}
