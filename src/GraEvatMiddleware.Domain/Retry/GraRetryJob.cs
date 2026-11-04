using GraEvatMiddleware.Domain.Common;

namespace GraEvatMiddleware.Domain.Retry;

public sealed class GraRetryJob
{
    private GraRetryJob()
    {
    }

    private GraRetryJob(
        Guid id,
        Guid transactionId,
        Guid tenantId,
        string correlationId,
        string idempotencyKey,
        string payloadJson,
        RetryFailureClassification classification,
        string errorCode,
        int maxAttempts,
        DateTimeOffset nextAttemptAt,
        DateTimeOffset now)
    {
        Id = id;
        TransactionId = transactionId;
        TenantId = tenantId;
        CorrelationId = correlationId;
        IdempotencyKey = idempotencyKey;
        PayloadJson = payloadJson;
        FailureClassification = classification;
        LastErrorCode = errorCode;
        MaxAttempts = maxAttempts;
        NextAttemptAt = nextAttemptAt;
        Status = classification == RetryFailureClassification.Ambiguous
            ? RetryJobStatus.ReconciliationPending
            : RetryJobStatus.Scheduled;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }
    public Guid TransactionId { get; private set; }
    public Guid TenantId { get; private set; }
    public string CorrelationId { get; private set; } = string.Empty;
    public string IdempotencyKey { get; private set; } = string.Empty;
    public string PayloadJson { get; private set; } = string.Empty;
    public RetryFailureClassification FailureClassification { get; private set; }
    public RetryJobStatus Status { get; private set; }
    public int AttemptNumber { get; private set; }
    public int MaxAttempts { get; private set; }
    public DateTimeOffset NextAttemptAt { get; private set; }
    public DateTimeOffset? LastAttemptAt { get; private set; }
    public string? LastErrorCode { get; private set; }
    public string? LeaseOwner { get; private set; }
    public DateTimeOffset? LeaseExpiresAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static GraRetryJob Schedule(
        Guid transactionId,
        Guid tenantId,
        string correlationId,
        string idempotencyKey,
        string payloadJson,
        RetryFailureClassification classification,
        string errorCode,
        int maxAttempts,
        DateTimeOffset nextAttemptAt,
        DateTimeOffset now)
    {
        if (maxAttempts < 1)
        {
            throw new DomainValidationException("Retry maximum attempts must be at least one.");
        }

        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            throw new DomainValidationException("Retry payload is required.");
        }

        return new GraRetryJob(
            Guid.NewGuid(), transactionId, tenantId, correlationId, idempotencyKey,
            payloadJson, classification, errorCode, maxAttempts, nextAttemptAt, now);
    }

    public bool TryClaim(string workerId, DateTimeOffset now, TimeSpan leaseDuration)
    {
        var claimableStatus = Status is RetryJobStatus.Scheduled or RetryJobStatus.ReconciliationPending ||
            Status == RetryJobStatus.Processing && LeaseExpiresAt <= now;
        if (!claimableStatus ||
            NextAttemptAt > now ||
            LeaseExpiresAt > now ||
            AttemptNumber >= MaxAttempts)
        {
            return false;
        }

        Status = RetryJobStatus.Processing;
        AttemptNumber++;
        LastAttemptAt = now;
        LeaseOwner = workerId;
        LeaseExpiresAt = now.Add(leaseDuration);
        UpdatedAt = now;
        return true;
    }

    public void ScheduleRetry(string errorCode, DateTimeOffset nextAttemptAt, DateTimeOffset now)
    {
        EnsureProcessing();
        FailureClassification = RetryFailureClassification.Temporary;
        Status = RetryJobStatus.Scheduled;
        LastErrorCode = errorCode;
        NextAttemptAt = nextAttemptAt;
        ReleaseLease(now);
    }

    public void ScheduleReconciliation(string errorCode, DateTimeOffset nextAttemptAt, DateTimeOffset now)
    {
        EnsureProcessing();
        FailureClassification = RetryFailureClassification.Ambiguous;
        Status = RetryJobStatus.ReconciliationPending;
        LastErrorCode = errorCode;
        NextAttemptAt = nextAttemptAt;
        ReleaseLease(now);
    }

    public void Complete(DateTimeOffset now)
    {
        EnsureProcessing();
        Status = RetryJobStatus.Completed;
        LastErrorCode = null;
        ReleaseLease(now);
    }

    public void Exhaust(string errorCode, DateTimeOffset now)
    {
        EnsureProcessing();
        Status = RetryJobStatus.Exhausted;
        LastErrorCode = errorCode;
        ReleaseLease(now);
    }

    private void ReleaseLease(DateTimeOffset now)
    {
        LeaseOwner = null;
        LeaseExpiresAt = null;
        UpdatedAt = now;
    }

    private void EnsureProcessing()
    {
        if (Status != RetryJobStatus.Processing)
        {
            throw new DomainValidationException("Retry job must be processing before it can be completed or rescheduled.");
        }
    }
}
