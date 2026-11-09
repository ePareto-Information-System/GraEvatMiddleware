using GraEvatMiddleware.Application.Abstractions;
using GraEvatMiddleware.GraClient.Client;
using GraEvatMiddleware.GraClient.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace GraEvatMiddleware.GraClient;

public static class DependencyInjection
{
    public static IServiceCollection AddGraClient(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<IValidateOptions<GraApiOptions>, GraApiOptionsValidator>();
        services.AddOptions<GraApiOptions>()
            .Bind(configuration.GetSection(GraApiOptions.SectionName))
            .ValidateOnStart();
        services.AddHttpClient<IGraEvatClient, GraEvatClient>((provider, client) =>
        {
            var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<GraApiOptions>>().Value;
            client.Timeout = TimeSpan.FromSeconds(Math.Clamp(options.RequestTimeoutSeconds, 1, 120));
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            AllowAutoRedirect = false
        });
        return services;
    }
}
