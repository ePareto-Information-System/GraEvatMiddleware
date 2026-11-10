using GraEvatMiddleware.Application.Abstractions;
using GraEvatMiddleware.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace GraEvatMiddleware.IntegrationTests;

internal sealed class PostgresGraEvatApiFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;

    public PostgresGraEvatApiFactory()
    {
        _connectionString = Environment.GetEnvironmentVariable(PostgresFactAttribute.ConnectionStringVariable)
            ?? throw new InvalidOperationException(
                $"{PostgresFactAttribute.ConnectionStringVariable} is required for PostgreSQL integration tests.");

        var builder = new NpgsqlConnectionStringBuilder(_connectionString);
        var databaseName = builder.Database ?? string.Empty;
        if (!databaseName.EndsWith("_test", StringComparison.OrdinalIgnoreCase) &&
            !databaseName.EndsWith("_tests", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"PostgreSQL integration tests require a database name ending in '_test' or '_tests'; '{databaseName}' is not allowed.");
        }
    }

    public CountingGraEvatClient GraClient { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Database"] = _connectionString,
                ["Security:AdminKey"] = "postgres-test-admin-key",
                ["Security:ApiKeyPepper"] = "postgres-test-api-key-pepper",
                ["GraRetry:Enabled"] = "true",
                ["GraRetry:WorkerEnabled"] = "false",
                ["GraRetry:MaximumAttempts"] = "3",
                ["GraRetry:InitialDelaySeconds"] = "1",
                ["GraRetry:LeaseSeconds"] = "1"
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IGraEvatClient>();
            services.AddSingleton<IGraEvatClient>(GraClient);
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        using var scope = host.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var actualDatabaseName = dbContext.Database.GetDbConnection().Database;
        if (!actualDatabaseName.EndsWith("_test", StringComparison.OrdinalIgnoreCase) &&
            !actualDatabaseName.EndsWith("_tests", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Refusing to migrate or truncate non-test database '{actualDatabaseName}'.");
        }

        dbContext.Database.Migrate();
        dbContext.Database.ExecuteSqlRaw(
            "TRUNCATE TABLE integration_exchange_logs, audit_events, gra_retry_jobs, api_credentials, evat_transactions, tenants RESTART IDENTITY CASCADE;");
        return host;
    }
}

internal sealed class CountingGraEvatClient : IGraEvatClient
{
    private int _submissionCount;
    private int _reconciliationCount;

    public int SubmissionCount => Volatile.Read(ref _submissionCount);
    public int ReconciliationCount => Volatile.Read(ref _reconciliationCount);

    public async Task<GraSubmissionResult> SubmitInvoiceAsync(
        GraInvoiceSubmission submission,
        CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _submissionCount);
        await Task.Delay(150, cancellationToken);
        if (submission.Invoice.InvoiceNumber.StartsWith("TIMEOUT", StringComparison.Ordinal))
        {
            throw new HttpRequestException("Controlled PostgreSQL concurrency test failure.");
        }

        return new GraSubmissionResult(
            true, false, false, $"GRA-{submission.Invoice.InvoiceNumber}", null);
    }

    public async Task<GraSubmissionResult> SubmitCancellationAsync(
        GraCancellationSubmission submission,
        CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _submissionCount);
        await Task.Delay(150, cancellationToken);
        return new GraSubmissionResult(true, false, false, submission.Cancellation.Reference, null);
    }

    public async Task<GraReconciliationResult> ReconcileInvoiceAsync(
        GraInvoiceReconciliation reconciliation,
        CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _reconciliationCount);
        await Task.Delay(150, cancellationToken);
        return new GraReconciliationResult(
            true, false, false, false, $"GRA-{reconciliation.InvoiceNumber}", null);
    }

    public Task<GraHealthResult> CheckHealthAsync(
        GraHealthCheck healthCheck,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new GraHealthResult(true, null));
}
