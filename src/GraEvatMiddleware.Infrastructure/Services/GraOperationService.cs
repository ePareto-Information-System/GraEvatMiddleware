using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GraEvatMiddleware.Application.Abstractions;
using GraEvatMiddleware.Application.Common;
using GraEvatMiddleware.Application.Operations;
using GraEvatMiddleware.Domain.Auditing;
using GraEvatMiddleware.Domain.Tenants;
using GraEvatMiddleware.Domain.Transactions;
using GraEvatMiddleware.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GraEvatMiddleware.Infrastructure.Services;

internal sealed class GraOperationService(AppDbContext dbContext, IGraEvatClient graClient) : IGraOperationService
{
    public async Task<GraOperationResult> SubmitAsync(
        SubmitGraOperationCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.Operation is not (TransactionOperation.CreditNote or TransactionOperation.DebitNote or
            TransactionOperation.StatementOfAccount or TransactionOperation.Inventory))
        {
            throw new ArgumentOutOfRangeException(nameof(command), "Unsupported GRA operation.");
        }

        var requestHash = HashRequest(command.Operation, command.Payload);
        var existing = await FindAsync(command.TenantId, command.Operation, command.IdempotencyKey, cancellationToken);
        if (existing is not null)
        {
            EnsureSameRequest(existing, requestHash);
            return new GraOperationResult(ToResult(existing), true);
        }

        var tenant = await GetActiveTenantAsync(command.TenantId, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var transaction = EVatTransaction.Create(tenant.Id, command.Operation, command.DocumentNumber,
            command.IdempotencyKey, requestHash, command.CorrelationId, now);
        transaction.MarkValidating(now);
        transaction.MarkSubmitting(now);
        dbContext.Transactions.Add(transaction);
        dbContext.AuditEvents.Add(CreateAudit(transaction, "transaction.submitting", now));

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            dbContext.ChangeTracker.Clear();
            existing = await FindAsync(command.TenantId, command.Operation, command.IdempotencyKey, cancellationToken);
            if (existing is null) throw;
            EnsureSameRequest(existing, requestHash);
            return new GraOperationResult(ToResult(existing), true);
        }

        try
        {
            var result = await graClient.SubmitOperationAsync(new GraOperationSubmission(
                transaction.Id, tenant.Id, transaction.CorrelationId, transaction.IdempotencyKey,
                transaction.Operation.ToString(), tenant.TaxpayerReference, tenant.GraSecurityKeySecretName,
                command.DocumentNumber, command.Payload), cancellationToken);
            Complete(transaction, result, DateTimeOffset.UtcNow);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            transaction.MarkPending("GRA_TIMEOUT_STATUS_UNKNOWN", DateTimeOffset.UtcNow);
        }
        catch (HttpRequestException)
        {
            transaction.MarkPending("GRA_COMMUNICATION_STATUS_UNKNOWN", DateTimeOffset.UtcNow);
        }

        dbContext.AuditEvents.Add(CreateAudit(transaction, "transaction.completed", transaction.UpdatedAt));
        await dbContext.SaveChangesAsync(cancellationToken);
        return new GraOperationResult(ToResult(transaction), false);
    }

    public async Task<IdentificationResult> ValidateIdentificationAsync(
        IdentificationQuery query,
        CancellationToken cancellationToken = default)
    {
        if (query.Kind is not ("tin" or "nationalId"))
        {
            throw new ArgumentOutOfRangeException(nameof(query), "Unsupported identification type.");
        }

        var tenant = await GetActiveTenantAsync(query.TenantId, cancellationToken);
        return await graClient.ValidateIdentificationAsync(new GraIdentificationRequest(
            tenant.Id, query.CorrelationId, $"Identification.{query.Kind}", tenant.TaxpayerReference,
            tenant.GraSecurityKeySecretName, query.Kind, query.Value), cancellationToken);
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

    private Task<EVatTransaction?> FindAsync(
        Guid tenantId,
        TransactionOperation operation,
        string idempotencyKey,
        CancellationToken cancellationToken) =>
        dbContext.Transactions.SingleOrDefaultAsync(item =>
            item.TenantId == tenantId && item.Operation == operation && item.IdempotencyKey == idempotencyKey,
            cancellationToken);

    private static string HashRequest(TransactionOperation operation, object payload)
    {
        var json = JsonSerializer.Serialize(new { operation, payload });
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

    private static void Complete(EVatTransaction transaction, GraSubmissionResult result, DateTimeOffset now)
    {
        if (result.IsSuccessful) transaction.MarkSucceeded(result.ExternalReference, now);
        else if (result.IsRejected) transaction.MarkRejected(result.ErrorCode ?? "GRA_REJECTED", now);
        else if (result.IsPending) transaction.MarkPending(result.ErrorCode ?? "GRA_STATUS_UNKNOWN", now);
        else transaction.MarkTemporaryFailure(result.ErrorCode ?? "GRA_TEMPORARY_FAILURE", now);
    }

    private static AuditEvent CreateAudit(EVatTransaction transaction, string action, DateTimeOffset now) =>
        AuditEvent.Create(transaction.TenantId, action, "evat_transaction", transaction.Id.ToString(),
            transaction.CorrelationId, JsonSerializer.Serialize(new
            {
                documentNumber = transaction.InvoiceNumber,
                operation = transaction.Operation.ToString(),
                status = transaction.Status.ToString()
            }), now);

    private static OperationTransactionResult ToResult(EVatTransaction transaction) => new(
        transaction.Id, transaction.InvoiceNumber, transaction.Status.ToString(), transaction.ExternalReference,
        transaction.ErrorCode, transaction.CorrelationId, transaction.CreatedAt, transaction.UpdatedAt);
}
