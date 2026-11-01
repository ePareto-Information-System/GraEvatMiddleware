namespace GraEvatMiddleware.Domain.Tenants;

public sealed class ApiCredential
{
    private ApiCredential()
    {
    }

    private ApiCredential(
        Guid id,
        Guid tenantId,
        string keyPrefix,
        string keyHash,
        DateTimeOffset createdAt,
        DateTimeOffset? expiresAt)
    {
        Id = id;
        TenantId = tenantId;
        KeyPrefix = keyPrefix;
        KeyHash = keyHash;
        Status = ApiCredentialStatus.Active;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Tenant Tenant { get; private set; } = null!;

    public string KeyPrefix { get; private set; } = string.Empty;

    public string KeyHash { get; private set; } = string.Empty;

    public ApiCredentialStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? ExpiresAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public DateTimeOffset? LastUsedAt { get; private set; }

    public static ApiCredential Issue(
        Guid tenantId,
        string keyPrefix,
        string keyHash,
        DateTimeOffset now,
        DateTimeOffset? expiresAt = null)
    {
        return new ApiCredential(Guid.NewGuid(), tenantId, keyPrefix, keyHash, now, expiresAt);
    }

    public bool IsUsable(DateTimeOffset now)
    {
        return Status == ApiCredentialStatus.Active && (ExpiresAt is null || ExpiresAt > now);
    }

    public void RecordUse(DateTimeOffset now)
    {
        LastUsedAt = now;
    }

    public void Revoke(DateTimeOffset now)
    {
        if (Status == ApiCredentialStatus.Revoked)
        {
            return;
        }

        Status = ApiCredentialStatus.Revoked;
        RevokedAt = now;
    }
}
