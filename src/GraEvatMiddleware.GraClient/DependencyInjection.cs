using GraEvatMiddleware.Application.Abstractions;
using GraEvatMiddleware.GraClient.Client;
using GraEvatMiddleware.GraClient.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GraEvatMiddleware.GraClient;

public static class DependencyInjection
{
    public static IServiceCollection AddGraClient(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<GraApiOptions>(configuration.GetSection(GraApiOptions.SectionName));
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
