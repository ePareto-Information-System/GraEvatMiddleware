using GraEvatMiddleware.Domain.Transactions;

namespace GraEvatMiddleware.UnitTests;

public sealed class TransactionStatusTests
{
    [Fact]
    public void PendingGra_IsDifferentFromTerminalStatuses()
    {
        Assert.NotEqual(TransactionStatus.Succeeded, TransactionStatus.PendingGra);
        Assert.NotEqual(TransactionStatus.Rejected, TransactionStatus.PendingGra);
        Assert.NotEqual(TransactionStatus.PermanentFailure, TransactionStatus.PendingGra);
    }
}
