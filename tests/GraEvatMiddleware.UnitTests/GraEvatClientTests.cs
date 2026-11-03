using System.Net;
using GraEvatMiddleware.Application.Abstractions;
using GraEvatMiddleware.Application.Invoices;
using GraEvatMiddleware.Application.Logging;
using GraEvatMiddleware.GraClient.Client;
using GraEvatMiddleware.GraClient.Configuration;
using GraEvatMiddleware.Infrastructure.Logging;
using Microsoft.Extensions.Options;

namespace GraEvatMiddleware.UnitTests;

public sealed class GraEvatClientTests
{
    [Fact]
    public async Task SuccessfulSubmission_IsNotChangedWhenExchangeLoggingFails()
    {
        using var httpClient = new HttpClient(new StaticResponseHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"response\":{\"status\":\"SUCCESS\",\"message\":{\"ysdcrecnum\":\"GRA-1\"}}}",
                System.Text.Encoding.UTF8, "application/json")
        }));
        var client = new GraEvatClient(
            httpClient,
            new FixedSecretProvider(),
            new SensitiveDataSanitizer(),
            new ThrowingExchangeWriter(),
            Options.Create(new GraApiOptions { Enabled = true, BaseUrl = "https://gra.example/" }),
            Options.Create(new ExchangeLoggingOptions()));

        var result = await client.SubmitInvoiceAsync(CreateSubmission());

        Assert.True(result.IsSuccessful);
        Assert.Equal("GRA-1", result.ExternalReference);
    }

    private static GraInvoiceSubmission CreateSubmission() => new(
        Guid.NewGuid(), Guid.NewGuid(), "correlation", "idempotency-key", "Invoice", "TIN-001",
        "gra-secret-name",
        new InvoiceCommand(
            "INV-001", "GHS", 1, "INCLUSIVE", new DateOnly(2026, 11, 3), 18, 0, 118,
            "Customer", "C0000000001", "NORMAL", null, 0, null,
            [new InvoiceItemCommand("ITEM-1", "GENERAL", "Service", 1, 118, 0, 0, 0, 0, 0, 0, 0, null, null)]));

    private sealed class FixedSecretProvider : IGraSecretProvider
    {
        public Task<string?> GetSecretAsync(string secretName, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>("security-key");
    }

    private sealed class ThrowingExchangeWriter : IIntegrationExchangeWriter
    {
        public Task WriteAsync(IntegrationExchangeRecord record, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Controlled logging failure.");
    }

    private sealed class StaticResponseHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => Task.FromResult(response);
    }
}
