using System.ComponentModel.DataAnnotations;

namespace GraEvatMiddleware.Api.Contracts;

public sealed class RegisterTenantRequest
{
    [Required, StringLength(200, MinimumLength = 1)]
    public string Name { get; init; } = string.Empty;

    [Required, RegularExpression("^[a-z0-9][a-z0-9-]{1,62}[a-z0-9]$")]
    public string Slug { get; init; } = string.Empty;

    [Required, StringLength(64, MinimumLength = 3)]
    public string TaxpayerReference { get; init; } = string.Empty;

    [Required, StringLength(200, MinimumLength = 1)]
    public string GraSecurityKeySecretName { get; init; } = string.Empty;
}

public sealed class UpdateTenantConfigurationRequest
{
    [Required, StringLength(64, MinimumLength = 3)]
    public string TaxpayerReference { get; init; } = string.Empty;

    [Required, StringLength(200, MinimumLength = 1)]
    public string GraSecurityKeySecretName { get; init; } = string.Empty;
}
