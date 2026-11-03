using System.Net;
using System.Net.Http.Json;
using GraEvatMiddleware.Api.Contracts;
using GraEvatMiddleware.Application.Invoices;
using GraEvatMiddleware.Application.Logging;
using GraEvatMiddleware.Application.Tenants;
using GraEvatMiddleware.Domain.Integration;
using GraEvatMiddleware.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GraEvatMiddleware.IntegrationTests;

public sealed class InvoiceWorkflowTests(GraEvatApiFactory factory) : IClassFixture<GraEvatApiFactory>
{
    [Fact]
    public async Task SubmitInvoice_ReplaysSameRequest_AndRejectsChangedRequest()
    {
        using var client = factory.CreateClient();
        var tenant = await RegisterTenantAsync(client);
        var request = CreateInvoiceRequest("INV-1001", 118m);

        using var firstRequest = CreateAuthenticatedInvoiceRequest(tenant.Credential.ApiKey, "invoice-key-1001", request);
        using var firstResponse = await client.SendAsync(firstRequest);
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        var firstTransaction = await firstResponse.Content.ReadFromJsonAsync<TransactionResult>();
        Assert.NotNull(firstTransaction);
        Assert.Equal("Succeeded", firstTransaction.Status);
        Assert.Equal("GRA-INV-1001", firstTransaction.ExternalReference);

        using var replayRequest = CreateAuthenticatedInvoiceRequest(tenant.Credential.ApiKey, "invoice-key-1001", request);
        using var replayResponse = await client.SendAsync(replayRequest);
        Assert.Equal(HttpStatusCode.OK, replayResponse.StatusCode);
        var replayedTransaction = await replayResponse.Content.ReadFromJsonAsync<TransactionResult>();
        Assert.Equal(firstTransaction.TransactionId, replayedTransaction?.TransactionId);

        var changedRequest = CreateInvoiceRequest("INV-1001", 119m);
        using var conflictRequest = CreateAuthenticatedInvoiceRequest(tenant.Credential.ApiKey, "invoice-key-1001", changedRequest);
        using var conflictResponse = await client.SendAsync(conflictRequest);
        Assert.Equal(HttpStatusCode.Conflict, conflictResponse.StatusCode);
    }

    [Fact]
    public async Task TransactionLookup_IsIsolatedByTenant()
    {
        using var client = factory.CreateClient();
        var firstTenant = await RegisterTenantAsync(client);
        var secondTenant = await RegisterTenantAsync(client);

        using var submitRequest = CreateAuthenticatedInvoiceRequest(
            firstTenant.Credential.ApiKey,
            "tenant-isolation-key",
            CreateInvoiceRequest("INV-2001", 236m));
        using var submitResponse = await client.SendAsync(submitRequest);
        var transaction = await submitResponse.Content.ReadFromJsonAsync<TransactionResult>();
        Assert.NotNull(transaction);

        using var lookupRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/v1/transactions/{transaction.TransactionId}");
        lookupRequest.Headers.Add("X-API-Key", secondTenant.Credential.ApiKey);
        using var lookupResponse = await client.SendAsync(lookupRequest);

        Assert.Equal(HttpStatusCode.NotFound, lookupResponse.StatusCode);
    }

    [Fact]
    public async Task SuspendedTenant_CannotUseIssuedCredential()
    {
        using var client = factory.CreateClient();
        var tenant = await RegisterTenantAsync(client);

        using var suspendRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/v1/admin/tenants/{tenant.TenantId}/suspend");
        suspendRequest.Headers.Add("X-Admin-Key", "integration-test-admin-key");
        using var suspendResponse = await client.SendAsync(suspendRequest);
        Assert.Equal(HttpStatusCode.NoContent, suspendResponse.StatusCode);

        using var lookupRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/v1/transactions/{Guid.NewGuid()}");
        lookupRequest.Headers.Add("X-API-Key", tenant.Credential.ApiKey);
        using var lookupResponse = await client.SendAsync(lookupRequest);

        Assert.Equal(HttpStatusCode.Unauthorized, lookupResponse.StatusCode);
    }

