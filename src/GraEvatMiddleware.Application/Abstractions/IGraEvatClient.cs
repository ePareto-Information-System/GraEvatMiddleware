using GraEvatMiddleware.Application.Invoices;
using GraEvatMiddleware.Application.Operations;

namespace GraEvatMiddleware.Application.Abstractions;

public interface IGraEvatClient
{
    Task<GraSubmissionResult> SubmitInvoiceAsync(
        GraInvoiceSubmission submission,
        CancellationToken cancellationToken = default);

    Task<GraSubmissionResult> SubmitCancellationAsync(
        GraCancellationSubmission submission,
        CancellationToken cancellationToken = default);

    Task<GraReconciliationResult> ReconcileInvoiceAsync(
        GraInvoiceReconciliation reconciliation,
        CancellationToken cancellationToken = default);

    Task<GraHealthResult> CheckHealthAsync(
        GraHealthCheck healthCheck,
        CancellationToken cancellationToken = default);

    Task<GraSubmissionResult> SubmitOperationAsync(
        GraOperationSubmission submission,
        CancellationToken cancellationToken = default);

    Task<IdentificationResult> ValidateIdentificationAsync(
        GraIdentificationRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record GraOperationSubmission(
    Guid TransactionId,
    Guid TenantId,
    string CorrelationId,
    string IdempotencyKey,
    string Operation,
    string TaxpayerReference,
    string SecurityKeySecretName,
    string DocumentNumber,
    object Payload);

public sealed record GraIdentificationRequest(
    Guid TenantId,
    string CorrelationId,
    string Operation,
    string TaxpayerReference,
    string SecurityKeySecretName,
    string Kind,
    string Value);

public sealed record GraInvoiceSubmission(
    Guid TransactionId,
    Guid TenantId,
    string CorrelationId,
    string IdempotencyKey,
    string Operation,
    string TaxpayerReference,
    string SecurityKeySecretName,
    InvoiceCommand Invoice,
    string GraFlag = "INVOICE");

public sealed record GraCancellationSubmission(
    Guid TransactionId,
    Guid TenantId,
    string CorrelationId,
    string IdempotencyKey,
    string Operation,
    string TaxpayerReference,
    string SecurityKeySecretName,
    CancellationCommand Cancellation,
    string GraFlag);

public sealed record GraSubmissionResult(
    bool IsSuccessful,
    bool IsPending,
    bool IsRejected,
    string? ExternalReference,
    string? ErrorCode,
    GraFailureKind FailureKind = GraFailureKind.None);

public sealed record GraInvoiceReconciliation(
    Guid TransactionId,
    Guid TenantId,
    string CorrelationId,
    string IdempotencyKey,
    string Operation,
    string TaxpayerReference,
    string SecurityKeySecretName,
    string InvoiceNumber,
    string GraFlag = "INVOICE",
    string? Reference = null);

public sealed record GraReconciliationResult(
    bool IsFound,
    bool IsNotFound,
    bool IsPending,
    bool IsRejected,
    string? ExternalReference,
    string? ErrorCode,
    GraFailureKind FailureKind = GraFailureKind.None);

public sealed record GraHealthCheck(
    string TaxpayerReference,
    string SecurityKeySecretName);

public sealed record GraHealthResult(
    bool IsHealthy,
    string? ErrorCode);

public enum GraFailureKind
{
    None,
    Configuration,
    Authentication,
    AlreadyProcessed,
    NotFound,
    RateLimited,
    Temporary,
    Ambiguous,
    BusinessRejection,
    InvalidResponse
}
