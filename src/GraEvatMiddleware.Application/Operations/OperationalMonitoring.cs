namespace GraEvatMiddleware.Application.Operations;

public interface IRequestMetrics
{
    void RequestStarted();
    void RequestCompleted(int statusCode, TimeSpan duration);
    RequestMetricsSnapshot GetSnapshot();
}

public interface IOperationalStatusService
{
    Task<OperationalStatus> GetAsync(CancellationToken cancellationToken = default);
}

public sealed record RequestMetricsSnapshot(
    long Started,
    long Completed,
    long Failed,
    long RateLimited,
    double AverageDurationMilliseconds);

public sealed record RetryMetricsSnapshot(
    int Scheduled,
    int Processing,
    int ReconciliationPending,
    int Exhausted,
    int ExpiredLeases);

public sealed record OperationalStatus(
    RequestMetricsSnapshot Requests,
    RetryMetricsSnapshot Retries,
    int PendingTransactions,
    DateTimeOffset Timestamp);
