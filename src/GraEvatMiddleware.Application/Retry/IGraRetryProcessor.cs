namespace GraEvatMiddleware.Application.Retry;

public interface IGraRetryProcessor
{
    Task<int> ProcessDueAsync(
        string workerId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);
}
