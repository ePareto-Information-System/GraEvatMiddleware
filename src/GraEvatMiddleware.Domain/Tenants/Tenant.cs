using GraEvatMiddleware.Domain.Common;
using GraEvatMiddleware.Domain.Transactions;

namespace GraEvatMiddleware.Domain.Tenants;

public sealed class Tenant
{
    private Tenant()
    {
    }

    private Tenant(
        Guid id,
        string name,
        string slug,
        string taxpayerReference,
        string graSecurityKeySecretName,
        DateTimeOffset now)
    {
        Id = id;
        Name = name;
        Slug = slug;
        TaxpayerReference = taxpayerReference;
        GraSecurityKeySecretName = graSecurityKeySecretName;
        Status = TenantStatus.Active;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string Slug { get; private set; } = string.Empty;

    public string TaxpayerReference { get; private set; } = string.Empty;

    public string GraSecurityKeySecretName { get; private set; } = string.Empty;

    public TenantStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public ICollection<ApiCredential> Credentials { get; } = new List<ApiCredential>();

    public ICollection<EVatTransaction> Transactions { get; } = new List<EVatTransaction>();

    public static Tenant Register(
        string name,
        string slug,
        string taxpayerReference,
        string graSecurityKeySecretName,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainValidationException("Tenant name is required.");
        }

        if (string.IsNullOrWhiteSpace(slug))
        {
            throw new DomainValidationException("Tenant slug is required.");
        }

        if (string.IsNullOrWhiteSpace(taxpayerReference))
        {
            throw new DomainValidationException("GRA taxpayer reference is required.");
        }

        if (string.IsNullOrWhiteSpace(graSecurityKeySecretName))
        {
            throw new DomainValidationException("GRA security-key secret name is required.");
        }

        return new Tenant(
            Guid.NewGuid(),
            name.Trim(),
            slug.Trim().ToLowerInvariant(),
            taxpayerReference.Trim(),
            graSecurityKeySecretName.Trim(),
            now);
    }

    public void Activate(DateTimeOffset now)
    {
        EnsureNotTerminated();
        Status = TenantStatus.Active;
        UpdatedAt = now;
    }

    public void Suspend(DateTimeOffset now)
    {
        EnsureNotTerminated();
        Status = TenantStatus.Suspended;
        UpdatedAt = now;
    }

    public void Terminate(DateTimeOffset now)
    {
        Status = TenantStatus.Terminated;
        UpdatedAt = now;

        foreach (var credential in Credentials.Where(item => item.Status == ApiCredentialStatus.Active))
        {
            credential.Revoke(now);
        }
    }

    private void EnsureNotTerminated()
    {
        if (Status == TenantStatus.Terminated)
        {
            throw new DomainValidationException("A terminated tenant cannot be reactivated or suspended.");
        }
    }
}
