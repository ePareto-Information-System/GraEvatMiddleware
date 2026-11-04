namespace GraEvatMiddleware.Application.Auditing;

public sealed record AuditEventResult(
    Guid Id,
    Guid TenantId,
    string Action,
    string ResourceType,
    string ResourceId,
    string CorrelationId,
    DateTimeOffset CreatedAt);
