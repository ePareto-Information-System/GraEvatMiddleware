using GraEvatMiddleware.Application.Abstractions;
using GraEvatMiddleware.Application.Auditing;
using GraEvatMiddleware.Application.Invoices;
using GraEvatMiddleware.Application.Health;
using GraEvatMiddleware.Application.Logging;
using GraEvatMiddleware.Application.Retry;
using GraEvatMiddleware.Application.Tenants;
using GraEvatMiddleware.Application.Transactions;
using GraEvatMiddleware.Infrastructure.Authentication;
using GraEvatMiddleware.Infrastructure.Logging;
using GraEvatMiddleware.Infrastructure.Persistence;
using GraEvatMiddleware.Infrastructure.Retry;
using GraEvatMiddleware.Infrastructure.Secrets;
using GraEvatMiddleware.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace GraEvatMiddleware.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Database")
            ?? throw new InvalidOperationException("ConnectionStrings:Database must be configured.");

        services.AddSingleton<IValidateOptions<SecurityOptions>, SecurityOptionsValidator>();
        services.AddOptions<SecurityOptions>()
            .Bind(configuration.GetSection(SecurityOptions.SectionName))
            .ValidateOnStart();
        services.Configure<ExchangeLoggingOptions>(configuration.GetSection(ExchangeLoggingOptions.SectionName));
        services.Configure<GraRetryOptions>(configuration.GetSection(GraRetryOptions.SectionName));
        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
        services.AddSingleton<ApiKeyMaterialService>();
        services.AddScoped<ITenantManagementService, TenantManagementService>();
        services.AddScoped<IAuditQueryService, AuditQueryService>();
        services.AddScoped<IReadinessService, ReadinessService>();
        services.AddScoped<IApiKeyAuthenticator, ApiKeyAuthenticator>();
        services.AddScoped<InvoiceService>();
        services.AddScoped<IInvoiceService>(provider => provider.GetRequiredService<InvoiceService>());
        services.AddScoped<ITransactionQueryService>(provider => provider.GetRequiredService<InvoiceService>());
        services.AddScoped<IGraSecretProvider, ConfigurationGraSecretProvider>();
        services.AddSingleton<IExchangeContentSanitizer, SensitiveDataSanitizer>();
        services.AddSingleton<IIntegrationExchangeWriter, IntegrationExchangeWriter>();
        services.AddScoped<IExchangeLogRetentionService, ExchangeLogRetentionService>();
        services.AddScoped<IGraRetryProcessor, GraRetryProcessor>();
        services.AddHostedService<GraRetryWorker>();
        return services;
    }
}
