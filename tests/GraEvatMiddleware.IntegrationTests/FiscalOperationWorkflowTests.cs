using System.Net;
using System.Net.Http.Json;
using GraEvatMiddleware.Api.Contracts;
using GraEvatMiddleware.Application.Invoices;
using GraEvatMiddleware.Application.Retry;
using GraEvatMiddleware.Application.Tenants;
using GraEvatMiddleware.Domain.Integration;
using GraEvatMiddleware.Domain.Retry;
using GraEvatMiddleware.Domain.Transactions;
using GraEvatMiddleware.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GraEvatMiddleware.IntegrationTests;

public sealed class FiscalOperationWorkflowTests(GraEvatApiFactory factory) : IClassFixture<GraEvatApiFactory>
{
    public static TheoryData<string, object, TransactionOperation> SuccessfulOperations => new()
    {
        { "/api/v1/refunds", CreateRefundRequest("REF-1001"), TransactionOperation.InvoiceRefund },
        { "/api/v1/purchases", CreateDocumentRequest("PUR-1001", null), TransactionOperation.Purchase },
        { "/api/v1/purchase-returns", CreateDocumentRequest("PRET-1001", "PUR-1001"), TransactionOperation.PurchaseReturn },
        { "/api/v1/refund-cancellations", CreateCancellationRequest("RCAN-1001", "REF-1001"), TransactionOperation.RefundCancellation },
        { "/api/v1/purchase-return-cancellations", CreateCancellationRequest("PRCAN-1001", "PRET-1001"), TransactionOperation.PurchaseReturnCancellation }
    };

    [Theory]
    [MemberData(nameof(SuccessfulOperations))]
    public async Task FiscalOperation_PersistsExpectedOperationAndAudit(
        string endpoint,
        object requestBody,
        TransactionOperation expectedOperation)
    {
        using var client = factory.CreateClient();
        var tenant = await RegisterTenantAsync(client);
        using var request = CreateAuthenticatedRequest(endpoint, tenant.Credential.ApiKey, $"key-{Guid.NewGuid():N}", requestBody);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<TransactionResult>();
        Assert.NotNull(result);
        Assert.Equal("Succeeded", result.Status);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var saved = await dbContext.Transactions.SingleAsync(item => item.Id == result.TransactionId);
        Assert.Equal(expectedOperation, saved.Operation);
        Assert.True(await dbContext.AuditEvents.AnyAsync(item =>
            item.TenantId == tenant.TenantId &&
            item.ResourceId == result.TransactionId.ToString() &&
            item.Action == "transaction.completed"));
    }

    [Fact]
    public async Task Refund_ReplaysSameRequest_AndRejectsChangedRequest()
    {
        using var client = factory.CreateClient();
        var tenant = await RegisterTenantAsync(client);
        var idempotencyKey = $"refund-{Guid.NewGuid():N}";
        var requestBody = CreateRefundRequest("REF-REPLAY-1");

        using var first = CreateAuthenticatedRequest("/api/v1/refunds", tenant.Credential.ApiKey, idempotencyKey, requestBody);
        using var firstResponse = await client.SendAsync(first);
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        var created = await firstResponse.Content.ReadFromJsonAsync<TransactionResult>();

        using var replay = CreateAuthenticatedRequest("/api/v1/refunds", tenant.Credential.ApiKey, idempotencyKey, requestBody);
        using var replayResponse = await client.SendAsync(replay);
        Assert.Equal(HttpStatusCode.OK, replayResponse.StatusCode);
        var replayed = await replayResponse.Content.ReadFromJsonAsync<TransactionResult>();
        Assert.Equal(created?.TransactionId, replayed?.TransactionId);

        var changed = CreateRefundRequest("REF-REPLAY-1", totalAmount: 59m);
        using var conflict = CreateAuthenticatedRequest("/api/v1/refunds", tenant.Credential.ApiKey, idempotencyKey, changed);
        using var conflictResponse = await client.SendAsync(conflict);
        Assert.Equal(HttpStatusCode.Conflict, conflictResponse.StatusCode);
    }

