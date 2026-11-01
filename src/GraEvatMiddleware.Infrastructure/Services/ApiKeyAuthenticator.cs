using GraEvatMiddleware.Application.Tenants;
using GraEvatMiddleware.Domain.Tenants;
using GraEvatMiddleware.Infrastructure.Authentication;
using GraEvatMiddleware.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GraEvatMiddleware.Infrastructure.Services;

internal sealed class ApiKeyAuthenticator(
    AppDbContext dbContext,
    ApiKeyMaterialService keyMaterialService) : IApiKeyAuthenticator
{
    public async Task<ApiKeyAuthenticationResult> AuthenticateAsync(
        string apiKey,
        CancellationToken cancellationToken = default)
    {
        var prefix = keyMaterialService.ExtractPrefix(apiKey);
        if (prefix is null)
        {
            return ApiKeyAuthenticationResult.Failed("INVALID_API_KEY");
        }

        var credential = await dbContext.ApiCredentials
            .Include(item => item.Tenant)
            .SingleOrDefaultAsync(item => item.KeyPrefix == prefix, cancellationToken);

        var now = DateTimeOffset.UtcNow;
        if (credential is null || !credential.IsUsable(now) || !keyMaterialService.Verify(apiKey, credential.KeyHash))
        {
            return ApiKeyAuthenticationResult.Failed("INVALID_API_KEY");
        }

        if (credential.Tenant.Status != TenantStatus.Active)
        {
            return ApiKeyAuthenticationResult.Failed("TENANT_NOT_ACTIVE");
        }

        credential.RecordUse(now);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ApiKeyAuthenticationResult.Succeeded(credential.TenantId, credential.Id);
    }
}
