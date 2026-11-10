using System.Net;
using System.Net.Http.Json;
using GraEvatMiddleware.Api.Contracts;
using GraEvatMiddleware.Application.Operations;
using GraEvatMiddleware.Application.Tenants;
using GraEvatMiddleware.Domain.Transactions;
using GraEvatMiddleware.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GraEvatMiddleware.IntegrationTests;

public sealed class RemainingGraOperationWorkflowTests(GraEvatApiFactory factory)
    : IClassFixture<GraEvatApiFactory>
{
    [Theory]
    [InlineData("/api/v1/credit-debit-notes", TransactionOperation.CreditNote)]
    [InlineData("/api/v1/statements-of-account", TransactionOperation.StatementOfAccount)]
    [InlineData("/api/v1/inventory", TransactionOperation.Inventory)]
    public async Task StateChangingOperation_PersistsTransactionAndSupportsReplay(
        string endpoint,
        TransactionOperation expectedOperation)
    {
        using var client = factory.CreateClient();
        var tenant = await RegisterTenantAsync(client);
        var idempotencyKey = $"remaining-{Guid.NewGuid():N}";
        var body = CreateBody(endpoint);

        using var first = CreateAuthenticatedPost(endpoint, tenant.Credential.ApiKey, idempotencyKey, body);
        using var firstResponse = await client.SendAsync(first);
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        var created = await firstResponse.Content.ReadFromJsonAsync<OperationTransactionResult>();
        Assert.NotNull(created);
        Assert.Equal("Succeeded", created.Status);

        using var replay = CreateAuthenticatedPost(endpoint, tenant.Credential.ApiKey, idempotencyKey, body);
        using var replayResponse = await client.SendAsync(replay);
        Assert.Equal(HttpStatusCode.OK, replayResponse.StatusCode);
        var replayed = await replayResponse.Content.ReadFromJsonAsync<OperationTransactionResult>();
        Assert.Equal(created.TransactionId, replayed?.TransactionId);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var saved = await dbContext.Transactions.SingleAsync(item => item.Id == created.TransactionId);
        Assert.Equal(expectedOperation, saved.Operation);
    }

    [Theory]
    [InlineData("/api/v1/credit-debit-notes")]
    [InlineData("/api/v1/statements-of-account")]
    [InlineData("/api/v1/inventory")]
    [InlineData("/api/v1/identifications/tin/C0034186913")]
    [InlineData("/api/v1/identifications/ghana-card/GHA-000XXXXXX-2")]
    public async Task RemainingOperation_RejectsMissingTenantKey(string endpoint)
    {
        using var client = factory.CreateClient();
        using var response = endpoint.Contains("identifications", StringComparison.Ordinal)
            ? await client.GetAsync(endpoint)
            : await client.PostAsJsonAsync(endpoint, CreateBody(endpoint));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DebitNote_PersistsDebitOperation()
    {
        using var client = factory.CreateClient();
        var tenant = await RegisterTenantAsync(client);
        var note = CreateNote("DEBIT", "DEBIT-001");
        using var request = CreateAuthenticatedPost(
            "/api/v1/credit-debit-notes", tenant.Credential.ApiKey, $"debit-{Guid.NewGuid():N}", note);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<OperationTransactionResult>();
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var saved = await dbContext.Transactions.SingleAsync(item => item.Id == created!.TransactionId);
        Assert.Equal(TransactionOperation.DebitNote, saved.Operation);
    }

    [Fact]
    public async Task ChangedOperationReplay_ReturnsConflict()
    {
        using var client = factory.CreateClient();
        var tenant = await RegisterTenantAsync(client);
        var key = $"note-conflict-{Guid.NewGuid():N}";
        using var first = CreateAuthenticatedPost(
            "/api/v1/credit-debit-notes", tenant.Credential.ApiKey, key, CreateNote("CREDIT", "NOTE-001"));
        using var firstResponse = await client.SendAsync(first);
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);

        using var changed = CreateAuthenticatedPost(
            "/api/v1/credit-debit-notes", tenant.Credential.ApiKey, key, CreateNote("CREDIT", "NOTE-002"));
        using var changedResponse = await client.SendAsync(changed);

        Assert.Equal(HttpStatusCode.Conflict, changedResponse.StatusCode);
    }

    [Theory]
    [InlineData("tin", "C0034186913")]
    [InlineData("ghana-card", "GHA-000XXXXXX-2")]
    public async Task Identification_ReturnsNormalizedResultWithoutIdempotency(
        string route,
        string value)
    {
        using var client = factory.CreateClient();
        var tenant = await RegisterTenantAsync(client);
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/identifications/{route}/{value}");
        request.Headers.Add("X-API-Key", tenant.Credential.ApiKey);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<IdentificationResult>();
        Assert.NotNull(result);
        Assert.True(result.IsValid);
        Assert.Equal("C0034186913", result.Tin);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var loggedUrls = await dbContext.IntegrationExchanges.AsNoTracking()
            .Where(item => item.Url.Contains("/identifications/"))
            .Select(item => item.Url)
            .ToListAsync();
        Assert.NotEmpty(loggedUrls);
        Assert.All(loggedUrls, url => Assert.Contains("[REDACTED]", url, StringComparison.Ordinal));
        Assert.DoesNotContain(loggedUrls, url => url.Contains(value, StringComparison.OrdinalIgnoreCase));
    }

    private static object CreateBody(string endpoint) => endpoint switch
    {
        "/api/v1/credit-debit-notes" => CreateNote("CREDIT", "NOTE-001"),
        "/api/v1/statements-of-account" => new StatementOfAccountRequest
        {
            Currency = "GHS",
            ExchangeRate = 1m,
            TotalVat = 18m,
            TotalAmount = 118m,
            TotalLevies = 0m,
            BusinessPartnerName = "Example Partner",
            BusinessPartnerTin = "C0000000000",
            GroupReferenceId = "STATEMENT-001",
            TransactionDate = new DateTimeOffset(2026, 11, 10, 12, 0, 0, TimeSpan.Zero),
            CalculationType = "INCLUSIVE",
            Lines =
            [
                new StatementLineRequest
                {
                    Currency = "GHS",
                    ExchangeRate = 1m,
                    CalculationType = "INCLUSIVE",
                    InvoiceNumber = "INV-001",
                    Flag = "INVOICE",
                    InvoiceVat = 18m,
                    InvoiceAmount = 118m,
                    InvoiceLevies = 0m,
                    TransactionDate = new DateTimeOffset(2026, 11, 9, 12, 0, 0, TimeSpan.Zero)
                }
            ]
        },
        "/api/v1/inventory" => new InventorySubmissionRequest
        {
            InventoryDate = new DateOnly(2026, 11, 10),
            Inventories =
            [
                new InventoryItemRequest
                {
                    Code = "ITEM-001",
                    Description = "Example item",
                    Quantity = 3m,
                    BatchCode = "BATCH-001",
                    UnitPrice = 100m,
                    ExpiryDate = new DateOnly(2027, 11, 10)
                }
            ]
        },
        _ => throw new ArgumentOutOfRangeException(nameof(endpoint))
    };

    private static CreditDebitNoteRequest CreateNote(string noteType, string noteNumber) => new()
    {
        NoteNumber = noteNumber,
        NoteType = noteType,
        Currency = "GHS",
        ExchangeRate = 1m,
        NoteAmount = 1200m,
        CalculationType = "EXCLUSIVE",
        BusinessPartnerName = "Example Partner",
        BusinessPartnerTin = "C0000000000",
        GroupReferenceId = "GROUP-001",
        TransactionDate = new DateOnly(2026, 11, 10),
        Remarks = "Controlled test",
        NoteLineType = "INVOICE",
        NoteLines =
        [
            new CreditDebitNoteLineRequest
            {
                InvoiceNumber = "INV-001",
                InvoiceDate = new DateOnly(2026, 11, 9),
                InvoiceAmount = 1200m
            }
        ]
    };

    private static HttpRequestMessage CreateAuthenticatedPost(
        string endpoint,
        string apiKey,
        string idempotencyKey,
        object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, endpoint) { Content = JsonContent.Create(body) };
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
