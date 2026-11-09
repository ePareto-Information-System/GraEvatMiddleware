using GraEvatMiddleware.Application.Operations;
using GraEvatMiddleware.Domain.Retry;
using GraEvatMiddleware.Domain.Transactions;
using GraEvatMiddleware.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GraEvatMiddleware.Infrastructure.Monitoring;

internal sealed class OperationalStatusService(
    AppDbContext dbContext,
    IRequestMetrics requestMetrics) : IOperationalStatusService
{
    public async Task<OperationalStatus> GetAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var retryCounts = await dbContext.GraRetryJobs
            .AsNoTracking()
            .GroupBy(job => job.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.Status, item => item.Count, cancellationToken);
        var expiredLeases = await dbContext.GraRetryJobs
            .AsNoTracking()
            .CountAsync(job =>
                job.Status == RetryJobStatus.Processing &&
                job.LeaseExpiresAt != null &&
                job.LeaseExpiresAt <= now,
                cancellationToken);
        var pendingTransactions = await dbContext.Transactions
            .AsNoTracking()
            .CountAsync(transaction =>
                transaction.Status == TransactionStatus.PendingGra ||
                transaction.Status == TransactionStatus.TemporaryFailure,
                cancellationToken);

        int Count(RetryJobStatus status) => retryCounts.GetValueOrDefault(status);
        return new OperationalStatus(
            requestMetrics.GetSnapshot(),
            new RetryMetricsSnapshot(
                Count(RetryJobStatus.Scheduled),
                Count(RetryJobStatus.Processing),
                Count(RetryJobStatus.ReconciliationPending),
                Count(RetryJobStatus.Exhausted),
                expiredLeases),
            pendingTransactions,
            now);
    }
}
