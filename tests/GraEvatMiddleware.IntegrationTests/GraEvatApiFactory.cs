using GraEvatMiddleware.Application.Abstractions;
using GraEvatMiddleware.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace GraEvatMiddleware.IntegrationTests;

public sealed class GraEvatApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"gra-evat-tests-{Guid.NewGuid():N}";
    private readonly IServiceProvider _databaseProvider = new ServiceCollection()
        .AddEntityFrameworkInMemoryDatabase()
        .BuildServiceProvider();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Security:AdminKey"] = "integration-test-admin-key",
                ["Security:ApiKeyPepper"] = "integration-test-api-key-pepper"
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<AppDbContext>();
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<IDatabaseProvider>();
            services.AddDbContext<AppDbContext>(options => options
                .UseInMemoryDatabase(_databaseName)
                .UseInternalServiceProvider(_databaseProvider));

            services.RemoveAll<IGraEvatClient>();
            services.AddSingleton<IGraEvatClient, SuccessfulGraEvatClient>();
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        using var scope = host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();
        return host;
    }
}

internal sealed class SuccessfulGraEvatClient : IGraEvatClient
{
    public Task<GraSubmissionResult> SubmitInvoiceAsync(
        GraInvoiceSubmission submission,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new GraSubmissionResult(
            IsSuccessful: true,
            IsPending: false,
            IsRejected: false,
            ExternalReference: $"GRA-{submission.Invoice.InvoiceNumber}",
            ErrorCode: null));
    }
}
