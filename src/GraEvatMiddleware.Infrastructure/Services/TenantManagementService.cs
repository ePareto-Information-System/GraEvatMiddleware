using System.Text.Json;
using GraEvatMiddleware.Application.Common;
using GraEvatMiddleware.Application.Tenants;
using GraEvatMiddleware.Domain.Auditing;
using GraEvatMiddleware.Domain.Tenants;
using GraEvatMiddleware.Infrastructure.Authentication;
using GraEvatMiddleware.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GraEvatMiddleware.Infrastructure.Services;

internal sealed class TenantManagementService(
    AppDbContext dbContext,
    ApiKeyMaterialService keyMaterialService) : ITenantManagementService
{
    public async Task<TenantRegistrationResult> RegisterAsync(
        RegisterTenantCommand command,
        CancellationToken cancellationToken = default)
    {
        var slug = command.Slug.Trim().ToLowerInvariant();
        if (await dbContext.Tenants.AnyAsync(item => item.Slug == slug, cancellationToken))
        {
            throw new ResourceConflictException("A tenant with this slug already exists.");
        }

        if (await dbContext.Tenants.AnyAsync(
                item => item.TaxpayerReference == command.TaxpayerReference.Trim(),
                cancellationToken))
        {
            throw new ResourceConflictException("This GRA taxpayer reference is already registered.");
        }

        var now = DateTimeOffset.UtcNow;
        var tenant = Tenant.Register(
            command.Name,
            slug,
            command.TaxpayerReference,
            command.GraSecurityKeySecretName,
            now);

        var issued = keyMaterialService.Generate();
        var credential = ApiCredential.Issue(tenant.Id, issued.Prefix, issued.Hash, now);
        tenant.Credentials.Add(credential);

        dbContext.Tenants.Add(tenant);
        dbContext.AuditEvents.Add(AuditEvent.Create(
            tenant.Id,
            "tenant.registered",
            "tenant",
            tenant.Id.ToString(),
            command.CorrelationId,
            JsonSerializer.Serialize(new { tenant.Slug, tenant.TaxpayerReference }),
            now));

        await dbContext.SaveChangesAsync(cancellationToken);

        return new TenantRegistrationResult(
            tenant.Id,
            tenant.Name,
            tenant.Slug,
            tenant.Status.ToString(),
            new IssuedCredentialResult(credential.Id, issued.RawKey, issued.Prefix, credential.CreatedAt));
    }

    public async Task<IssuedCredentialResult> IssueCredentialAsync(
        Guid tenantId,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var tenant = await FindTenantAsync(tenantId, cancellationToken);
        if (tenant.Status == TenantStatus.Terminated)
        {
            throw new ResourceConflictException("Credentials cannot be issued for a terminated tenant.");
        }

        var now = DateTimeOffset.UtcNow;
        var issued = keyMaterialService.Generate();
        var credential = ApiCredential.Issue(tenant.Id, issued.Prefix, issued.Hash, now);
        tenant.Credentials.Add(credential);
        dbContext.AuditEvents.Add(AuditEvent.Create(
            tenant.Id,
            "credential.issued",
            "api_credential",
            credential.Id.ToString(),
            correlationId,
            "{}",
            now));
        await dbContext.SaveChangesAsync(cancellationToken);

        return new IssuedCredentialResult(credential.Id, issued.RawKey, issued.Prefix, now);
    }

    public async Task RevokeCredentialAsync(
        Guid tenantId,
        Guid credentialId,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var credential = await dbContext.ApiCredentials.SingleOrDefaultAsync(
            item => item.Id == credentialId && item.TenantId == tenantId,
            cancellationToken) ?? throw new ResourceNotFoundException("Credential was not found.");

        var now = DateTimeOffset.UtcNow;
        credential.Revoke(now);
        dbContext.AuditEvents.Add(AuditEvent.Create(
            tenantId,
            "credential.revoked",
            "api_credential",
            credential.Id.ToString(),
            correlationId,
            "{}",
            now));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task ActivateAsync(Guid tenantId, string correlationId, CancellationToken cancellationToken = default)
    {
        return SetStatusAsync(tenantId, true, correlationId, cancellationToken);
    }

    public Task SuspendAsync(Guid tenantId, string correlationId, CancellationToken cancellationToken = default)
    {
        return SetStatusAsync(tenantId, false, correlationId, cancellationToken);
    }

    private async Task SetStatusAsync(
        Guid tenantId,
        bool activate,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var tenant = await FindTenantAsync(tenantId, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        if (activate)
        {
            tenant.Activate(now);
        }
        else
        {
            tenant.Suspend(now);
        }

        dbContext.AuditEvents.Add(AuditEvent.Create(
            tenant.Id,
            activate ? "tenant.activated" : "tenant.suspended",
            "tenant",
            tenant.Id.ToString(),
            correlationId,
            "{}",
            now));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<Tenant> FindTenantAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        return await dbContext.Tenants
            .Include(item => item.Credentials)
            .SingleOrDefaultAsync(item => item.Id == tenantId, cancellationToken)
            ?? throw new ResourceNotFoundException("Tenant was not found.");
    }
}
