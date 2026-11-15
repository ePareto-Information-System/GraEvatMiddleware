using GraEvatMiddleware.Application.Invoices;

namespace GraEvatMiddleware.Application.Transactions;

public interface ITransactionQueryService
{
    Task<TransactionResult?> FindAsync(Guid tenantId, Guid transactionId, CancellationToken cancellationToken = default);
}
