namespace GraEvatMiddleware.Application.Auditing;

public interface IAuditQueryService
{
    Task<IReadOnlyList<AuditEventResult>> ListAsync(
        Guid tenantId,
        int limit,
        CancellationToken cancellationToken = default);
}
