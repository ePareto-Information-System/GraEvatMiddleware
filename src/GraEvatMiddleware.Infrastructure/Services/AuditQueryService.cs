using GraEvatMiddleware.Application.Auditing;
using GraEvatMiddleware.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GraEvatMiddleware.Infrastructure.Services;

internal sealed class AuditQueryService(AppDbContext dbContext) : IAuditQueryService
{
    public async Task<IReadOnlyList<AuditEventResult>> ListAsync(
        Guid tenantId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var boundedLimit = Math.Clamp(limit, 1, 200);
        return await dbContext.AuditEvents
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId)
            .OrderByDescending(item => item.CreatedAt)
            .Take(boundedLimit)
            .Select(item => new AuditEventResult(
                item.Id,
                item.TenantId!.Value,
                item.Action,
                item.ResourceType,
                item.ResourceId,
                item.CorrelationId,
                item.CreatedAt))
            .ToListAsync(cancellationToken);
    }
}
