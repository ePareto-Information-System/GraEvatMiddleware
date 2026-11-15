using GraEvatMiddleware.Application.Abstractions;
using Microsoft.Extensions.Configuration;

namespace GraEvatMiddleware.Infrastructure.Secrets;

internal sealed class ConfigurationGraSecretProvider(IConfiguration configuration) : IGraSecretProvider
{
    public Task<string?> GetSecretAsync(string secretName, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(configuration[$"GraSecrets:{secretName}"]);
    }
}
