using GraEvatMiddleware.Domain.Common;
using GraEvatMiddleware.Domain.Tenants;

namespace GraEvatMiddleware.UnitTests;

public sealed class TenantLifecycleTests
{
    [Fact]
    public void TerminatingTenant_RevokesActiveCredentials()
    {
        var now = DateTimeOffset.Parse("2026-11-01T10:00:00Z");
        var tenant = Tenant.Register("Example", "example", "TIN-001", "gra-secret", now);
        var credential = ApiCredential.Issue(
            tenant.Id,
            "abc123def456",
            "hashed-key",
            now,
            expiresAt: null);
        tenant.Credentials.Add(credential);

        tenant.Terminate(now.AddMinutes(1));

        Assert.Equal(TenantStatus.Terminated, tenant.Status);
        Assert.Equal(ApiCredentialStatus.Revoked, credential.Status);
        Assert.Equal(now.AddMinutes(1), credential.RevokedAt);
    }

    [Fact]
    public void TerminatedTenant_CannotBeReactivated()
    {
        var tenant = Tenant.Register(
            "Example",
            "example",
            "TIN-001",
            "gra-secret",
            DateTimeOffset.UtcNow);
        tenant.Terminate(DateTimeOffset.UtcNow);

        Assert.Throws<DomainValidationException>(() => tenant.Activate(DateTimeOffset.UtcNow));
    }
}
