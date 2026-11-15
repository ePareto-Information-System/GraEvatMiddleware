using System.Net;
using System.Net.Http.Json;
using GraEvatMiddleware.Api.Contracts;
using GraEvatMiddleware.Application.Invoices;
using GraEvatMiddleware.Application.Retry;
using GraEvatMiddleware.Application.Tenants;
using GraEvatMiddleware.Domain.Retry;
using GraEvatMiddleware.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GraEvatMiddleware.IntegrationTests;

public sealed class PostgresConcurrencyTests
{
    [PostgresFact]
    public async Task ConcurrentIdenticalSubmissions_CreateOneTransactionAndCallGraOnce()
    {
        await using var factory = new PostgresGraEvatApiFactory();
        using var client = factory.CreateClient();
        var tenant = await RegisterTenantAsync(client);
        var invoice = CreateInvoiceRequest("PG-CONCURRENT-001");

        using var firstRequest = CreateInvoiceRequestMessage(tenant.Credential.ApiKey, "pg-concurrent-key", invoice);
        using var secondRequest = CreateInvoiceRequestMessage(tenant.Credential.ApiKey, "pg-concurrent-key", invoice);
        var responses = await Task.WhenAll(client.SendAsync(firstRequest), client.SendAsync(secondRequest));
        using var firstResponse = responses[0];
        using var secondResponse = responses[1];

        Assert.Contains(firstResponse.StatusCode, new[] { HttpStatusCode.Created, HttpStatusCode.OK });
        Assert.Contains(secondResponse.StatusCode, new[] { HttpStatusCode.Created, HttpStatusCode.OK });
        var first = await firstResponse.Content.ReadFromJsonAsync<TransactionResult>();
        var second = await secondResponse.Content.ReadFromJsonAsync<TransactionResult>();
        Assert.Equal(first?.TransactionId, second?.TransactionId);
        Assert.Equal(1, factory.GraClient.SubmissionCount);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await dbContext.Transactions.CountAsync(item => item.IdempotencyKey == "pg-concurrent-key"));
    }

    [PostgresFact]
    public async Task ConcurrentRetryWorkers_OnlyOneWorkerClaimsAndReconcilesJob()
    {
        await using var factory = new PostgresGraEvatApiFactory();
        using var client = factory.CreateClient();
        var tenant = await RegisterTenantAsync(client);
        using var request = CreateInvoiceRequestMessage(
            tenant.Credential.ApiKey,
            "pg-retry-concurrent-key",
            CreateInvoiceRequest("TIMEOUT-PG-CONCURRENT-001"));
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        await using var firstScope = factory.Services.CreateAsyncScope();
        await using var secondScope = factory.Services.CreateAsyncScope();
        var now = DateTimeOffset.UtcNow.AddMinutes(1);
        var firstWorker = firstScope.ServiceProvider.GetRequiredService<IGraRetryProcessor>();
        var secondWorker = secondScope.ServiceProvider.GetRequiredService<IGraRetryProcessor>();
        var processed = await Task.WhenAll(
            firstWorker.ProcessDueAsync("postgres-worker-a", now),
            secondWorker.ProcessDueAsync("postgres-worker-b", now));

        Assert.Equal(1, processed.Sum());
        Assert.Equal(1, factory.GraClient.ReconciliationCount);
        await using var verificationScope = factory.Services.CreateAsyncScope();
        var dbContext = verificationScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var job = await dbContext.GraRetryJobs.SingleAsync();
        Assert.Equal(RetryJobStatus.Completed, job.Status);
        Assert.Equal(1, job.AttemptNumber);
    }

    [PostgresFact]
    public async Task ExpiredRetryLease_IsRecoveredAfterWorkerRestart()
    {
        await using var factory = new PostgresGraEvatApiFactory();
        using var client = factory.CreateClient();
        var tenant = await RegisterTenantAsync(client);
        using var request = CreateInvoiceRequestMessage(
            tenant.Credential.ApiKey,
            "pg-expired-lease-key",
            CreateInvoiceRequest("TIMEOUT-PG-LEASE-001"));
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var abandonedAt = DateTimeOffset.UtcNow.AddMinutes(1);
        await using (var abandonedScope = factory.Services.CreateAsyncScope())
        {
            var dbContext = abandonedScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var job = await dbContext.GraRetryJobs.SingleAsync();
            Assert.True(job.TryClaim("stopped-worker", abandonedAt, TimeSpan.FromSeconds(1)));
            await dbContext.SaveChangesAsync();
        }

        await using var recoveryScope = factory.Services.CreateAsyncScope();
        var processor = recoveryScope.ServiceProvider.GetRequiredService<IGraRetryProcessor>();
        var processed = await processor.ProcessDueAsync("replacement-worker", abandonedAt.AddSeconds(2));

        Assert.Equal(1, processed);
        await using var verificationScope = factory.Services.CreateAsyncScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var recovered = await verificationDb.GraRetryJobs.SingleAsync();
        Assert.Equal(RetryJobStatus.Completed, recovered.Status);
        Assert.Equal(2, recovered.AttemptNumber);
        Assert.Null(recovered.LeaseOwner);
        Assert.Null(recovered.LeaseExpiresAt);
    }

    private static async Task<TenantRegistrationResult> RegisterTenantAsync(HttpClient client)
    {
        var suffix = Guid.NewGuid().ToString("N")[..10];
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/admin/tenants")
        {
            Content = JsonContent.Create(new RegisterTenantRequest
            {
                Name = $"PostgreSQL Tenant {suffix}",
                Slug = $"postgres-{suffix}",
                TaxpayerReference = $"PG-{suffix}",
                GraSecurityKeySecretName = $"pg-secret-{suffix}"
            })
        };
        request.Headers.Add("X-Admin-Key", "postgres-test-admin-key");
        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TenantRegistrationResult>())!;
    }

    private static HttpRequestMessage CreateInvoiceRequestMessage(
        string apiKey,
        string idempotencyKey,
        InvoiceRequest invoice)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/invoices")
        {
            Content = JsonContent.Create(invoice)
        };
        request.Headers.Add("X-API-Key", apiKey);
        request.Headers.Add("Idempotency-Key", idempotencyKey);
        return request;
    }

    private static InvoiceRequest CreateInvoiceRequest(string invoiceNumber) => new()
    {
        InvoiceNumber = invoiceNumber,
        Currency = "GHS",
        ExchangeRate = 1m,
        CalculationType = "INCLUSIVE",
        TransactionDate = new DateOnly(2026, 11, 9),
        TotalVat = 18m,
        TotalLevy = 0m,
        TotalAmount = 118m,
        BusinessPartnerName = "PostgreSQL Test Customer",
        BusinessPartnerTin = "C0000000001",
        SaleType = "NORMAL",
        Items =
        [
            new InvoiceItemRequest
            {
                ItemCode = "PG-ITEM-001",
                ItemCategory = string.Empty,
                Description = "PostgreSQL concurrency test item",
                Quantity = 1m,
                UnitPrice = 100m,
                LevyAmountA = 0m,
                LevyAmountB = 0m,
                LevyAmountC = 0m,
                LevyAmountD = 0m,
                LevyAmountE = 0m
            }
        ]
    };
}
