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

public class GraEvatApiFactory : WebApplicationFactory<Program>
{
    private readonly IReadOnlyDictionary<string, string?> _configurationOverrides;
    private readonly string _databaseName = $"gra-evat-tests-{Guid.NewGuid():N}";
    private readonly IServiceProvider _databaseProvider = new ServiceCollection()
        .AddEntityFrameworkInMemoryDatabase()
        .BuildServiceProvider();

    public GraEvatApiFactory() : this(null)
    {
    }

    internal GraEvatApiFactory(IReadOnlyDictionary<string, string?>? configurationOverrides)
    {
        _configurationOverrides = configurationOverrides ?? new Dictionary<string, string?>();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Security:AdminKey"] = "integration-test-admin-key",
                ["Security:ApiKeyPepper"] = "integration-test-api-key-pepper",
                ["GraRetry:Enabled"] = "true",
                ["GraRetry:WorkerEnabled"] = "false",
                ["GraRetry:MaximumAttempts"] = "3",
                ["GraRetry:InitialDelaySeconds"] = "1"
            });
            configuration.AddInMemoryCollection(_configurationOverrides);
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
    public async Task<GraSubmissionResult> SubmitCancellationAsync(
        GraCancellationSubmission submission,
        CancellationToken cancellationToken = default)
    {
        await writer.WriteAsync(new IntegrationExchangeRecord(
            submission.TenantId, submission.TransactionId, submission.CorrelationId,
            submission.IdempotencyKey, ExchangeDirection.Outbound, "Middleware", "GRA",
            submission.Operation, "POST", "/vsdc/api/v1/taxpayer/test/cancellation",
            sanitizer.SanitizeHeaders(new Dictionary<string, string[]> { ["security_key"] = ["test-secret"] }),
            sanitizer.SanitizeBody(System.Text.Json.JsonSerializer.Serialize(submission.Cancellation),
                "application/json", 65_536).Content,
            false, 200, "{}", null, false, 1, 1, ExchangeOutcome.Succeeded,
            null, DateTimeOffset.UtcNow), cancellationToken);
        return new GraSubmissionResult(true, false, false, submission.Cancellation.Reference, null);
    }

    public async Task<GraSubmissionResult> SubmitInvoiceAsync(
        GraInvoiceSubmission submission,
        CancellationToken cancellationToken = default)
    {
        var isRejected = submission.Invoice.InvoiceNumber.StartsWith("REJECT", StringComparison.Ordinal);
        var isCommunicationFailure = submission.Invoice.InvoiceNumber.StartsWith("TIMEOUT", StringComparison.Ordinal);
        var isTemporary = submission.Invoice.InvoiceNumber.StartsWith("TEMPORARY", StringComparison.Ordinal) &&
            !submission.Operation.EndsWith("Retry", StringComparison.Ordinal);
        var errorCode = isRejected ? "GRA_BUSINESS_REJECTION" :
            isCommunicationFailure ? "GRA_COMMUNICATION_STATUS_UNKNOWN" :
            isTemporary ? "E990" : null;
        await writer.WriteAsync(new IntegrationExchangeRecord(
            submission.TenantId, submission.TransactionId, submission.CorrelationId,
            submission.IdempotencyKey, ExchangeDirection.Outbound, "Middleware", "GRA",
            submission.Operation, "POST", "/vsdc/api/v1/taxpayer/test/invoice",
            sanitizer.SanitizeHeaders(new Dictionary<string, string[]> { ["security_key"] = ["test-secret"] }),
            sanitizer.SanitizeBody(System.Text.Json.JsonSerializer.Serialize(submission.Invoice), "application/json", 65_536).Content,
            false, isCommunicationFailure ? null : isRejected ? 400 : isTemporary ? 503 : 200,
            "{}", isCommunicationFailure ? null : isRejected ? "{\"status\":\"FAILED\"}" :
                isTemporary ? "{\"code\":\"E990\"}" : "{\"status\":\"SUCCESS\"}",
            false, 1, 1,
            isCommunicationFailure || isTemporary ? ExchangeOutcome.Failed :
                isRejected ? ExchangeOutcome.Rejected : ExchangeOutcome.Succeeded,
            errorCode, DateTimeOffset.UtcNow), cancellationToken);

        if (isCommunicationFailure) throw new HttpRequestException("Controlled communication failure.");
        if (isTemporary) return new GraSubmissionResult(false, true, false, null, errorCode);
        return isRejected
            ? new GraSubmissionResult(false, false, true, null, errorCode)
            : new GraSubmissionResult(true, false, false, $"GRA-{submission.Invoice.InvoiceNumber}", null);
    }

    public async Task<GraReconciliationResult> ReconcileInvoiceAsync(
        GraInvoiceReconciliation reconciliation,
        CancellationToken cancellationToken = default)
    {
        var isNotFound = reconciliation.InvoiceNumber.StartsWith("TEMPORARY", StringComparison.Ordinal);
        await writer.WriteAsync(new IntegrationExchangeRecord(
            reconciliation.TenantId, reconciliation.TransactionId, reconciliation.CorrelationId,
            reconciliation.IdempotencyKey, ExchangeDirection.Outbound, "Middleware", "GRA",
            reconciliation.Operation, "POST", "/vsdc/api/v1/taxpayer/test/invoice/callback",
            sanitizer.SanitizeHeaders(new Dictionary<string, string[]> { ["security_key"] = ["test-secret"] }),
            $"{{\"invoiceNumber\":\"{reconciliation.InvoiceNumber}\"}}", false, 200,
            "{}", isNotFound ? "{\"code\":\"E701\"}" : "{\"response\":{\"status\":\"SUCCESS\"}}",
            false, 1, 1,
            ExchangeOutcome.Succeeded, null, DateTimeOffset.UtcNow), cancellationToken);
        if (isNotFound)
        {
            return new GraReconciliationResult(false, true, false, false, null, "E701");
        }

        return new GraReconciliationResult(
            true, false, false, false, $"GRA-{reconciliation.InvoiceNumber}", null);
    }

    public Task<GraHealthResult> CheckHealthAsync(
        GraHealthCheck healthCheck,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new GraHealthResult(true, null));
}
