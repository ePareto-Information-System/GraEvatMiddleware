using GraEvatMiddleware.Application.Abstractions;
using GraEvatMiddleware.Application.Logging;
using GraEvatMiddleware.Domain.Integration;
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

internal sealed class SuccessfulGraEvatClient(
    IIntegrationExchangeWriter writer,
    IExchangeContentSanitizer sanitizer) : IGraEvatClient
{
    public async Task<GraSubmissionResult> SubmitInvoiceAsync(
        GraInvoiceSubmission submission,
        CancellationToken cancellationToken = default)
    {
        var isRejected = submission.Invoice.InvoiceNumber.StartsWith("REJECT", StringComparison.Ordinal);
        var isCommunicationFailure = submission.Invoice.InvoiceNumber.StartsWith("TIMEOUT", StringComparison.Ordinal);
        var errorCode = isRejected ? "GRA_BUSINESS_REJECTION" :
            isCommunicationFailure ? "GRA_COMMUNICATION_STATUS_UNKNOWN" : null;
        await writer.WriteAsync(new IntegrationExchangeRecord(
            submission.TenantId, submission.TransactionId, submission.CorrelationId,
            submission.IdempotencyKey, ExchangeDirection.Outbound, "Middleware", "GRA",
            submission.Operation, "POST", "/vsdc/api/v1/taxpayer/test/invoice",
            sanitizer.SanitizeHeaders(new Dictionary<string, string[]> { ["security_key"] = ["test-secret"] }),
            sanitizer.SanitizeBody(System.Text.Json.JsonSerializer.Serialize(submission.Invoice), "application/json", 65_536).Content,
            false, isCommunicationFailure ? null : isRejected ? 400 : 200,
            "{}", isCommunicationFailure ? null : isRejected ? "{\"status\":\"FAILED\"}" : "{\"status\":\"SUCCESS\"}",
            false, 1, 1,
            isCommunicationFailure ? ExchangeOutcome.Failed : isRejected ? ExchangeOutcome.Rejected : ExchangeOutcome.Succeeded,
            errorCode, DateTimeOffset.UtcNow), cancellationToken);

        if (isCommunicationFailure) throw new HttpRequestException("Controlled communication failure.");
        return isRejected
            ? new GraSubmissionResult(false, false, true, null, errorCode)
            : new GraSubmissionResult(true, false, false, $"GRA-{submission.Invoice.InvoiceNumber}", null);
    }
}
