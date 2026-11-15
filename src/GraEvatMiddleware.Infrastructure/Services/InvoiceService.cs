using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GraEvatMiddleware.Application.Abstractions;
using GraEvatMiddleware.Application.Common;
using GraEvatMiddleware.Application.Invoices;
using GraEvatMiddleware.Application.Retry;
using GraEvatMiddleware.Application.Transactions;
using GraEvatMiddleware.Domain.Auditing;
using GraEvatMiddleware.Domain.Retry;
using GraEvatMiddleware.Domain.Tenants;
using GraEvatMiddleware.Domain.Transactions;
using GraEvatMiddleware.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GraEvatMiddleware.Infrastructure.Services;

internal sealed class InvoiceService(
    AppDbContext dbContext,
    IGraEvatClient graClient,
    IOptions<GraRetryOptions> retryOptions) : IInvoiceService, IFiscalTransactionService, ITransactionQueryService
{
    public Task<InvoiceSubmissionResult> SubmitAsync(
        SubmitInvoiceCommand command,
        CancellationToken cancellationToken = default) =>
        SubmitDocumentCoreAsync(command.TenantId, TransactionOperation.Invoice, "INVOICE",
            command.IdempotencyKey, command.CorrelationId, command.Invoice, cancellationToken);

    public Task<InvoiceSubmissionResult> SubmitDocumentAsync(
        SubmitFiscalDocumentCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.Operation is not (TransactionOperation.InvoiceRefund or
            TransactionOperation.Purchase or TransactionOperation.PurchaseReturn))
        {
            throw new ArgumentOutOfRangeException(nameof(command), "Unsupported fiscal document operation.");
        }

        return SubmitDocumentCoreAsync(command.TenantId, command.Operation, command.GraFlag,
            command.IdempotencyKey, command.CorrelationId, command.Document, cancellationToken);
    }

    public async Task<InvoiceSubmissionResult> SubmitCancellationAsync(
        SubmitCancellationCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.Operation is not (TransactionOperation.RefundCancellation or
            TransactionOperation.PurchaseReturnCancellation))
        {
            throw new ArgumentOutOfRangeException(nameof(command), "Unsupported cancellation operation.");
        }

        var requestHash = HashRequest(command.Operation, command.Cancellation);
        var existing = await FindByIdempotencyKeyAsync(
            command.TenantId, command.Operation, command.IdempotencyKey, cancellationToken);
        if (existing is not null)
        {
            EnsureSameRequest(existing, requestHash);
            return new InvoiceSubmissionResult(ToResult(existing), true);
        }

        var tenant = await GetActiveTenantAsync(command.TenantId, cancellationToken);
        var transaction = CreateTransaction(tenant.Id, command.Operation, command.Cancellation.InvoiceNumber,
            command.IdempotencyKey, requestHash, command.CorrelationId);
        var saved = await SaveNewTransactionAsync(transaction, cancellationToken);
        if (saved is not null)
        {
            EnsureSameRequest(saved, requestHash);
            return new InvoiceSubmissionResult(ToResult(saved), true);
        }

        try
        {
            var result = await graClient.SubmitCancellationAsync(
                new GraCancellationSubmission(transaction.Id, tenant.Id, transaction.CorrelationId,
                    transaction.IdempotencyKey, transaction.Operation.ToString(), tenant.TaxpayerReference,
                    tenant.GraSecurityKeySecretName, command.Cancellation, command.GraFlag),
                cancellationToken);
            CompleteTransaction(transaction, result, DateTimeOffset.UtcNow);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            transaction.MarkPending("GRA_TIMEOUT_STATUS_UNKNOWN", DateTimeOffset.UtcNow);
        }
        catch (HttpRequestException)
        {
            transaction.MarkPending("GRA_COMMUNICATION_STATUS_UNKNOWN", DateTimeOffset.UtcNow);
        }

        await SaveCompletedAsync(transaction, cancellationToken);
        return new InvoiceSubmissionResult(ToResult(transaction), false);
    }

    public async Task<TransactionResult?> FindAsync(
        Guid tenantId,
        Guid transactionId,
        CancellationToken cancellationToken = default)
    {
        var transaction = await dbContext.Transactions.AsNoTracking().SingleOrDefaultAsync(
            item => item.TenantId == tenantId && item.Id == transactionId, cancellationToken);
        return transaction is null ? null : ToResult(transaction);
    }

    private async Task<InvoiceSubmissionResult> SubmitDocumentCoreAsync(
        Guid tenantId,
        TransactionOperation operation,
        string graFlag,
        string idempotencyKey,
        string correlationId,
        InvoiceCommand document,
        CancellationToken cancellationToken)
    {
        var requestHash = HashRequest(operation, document);
        var existing = await FindByIdempotencyKeyAsync(tenantId, operation, idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            EnsureSameRequest(existing, requestHash);
            return new InvoiceSubmissionResult(ToResult(existing), true);
        }

        var tenant = await GetActiveTenantAsync(tenantId, cancellationToken);
        var transaction = CreateTransaction(
            tenant.Id, operation, document.InvoiceNumber, idempotencyKey, requestHash, correlationId);
        var saved = await SaveNewTransactionAsync(transaction, cancellationToken);
        if (saved is not null)
        {
            EnsureSameRequest(saved, requestHash);
            return new InvoiceSubmissionResult(ToResult(saved), true);
        }

        try
        {
            var result = await graClient.SubmitInvoiceAsync(
                new GraInvoiceSubmission(transaction.Id, tenant.Id, transaction.CorrelationId,
                    transaction.IdempotencyKey, transaction.Operation.ToString(), tenant.TaxpayerReference,
                    tenant.GraSecurityKeySecretName, document, graFlag),
                cancellationToken);
            CompleteTransaction(transaction, result, DateTimeOffset.UtcNow);
            if (result.IsPending)
            {
                ScheduleRetryIfRequired(transaction, graFlag, document,
                    result.ErrorCode ?? "GRA_STATUS_UNKNOWN", result.FailureKind, transaction.UpdatedAt);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            var now = DateTimeOffset.UtcNow;
            transaction.MarkPending("GRA_TIMEOUT_STATUS_UNKNOWN", now);
            ScheduleRetryIfRequired(transaction, graFlag, document,
                "GRA_TIMEOUT_STATUS_UNKNOWN", GraFailureKind.Ambiguous, now);
        }
        catch (HttpRequestException)
        {
            var now = DateTimeOffset.UtcNow;
            transaction.MarkPending("GRA_COMMUNICATION_STATUS_UNKNOWN", now);
            ScheduleRetryIfRequired(transaction, graFlag, document,
                "GRA_COMMUNICATION_STATUS_UNKNOWN", GraFailureKind.Ambiguous, now);
        }

        await SaveCompletedAsync(transaction, cancellationToken);
        return new InvoiceSubmissionResult(ToResult(transaction), false);
    }

    private async Task<Tenant> GetActiveTenantAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var tenant = await dbContext.Tenants.SingleOrDefaultAsync(item => item.Id == tenantId, cancellationToken)
            ?? throw new ResourceNotFoundException("Tenant was not found.");
        if (tenant.Status != TenantStatus.Active)
        {
            throw new ResourceConflictException("Tenant is not active.");
        }

        return tenant;
    }

    private static EVatTransaction CreateTransaction(
        Guid tenantId,
        TransactionOperation operation,
        string invoiceNumber,
        string idempotencyKey,
        string requestHash,
        string correlationId)
    {
        var now = DateTimeOffset.UtcNow;
        var transaction = EVatTransaction.Create(
            tenantId, operation, invoiceNumber, idempotencyKey, requestHash, correlationId, now);
        transaction.MarkValidating(now);
        transaction.MarkSubmitting(now);
        return transaction;
    }

    private async Task<EVatTransaction?> SaveNewTransactionAsync(
        EVatTransaction transaction,
        CancellationToken cancellationToken)
    {
        dbContext.Transactions.Add(transaction);
        dbContext.AuditEvents.Add(CreateTransactionAudit(transaction, "transaction.submitting", transaction.UpdatedAt));
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return null;
        }
        catch (DbUpdateException)
        {
            dbContext.ChangeTracker.Clear();
            var existing = await FindByIdempotencyKeyAsync(
                transaction.TenantId, transaction.Operation, transaction.IdempotencyKey, cancellationToken);
            if (existing is not null)
            {
                return existing;
            }

            throw;
        }
    }

    private async Task SaveCompletedAsync(EVatTransaction transaction, CancellationToken cancellationToken)
    {
        dbContext.AuditEvents.Add(CreateTransactionAudit(transaction, "transaction.completed", transaction.UpdatedAt));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static void CompleteTransaction(
        EVatTransaction transaction,
        GraSubmissionResult result,
        DateTimeOffset now)
    {
        if (result.IsSuccessful)
        {
            transaction.MarkSucceeded(result.ExternalReference, now);
        }
        else if (result.IsRejected)
        {
            transaction.MarkRejected(result.ErrorCode ?? "GRA_REJECTED", now);
        }
        else if (result.IsPending)
        {
            transaction.MarkPending(result.ErrorCode ?? "GRA_STATUS_UNKNOWN", now);
        }
        else
        {
            transaction.MarkTemporaryFailure(result.ErrorCode ?? "GRA_TEMPORARY_FAILURE", now);
        }
    }

    private Task<EVatTransaction?> FindByIdempotencyKeyAsync(
        Guid tenantId,
        TransactionOperation operation,
        string idempotencyKey,
        CancellationToken cancellationToken) =>
        dbContext.Transactions.SingleOrDefaultAsync(
            item => item.TenantId == tenantId && item.Operation == operation && item.IdempotencyKey == idempotencyKey,
            cancellationToken);

    private static string HashRequest<T>(TransactionOperation operation, T command)
    {
        var json = JsonSerializer.Serialize(new { operation, command });
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
        DateTimeOffset now) =>
        AuditEvent.Create(transaction.TenantId, action, "evat_transaction", transaction.Id.ToString(),
            transaction.CorrelationId, JsonSerializer.Serialize(new
            {
                transaction.InvoiceNumber,
                operation = transaction.Operation.ToString(),
                status = transaction.Status.ToString()
            }), now);

    private void ScheduleRetryIfRequired(
        EVatTransaction transaction,
        string graFlag,
        InvoiceCommand document,
        string errorCode,
        GraFailureKind failureKind,
        DateTimeOffset now)
    {
        if (!retryOptions.Value.Enabled || failureKind == GraFailureKind.Configuration ||
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
        var retry = GraRetryJob.Schedule(transaction.Id, transaction.TenantId, transaction.CorrelationId,
            transaction.IdempotencyKey, JsonSerializer.Serialize(new FiscalRetryPayload(graFlag, document)),
            classification, errorCode, Math.Max(1, retryOptions.Value.MaximumAttempts),
            now.AddSeconds(Math.Max(1, retryOptions.Value.InitialDelaySeconds)), now);
        dbContext.GraRetryJobs.Add(retry);
        dbContext.AuditEvents.Add(AuditEvent.Create(transaction.TenantId, "retry.scheduled", "gra_retry_job",
            retry.Id.ToString(), transaction.CorrelationId, JsonSerializer.Serialize(new
            {
                transaction.Id,
                classification = classification.ToString(),
                errorCode
            }), now));
    }

    private static TransactionResult ToResult(EVatTransaction transaction) => new(
        transaction.Id, transaction.InvoiceNumber, transaction.Status.ToString(), transaction.ExternalReference,
        transaction.ErrorCode, transaction.CorrelationId, transaction.CreatedAt, transaction.UpdatedAt);
}
