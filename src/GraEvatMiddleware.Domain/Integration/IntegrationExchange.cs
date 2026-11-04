namespace GraEvatMiddleware.Domain.Integration;

public sealed class IntegrationExchange
{
    private IntegrationExchange()
    {
    }

    public Guid Id { get; private set; }
    public Guid? TenantId { get; private set; }
    public Guid? TransactionId { get; private set; }
    public string CorrelationId { get; private set; } = string.Empty;
    public string? IdempotencyKey { get; private set; }
    public ExchangeDirection Direction { get; private set; }
    public string SourceSystem { get; private set; } = string.Empty;
    public string DestinationSystem { get; private set; } = string.Empty;
    public string Operation { get; private set; } = string.Empty;
    public string HttpMethod { get; private set; } = string.Empty;
    public string Url { get; private set; } = string.Empty;
    public string? RequestHeadersJson { get; private set; }
    public string? RequestBody { get; private set; }
    public bool RequestBodyTruncated { get; private set; }
    public int? ResponseStatusCode { get; private set; }
    public string? ResponseHeadersJson { get; private set; }
    public string? ResponseBody { get; private set; }
    public bool ResponseBodyTruncated { get; private set; }
    public long DurationMilliseconds { get; private set; }
    public int AttemptNumber { get; private set; }
    public ExchangeOutcome Outcome { get; private set; }
    public string? ErrorCode { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static IntegrationExchange Capture(
        Guid? tenantId, Guid? transactionId, string correlationId, string? idempotencyKey,
        ExchangeDirection direction, string sourceSystem, string destinationSystem, string operation,
        string httpMethod, string url, string? requestHeadersJson, string? requestBody,
        bool requestBodyTruncated, int? responseStatusCode, string? responseHeadersJson,
        string? responseBody, bool responseBodyTruncated, long durationMilliseconds,
        int attemptNumber, ExchangeOutcome outcome, string? errorCode, DateTimeOffset createdAt)
    {
        return new IntegrationExchange
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TransactionId = transactionId,
            CorrelationId = correlationId,
            IdempotencyKey = idempotencyKey,
            Direction = direction,
            SourceSystem = sourceSystem,
            DestinationSystem = destinationSystem,
            Operation = operation,
            HttpMethod = httpMethod,
            Url = url,
            RequestHeadersJson = requestHeadersJson,
            RequestBody = requestBody,
            RequestBodyTruncated = requestBodyTruncated,
            ResponseStatusCode = responseStatusCode,
            ResponseHeadersJson = responseHeadersJson,
            ResponseBody = responseBody,
            ResponseBodyTruncated = responseBodyTruncated,
            DurationMilliseconds = Math.Max(0, durationMilliseconds),
            AttemptNumber = Math.Max(1, attemptNumber),
            Outcome = outcome,
            ErrorCode = errorCode,
            CreatedAt = createdAt
        };
    }
}
