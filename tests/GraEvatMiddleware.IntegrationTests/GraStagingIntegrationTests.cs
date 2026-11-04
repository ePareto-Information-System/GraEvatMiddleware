using GraEvatMiddleware.Application.Abstractions;
using GraEvatMiddleware.Application.Invoices;
using GraEvatMiddleware.Application.Logging;
using GraEvatMiddleware.GraClient.Client;
using GraEvatMiddleware.GraClient.Configuration;
using GraEvatMiddleware.Infrastructure.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Xunit.Abstractions;

namespace GraEvatMiddleware.IntegrationTests;

public sealed class GraStagingIntegrationTests(ITestOutputHelper output)
{
    [GraStagingFact]
    [Trait("Category", "GraStaging")]
    public async Task DocumentedHealthInvoiceAndCallback_WorkAgainstStaging()
    {
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets(typeof(Program).Assembly, optional: true)
            .AddEnvironmentVariables()
            .Build();
        var options = configuration.GetSection(GraApiOptions.SectionName).Get<GraApiOptions>()
            ?? throw new InvalidOperationException("GraApi staging configuration is missing.");
        var taxpayerReference = configuration["GraStaging:TaxpayerReference"]
            ?? throw new InvalidOperationException("GraStaging:TaxpayerReference is missing.");
        var secretName = configuration["GraStaging:SecurityKeySecretName"]
            ?? throw new InvalidOperationException("GraStaging:SecurityKeySecretName is missing.");
        Assert.True(options.EnableStagingIntegrationTests);

        var writer = new CollectingExchangeWriter();
        using var httpClient = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
        {
            Timeout = TimeSpan.FromSeconds(Math.Clamp(options.RequestTimeoutSeconds, 1, 120))
        };
        var client = new GraEvatClient(
            httpClient,
            new ConfigurationSecretProvider(configuration),
            new SensitiveDataSanitizer(),
            writer,
            Options.Create(options),
            Options.Create(new ExchangeLoggingOptions()));

        var health = await client.CheckHealthAsync(new GraHealthCheck(taxpayerReference, secretName));
        output.WriteLine("GET {0}vsdc/api/v1/taxpayer/{1}/health -> healthy={2}",
            options.BaseUrl,
            taxpayerReference,
            health.IsHealthy);
        Assert.True(health.IsHealthy, health.ErrorCode);

        var invoiceNumber = Environment.GetEnvironmentVariable("GRA_STAGING_INVOICE_NUMBER")
            ?? $"EVAT-STAGE-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
        var transactionId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var submission = new GraInvoiceSubmission(
            transactionId,
            tenantId,
            $"staging-{Guid.NewGuid():N}",
            $"staging-{Guid.NewGuid():N}",
            "Invoice",
            taxpayerReference,
            secretName,
            CreateStagingInvoice(invoiceNumber));

        var submitted = await client.SubmitInvoiceAsync(submission);
        output.WriteLine("POST vsdc/api/v1/taxpayer/{0}/invoice invoice={1} -> success={2}, externalReference={3}",
            taxpayerReference,
            invoiceNumber,
            submitted.IsSuccessful,
            submitted.ExternalReference ?? "(none)");
        Assert.True(submitted.IsSuccessful, submitted.ErrorCode);
        Assert.False(string.IsNullOrWhiteSpace(submitted.ExternalReference));

        var reconciled = await client.ReconcileInvoiceAsync(new GraInvoiceReconciliation(
            transactionId,
            tenantId,
            submission.CorrelationId,
            submission.IdempotencyKey,
            "InvoiceReconciliation",
            taxpayerReference,
            secretName,
            invoiceNumber));
        output.WriteLine("POST vsdc/api/v1/taxpayer/{0}/invoice/callback invoice={1} -> found={2}, externalReference={3}",
            taxpayerReference,
            invoiceNumber,
            reconciled.IsFound,
            reconciled.ExternalReference ?? "(none)");
        Assert.True(reconciled.IsFound, reconciled.ErrorCode);
        Assert.False(string.IsNullOrWhiteSpace(reconciled.ExternalReference));
        Assert.Equal(2, writer.Records.Count);
        Assert.All(writer.Records, record =>
        {
            Assert.Contains("[REDACTED]", record.RequestHeadersJson, StringComparison.Ordinal);
            Assert.DoesNotContain(configuration[$"GraSecrets:{secretName}"]!,
                $"{record.RequestHeadersJson}{record.RequestBody}{record.ResponseHeadersJson}{record.ResponseBody}",
                StringComparison.Ordinal);
        });
    }

    private static InvoiceCommand CreateStagingInvoice(string invoiceNumber) => new(
        invoiceNumber,
        "GHS",
        1m,
        "EXCLUSIVE",
        DateOnly.FromDateTime(DateTime.UtcNow),
        15m,
        5m,
        100m,
        "Ama Ghana (Cash Customer)",
        "C0000000000",
        "NORMAL",
        "GENERAL",
        0m,
        null,
        [new InvoiceItemCommand(
            invoiceNumber.Replace("-", string.Empty, StringComparison.Ordinal),
            string.Empty,
            "Staging integration test item",
            1m,
            100m,
            2.5m,
            2.5m,
            0m,
            0m,
            0m,
            0m,
            0m,
            null,
            null)]);

    private sealed class ConfigurationSecretProvider(IConfiguration configuration) : IGraSecretProvider
    {
        public Task<string?> GetSecretAsync(
            string secretName,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(configuration[$"GraSecrets:{secretName}"]);
    }

    private sealed class CollectingExchangeWriter : IIntegrationExchangeWriter
    {
        public List<IntegrationExchangeRecord> Records { get; } = [];

        public Task WriteAsync(
            IntegrationExchangeRecord record,
            CancellationToken cancellationToken = default)
        {
            Records.Add(record);
            return Task.CompletedTask;
        }
    }
}

internal sealed class GraStagingFactAttribute : FactAttribute
{
    public GraStagingFactAttribute()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("GRA_STAGING_TESTS_ENABLED"),
                "true",
                StringComparison.OrdinalIgnoreCase))
        {
            Skip = "Set GRA_STAGING_TESTS_ENABLED=true to run real GRA staging tests.";
        }
    }
}
