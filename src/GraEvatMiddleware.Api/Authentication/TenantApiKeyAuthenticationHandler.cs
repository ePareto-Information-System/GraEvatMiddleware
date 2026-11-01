using System.Security.Claims;
using System.Text.Encodings.Web;
using GraEvatMiddleware.Application.Tenants;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace GraEvatMiddleware.Api.Authentication;

public sealed class TenantApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IApiKeyAuthenticator authenticator)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "TenantApiKey";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var apiKey = Request.Headers["X-API-Key"].ToString();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return AuthenticateResult.Fail("X-API-Key is required.");
        }

        var result = await authenticator.AuthenticateAsync(apiKey, Context.RequestAborted);
        if (!result.IsAuthenticated || result.TenantId is null || result.CredentialId is null)
        {
            return AuthenticateResult.Fail(result.FailureCode ?? "Authentication failed.");
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, result.TenantId.Value.ToString()),
            new Claim("tenant_id", result.TenantId.Value.ToString()),
            new Claim("credential_id", result.CredentialId.Value.ToString())
        };
        var identity = new ClaimsIdentity(claims, SchemeName);
        return AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
    }
}
