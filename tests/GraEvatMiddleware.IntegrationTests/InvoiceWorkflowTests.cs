using System.Net;
using System.Net.Http.Json;
using GraEvatMiddleware.Api.Contracts;
using GraEvatMiddleware.Application.Invoices;
using GraEvatMiddleware.Application.Tenants;

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
