namespace GraEvatMiddleware.Domain.Retry;

public enum RetryJobStatus
{
    Scheduled = 1,
    Processing = 2,
    ReconciliationPending = 3,
    Completed = 4,
    Exhausted = 5
}
