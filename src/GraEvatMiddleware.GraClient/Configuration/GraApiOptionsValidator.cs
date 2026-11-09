using Microsoft.Extensions.Options;

namespace GraEvatMiddleware.GraClient.Configuration;

public sealed class GraApiOptionsValidator : IValidateOptions<GraApiOptions>
{
    private const string DocumentedStagingHost = "vsdcstaging.vat-gh.com";

    public ValidateOptionsResult Validate(string? name, GraApiOptions options)
    {
        if (!options.Enabled)
        {
            return ValidateOptionsResult.Success;
        }

        var failures = new List<string>();
        if (!string.Equals(options.Environment, "Staging", StringComparison.OrdinalIgnoreCase))
        {
            failures.Add("GraApi:Environment must be Staging while the staging-only safety control is active.");
        }

        if (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var baseUri))
        {
            failures.Add("GraApi:BaseUrl must be an absolute URL.");
        }
        else
        {
            if (!string.Equals(baseUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                failures.Add("GraApi:BaseUrl must use HTTPS.");
            }

            if (!options.AllowedHosts.Contains(baseUri.Host, StringComparer.OrdinalIgnoreCase))
            {
                failures.Add("GraApi:AllowedHosts must contain the configured base URL host.");
            }

            if (!IsApprovedStagingHost(baseUri.Host))
            {
                failures.Add("GraApi:BaseUrl must use the documented staging host or an explicit test host.");
            }
        }

        if (options.RequestTimeoutSeconds is < 1 or > 120)
        {
            failures.Add("GraApi:RequestTimeoutSeconds must be between 1 and 120.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    internal static bool IsApprovedStagingHost(string host) =>
        string.Equals(host, DocumentedStagingHost, StringComparison.OrdinalIgnoreCase) ||
        host.EndsWith(".test", StringComparison.OrdinalIgnoreCase) ||
        host.EndsWith(".example", StringComparison.OrdinalIgnoreCase);
}
