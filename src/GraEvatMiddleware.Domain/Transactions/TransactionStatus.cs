namespace GraEvatMiddleware.Domain.Transactions;

public enum TransactionStatus
{
    Received = 1,
    Validating = 2,
    Submitting = 3,
    PendingGra = 4,
    Succeeded = 5,
    Rejected = 6,
    TemporaryFailure = 7,
    PermanentFailure = 8
}
