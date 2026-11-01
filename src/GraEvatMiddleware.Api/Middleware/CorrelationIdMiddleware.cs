namespace GraEvatMiddleware.Api.Middleware;

public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    private const string HeaderName = "X-Correlation-ID";

    public async Task InvokeAsync(HttpContext context)
    {
        var supplied = context.Request.Headers[HeaderName].ToString().Trim();
        context.TraceIdentifier = string.IsNullOrWhiteSpace(supplied) || supplied.Length > 100
            ? Guid.NewGuid().ToString("N")
            : supplied;
        context.Response.Headers[HeaderName] = context.TraceIdentifier;
        await next(context);
    }
}
