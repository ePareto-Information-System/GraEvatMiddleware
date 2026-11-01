using GraEvatMiddleware.Application.Invoices;

namespace GraEvatMiddleware.Application.Abstractions;

public interface IGraEvatClient
{
    Task<GraSubmissionResult> SubmitInvoiceAsync(
        GraInvoiceSubmission submission,
        CancellationToken cancellationToken = default);
}

public sealed record GraInvoiceSubmission(
    Guid TenantId,
    string TaxpayerReference,
    string SecurityKeySecretName,
    InvoiceCommand Invoice);

public sealed record GraSubmissionResult(
    bool IsSuccessful,
    bool IsPending,
    bool IsRejected,
    string? ExternalReference,
    string? ErrorCode);
