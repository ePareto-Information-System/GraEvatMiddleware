using GraEvatMiddleware.GraClient.Configuration;
using GraEvatMiddleware.Infrastructure.Authentication;

namespace GraEvatMiddleware.UnitTests;

public sealed class ConfigurationValidationTests
{
    [Fact]
    public void SecurityConfiguration_RejectsMissingAndMatchingSecrets()
    {
        var validator = new SecurityOptionsValidator();

        var missing = validator.Validate(null, new SecurityOptions());
        var matching = validator.Validate(null, new SecurityOptions
        {
            AdminKey = "same-secure-value-with-enough-length",
            ApiKeyPepper = "same-secure-value-with-enough-length"
        });

        Assert.True(missing.Failed);
        Assert.True(matching.Failed);
    }

    [Fact]
    public void SecurityConfiguration_AcceptsDistinctStrongSecrets()
    {
        var validator = new SecurityOptionsValidator();

        var result = validator.Validate(null, new SecurityOptions
        {
            AdminKey = "admin-key-with-at-least-32-characters",
            ApiKeyPepper = "pepper-with-a-different-32-character-value"
        });

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void GraConfiguration_WhenEnabled_RequiresApprovedStagingSettings()
    {
        var validator = new GraApiOptionsValidator();

        var result = validator.Validate(null, new GraApiOptions
        {
            Enabled = true,
            Environment = "Staging",
            BaseUrl = "http://vsdcstaging.vat-gh.com/",
            AllowedHosts = []
        });

        Assert.True(result.Failed);
    }

    [Fact]
    public void GraConfiguration_WhenDisabled_DoesNotRequireEndpointSettings()
    {
        var validator = new GraApiOptionsValidator();

        var result = validator.Validate(null, new GraApiOptions { Enabled = false });

        Assert.True(result.Succeeded);
    }
}
