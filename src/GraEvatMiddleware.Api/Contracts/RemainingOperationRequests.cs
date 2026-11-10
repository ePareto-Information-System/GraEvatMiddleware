using System.ComponentModel.DataAnnotations;
using GraEvatMiddleware.Application.Operations;

namespace GraEvatMiddleware.Api.Contracts;

public sealed class CreditDebitNoteRequest
{
    [Required, StringLength(100, MinimumLength = 1)]
    public string NoteNumber { get; init; } = string.Empty;

    [Required, RegularExpression("^(CREDIT|DEBIT)$")]
    public string NoteType { get; init; } = string.Empty;

    [Required, StringLength(10, MinimumLength = 3)]
    public string Currency { get; init; } = string.Empty;

    [Range(typeof(decimal), "0.000001", "999999999999999")]
    public decimal ExchangeRate { get; init; }

    [Range(typeof(decimal), "0.000001", "999999999999999")]
    public decimal NoteAmount { get; init; }

    [Required, RegularExpression("^(INCLUSIVE|EXCLUSIVE)$")]
    public string CalculationType { get; init; } = string.Empty;

    [Required, StringLength(200, MinimumLength = 1)]
    public string BusinessPartnerName { get; init; } = string.Empty;

    [Required, StringLength(20, MinimumLength = 1)]
    public string BusinessPartnerTin { get; init; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 1)]
    public string GroupReferenceId { get; init; } = string.Empty;

    public DateOnly TransactionDate { get; init; }

    [StringLength(500)]
    public string? Remarks { get; init; }

    [Required, RegularExpression("^(INVOICE|PURCHASE)$")]
    public string NoteLineType { get; init; } = string.Empty;

    [Required, MinLength(1)]
    public List<CreditDebitNoteLineRequest> NoteLines { get; init; } = [];

    public CreditDebitNoteCommand ToCommand() => new(
        NoteNumber, Currency, ExchangeRate, NoteAmount, CalculationType,
        BusinessPartnerName, BusinessPartnerTin, GroupReferenceId, TransactionDate,
        Remarks, NoteLineType, NoteLines.Select(line => line.ToCommand()).ToArray());
}

public sealed class CreditDebitNoteLineRequest
{
    [Required, StringLength(100, MinimumLength = 1)]
    public string InvoiceNumber { get; init; } = string.Empty;

    public DateOnly InvoiceDate { get; init; }

    [Range(typeof(decimal), "0.000001", "999999999999999")]
    public decimal InvoiceAmount { get; init; }

    public CreditDebitNoteLineCommand ToCommand() => new(InvoiceNumber, InvoiceDate, InvoiceAmount);
}

public sealed class StatementOfAccountRequest
{
    [Required, StringLength(10, MinimumLength = 3)]
    public string Currency { get; init; } = string.Empty;

    [Range(typeof(decimal), "0.000001", "999999999999999")]
    public decimal ExchangeRate { get; init; }

    [Range(typeof(decimal), "0", "999999999999999")]
    public decimal TotalVat { get; init; }

    [Range(typeof(decimal), "0", "999999999999999")]
    public decimal TotalAmount { get; init; }

    [Range(typeof(decimal), "0", "999999999999999")]
    public decimal TotalLevies { get; init; }

    [Required, StringLength(200, MinimumLength = 1)]
    public string BusinessPartnerName { get; init; } = string.Empty;

    [Required, StringLength(20, MinimumLength = 1)]
    public string BusinessPartnerTin { get; init; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 1)]
    public string GroupReferenceId { get; init; } = string.Empty;

    public DateTimeOffset TransactionDate { get; init; }

    [Required, RegularExpression("^(INCLUSIVE|EXCLUSIVE)$")]
    public string CalculationType { get; init; } = string.Empty;

    [Required, MinLength(1)]
    public List<StatementLineRequest> Lines { get; init; } = [];

    public StatementOfAccountCommand ToCommand() => new(
        Currency, ExchangeRate, TotalVat, TotalAmount, TotalLevies,
        BusinessPartnerName, BusinessPartnerTin, GroupReferenceId, TransactionDate,
        CalculationType, Lines.Select(line => line.ToCommand()).ToArray());
}

public sealed class StatementLineRequest
{
    [Required, StringLength(10, MinimumLength = 3)]
    public string Currency { get; init; } = string.Empty;

    [Range(typeof(decimal), "0.000001", "999999999999999")]
    public decimal ExchangeRate { get; init; }

    [Required, RegularExpression("^(INCLUSIVE|EXCLUSIVE)$")]
    public string CalculationType { get; init; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 1)]
    public string InvoiceNumber { get; init; } = string.Empty;

    [StringLength(100)]
    public string? Reference { get; init; }

    [Required, RegularExpression("^(INVOICE|REFUND|PARTIAL_REFUND|CREDIT_NOTE|DEBIT_NOTE|REFUND_CANCELATION)$")]
    public string Flag { get; init; } = string.Empty;

    [Range(typeof(decimal), "0", "999999999999999")]
    public decimal InvoiceVat { get; init; }

    [Range(typeof(decimal), "0", "999999999999999")]
    public decimal InvoiceAmount { get; init; }

    [Range(typeof(decimal), "0", "999999999999999")]
    public decimal InvoiceLevies { get; init; }

    public DateTimeOffset TransactionDate { get; init; }

    public StatementLineCommand ToCommand() => new(
        Currency, ExchangeRate, CalculationType, InvoiceNumber, Reference, Flag,
        InvoiceVat, InvoiceAmount, InvoiceLevies, TransactionDate);
}

public sealed class InventorySubmissionRequest
{
    public DateOnly InventoryDate { get; init; }

    [Required, MinLength(1)]
    public List<InventoryItemRequest> Inventories { get; init; } = [];

    public InventorySubmissionCommand ToCommand() => new(
        InventoryDate, Inventories.Select(item => item.ToCommand()).ToArray());
}

public sealed class InventoryItemRequest
{
    [Required, StringLength(100, MinimumLength = 1)]
    public string Code { get; init; } = string.Empty;

    [Required, StringLength(250, MinimumLength = 1)]
    public string Description { get; init; } = string.Empty;

    [Range(typeof(decimal), "0", "999999999999999")]
    public decimal Quantity { get; init; }

    [StringLength(100)]
    public string? BatchCode { get; init; }

    [Range(typeof(decimal), "0", "999999999999999")]
    public decimal UnitPrice { get; init; }

    public DateOnly? ExpiryDate { get; init; }

    public InventoryItemCommand ToCommand() => new(Code, Description, Quantity, BatchCode, UnitPrice, ExpiryDate);
}
