using GraEvatMiddleware.Application.Logging;
using GraEvatMiddleware.Domain.Integration;
using GraEvatMiddleware.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace GraEvatMiddleware.Infrastructure.Logging;

internal sealed class IntegrationExchangeWriter(IServiceScopeFactory scopeFactory) : IIntegrationExchangeWriter
{
    public async Task WriteAsync(IntegrationExchangeRecord record, CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        dbContext.IntegrationExchanges.Add(IntegrationExchange.Capture(
            record.TenantId, record.TransactionId, record.CorrelationId, record.IdempotencyKey,
            record.Direction, record.SourceSystem, record.DestinationSystem, record.Operation,
            record.HttpMethod, record.Url, record.RequestHeadersJson, record.RequestBody,
            record.RequestBodyTruncated, record.ResponseStatusCode, record.ResponseHeadersJson,
            record.ResponseBody, record.ResponseBodyTruncated, record.DurationMilliseconds,
            record.AttemptNumber, record.Outcome, record.ErrorCode, record.CreatedAt));
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
