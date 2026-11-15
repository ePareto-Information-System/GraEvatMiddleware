using GraEvatMiddleware.Domain.Integration;

namespace GraEvatMiddleware.Application.Logging;

public sealed record SanitizedContent(string? Content, bool IsTruncated, bool IsSupported);

public interface IExchangeContentSanitizer
{
    string SanitizeHeaders(IReadOnlyDictionary<string, string[]> headers);
    SanitizedContent SanitizeBody(string? body, string? contentType, int maximumCharacters);
}

public sealed record IntegrationExchangeRecord(
    Guid? TenantId, Guid? TransactionId, string CorrelationId, string? IdempotencyKey,
    ExchangeDirection Direction, string SourceSystem, string DestinationSystem, string Operation,
    string HttpMethod, string Url, string? RequestHeadersJson, string? RequestBody,
    bool RequestBodyTruncated, int? ResponseStatusCode, string? ResponseHeadersJson,
    string? ResponseBody, bool ResponseBodyTruncated, long DurationMilliseconds,
    int AttemptNumber, ExchangeOutcome Outcome, string? ErrorCode, DateTimeOffset CreatedAt);

public interface IIntegrationExchangeWriter
{
    Task WriteAsync(IntegrationExchangeRecord record, CancellationToken cancellationToken = default);
}

public interface IExchangeLogRetentionService
{
    Task<int> DeleteExpiredAsync(DateTimeOffset now, CancellationToken cancellationToken = default);
}
