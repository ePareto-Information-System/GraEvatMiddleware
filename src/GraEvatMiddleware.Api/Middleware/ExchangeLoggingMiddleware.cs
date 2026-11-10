using System.Diagnostics;
using System.Security.Claims;
using System.Text;
using GraEvatMiddleware.Application.Logging;
using GraEvatMiddleware.Domain.Integration;
using Microsoft.Extensions.Options;

namespace GraEvatMiddleware.Api.Middleware;

public sealed class ExchangeLoggingMiddleware(
    RequestDelegate next,
    IOptions<ExchangeLoggingOptions> options,
    IExchangeContentSanitizer sanitizer,
    ILogger<ExchangeLoggingMiddleware> logger)
{
    public const string TransactionIdItemKey = "IntegrationExchange.TransactionId";

    public async Task InvokeAsync(HttpContext context, IIntegrationExchangeWriter writer)
    {
        if (!options.Value.Enabled)
        {
            await next(context);
            return;
        }

        var requestBody = await ReadRequestBodyAsync(context.Request, context.RequestAborted);
        var originalResponseBody = context.Response.Body;
        await using var capturedResponseBody = new MemoryStream();
        context.Response.Body = capturedResponseBody;
        var stopwatch = Stopwatch.StartNew();

        try
        {
            await next(context);
        }
        finally
        {
            stopwatch.Stop();
            capturedResponseBody.Position = 0;
            var responseBody = await new StreamReader(capturedResponseBody, Encoding.UTF8, leaveOpen: true)
                .ReadToEndAsync(CancellationToken.None);
            capturedResponseBody.Position = 0;
            await capturedResponseBody.CopyToAsync(originalResponseBody, CancellationToken.None);
            context.Response.Body = originalResponseBody;
            await TryWriteAsync(context, writer, requestBody, responseBody, stopwatch.ElapsedMilliseconds);
        }
    }

    private async Task TryWriteAsync(HttpContext context, IIntegrationExchangeWriter writer,
        string? rawRequestBody, string? rawResponseBody, long durationMilliseconds)
    {
        try
        {
            var setting = options.Value;
            var request = setting.CaptureBodies
                ? sanitizer.SanitizeBody(rawRequestBody, context.Request.ContentType, setting.MaximumBodyCharacters)
                : new SanitizedContent(null, false, false);
            var response = setting.CaptureBodies
                ? sanitizer.SanitizeBody(rawResponseBody, context.Response.ContentType, setting.MaximumBodyCharacters)
                : new SanitizedContent(null, false, false);
            Guid? tenantId = Guid.TryParse(context.User.FindFirstValue("tenant_id"), out var parsedTenantId)
                ? parsedTenantId : null;
            Guid? transactionId = context.Items.TryGetValue(TransactionIdItemKey, out var value) &&
                                  value is Guid parsedTransactionId ? parsedTransactionId : null;
            var statusCode = context.Response.StatusCode;
            var outcome = statusCode < 400 ? ExchangeOutcome.Succeeded :
                statusCode < 500 ? ExchangeOutcome.Rejected : ExchangeOutcome.Failed;

            await writer.WriteAsync(new IntegrationExchangeRecord(
                tenantId, transactionId, context.TraceIdentifier,
                context.Request.Headers["Idempotency-Key"].FirstOrDefault(),
                ExchangeDirection.Inbound, "ExternalClient", "Middleware",
                context.GetEndpoint()?.DisplayName ?? context.Request.Path.Value ?? "Unknown",
                context.Request.Method, SanitizePath(context.Request.Path.Value),
                sanitizer.SanitizeHeaders(ToDictionary(context.Request.Headers)), request.Content,
                request.IsTruncated, statusCode,
                sanitizer.SanitizeHeaders(ToDictionary(context.Response.Headers)), response.Content,
                response.IsTruncated, durationMilliseconds, 1, outcome, null, DateTimeOffset.UtcNow),
                CancellationToken.None);
        }
        catch
        {
            logger.LogWarning("An integration exchange log could not be persisted.");
        }
    }

    private static async Task<string?> ReadRequestBodyAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        if (request.ContentLength is null or 0) return null;
        request.EnableBuffering();
        using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
        var body = await reader.ReadToEndAsync(cancellationToken);
        request.Body.Position = 0;
        return body;
    }

    private static Dictionary<string, string[]> ToDictionary(IHeaderDictionary headers) =>
        headers.ToDictionary(
            item => item.Key,
            item => item.Value.Select(value => value ?? string.Empty).ToArray(),
            StringComparer.OrdinalIgnoreCase);

    private static string SanitizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return "/";
        var marker = path.IndexOf("/identifications/", StringComparison.OrdinalIgnoreCase);
        if (marker < 0) return path;
        var finalSeparator = path.LastIndexOf('/');
        return finalSeparator > marker ? string.Concat(path.AsSpan(0, finalSeparator + 1), "[REDACTED]") : path;
    }
}
