namespace GraEvatMiddleware.Domain.Auditing;

public sealed class AuditEvent
{
    private AuditEvent()
    {
    }

    private AuditEvent(
        Guid id,
        Guid? tenantId,
        string action,
        string resourceType,
        string resourceId,
        string correlationId,
        string metadataJson,
        DateTimeOffset createdAt)
    {
        Id = id;
        TenantId = tenantId;
        Action = action;
        ResourceType = resourceType;
        ResourceId = resourceId;
        CorrelationId = correlationId;
        MetadataJson = metadataJson;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid? TenantId { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public string ResourceType { get; private set; } = string.Empty;
    public string ResourceId { get; private set; } = string.Empty;
    public string CorrelationId { get; private set; } = string.Empty;
    public string MetadataJson { get; private set; } = "{}";
    public DateTimeOffset CreatedAt { get; private set; }

    public static AuditEvent Create(
        Guid? tenantId,
        string action,
        string resourceType,
        string resourceId,
        string correlationId,
        string metadataJson,
        DateTimeOffset now)
    {
        return new AuditEvent(
            Guid.NewGuid(), tenantId, action, resourceType, resourceId,
            correlationId, metadataJson, now);
    }
}
