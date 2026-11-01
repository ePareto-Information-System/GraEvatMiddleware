using GraEvatMiddleware.Domain.Transactions;
using GraEvatMiddleware.Domain.Common;

namespace GraEvatMiddleware.UnitTests;

public sealed class TransactionStatusTests
{
    [Fact]
    public void PendingGra_IsDifferentFromTerminalStatuses()
    {
        Assert.NotEqual(TransactionStatus.Succeeded, TransactionStatus.PendingGra);
        Assert.NotEqual(TransactionStatus.Rejected, TransactionStatus.PendingGra);
        Assert.NotEqual(TransactionStatus.PermanentFailure, TransactionStatus.PendingGra);
    }

    [Fact]
    public void InvoiceTransaction_FollowsSuccessfulSubmissionLifecycle()
    {
        var createdAt = DateTimeOffset.Parse("2026-11-01T10:00:00Z");
        var transaction = EVatTransaction.CreateInvoice(
            Guid.NewGuid(),
            "INV-001",
            "idempotency-key",
            "request-hash",
            "correlation-id",
            createdAt);

        transaction.MarkValidating(createdAt.AddSeconds(1));
        transaction.MarkSubmitting(createdAt.AddSeconds(2));
        transaction.MarkSucceeded("GRA-REFERENCE", createdAt.AddSeconds(3));

        Assert.Equal(TransactionStatus.Succeeded, transaction.Status);
        Assert.Equal("GRA-REFERENCE", transaction.ExternalReference);
        Assert.Null(transaction.ErrorCode);
    }

    [Fact]
    public void InvoiceTransaction_RejectsInvalidTransition()
    {
        var transaction = EVatTransaction.CreateInvoice(
            Guid.NewGuid(),
            "INV-002",
            "idempotency-key",
            "request-hash",
            "correlation-id",
            DateTimeOffset.UtcNow);

        Assert.Throws<DomainValidationException>(() =>
            transaction.MarkSucceeded("GRA-REFERENCE", DateTimeOffset.UtcNow));
    }
}