    [Fact]
    public async Task ExchangeLogs_AreTenantScopedCorrelatedAndSecretFree()
    {
        using var client = factory.CreateClient();
        var tenant = await RegisterTenantAsync(client);
        const string correlationId = "integration-correlation-001";
        using var submitRequest = CreateAuthenticatedInvoiceRequest(
            tenant.Credential.ApiKey, "logged-invoice-key", CreateInvoiceRequest("INV-LOG-001", 118m));
        submitRequest.Headers.Add("X-Correlation-ID", correlationId);
        using var submitResponse = await client.SendAsync(submitRequest);
        Assert.Equal(HttpStatusCode.Created, submitResponse.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logs = await dbContext.IntegrationExchanges
            .Where(item => item.CorrelationId == correlationId)
            .OrderBy(item => item.Direction)
            .ToListAsync();

        Assert.Equal(2, logs.Count);
        Assert.Contains(logs, item => item.Direction == ExchangeDirection.Inbound &&
                                     item.TenantId == tenant.TenantId && item.TransactionId != null);
        Assert.Contains(logs, item => item.Direction == ExchangeDirection.Outbound &&
                                     item.TenantId == tenant.TenantId && item.TransactionId != null);
        Assert.All(logs, item => Assert.DoesNotContain(tenant.Credential.ApiKey,
            $"{item.RequestHeadersJson}{item.RequestBody}{item.ResponseHeadersJson}{item.ResponseBody}",
            StringComparison.Ordinal));
        Assert.Contains(logs, item => item.Direction == ExchangeDirection.Outbound &&
                                     item.RequestHeadersJson!.Contains("[REDACTED]", StringComparison.Ordinal));
    }

    [Fact]
    public async Task UnauthorizedRequest_IsLoggedWithoutApiKeyDisclosure()
    {
        using var client = factory.CreateClient();
        const string invalidKey = "evat.invalid.invalid-secret";
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/transactions/{Guid.NewGuid()}");
        request.Headers.Add("X-API-Key", invalidKey);
        request.Headers.Add("X-Correlation-ID", "unauthorized-correlation");
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var log = await dbContext.IntegrationExchanges.SingleAsync(
            item => item.CorrelationId == "unauthorized-correlation");
        Assert.Equal(401, log.ResponseStatusCode);
        Assert.Contains("[REDACTED]", log.RequestHeadersJson, StringComparison.Ordinal);
        Assert.DoesNotContain(invalidKey, log.RequestHeadersJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TenantRegistrationLog_RedactsIssuedApiKey()
    {
        using var client = factory.CreateClient();
        var tenant = await RegisterTenantAsync(client);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var log = await dbContext.IntegrationExchanges
            .Where(item => item.Url == "/api/v1/admin/tenants" && item.ResponseStatusCode == 201)
            .OrderByDescending(item => item.CreatedAt)
            .FirstAsync();
        Assert.Contains("[REDACTED]", log.ResponseBody, StringComparison.Ordinal);
        Assert.DoesNotContain(tenant.Credential.ApiKey, log.ResponseBody, StringComparison.Ordinal);
        Assert.DoesNotContain("integration-test-admin-key", log.RequestHeadersJson, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("REJECT-001", "Rejected", ExchangeOutcome.Rejected)]
    [InlineData("TIMEOUT-001", "PendingGra", ExchangeOutcome.Failed)]
    public async Task GraFailure_IsRecordedWithoutResubmission(
        string invoiceNumber,
        string expectedStatus,
        ExchangeOutcome expectedOutcome)
    {
        using var client = factory.CreateClient();
        var tenant = await RegisterTenantAsync(client);
        var correlationId = $"failure-{invoiceNumber}";
        using var request = CreateAuthenticatedInvoiceRequest(
            tenant.Credential.ApiKey, $"key-{invoiceNumber}", CreateInvoiceRequest(invoiceNumber, 118m));
        request.Headers.Add("X-Correlation-ID", correlationId);
        using var response = await client.SendAsync(request);
        var transaction = await response.Content.ReadFromJsonAsync<TransactionResult>();
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(expectedStatus, transaction?.Status);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var outboundLogs = await dbContext.IntegrationExchanges
            .Where(item => item.CorrelationId == correlationId && item.Direction == ExchangeDirection.Outbound)
            .ToListAsync();
        Assert.Single(outboundLogs);
        Assert.Equal(expectedOutcome, outboundLogs[0].Outcome);
    }

    [Fact]
    public async Task RetentionService_DeletesOnlyExpiredExchangeLogs()
    {
        var now = new DateTimeOffset(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        dbContext.IntegrationExchanges.Add(CreateExchange(now.AddDays(-91), "expired-retention-log"));
        dbContext.IntegrationExchanges.Add(CreateExchange(now.AddDays(-89), "current-retention-log"));
        await dbContext.SaveChangesAsync();

        var service = scope.ServiceProvider.GetRequiredService<IExchangeLogRetentionService>();
        var deleted = await service.DeleteExpiredAsync(now);

        Assert.Equal(1, deleted);
        Assert.False(await dbContext.IntegrationExchanges.AnyAsync(
            item => item.CorrelationId == "expired-retention-log"));
        Assert.True(await dbContext.IntegrationExchanges.AnyAsync(
            item => item.CorrelationId == "current-retention-log"));
    }

    private static IntegrationExchange CreateExchange(DateTimeOffset createdAt, string correlationId) =>
        IntegrationExchange.Capture(
            null, null, correlationId, null, ExchangeDirection.Inbound, "Test", "Middleware",
            "RetentionTest", "GET", "/test", "{}", null, false, 200, "{}", null,
            false, 1, 1, ExchangeOutcome.Succeeded, null, createdAt);

    private static async Task<TenantRegistrationResult> RegisterTenantAsync(HttpClient client)
    {
        var suffix = Guid.NewGuid().ToString("N")[..10];
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/admin/tenants")
        {
            Content = JsonContent.Create(new RegisterTenantRequest
            {
                Name = $"Tenant {suffix}",
                Slug = $"tenant-{suffix}",
                TaxpayerReference = $"TIN-{suffix}",
                GraSecurityKeySecretName = $"gra-secret-{suffix}"
            })
        };
        request.Headers.Add("X-Admin-Key", "integration-test-admin-key");
        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TenantRegistrationResult>())!;
    }

    private static HttpRequestMessage CreateAuthenticatedInvoiceRequest(
        string apiKey,
        string idempotencyKey,
        InvoiceRequest request)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, "/api/v1/invoices")
        {
            Content = JsonContent.Create(request)
        };
        message.Headers.Add("X-API-Key", apiKey);
        message.Headers.Add("Idempotency-Key", idempotencyKey);
        return message;
    }

    private static InvoiceRequest CreateInvoiceRequest(string invoiceNumber, decimal totalAmount)
    {
        return new InvoiceRequest
        {
            InvoiceNumber = invoiceNumber,
            Currency = "GHS",
            ExchangeRate = 1m,
            CalculationType = "INCLUSIVE",
            TransactionDate = new DateOnly(2026, 11, 1),
            TotalVat = 18m,
            TotalLevy = 0m,
            TotalAmount = totalAmount,
            BusinessPartnerName = "Example Customer",
            BusinessPartnerTin = "C0000000001",
            SaleType = "NORMAL",
            DiscountAmount = 0m,
            Items =
            [
                new InvoiceItemRequest
                {
                    ItemCode = "ITEM-001",
                    ItemCategory = "GENERAL",
                    Description = "Consulting service",
                    Quantity = 1m,
                    UnitPrice = totalAmount
                }
            ]
        };
    }
}
