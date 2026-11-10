using GraEvatMiddleware.Application.Invoices;
using GraEvatMiddleware.Domain.Transactions;

namespace GraEvatMiddleware.Application.Operations;

public sealed record CreditDebitNoteCommand(
    string NoteNumber,
    string Currency,
    decimal ExchangeRate,
    decimal NoteAmount,
    string CalculationType,
    string BusinessPartnerName,
    string BusinessPartnerTin,
    string GroupReferenceId,
    DateOnly TransactionDate,
    string? Remarks,
    string NoteLineFlag,
    IReadOnlyList<CreditDebitNoteLineCommand> NoteLines);

public sealed record CreditDebitNoteLineCommand(
    string InvoiceNumber,
    DateOnly InvoiceDate,
    decimal InvoiceAmount);

public sealed record StatementOfAccountCommand(
    string Currency,
    decimal ExchangeRate,
    decimal TotalVat,
    decimal TotalAmount,
    decimal TotalLevies,
    string BusinessPartnerName,
    string BusinessPartnerTin,
    string GroupReferenceId,
    DateTimeOffset TransactionDate,
    string CalculationType,
    IReadOnlyList<StatementLineCommand> Lines);

public sealed record StatementLineCommand(
    string Currency,
    decimal ExchangeRate,
    string CalculationType,
    string InvoiceNumber,
    string? Reference,
    string Flag,
    decimal InvoiceVat,
    decimal InvoiceAmount,
    decimal InvoiceLevies,
    DateTimeOffset TransactionDate);

public sealed record InventorySubmissionCommand(
    DateOnly InventoryDate,
    IReadOnlyList<InventoryItemCommand> Inventories);

public sealed record InventoryItemCommand(
    string Code,
    string Description,
    decimal Quantity,
    string? BatchCode,
    decimal UnitPrice,
    DateOnly? ExpiryDate);

public sealed record SubmitGraOperationCommand(
    Guid TenantId,
    TransactionOperation Operation,
    string DocumentNumber,
    string IdempotencyKey,
    string CorrelationId,
    object Payload);

public sealed record IdentificationQuery(
    Guid TenantId,
    string Kind,
    string Value,
    string CorrelationId);

public sealed record OperationTransactionResult(
    Guid TransactionId,
    string DocumentNumber,
    string Status,
    string? ExternalReference,
    string? ErrorCode,
    string CorrelationId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record GraOperationResult(OperationTransactionResult Transaction, bool IsReplay);

public sealed record IdentificationResult(
    bool IsValid,
    string? Tin,
    string? Type,
    string? Name,
    string? Sector,
    string? Address,
    string? ErrorCode);

public interface IGraOperationService
{
    Task<GraOperationResult> SubmitAsync(
        SubmitGraOperationCommand command,
        CancellationToken cancellationToken = default);

    Task<IdentificationResult> ValidateIdentificationAsync(
        IdentificationQuery query,
        CancellationToken cancellationToken = default);
}
