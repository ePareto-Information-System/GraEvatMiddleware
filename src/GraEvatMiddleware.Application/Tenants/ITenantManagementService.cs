namespace GraEvatMiddleware.Application.Tenants;

public interface ITenantManagementService
{
    Task<TenantRegistrationResult> RegisterAsync(RegisterTenantCommand command, CancellationToken cancellationToken = default);
    Task<IssuedCredentialResult> IssueCredentialAsync(Guid tenantId, string correlationId, CancellationToken cancellationToken = default);
    Task RevokeCredentialAsync(Guid tenantId, Guid credentialId, string correlationId, CancellationToken cancellationToken = default);
    Task ActivateAsync(Guid tenantId, string correlationId, CancellationToken cancellationToken = default);
    Task SuspendAsync(Guid tenantId, string correlationId, CancellationToken cancellationToken = default);
}

public interface IApiKeyAuthenticator
{
    Task<ApiKeyAuthenticationResult> AuthenticateAsync(string apiKey, CancellationToken cancellationToken = default);
}
