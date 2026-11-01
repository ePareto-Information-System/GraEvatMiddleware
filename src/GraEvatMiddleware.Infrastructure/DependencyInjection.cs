using GraEvatMiddleware.Application.Abstractions;
using GraEvatMiddleware.Application.Invoices;
using GraEvatMiddleware.Application.Tenants;
using GraEvatMiddleware.Application.Transactions;
using GraEvatMiddleware.Infrastructure.Authentication;
using GraEvatMiddleware.Infrastructure.Persistence;
using GraEvatMiddleware.Infrastructure.Secrets;
using GraEvatMiddleware.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GraEvatMiddleware.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Database")
            ?? throw new InvalidOperationException("ConnectionStrings:Database must be configured.");

        services.Configure<SecurityOptions>(configuration.GetSection(SecurityOptions.SectionName));
        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
        services.AddSingleton<ApiKeyMaterialService>();
        services.AddScoped<ITenantManagementService, TenantManagementService>();
        services.AddScoped<IApiKeyAuthenticator, ApiKeyAuthenticator>();
        services.AddScoped<InvoiceService>();
        services.AddScoped<IInvoiceService>(provider => provider.GetRequiredService<InvoiceService>());
        services.AddScoped<ITransactionQueryService>(provider => provider.GetRequiredService<InvoiceService>());
        services.AddScoped<IGraSecretProvider, ConfigurationGraSecretProvider>();
        return services;
    }
}
