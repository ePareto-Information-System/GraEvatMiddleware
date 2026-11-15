using System.Security.Claims;

namespace GraEvatMiddleware.Api.Authentication;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetTenantId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue("tenant_id");
        return Guid.TryParse(value, out var tenantId)
            ? tenantId
            : throw new InvalidOperationException("Authenticated tenant claim is missing.");
    }
}