    [Fact]
    public async Task TemporaryRefund_IsReconciledBeforeRetryAndKeepsOperationContext()
    {
        using var client = factory.CreateClient();
        var tenant = await RegisterTenantAsync(client);
        using var request = CreateAuthenticatedRequest(
            "/api/v1/refunds",
            tenant.Credential.ApiKey,
            $"refund-retry-{Guid.NewGuid():N}",
            CreateRefundRequest("TEMPORARY-REFUND-001"));
        using var response = await client.SendAsync(request);
        var transaction = (await response.Content.ReadFromJsonAsync<TransactionResult>())!;
        Assert.Equal("PendingGra", transaction.Status);

        await using var scope = factory.Services.CreateAsyncScope();
        var processor = scope.ServiceProvider.GetRequiredService<IGraRetryProcessor>();
        var processed = await processor.ProcessDueAsync(
            "refund-integration-worker",
            DateTimeOffset.UtcNow.AddMinutes(1));

        Assert.Equal(1, processed);
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var saved = await dbContext.Transactions.SingleAsync(item => item.Id == transaction.TransactionId);
        var retry = await dbContext.GraRetryJobs.SingleAsync(item => item.TransactionId == transaction.TransactionId);
        Assert.Equal("Succeeded", saved.Status.ToString());
        Assert.Equal(RetryJobStatus.Completed, retry.Status);
        var operations = await dbContext.IntegrationExchanges
            .Where(item => item.TransactionId == transaction.TransactionId &&
                           item.Direction == ExchangeDirection.Outbound)
            .OrderBy(item => item.CreatedAt)
            .Select(item => item.Operation)
            .ToListAsync();
        Assert.Equal(["InvoiceRefund", "InvoiceRefundReconciliation", "InvoiceRefundRetry"], operations);
    }

    [Theory]
    [InlineData("/api/v1/refunds")]
    [InlineData("/api/v1/purchases")]
    [InlineData("/api/v1/purchase-returns")]
    [InlineData("/api/v1/refund-cancellations")]
    [InlineData("/api/v1/purchase-return-cancellations")]
    public async Task FiscalOperation_RejectsMissingTenantKey(string endpoint)
    {
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync(endpoint, CreateDocumentRequest("AUTH-1", "REF-1"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/v1/purchase-returns")]
    [InlineData("/api/v1/refund-cancellations")]
    [InlineData("/api/v1/purchase-return-cancellations")]
    public async Task ReferenceDependentOperation_RejectsMissingReference(string endpoint)
    {
        using var client = factory.CreateClient();
        var tenant = await RegisterTenantAsync(client);
        object body = endpoint.Contains("cancellations", StringComparison.Ordinal)
            ? CreateCancellationRequest("INVALID-1", string.Empty)
            : CreateDocumentRequest("INVALID-1", string.Empty);
        using var request = CreateAuthenticatedRequest(endpoint, tenant.Credential.ApiKey, $"key-{Guid.NewGuid():N}", body);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    private static RefundRequest CreateRefundRequest(string number, decimal totalAmount = 118m) => new()
    {
        InvoiceNumber = number,
        Currency = "GHS",
        ExchangeRate = 1m,
        CalculationType = "INCLUSIVE",
        TransactionDate = new DateOnly(2026, 11, 9),
        TotalVat = 18m,
        TotalLevy = 0m,
        TotalAmount = totalAmount,
        BusinessPartnerName = "Example Partner",
        BusinessPartnerTin = "C0000000001",
        SaleType = "NORMAL",
        DiscountAmount = 0m,
        Reference = "INV-ORIGINAL-1",
        RefundType = "FULL",
        Items =
        [
            new InvoiceItemRequest
            {
                ItemCode = "ITEM-001",
                ItemCategory = string.Empty,
                Description = "Test item",
                Quantity = 1m,
                UnitPrice = totalAmount
            }
        ]
    };

    private static FiscalDocumentRequest CreateDocumentRequest(string number, string? reference) => new()
    {
        InvoiceNumber = number,
        Currency = "GHS",
        ExchangeRate = 1m,
        CalculationType = "INCLUSIVE",
        TransactionDate = new DateOnly(2026, 11, 9),
        TotalVat = 18m,
        TotalLevy = 0m,
        TotalAmount = 118m,
        BusinessPartnerName = "Example Partner",
        BusinessPartnerTin = "C0000000001",
        SaleType = "NORMAL",
        DiscountAmount = 0m,
        Reference = reference,
        Items =
        [
            new InvoiceItemRequest
            {
                ItemCode = "ITEM-001",
                ItemCategory = string.Empty,
                Description = "Test item",
                Quantity = 1m,
                UnitPrice = 118m
            }
        ]
    };

    private static CancellationRequest CreateCancellationRequest(string number, string reference) => new()
    {
        InvoiceNumber = number,
        Reference = reference,
        TransactionDate = new DateTimeOffset(2026, 11, 9, 12, 0, 0, TimeSpan.Zero),
        TotalAmount = 118m
    };

    private static HttpRequestMessage CreateAuthenticatedRequest(
        string endpoint,
        string apiKey,
        string idempotencyKey,
        object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Add("X-API-Key", apiKey);
        request.Headers.Add("Idempotency-Key", idempotencyKey);
        return request;
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
}
