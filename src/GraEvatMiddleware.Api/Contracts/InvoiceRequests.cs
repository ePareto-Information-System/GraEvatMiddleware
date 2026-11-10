using System.ComponentModel.DataAnnotations;
using GraEvatMiddleware.Application.Invoices;

namespace GraEvatMiddleware.Api.Contracts;

public class FiscalDocumentRequest
{
    [Required, StringLength(100, MinimumLength = 1)]
    public string InvoiceNumber { get; init; } = string.Empty;

    [Required, RegularExpression("^(GHS|USD|EUR|GBP)$")]
    public string Currency { get; init; } = string.Empty;

    [Range(typeof(decimal), "0.000001", "999999999999999")]
    public decimal ExchangeRate { get; init; }

    [Required, RegularExpression("^(INCLUSIVE|EXCLUSIVE)$")]
    public string CalculationType { get; init; } = string.Empty;

    public DateOnly TransactionDate { get; init; }

    [Range(typeof(decimal), "0", "999999999999999")]
    public decimal TotalVat { get; init; }

    [Range(typeof(decimal), "0", "999999999999999")]
    public decimal TotalLevy { get; init; }

    [Range(typeof(decimal), "0", "999999999999999")]
    public decimal TotalAmount { get; init; }

    [Required, StringLength(100, MinimumLength = 1)]
    public string BusinessPartnerName { get; init; } = string.Empty;

    [Required, StringLength(15, MinimumLength = 1)]
    public string BusinessPartnerTin { get; init; } = string.Empty;

    [Required, RegularExpression("^(NORMAL|EXPORT)$")]
    public string SaleType { get; init; } = string.Empty;

    [RegularExpression("^(GENERAL|SELECTIVE)$")]
    public string? DiscountType { get; init; }

    [Range(typeof(decimal), "0", "999999999999999")]
    public decimal DiscountAmount { get; init; }

    [StringLength(50)]
    public string? GroupReferenceId { get; init; }

    [StringLength(50)]
    public string? Reference { get; init; }

    [Required, MinLength(1)]
    public List<InvoiceItemRequest> Items { get; init; } = [];

    public InvoiceCommand ToCommand()
    {
        return new InvoiceCommand(
            InvoiceNumber,
            Currency,
            ExchangeRate,
            CalculationType,
            TransactionDate,
            TotalVat,
            TotalLevy,
            TotalAmount,
            BusinessPartnerName,
            BusinessPartnerTin,
            SaleType,
            DiscountType,
            DiscountAmount,
            GroupReferenceId,
            Items.Select(item => item.ToCommand()).ToArray(),
            Reference);
    }
}

public sealed class InvoiceRequest : FiscalDocumentRequest;

public sealed class RefundRequest : FiscalDocumentRequest
{
    [Required, RegularExpression("^(FULL|PARTIAL)$")]
    public string RefundType { get; init; } = string.Empty;
}

public sealed class CancellationRequest
{
    [Required, StringLength(100, MinimumLength = 1)]
    public string InvoiceNumber { get; init; } = string.Empty;

    [Required, StringLength(50, MinimumLength = 1)]
    public string Reference { get; init; } = string.Empty;

    public DateTimeOffset TransactionDate { get; init; }

    [Range(typeof(decimal), "0.000001", "999999999999999")]
    public decimal TotalAmount { get; init; }

    public CancellationCommand ToCommand() => new(
        InvoiceNumber,
        Reference,
        TransactionDate,
        TotalAmount);
}

public sealed class InvoiceItemRequest
{
    [Required, StringLength(50, MinimumLength = 1)]
    public string ItemCode { get; init; } = string.Empty;

    [StringLength(50), RegularExpression("^(|CST|TRSM|EXM|RNT|EXC_PLASTIC)$")]
    public string ItemCategory { get; init; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 1)]
    public string Description { get; init; } = string.Empty;

    [Range(typeof(decimal), "0.000001", "999999999999999")]
    public decimal Quantity { get; init; }

    [Range(typeof(decimal), "0", "999999999999999")]
    public decimal UnitPrice { get; init; }

    [Range(typeof(decimal), "0", "999999999999999")]
    public decimal LevyAmountA { get; init; }

    [Range(typeof(decimal), "0", "999999999999999")]
    public decimal LevyAmountB { get; init; }

    [Range(typeof(decimal), "0", "999999999999999")]
    public decimal LevyAmountC { get; init; }

    [Range(typeof(decimal), "0", "999999999999999")]
    public decimal LevyAmountD { get; init; }

    [Range(typeof(decimal), "0", "999999999999999")]
    public decimal LevyAmountE { get; init; }

    [Range(typeof(decimal), "0", "999999999999999")]
    public decimal DiscountAmount { get; init; }

    [Range(typeof(decimal), "0", "999999999999999")]
    public decimal ExciseAmount { get; init; }

    [StringLength(100)]
    public string? BatchCode { get; init; }

    public DateOnly? ExpireDate { get; init; }

    public InvoiceItemCommand ToCommand()
    {
        return new InvoiceItemCommand(
            ItemCode,
            ItemCategory,
            Description,
            Quantity,
            UnitPrice,
            LevyAmountA,
            LevyAmountB,
            LevyAmountC,
            LevyAmountD,
            LevyAmountE,
            DiscountAmount,
            ExciseAmount,
            BatchCode,
            ExpireDate);
    }
}
