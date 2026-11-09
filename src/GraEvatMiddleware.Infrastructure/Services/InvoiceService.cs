using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GraEvatMiddleware.Application.Abstractions;
using GraEvatMiddleware.Application.Common;
using GraEvatMiddleware.Application.Invoices;
using GraEvatMiddleware.Application.Retry;
using GraEvatMiddleware.Application.Transactions;
using GraEvatMiddleware.Domain.Auditing;
using GraEvatMiddleware.Domain.Tenants;
using GraEvatMiddleware.Domain.Transactions;
using GraEvatMiddleware.Domain.Retry;
using GraEvatMiddleware.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GraEvatMiddleware.Infrastructure.Services;

internal sealed class InvoiceService(
    AppDbContext dbContext,
    IGraEvatClient graClient,
    IOptions<GraRetryOptions> retryOptions) : IInvoiceService, ITransactionQueryService
{
    public async Task<InvoiceSubmissionResult> SubmitAsync(
        SubmitInvoiceCommand command,
        CancellationToken cancellationToken = default)
    {
        var requestHash = HashRequest(command.Invoice);
        var existing = await FindByIdempotencyKeyAsync(command.TenantId, command.IdempotencyKey, cancellationToken);
        if (existing is not null)
        {
            EnsureSameRequest(existing, requestHash);
            return new InvoiceSubmissionResult(ToResult(existing), true);
        }

        var tenant = await dbContext.Tenants.SingleOrDefaultAsync(
            item => item.Id == command.TenantId,
            cancellationToken) ?? throw new ResourceNotFoundException("Tenant was not found.");

        if (tenant.Status != TenantStatus.Active)
        {
            throw new ResourceConflictException("Tenant is not active.");
        }

        var now = DateTimeOffset.UtcNow;
        var transaction = EVatTransaction.CreateInvoice(
            tenant.Id,
            command.Invoice.InvoiceNumber,
            command.IdempotencyKey,
            requestHash,
            command.CorrelationId,
            now);
        transaction.MarkValidating(now);
        transaction.MarkSubmitting(now);
        dbContext.Transactions.Add(transaction);
        dbContext.AuditEvents.Add(CreateTransactionAudit(transaction, "transaction.submitting", now));

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            dbContext.ChangeTracker.Clear();
            existing = await FindByIdempotencyKeyAsync(command.TenantId, command.IdempotencyKey, cancellationToken);
            if (existing is null)
            {
                throw;
            }

            EnsureSameRequest(existing, requestHash);
            return new InvoiceSubmissionResult(ToResult(existing), true);
        }

        try
        {
            var result = await graClient.SubmitInvoiceAsync(
                new GraInvoiceSubmission(
                    transaction.Id,
                    tenant.Id,
                    transaction.CorrelationId,
                    transaction.IdempotencyKey,
                    transaction.Operation.ToString(),
                    tenant.TaxpayerReference,
                    tenant.GraSecurityKeySecretName,
                    command.Invoice),
                cancellationToken);

            var completedAt = DateTimeOffset.UtcNow;
            if (result.IsSuccessful)
            {
                transaction.MarkSucceeded(result.ExternalReference, completedAt);
            }
            else if (result.IsRejected)
            {
                transaction.MarkRejected(result.ErrorCode ?? "GRA_REJECTED", completedAt);
            }
            else if (result.IsPending)
            {
                transaction.MarkPending(result.ErrorCode ?? "GRA_STATUS_UNKNOWN", completedAt);
                ScheduleRetryIfRequired(transaction, command.Invoice,
                    result.ErrorCode ?? "GRA_STATUS_UNKNOWN", result.FailureKind, completedAt);
            }
            else
            {
                transaction.MarkTemporaryFailure(result.ErrorCode ?? "GRA_TEMPORARY_FAILURE", completedAt);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            var failedAt = DateTimeOffset.UtcNow;
            transaction.MarkPending("GRA_TIMEOUT_STATUS_UNKNOWN", failedAt);
            ScheduleRetryIfRequired(transaction, command.Invoice, "GRA_TIMEOUT_STATUS_UNKNOWN", GraFailureKind.Ambiguous, failedAt);
        }
        catch (HttpRequestException)
        {
            var failedAt = DateTimeOffset.UtcNow;
            transaction.MarkPending("GRA_COMMUNICATION_STATUS_UNKNOWN", failedAt);
            ScheduleRetryIfRequired(transaction, command.Invoice, "GRA_COMMUNICATION_STATUS_UNKNOWN", GraFailureKind.Ambiguous, failedAt);
        }

        dbContext.AuditEvents.Add(CreateTransactionAudit(transaction, "transaction.completed", transaction.UpdatedAt));
        await dbContext.SaveChangesAsync(cancellationToken);
        return new InvoiceSubmissionResult(ToResult(transaction), false);
    }

    public async Task<TransactionResult?> FindAsync(
        Guid tenantId,
        Guid transactionId,
        CancellationToken cancellationToken = default)
    {
        var transaction = await dbContext.Transactions
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.TenantId == tenantId && item.Id == transactionId,
                cancellationToken);
        return transaction is null ? null : ToResult(transaction);
    }

    private Task<EVatTransaction?> FindByIdempotencyKeyAsync(
        Guid tenantId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        return dbContext.Transactions.SingleOrDefaultAsync(
            item => item.TenantId == tenantId &&
                    item.Operation == TransactionOperation.Invoice &&
                    item.IdempotencyKey == idempotencyKey,
            cancellationToken);
    }

    private static string HashRequest(InvoiceCommand command)
    {
        var json = JsonSerializer.Serialize(command);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant();
    }

    private static void EnsureSameRequest(EVatTransaction transaction, string requestHash)
    {
        if (!string.Equals(transaction.RequestHash, requestHash, StringComparison.Ordinal))
        {
            throw new IdempotencyConflictException(
                "The idempotency key has already been used with a different request body.");
        }
    }

    private static AuditEvent CreateTransactionAudit(
        EVatTransaction transaction,
        string action,
        DateTimeOffset now)
    {
        return AuditEvent.Create(
            transaction.TenantId,
            action,
            "evat_transaction",
            transaction.Id.ToString(),
            transaction.CorrelationId,
            JsonSerializer.Serialize(new { transaction.InvoiceNumber, status = transaction.Status.ToString() }),
            now);
    }

    private void ScheduleRetryIfRequired(
        EVatTransaction transaction,
        InvoiceCommand invoice,
        string errorCode,
        GraFailureKind failureKind,
        DateTimeOffset now)
    {
        if (!retryOptions.Value.Enabled ||
            failureKind == GraFailureKind.Configuration ||
            errorCode is "GRA_INTEGRATION_DISABLED" or "GRA_SECRET_NOT_CONFIGURED" or
                "GRA_ENVIRONMENT_NOT_ALLOWED" or "GRA_BASE_URL_INVALID" or
                "GRA_HTTPS_REQUIRED" or "GRA_HOST_NOT_ALLOWED" or "GRA_STAGING_HOST_REQUIRED")
        {
            return;
        }

        var classification = failureKind is GraFailureKind.Temporary or GraFailureKind.RateLimited ||
            errorCode is "GRA_SERVICE_UNAVAILABLE" or "E990"
            ? RetryFailureClassification.Temporary
            : RetryFailureClassification.Ambiguous;
        var retry = GraRetryJob.Schedule(
            transaction.Id,
            transaction.TenantId,
            transaction.CorrelationId,
            transaction.IdempotencyKey,
            JsonSerializer.Serialize(invoice),
            classification,
            errorCode,
            Math.Max(1, retryOptions.Value.MaximumAttempts),
            now.AddSeconds(Math.Max(1, retryOptions.Value.InitialDelaySeconds)),
            now);
        dbContext.GraRetryJobs.Add(retry);
        dbContext.AuditEvents.Add(AuditEvent.Create(
            transaction.TenantId,
            "retry.scheduled",
            "gra_retry_job",
            retry.Id.ToString(),
            transaction.CorrelationId,
            JsonSerializer.Serialize(new
            {
                transaction.Id,
                classification = classification.ToString(),
                errorCode
            }),
            now));
    }

    private static TransactionResult ToResult(EVatTransaction transaction)
    {
        return new TransactionResult(
            transaction.Id,
            transaction.InvoiceNumber,
            transaction.Status.ToString(),
            transaction.ExternalReference,
            transaction.ErrorCode,
            transaction.CorrelationId,
            transaction.CreatedAt,
            transaction.UpdatedAt);
    }
}
