namespace GraEvatMiddleware.Application.Tenants;

public sealed record RegisterTenantCommand(
    string Name,
    string Slug,
    string TaxpayerReference,
    string GraSecurityKeySecretName,
    string CorrelationId);

public sealed record TenantRegistrationResult(
    Guid TenantId,
    string Name,
    string Slug,
    string Status,
    IssuedCredentialResult Credential);

public sealed record IssuedCredentialResult(
    Guid CredentialId,
    string ApiKey,
    string KeyPrefix,
    DateTimeOffset CreatedAt);

public sealed record ApiKeyAuthenticationResult(
    bool IsAuthenticated,
    Guid? TenantId,
    Guid? CredentialId,
    string? FailureCode)
{
    public static ApiKeyAuthenticationResult Failed(string failureCode) =>
        new(false, null, null, failureCode);

    public static ApiKeyAuthenticationResult Succeeded(Guid tenantId, Guid credentialId) =>
        new(true, tenantId, credentialId, null);
}
