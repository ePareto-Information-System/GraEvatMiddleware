using Microsoft.Extensions.Options;

namespace GraEvatMiddleware.Infrastructure.Authentication;

public sealed class SecurityOptionsValidator : IValidateOptions<SecurityOptions>
{
    private const int MinimumSecretLength = 24;

    public ValidateOptionsResult Validate(string? name, SecurityOptions options)
    {
        var failures = new List<string>();
        ValidateSecret(options.AdminKey, "Security:AdminKey", failures);
        ValidateSecret(options.ApiKeyPepper, "Security:ApiKeyPepper", failures);

        if (!string.IsNullOrWhiteSpace(options.AdminKey) &&
            string.Equals(options.AdminKey, options.ApiKeyPepper, StringComparison.Ordinal))
        {
            failures.Add("Security:AdminKey and Security:ApiKeyPepper must be different values.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static void ValidateSecret(string value, string settingName, ICollection<string> failures)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            failures.Add($"{settingName} must be configured outside committed settings.");
            return;
        }

        if (value.Length < MinimumSecretLength)
        {
            failures.Add($"{settingName} must contain at least {MinimumSecretLength} characters.");
        }

        if (value.Contains("CHANGE_THIS", StringComparison.OrdinalIgnoreCase))
        {
            failures.Add($"{settingName} still contains a placeholder value.");
        }
    }
}
