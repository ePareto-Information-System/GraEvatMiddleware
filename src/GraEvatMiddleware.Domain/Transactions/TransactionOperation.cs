namespace GraEvatMiddleware.Domain.Transactions;

public enum TransactionOperation
{
    Invoice = 1,
    InvoiceRefund = 2,
    RefundCancellation = 3,
    Purchase = 4,
    PurchaseReturn = 5,
    PurchaseReturnCancellation = 6,
    CreditNote = 7,
    DebitNote = 8,
    StatementOfAccount = 9,
    Inventory = 10
}
