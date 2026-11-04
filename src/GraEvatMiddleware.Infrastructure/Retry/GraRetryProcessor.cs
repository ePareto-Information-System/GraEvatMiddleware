using System.Text.Json;
using GraEvatMiddleware.Application.Abstractions;
using GraEvatMiddleware.Application.Invoices;
using GraEvatMiddleware.Application.Retry;
using GraEvatMiddleware.Domain.Auditing;
using GraEvatMiddleware.Domain.Retry;
using GraEvatMiddleware.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GraEvatMiddleware.Infrastructure.Retry;

internal sealed class GraRetryProcessor(
    AppDbContext dbContext,
    IGraEvatClient graClient,
    IOptions<GraRetryOptions> options) : IGraRetryProcessor
{
    private readonly GraRetryOptions _options = options.Value;

    public async Task<int> ProcessDueAsync(
        string workerId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var job = await ClaimNextAsync(workerId, now, cancellationToken);
        if (job is null)
        {
            return 0;
        }

        await ProcessClaimedAsync(job, now, cancellationToken);
        return 1;
    }

    private async Task<GraRetryJob?> ClaimNextAsync(
        string workerId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var dueQuery = dbContext.GraRetryJobs
            .Where(item =>
                (item.Status == RetryJobStatus.Scheduled ||
                 item.Status == RetryJobStatus.ReconciliationPending ||
                 item.Status == RetryJobStatus.Processing && item.LeaseExpiresAt <= now) &&
                item.NextAttemptAt <= now &&
                item.AttemptNumber < item.MaxAttempts &&
                (item.LeaseExpiresAt == null || item.LeaseExpiresAt <= now));

        if (string.Equals(
                dbContext.Database.ProviderName,
                "Microsoft.EntityFrameworkCore.InMemory",
                StringComparison.Ordinal))
        {
            var inMemoryJob = await dueQuery.OrderBy(item => item.NextAttemptAt)
                .FirstOrDefaultAsync(cancellationToken);
            if (inMemoryJob is null ||
                !inMemoryJob.TryClaim(workerId, now, TimeSpan.FromSeconds(_options.LeaseSeconds)))
            {
                return null;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            return inMemoryJob;
        }

        var candidateId = await dueQuery.AsNoTracking()
            .OrderBy(item => item.NextAttemptAt)
            .Select(item => (Guid?)item.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (candidateId is null)
        {
            return null;
        }

        var leaseExpiresAt = now.AddSeconds(_options.LeaseSeconds);
        var updated = await dbContext.GraRetryJobs
            .Where(item => item.Id == candidateId &&
                (item.Status == RetryJobStatus.Scheduled ||
                 item.Status == RetryJobStatus.ReconciliationPending ||
                 item.Status == RetryJobStatus.Processing && item.LeaseExpiresAt <= now) &&
                item.NextAttemptAt <= now &&
                item.AttemptNumber < item.MaxAttempts &&
                (item.LeaseExpiresAt == null || item.LeaseExpiresAt <= now))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.Status, RetryJobStatus.Processing)
                .SetProperty(item => item.AttemptNumber, item => item.AttemptNumber + 1)
                .SetProperty(item => item.LastAttemptAt, now)
                .SetProperty(item => item.LeaseOwner, workerId)
                .SetProperty(item => item.LeaseExpiresAt, leaseExpiresAt)
                .SetProperty(item => item.UpdatedAt, now), cancellationToken);
        if (updated == 0)
        {
            return null;
        }

        dbContext.ChangeTracker.Clear();
        return await dbContext.GraRetryJobs.SingleAsync(item => item.Id == candidateId, cancellationToken);
    }

    private async Task ProcessClaimedAsync(
        GraRetryJob job,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var transaction = await dbContext.Transactions.SingleAsync(
            item => item.Id == job.TransactionId && item.TenantId == job.TenantId,
            cancellationToken);
        var tenant = await dbContext.Tenants.SingleAsync(
            item => item.Id == job.TenantId,
            cancellationToken);

        try
        {
            var reconciliation = await graClient.ReconcileInvoiceAsync(
                new GraInvoiceReconciliation(
                    transaction.Id,
                    tenant.Id,
                    job.CorrelationId,
                    job.IdempotencyKey,
                    "InvoiceReconciliation",
                    tenant.TaxpayerReference,
                    tenant.GraSecurityKeySecretName,
                    transaction.InvoiceNumber),
                cancellationToken);

            if (reconciliation.IsFound)
            {
                transaction.MarkReconciledSucceeded(reconciliation.ExternalReference, now);
                job.Complete(now);
                AddAudit(job, "retry.reconciled", now);
            }
            else if (reconciliation.IsNotFound &&
                     job.FailureClassification == RetryFailureClassification.Temporary)
            {
                await ResubmitAsync(job, transaction, tenant.TaxpayerReference,
                    tenant.GraSecurityKeySecretName, now, cancellationToken);
            }
            else if (reconciliation.IsRejected)
            {
                job.Exhaust(reconciliation.ErrorCode ?? "GRA_RECONCILIATION_REJECTED", now);
                AddAudit(job, "retry.exhausted", now);
            }
            else
            {
                RescheduleOrExhaust(job, reconciliation.ErrorCode ?? "GRA_RECONCILIATION_PENDING", now);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            RescheduleOrExhaust(job, "GRA_RECONCILIATION_TIMEOUT", now);
        }
        catch (HttpRequestException)
        {
            RescheduleOrExhaust(job, "GRA_RECONCILIATION_COMMUNICATION_FAILURE", now);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task ResubmitAsync(
        GraRetryJob job,
        GraEvatMiddleware.Domain.Transactions.EVatTransaction transaction,
        string taxpayerReference,
        string secretName,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var invoice = JsonSerializer.Deserialize<InvoiceCommand>(job.PayloadJson)
            ?? throw new InvalidOperationException("The persisted retry payload is invalid.");
        transaction.MarkRetrying(now);
        AddAudit(job, "retry.submitting", now);
        await dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            var result = await graClient.SubmitInvoiceAsync(
                new GraInvoiceSubmission(
                    transaction.Id,
                    job.TenantId,
                    job.CorrelationId,
                    job.IdempotencyKey,
                    "InvoiceRetry",
                    taxpayerReference,
                    secretName,
                    invoice),
                cancellationToken);
            var completedAt = DateTimeOffset.UtcNow;
            if (result.IsSuccessful)
            {
                transaction.MarkSucceeded(result.ExternalReference, completedAt);
                job.Complete(completedAt);
                AddAudit(job, "retry.succeeded", completedAt);
            }
            else if (result.IsRejected)
            {
                transaction.MarkRejected(result.ErrorCode ?? "GRA_REJECTED", completedAt);
                job.Complete(completedAt);
                AddAudit(job, "retry.rejected", completedAt);
            }
            else
            {
                transaction.MarkPending(result.ErrorCode ?? "GRA_STATUS_UNKNOWN", completedAt);
                RescheduleOrExhaust(job, result.ErrorCode ?? "GRA_STATUS_UNKNOWN", completedAt);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            transaction.MarkPending("GRA_TIMEOUT_STATUS_UNKNOWN", DateTimeOffset.UtcNow);
            RescheduleOrExhaust(job, "GRA_TIMEOUT_STATUS_UNKNOWN", DateTimeOffset.UtcNow);
        }
        catch (HttpRequestException)
        {
            transaction.MarkPending("GRA_COMMUNICATION_STATUS_UNKNOWN", DateTimeOffset.UtcNow);
            RescheduleOrExhaust(job, "GRA_COMMUNICATION_STATUS_UNKNOWN", DateTimeOffset.UtcNow);
        }
    }

    private void RescheduleOrExhaust(GraRetryJob job, string errorCode, DateTimeOffset now)
    {
        if (job.AttemptNumber >= job.MaxAttempts)
        {
            job.Exhaust(errorCode, now);
            AddAudit(job, "retry.exhausted", now);
            return;
        }

        var exponent = Math.Max(0, job.AttemptNumber - 1);
        var delaySeconds = Math.Min(
            _options.MaximumDelaySeconds,
            _options.InitialDelaySeconds * Math.Pow(_options.BackoffMultiplier, exponent));
        var isAmbiguous = errorCode is "GRA_TIMEOUT_STATUS_UNKNOWN" or
            "GRA_COMMUNICATION_STATUS_UNKNOWN" or
            "GRA_RECONCILIATION_TIMEOUT" or
            "GRA_RECONCILIATION_COMMUNICATION_FAILURE" or
            "E700";
        if (job.FailureClassification == RetryFailureClassification.Temporary && !isAmbiguous)
        {
            job.ScheduleRetry(errorCode, now.AddSeconds(delaySeconds), now);
            AddAudit(job, "retry.scheduled", now);
        }
        else
        {
            job.ScheduleReconciliation(errorCode, now.AddSeconds(delaySeconds), now);
            AddAudit(job, "retry.reconciliation_scheduled", now);
        }
    }

    private void AddAudit(GraRetryJob job, string action, DateTimeOffset now)
    {
        dbContext.AuditEvents.Add(AuditEvent.Create(
            job.TenantId,
            action,
            "gra_retry_job",
            job.Id.ToString(),
            job.CorrelationId,
            JsonSerializer.Serialize(new
            {
                job.TransactionId,
                job.AttemptNumber,
                status = job.Status.ToString(),
                job.LastErrorCode
            }),
            now));
    }
}
