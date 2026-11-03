using GraEvatMiddleware.Application.Logging;
using GraEvatMiddleware.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GraEvatMiddleware.Infrastructure.Logging;

internal sealed class ExchangeLogRetentionService(
    AppDbContext dbContext,
    IOptions<ExchangeLoggingOptions> options) : IExchangeLogRetentionService
{
    public async Task<int> DeleteExpiredAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var cutoff = now.AddDays(-Math.Max(1, options.Value.RetentionDays));
        var expired = await dbContext.IntegrationExchanges
            .Where(item => item.CreatedAt < cutoff)
            .ToListAsync(cancellationToken);
        dbContext.IntegrationExchanges.RemoveRange(expired);
        await dbContext.SaveChangesAsync(cancellationToken);
        return expired.Count;
    }
}
