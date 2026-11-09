using GraEvatMiddleware.Domain.Common;
using GraEvatMiddleware.Domain.Tenants;

namespace GraEvatMiddleware.Domain.Transactions;

public sealed class EVatTransaction
{
    private EVatTransaction()
    {
    }

    private EVatTransaction(
        Guid id,
        Guid tenantId,
        TransactionOperation operation,
        string invoiceNumber,
        string idempotencyKey,
        string requestHash,
        string correlationId,
        DateTimeOffset now)
    {
        Id = id;
        TenantId = tenantId;
        Operation = operation;
        InvoiceNumber = invoiceNumber;
        IdempotencyKey = idempotencyKey;
        RequestHash = requestHash;
        CorrelationId = correlationId;
        Status = TransactionStatus.Received;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Tenant Tenant { get; private set; } = null!;

    public TransactionOperation Operation { get; private set; }

    public string InvoiceNumber { get; private set; } = string.Empty;

    public string IdempotencyKey { get; private set; } = string.Empty;

    public string RequestHash { get; private set; } = string.Empty;

    public string CorrelationId { get; private set; } = string.Empty;

    public TransactionStatus Status { get; private set; }

    public string? ExternalReference { get; private set; }

    public string? ErrorCode { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static EVatTransaction CreateInvoice(
        Guid tenantId,
        string invoiceNumber,
        string idempotencyKey,
        string requestHash,
        string correlationId,
        DateTimeOffset now)
    {
        return Create(
            tenantId,
            TransactionOperation.Invoice,
            invoiceNumber,
            idempotencyKey,
            requestHash,
            correlationId,
            now);
    }

    public static EVatTransaction Create(
        Guid tenantId,
        TransactionOperation operation,
        string invoiceNumber,
        string idempotencyKey,
        string requestHash,
        string correlationId,
        DateTimeOffset now)
    {
        if (!Enum.IsDefined(operation))
        {
            throw new DomainValidationException("Transaction operation is invalid.");
        }

        if (string.IsNullOrWhiteSpace(invoiceNumber))
        {
            throw new DomainValidationException("Invoice number is required.");
        }

        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            throw new DomainValidationException("Idempotency key is required.");
        }

        return new EVatTransaction(
            Guid.NewGuid(),
            tenantId,
            operation,
            invoiceNumber,
            idempotencyKey,
            requestHash,
            correlationId,
            now);
    }

    public void MarkValidating(DateTimeOffset now)
    {
        TransitionFrom(TransactionStatus.Received, TransactionStatus.Validating, now);
    }

    public void MarkSubmitting(DateTimeOffset now)
    {
        TransitionFrom(TransactionStatus.Validating, TransactionStatus.Submitting, now);
    }

    public void MarkSucceeded(string? externalReference, DateTimeOffset now)
    {
        EnsureStatus(TransactionStatus.Submitting);
        Status = TransactionStatus.Succeeded;
        ExternalReference = externalReference;
        ErrorCode = null;
        UpdatedAt = now;
    }

    public void MarkPending(string errorCode, DateTimeOffset now)
    {
        EnsureStatus(TransactionStatus.Submitting);
        Status = TransactionStatus.PendingGra;
        ErrorCode = errorCode;
        UpdatedAt = now;
    }

    public void MarkRejected(string errorCode, DateTimeOffset now)
    {
        EnsureStatus(TransactionStatus.Submitting);
        Status = TransactionStatus.Rejected;
        ErrorCode = errorCode;
        UpdatedAt = now;
    }

    public void MarkTemporaryFailure(string errorCode, DateTimeOffset now)
    {
        EnsureStatus(TransactionStatus.Submitting);
        Status = TransactionStatus.TemporaryFailure;
        ErrorCode = errorCode;
        UpdatedAt = now;
    }

    public void MarkRetrying(DateTimeOffset now)
    {
        if (Status is not (TransactionStatus.PendingGra or TransactionStatus.TemporaryFailure))
        {
            throw new DomainValidationException($"Transaction cannot be retried from {Status}.");
        }

        Status = TransactionStatus.Submitting;
        UpdatedAt = now;
    }

    public void MarkReconciledSucceeded(string? externalReference, DateTimeOffset now)
    {
        if (Status is not (TransactionStatus.PendingGra or TransactionStatus.TemporaryFailure))
        {
            throw new DomainValidationException($"Transaction cannot be reconciled from {Status}.");
        }

        Status = TransactionStatus.Succeeded;
        ExternalReference = externalReference;
        ErrorCode = null;
        UpdatedAt = now;
    }

    private void TransitionFrom(TransactionStatus expected, TransactionStatus next, DateTimeOffset now)
    {
        EnsureStatus(expected);
        Status = next;
        UpdatedAt = now;
    }

    private void EnsureStatus(TransactionStatus expected)
    {
        if (Status != expected)
        {
            throw new DomainValidationException($"Transaction cannot transition from {Status}; expected {expected}.");
        }
    }
}
