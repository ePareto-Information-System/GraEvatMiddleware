namespace GraEvatMiddleware.Application.Invoices;

public sealed record InvoiceCommand(
    string InvoiceNumber,
    string Currency,
    decimal ExchangeRate,
    string CalculationType,
    DateOnly TransactionDate,
    decimal TotalVat,
    decimal TotalLevy,
    decimal TotalAmount,
    string BusinessPartnerName,
    string BusinessPartnerTin,
    string SaleType,
    string? DiscountType,
    decimal DiscountAmount,
    string? GroupReferenceId,
    IReadOnlyList<InvoiceItemCommand> Items);

public sealed record InvoiceItemCommand(
    string ItemCode,
    string ItemCategory,
    string Description,
    decimal Quantity,
    decimal UnitPrice,
    decimal LevyAmountA,
    decimal LevyAmountB,
    decimal LevyAmountC,
    decimal LevyAmountD,
    decimal LevyAmountE,
    decimal DiscountAmount,
    decimal ExciseAmount,
    string? BatchCode,
    DateOnly? ExpireDate);

public sealed record SubmitInvoiceCommand(Guid TenantId, string IdempotencyKey, string CorrelationId, InvoiceCommand Invoice);

public sealed record InvoiceSubmissionResult(TransactionResult Transaction, bool IsReplay);

public sealed record TransactionResult(
    Guid TransactionId,
    string InvoiceNumber,
    string Status,
    string? ExternalReference,
    string? ErrorCode,
    string CorrelationId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
