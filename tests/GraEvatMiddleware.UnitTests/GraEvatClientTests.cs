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
    public async Task StagingSubmission_UsesApprovedHostAndVersion82InvoicePath()
    {
        var handler = new RecordingResponseHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"response\":{\"status\":\"SUCCESS\",\"message\":{\"ysdcrecnum\":\"1001-6239-NS8\"}}}",
                System.Text.Encoding.UTF8, "application/json")
        });
        using var httpClient = new HttpClient(handler);
        var client = CreateClient(httpClient, new GraApiOptions
        {
            Enabled = true,
            Environment = "Staging",
            BaseUrl = "https://vsdcstaging.vat-gh.com/",
            AllowedHosts = ["vsdcstaging.vat-gh.com"]
        });

        var result = await client.SubmitInvoiceAsync(CreateSubmission());

        Assert.True(result.IsSuccessful);
        Assert.Equal("https://vsdcstaging.vat-gh.com/vsdc/api/v1/taxpayer/TIN-001/invoice",
            handler.RequestUri?.AbsoluteUri);
        Assert.True(handler.HadSecurityKeyHeader);
        Assert.Contains("\"totalExciseAmount\":0", handler.RequestBody, StringComparison.Ordinal);
        Assert.Contains("\"flag\":\"INVOICE\"", handler.RequestBody, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("REFUND")]
    [InlineData("PARTIAL_REFUND")]
    [InlineData("PURCHASE")]
    [InlineData("PURCHASE_RETURN")]
    public async Task FiscalDocument_UsesDocumentedInvoicePathFlagAndReference(string flag)
    {
        var handler = new RecordingResponseHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"response\":{\"status\":\"SUCCESS\"}}",
                System.Text.Encoding.UTF8, "application/json")
        });
        using var httpClient = new HttpClient(handler);
        var client = CreateStagingClient(httpClient);

        var result = await client.SubmitInvoiceAsync(CreateSubmission(flag, "ORIGINAL-001"));

        Assert.True(result.IsSuccessful);
        Assert.EndsWith("/vsdc/api/v1/taxpayer/TIN-001/invoice", handler.RequestUri?.AbsoluteUri,
            StringComparison.Ordinal);
        Assert.Contains($"\"flag\":\"{flag}\"", handler.RequestBody, StringComparison.Ordinal);
        Assert.Contains("\"reference\":\"ORIGINAL-001\"", handler.RequestBody, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("REFUND_CANCELATION")]
    [InlineData("PURCHASE_RETURN_CANCELATION")]
    public async Task Cancellation_UsesDocumentedPathAndExactFlag(string flag)
    {
        var handler = new RecordingResponseHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(string.Empty)
        });
        using var httpClient = new HttpClient(handler);
        var client = CreateStagingClient(httpClient);
        var submission = new GraCancellationSubmission(
            Guid.NewGuid(), Guid.NewGuid(), "correlation", "idempotency-key", "Cancellation",
            "TIN-001", "gra-secret-name",
            new CancellationCommand("CAN-001", "ORIGINAL-001",
                new DateTimeOffset(2026, 11, 9, 12, 0, 0, TimeSpan.Zero), 118m),
            flag);

        var result = await client.SubmitCancellationAsync(submission);

        Assert.True(result.IsSuccessful);
        Assert.Equal("ORIGINAL-001", result.ExternalReference);
        Assert.EndsWith("/vsdc/api/v1/taxpayer/TIN-001/cancellation", handler.RequestUri?.AbsoluteUri,
            StringComparison.Ordinal);
        Assert.Contains($"\"flag\":\"{flag}\"", handler.RequestBody, StringComparison.Ordinal);
        Assert.Contains("\"invoiceNumber\":\"CAN-001\"", handler.RequestBody, StringComparison.Ordinal);
        Assert.Contains("\"reference\":\"ORIGINAL-001\"", handler.RequestBody, StringComparison.Ordinal);
        Assert.Contains("\"totalAmount\":118", handler.RequestBody, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Production", "https://vsdcstaging.vat-gh.com/", "GRA_ENVIRONMENT_NOT_ALLOWED")]
    [InlineData("Staging", "http://vsdcstaging.vat-gh.com/", "GRA_HTTPS_REQUIRED")]
    [InlineData("Staging", "https://example.com/", "GRA_HOST_NOT_ALLOWED")]
    public async Task UnsafeConfiguration_IsRejectedBeforeSending(
        string environment,
        string baseUrl,
        string expectedError)
    {
        var handler = new RecordingResponseHandler(new HttpResponseMessage(HttpStatusCode.OK));
        using var httpClient = new HttpClient(handler);
        var client = CreateClient(httpClient, new GraApiOptions
        {
            Enabled = true,
            Environment = environment,
            BaseUrl = baseUrl,
            AllowedHosts = ["vsdcstaging.vat-gh.com"]
        });

        var result = await client.SubmitInvoiceAsync(CreateSubmission());

        Assert.True(result.IsPending);
        Assert.Equal(expectedError, result.ErrorCode);
        Assert.Null(handler.RequestUri);
    }

    [Fact]
    public async Task StagingEnvironment_RejectsAnAllowedButNonStagingRealHost()
    {
        var handler = new RecordingResponseHandler(new HttpResponseMessage(HttpStatusCode.OK));
        using var httpClient = new HttpClient(handler);
        var client = CreateClient(httpClient, new GraApiOptions
        {
            Enabled = true,
            Environment = "Staging",
            BaseUrl = "https://vsdc.vat-gh.com/",
            AllowedHosts = ["vsdc.vat-gh.com"]
        });

        var result = await client.SubmitInvoiceAsync(CreateSubmission());

        Assert.Equal("GRA_STAGING_HOST_REQUIRED", result.ErrorCode);
        Assert.Null(handler.RequestUri);
    }

    [Fact]
    public async Task Reconciliation_UsesDocumentedCallbackAndParsesSuccessfulInvoice()
    {
        var handler = new RecordingResponseHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"response\":{\"status\":\"SUCCESS\",\"message\":{\"num\":\"INV-001\",\"ysdcrecnum\":\"1001-5FC8-NS1\"}}}",
                System.Text.Encoding.UTF8, "application/json")
        });
        using var httpClient = new HttpClient(handler);
        var client = CreateClient(httpClient, new GraApiOptions
        {
            Enabled = true,
            Environment = "Staging",
            BaseUrl = "https://vsdcstaging.vat-gh.com/",
            AllowedHosts = ["vsdcstaging.vat-gh.com"]
        });

        var result = await client.ReconcileInvoiceAsync(new GraInvoiceReconciliation(
            Guid.NewGuid(), Guid.NewGuid(), "correlation", "idempotency-key", "InvoiceReconciliation",
            "TIN-001", "gra-secret-name", "INV-001"));

        Assert.True(result.IsFound);
        Assert.Equal("1001-5FC8-NS1", result.ExternalReference);
        Assert.Equal("https://vsdcstaging.vat-gh.com/vsdc/api/v1/taxpayer/TIN-001/invoice/callback",
            handler.RequestUri?.AbsoluteUri);
        Assert.Contains("\"invoiceNumber\":\"INV-001\"", handler.RequestBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reconciliation_NormalizesDocumentedNumericNotFoundCode()
    {
        var handler = new RecordingResponseHandler(new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("{\"status\":701,\"error\":\"invoice not found\"}",
                System.Text.Encoding.UTF8, "application/json")
        });
        using var httpClient = new HttpClient(handler);
        var client = CreateClient(httpClient, new GraApiOptions
        {
            Enabled = true,
            Environment = "Staging",
            BaseUrl = "https://vsdcstaging.vat-gh.com/",
            AllowedHosts = ["vsdcstaging.vat-gh.com"]
        });

        var result = await client.ReconcileInvoiceAsync(new GraInvoiceReconciliation(
            Guid.NewGuid(), Guid.NewGuid(), "correlation", "idempotency-key", "InvoiceReconciliation",
            "TIN-001", "gra-secret-name", "INV-001"));

        Assert.True(result.IsNotFound);
        Assert.Equal("E701", result.ErrorCode);
        Assert.Equal(GraFailureKind.NotFound, result.FailureKind);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, "{\"status\":900}", "E900", GraFailureKind.Authentication, false, true)]
    [InlineData(HttpStatusCode.Forbidden, "{}", "GRA_AUTHENTICATION_FAILED", GraFailureKind.Authentication, false, true)]
    [InlineData(HttpStatusCode.Conflict, "{\"code\":\"E700\"}", "E700", GraFailureKind.AlreadyProcessed, true, false)]
    [InlineData(HttpStatusCode.TooManyRequests, "{\"code\":\"E990\"}", "E990", GraFailureKind.RateLimited, true, false)]
    [InlineData(HttpStatusCode.ServiceUnavailable, "{}", "GRA_SERVICE_UNAVAILABLE", GraFailureKind.Temporary, true, false)]
    public async Task Submission_ClassifiesDocumentedAndTransportErrors(
        HttpStatusCode statusCode,
        string responseBody,
        string expectedCode,
        GraFailureKind expectedKind,
        bool expectedPending,
        bool expectedRejected)
    {
        using var httpClient = new HttpClient(new StaticResponseHandler(new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(responseBody, System.Text.Encoding.UTF8, "application/json")
        }));
        var client = CreateClient(httpClient, new GraApiOptions
        {
            Enabled = true,
            Environment = "Staging",
            BaseUrl = "https://gra.example/",
            AllowedHosts = ["gra.example"]
        });

        var result = await client.SubmitInvoiceAsync(CreateSubmission());

        Assert.Equal(expectedCode, result.ErrorCode);
        Assert.Equal(expectedKind, result.FailureKind);
        Assert.Equal(expectedPending, result.IsPending);
        Assert.Equal(expectedRejected, result.IsRejected);
    }

    [Fact]
    public async Task SuccessfulHttpResponse_WithMalformedBody_IsClassifiedAsInvalidResponse()
    {
        using var httpClient = new HttpClient(new StaticResponseHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("not-json", System.Text.Encoding.UTF8, "application/json")
        }));
        var client = CreateClient(httpClient, new GraApiOptions
        {
            Enabled = true,
            Environment = "Staging",
            BaseUrl = "https://gra.example/",
            AllowedHosts = ["gra.example"]
        });

        var result = await client.SubmitInvoiceAsync(CreateSubmission());

        Assert.True(result.IsPending);
        Assert.Equal("GRA_RESPONSE_INVALID", result.ErrorCode);
        Assert.Equal(GraFailureKind.InvalidResponse, result.FailureKind);
    }

    [Fact]
    public async Task SuccessfulSubmission_IsNotChangedWhenExchangeLoggingFails()
    {
        using var httpClient = new HttpClient(new StaticResponseHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"response\":{\"status\":\"SUCCESS\",\"message\":{\"ysdcrecnum\":\"GRA-1\"}}}",
                System.Text.Encoding.UTF8, "application/json")
        }));
        var client = CreateClient(httpClient, new GraApiOptions
        {
            Enabled = true,
            Environment = "Staging",
            BaseUrl = "https://gra.example/",
            AllowedHosts = ["gra.example"]
        }, new ThrowingExchangeWriter());

        var result = await client.SubmitInvoiceAsync(CreateSubmission());

        Assert.True(result.IsSuccessful);
        Assert.Equal("GRA-1", result.ExternalReference);
    }

    private static GraEvatClient CreateClient(
        HttpClient httpClient,
        GraApiOptions options,
        IIntegrationExchangeWriter? writer = null) => new(
        httpClient,
        new FixedSecretProvider(),
        new SensitiveDataSanitizer(),
        writer ?? new NullExchangeWriter(),
        Options.Create(options),
        Options.Create(new ExchangeLoggingOptions()));

    private static GraEvatClient CreateStagingClient(HttpClient httpClient) => CreateClient(
        httpClient,
        new GraApiOptions
        {
            Enabled = true,
            Environment = "Staging",
            BaseUrl = "https://vsdcstaging.vat-gh.com/",
            AllowedHosts = ["vsdcstaging.vat-gh.com"]
        });

    private static GraInvoiceSubmission CreateSubmission(
        string flag = "INVOICE",
        string? reference = null) => new(
        Guid.NewGuid(), Guid.NewGuid(), "correlation", "idempotency-key", "Invoice", "TIN-001",
        "gra-secret-name",
        new InvoiceCommand(
            "INV-001", "GHS", 1, "INCLUSIVE", new DateOnly(2026, 11, 3), 18, 0, 118,
            "Customer", "C0000000001", "NORMAL", null, 0, null,
            [new InvoiceItemCommand("ITEM-1", string.Empty, "Service", 1, 118, 0, 0, 0, 0, 0, 0, 0, null, null)],
            reference),
        flag);

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

    private sealed class NullExchangeWriter : IIntegrationExchangeWriter
    {
        public Task WriteAsync(IntegrationExchangeRecord record, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class StaticResponseHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => Task.FromResult(response);
    }

    private sealed class RecordingResponseHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }
        public bool HadSecurityKeyHeader { get; private set; }
        public string RequestBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            HadSecurityKeyHeader = request.Headers.Contains("security_key");
            RequestBody = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return response;
        }
    }
}
