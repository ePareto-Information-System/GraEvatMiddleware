using System.Net;
using System.Net.Http.Json;
using System.Diagnostics;
using System.Text.Json;
using GraEvatMiddleware.Application.Abstractions;
using GraEvatMiddleware.Application.Invoices;
using GraEvatMiddleware.Application.Logging;
using GraEvatMiddleware.Domain.Integration;
using GraEvatMiddleware.GraClient.Configuration;
using Microsoft.Extensions.Options;

namespace GraEvatMiddleware.GraClient.Client;

public sealed class GraEvatClient(
    HttpClient httpClient,
    IGraSecretProvider secretProvider,
    IExchangeContentSanitizer sanitizer,
    IIntegrationExchangeWriter exchangeWriter,
    IOptions<GraApiOptions> options,
    IOptions<ExchangeLoggingOptions> exchangeOptions) : IGraEvatClient
{
    private const string DocumentedStagingHost = "vsdcstaging.vat-gh.com";
    private readonly GraApiOptions _options = options.Value;

    public async Task<GraSubmissionResult> SubmitInvoiceAsync(
        GraInvoiceSubmission submission,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return new GraSubmissionResult(
                IsSuccessful: false,
                IsPending: true,
                IsRejected: false,
                ExternalReference: null,
                ErrorCode: "GRA_INTEGRATION_DISABLED");
        }

        if (!string.Equals(_options.Environment, "Staging", StringComparison.OrdinalIgnoreCase))
        {
            return new GraSubmissionResult(false, true, false, null, "GRA_ENVIRONMENT_NOT_ALLOWED");
        }

        if (!Uri.TryCreate(_options.BaseUrl, UriKind.Absolute, out var baseUri))
        {
            return new GraSubmissionResult(false, true, false, null, "GRA_BASE_URL_INVALID");
        }

        if (!string.Equals(baseUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return new GraSubmissionResult(false, true, false, null, "GRA_HTTPS_REQUIRED");
        }

        if (!_options.AllowedHosts.Contains(baseUri.Host, StringComparer.OrdinalIgnoreCase))
        {
            return new GraSubmissionResult(false, true, false, null, "GRA_HOST_NOT_ALLOWED");
        }

        if (!string.Equals(baseUri.Host, DocumentedStagingHost, StringComparison.OrdinalIgnoreCase) &&
            !baseUri.Host.EndsWith(".test", StringComparison.OrdinalIgnoreCase) &&
            !baseUri.Host.EndsWith(".example", StringComparison.OrdinalIgnoreCase))
        {
            return new GraSubmissionResult(false, true, false, null, "GRA_STAGING_HOST_REQUIRED");
        }

        var securityKey = await secretProvider.GetSecretAsync(
            submission.SecurityKeySecretName,
            cancellationToken);
        if (string.IsNullOrWhiteSpace(securityKey))
        {
            return new GraSubmissionResult(false, true, false, null, "GRA_SECRET_NOT_CONFIGURED");
        }

        var relativeUrl = $"vsdc/api/v1/taxpayer/{Uri.EscapeDataString(submission.TaxpayerReference)}/invoice";
        var payload = ToGraPayload(submission.Invoice);
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri(new Uri(baseUri.AbsoluteUri.TrimEnd('/') + "/"), relativeUrl));
        request.Headers.Add("security_key", securityKey);
        request.Content = JsonContent.Create(payload);
        var requestBody = JsonSerializer.Serialize(payload);
        var stopwatch = Stopwatch.StartNew();

        HttpResponseMessage? response = null;
        string? responseBody = null;
        try
        {
            response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            await TryWriteExchangeAsync(submission, relativeUrl, requestBody, null, null,
                stopwatch.ElapsedMilliseconds, ExchangeOutcome.Unknown, "GRA_TIMEOUT_STATUS_UNKNOWN");
            throw;
        }
        catch (HttpRequestException)
        {
            stopwatch.Stop();
            await TryWriteExchangeAsync(submission, relativeUrl, requestBody, null, null,
                stopwatch.ElapsedMilliseconds, ExchangeOutcome.Failed, "GRA_COMMUNICATION_STATUS_UNKNOWN");
            throw;
        }

        using (response)
        {
            stopwatch.Stop();
            var result = MapResponse(response, responseBody);
            var outcome = result.IsSuccessful ? ExchangeOutcome.Succeeded :
                result.IsRejected ? ExchangeOutcome.Rejected : ExchangeOutcome.Unknown;
            await TryWriteExchangeAsync(submission, relativeUrl, requestBody, response,
                responseBody, stopwatch.ElapsedMilliseconds, outcome, result.ErrorCode);
            return result;
        }
    }

    public async Task<GraReconciliationResult> ReconcileInvoiceAsync(
        GraInvoiceReconciliation reconciliation,
        CancellationToken cancellationToken = default)
    {
        var configurationError = ValidateConfiguration(out var baseUri);
        if (configurationError is not null)
        {
            return new GraReconciliationResult(false, false, true, false, null, configurationError);
        }

        var securityKey = await secretProvider.GetSecretAsync(
            reconciliation.SecurityKeySecretName,
            cancellationToken);
        if (string.IsNullOrWhiteSpace(securityKey))
        {
            return new GraReconciliationResult(false, false, true, false, null, "GRA_SECRET_NOT_CONFIGURED");
        }

        var relativeUrl = $"vsdc/api/v1/taxpayer/{Uri.EscapeDataString(reconciliation.TaxpayerReference)}/invoice/callback";
        var payload = new { reference = string.Empty, invoiceNumber = reconciliation.InvoiceNumber, flag = "INVOICE" };
        var requestBody = JsonSerializer.Serialize(payload);
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri(new Uri(baseUri!.AbsoluteUri.TrimEnd('/') + "/"), relativeUrl));
        request.Headers.Add("security_key", securityKey);
        request.Content = JsonContent.Create(payload);
        var stopwatch = Stopwatch.StartNew();

        HttpResponseMessage? response = null;
        string? responseBody = null;
        try
        {
            response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            await TryWriteReconciliationExchangeAsync(reconciliation, relativeUrl, requestBody, null, null,
                stopwatch.ElapsedMilliseconds, ExchangeOutcome.Unknown, "GRA_RECONCILIATION_TIMEOUT");
            throw;
        }
        catch (HttpRequestException)
        {
            stopwatch.Stop();
            await TryWriteReconciliationExchangeAsync(reconciliation, relativeUrl, requestBody, null, null,
                stopwatch.ElapsedMilliseconds, ExchangeOutcome.Unknown, "GRA_RECONCILIATION_COMMUNICATION_FAILURE");
            throw;
        }

        using (response)
        {
            stopwatch.Stop();
            var result = MapReconciliationResponse(response, responseBody);
            var outcome = result.IsFound ? ExchangeOutcome.Succeeded :
                result.IsRejected ? ExchangeOutcome.Rejected : ExchangeOutcome.Unknown;
            await TryWriteReconciliationExchangeAsync(reconciliation, relativeUrl, requestBody, response,
                responseBody, stopwatch.ElapsedMilliseconds, outcome, result.ErrorCode);
            return result;
        }
    }

    public async Task<GraHealthResult> CheckHealthAsync(
        GraHealthCheck healthCheck,
        CancellationToken cancellationToken = default)
    {
        var configurationError = ValidateConfiguration(out var baseUri);
        if (configurationError is not null)
        {
            return new GraHealthResult(false, configurationError);
        }

        var securityKey = await secretProvider.GetSecretAsync(
            healthCheck.SecurityKeySecretName,
            cancellationToken);
        if (string.IsNullOrWhiteSpace(securityKey))
        {
            return new GraHealthResult(false, "GRA_SECRET_NOT_CONFIGURED");
        }

        var relativeUrl = $"vsdc/api/v1/taxpayer/{Uri.EscapeDataString(healthCheck.TaxpayerReference)}/health";
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            new Uri(new Uri(baseUri!.AbsoluteUri.TrimEnd('/') + "/"), relativeUrl));
        request.Headers.Add("security_key", securityKey);
        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return new GraHealthResult(false, GetErrorCode(responseBody) ?? "GRA_HEALTH_CHECK_FAILED");
        }

        try
        {
            using var document = JsonDocument.Parse(responseBody);
            var status = GetString(document.RootElement, "status");
            return string.Equals(status, "UP", StringComparison.OrdinalIgnoreCase)
                ? new GraHealthResult(true, null)
                : new GraHealthResult(false, "GRA_HEALTH_RESPONSE_INVALID");
        }
        catch (JsonException)
        {
            return new GraHealthResult(false, "GRA_HEALTH_RESPONSE_INVALID");
        }
    }

    private string? ValidateConfiguration(out Uri? baseUri)
    {
        baseUri = null;
        if (!_options.Enabled)
        {
            return "GRA_INTEGRATION_DISABLED";
        }

        if (!string.Equals(_options.Environment, "Staging", StringComparison.OrdinalIgnoreCase))
        {
            return "GRA_ENVIRONMENT_NOT_ALLOWED";
        }

        if (!Uri.TryCreate(_options.BaseUrl, UriKind.Absolute, out baseUri))
        {
            return "GRA_BASE_URL_INVALID";
        }

        if (!string.Equals(baseUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return "GRA_HTTPS_REQUIRED";
        }

        if (!_options.AllowedHosts.Contains(baseUri.Host, StringComparer.OrdinalIgnoreCase))
        {
            return "GRA_HOST_NOT_ALLOWED";
        }

        return string.Equals(baseUri.Host, DocumentedStagingHost, StringComparison.OrdinalIgnoreCase) ||
               baseUri.Host.EndsWith(".test", StringComparison.OrdinalIgnoreCase) ||
               baseUri.Host.EndsWith(".example", StringComparison.OrdinalIgnoreCase)
            ? null
            : "GRA_STAGING_HOST_REQUIRED";
    }

    private static GraSubmissionResult MapResponse(HttpResponseMessage response, string? responseBody)
    {
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            return new GraSubmissionResult(false, false, true, null, "GRA_AUTHENTICATION_FAILED");
        }

        var errorCode = GetErrorCode(responseBody);
        if ((int)response.StatusCode >= 500 || string.Equals(errorCode, "E990", StringComparison.OrdinalIgnoreCase))
        {
            return new GraSubmissionResult(false, true, false, null, errorCode ?? "GRA_SERVICE_UNAVAILABLE");
        }

        if (!response.IsSuccessStatusCode)
        {
            var requiresReconciliation = string.Equals(errorCode, "E700", StringComparison.OrdinalIgnoreCase);
            return new GraSubmissionResult(false, requiresReconciliation, !requiresReconciliation, null,
                errorCode ?? "GRA_REQUEST_REJECTED");
        }

        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return new GraSubmissionResult(false, true, false, null, "GRA_RESPONSE_INVALID");
        }

        try
        {
            using var document = JsonDocument.Parse(responseBody);
            return ParseResponse(document.RootElement);
        }
        catch (JsonException)
        {
            return new GraSubmissionResult(false, true, false, null, "GRA_RESPONSE_INVALID");
        }
    }

    private static GraReconciliationResult MapReconciliationResponse(
        HttpResponseMessage response,
        string? responseBody)
    {
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            return new GraReconciliationResult(false, false, false, true, null, "GRA_AUTHENTICATION_FAILED");
        }

        var errorCode = GetErrorCode(responseBody);
        if (string.Equals(errorCode, "E701", StringComparison.OrdinalIgnoreCase))
        {
            return new GraReconciliationResult(false, true, false, false, null, errorCode);
        }

        if (!response.IsSuccessStatusCode)
        {
            var pending = (int)response.StatusCode >= 500 ||
                string.Equals(errorCode, "E990", StringComparison.OrdinalIgnoreCase);
            return new GraReconciliationResult(false, false, pending, !pending, null,
                errorCode ?? "GRA_RECONCILIATION_FAILED");
        }

        var submission = string.IsNullOrWhiteSpace(responseBody)
            ? new GraSubmissionResult(false, true, false, null, "GRA_RESPONSE_INVALID")
            : ParseResponseBody(responseBody);
        return submission.IsSuccessful
            ? new GraReconciliationResult(true, false, false, false, submission.ExternalReference, null)
            : new GraReconciliationResult(false, false, true, false, null, submission.ErrorCode);
    }

    private async Task TryWriteExchangeAsync(
        GraInvoiceSubmission submission,
        string relativeUrl,
        string requestBody,
        HttpResponseMessage? response,
        string? responseBody,
        long durationMilliseconds,
        ExchangeOutcome outcome,
        string? errorCode)
    {
        if (!exchangeOptions.Value.Enabled)
        {
            return;
        }

        try
        {
            var captureBodies = exchangeOptions.Value.CaptureBodies;
            var maximumCharacters = exchangeOptions.Value.MaximumBodyCharacters;
            var requestContent = captureBodies
                ? sanitizer.SanitizeBody(requestBody, "application/json", maximumCharacters)
                : new SanitizedContent(null, false, false);
            var responseContent = captureBodies
                ? sanitizer.SanitizeBody(responseBody, response?.Content.Headers.ContentType?.ToString(), maximumCharacters)
                : new SanitizedContent(null, false, false);
            var requestHeaders = sanitizer.SanitizeHeaders(new Dictionary<string, string[]>
            {
                ["security_key"] = ["present"],
                ["Content-Type"] = ["application/json"]
            });
            var responseHeaders = response is null ? null : sanitizer.SanitizeHeaders(
                response.Headers.Concat(response.Content.Headers)
                    .ToDictionary(item => item.Key, item => item.Value.ToArray(), StringComparer.OrdinalIgnoreCase));

            await exchangeWriter.WriteAsync(new IntegrationExchangeRecord(
                submission.TenantId, submission.TransactionId, submission.CorrelationId,
                submission.IdempotencyKey, ExchangeDirection.Outbound, "Middleware", "GRA",
                submission.Operation, HttpMethod.Post.Method, relativeUrl, requestHeaders,
                requestContent.Content, requestContent.IsTruncated, (int?)response?.StatusCode,
                responseHeaders, responseContent.Content, responseContent.IsTruncated,
                durationMilliseconds, 1, outcome, errorCode, DateTimeOffset.UtcNow),
                CancellationToken.None);
        }
        catch
        {
            // Exchange logging must never change the result of a tax operation.
        }
    }

    private async Task TryWriteReconciliationExchangeAsync(
        GraInvoiceReconciliation reconciliation,
        string relativeUrl,
        string requestBody,
        HttpResponseMessage? response,
        string? responseBody,
        long durationMilliseconds,
        ExchangeOutcome outcome,
        string? errorCode)
    {
        if (!exchangeOptions.Value.Enabled)
        {
            return;
        }

        try
        {
            var captureBodies = exchangeOptions.Value.CaptureBodies;
            var maximumCharacters = exchangeOptions.Value.MaximumBodyCharacters;
            var requestContent = captureBodies
                ? sanitizer.SanitizeBody(requestBody, "application/json", maximumCharacters)
                : new SanitizedContent(null, false, false);
            var responseContent = captureBodies
                ? sanitizer.SanitizeBody(responseBody, response?.Content.Headers.ContentType?.ToString(), maximumCharacters)
                : new SanitizedContent(null, false, false);
            var requestHeaders = sanitizer.SanitizeHeaders(new Dictionary<string, string[]>
            {
                ["security_key"] = ["present"],
                ["Content-Type"] = ["application/json"]
            });
            var responseHeaders = response is null ? null : sanitizer.SanitizeHeaders(
                response.Headers.Concat(response.Content.Headers)
                    .ToDictionary(item => item.Key, item => item.Value.ToArray(), StringComparer.OrdinalIgnoreCase));

            await exchangeWriter.WriteAsync(new IntegrationExchangeRecord(
                reconciliation.TenantId, reconciliation.TransactionId, reconciliation.CorrelationId,
                reconciliation.IdempotencyKey, ExchangeDirection.Outbound, "Middleware", "GRA",
                reconciliation.Operation, HttpMethod.Post.Method, relativeUrl, requestHeaders,
                requestContent.Content, requestContent.IsTruncated, (int?)response?.StatusCode,
                responseHeaders, responseContent.Content, responseContent.IsTruncated,
                durationMilliseconds, 1, outcome, errorCode, DateTimeOffset.UtcNow),
                CancellationToken.None);
        }
        catch
        {
            // Exchange logging must never change the result of a tax operation.
        }
    }

    private static object ToGraPayload(InvoiceCommand invoice)
    {
        return new
        {
            currency = invoice.Currency,
            exchangeRate = invoice.ExchangeRate,
            invoiceNumber = invoice.InvoiceNumber,
            totalLevy = invoice.TotalLevy,
            userName = "middleware",
            flag = "INVOICE",
            calculationType = invoice.CalculationType,
            totalVat = invoice.TotalVat,
            transactionDate = invoice.TransactionDate.ToString("yyyy-MM-dd"),
            totalAmount = invoice.TotalAmount,
            totalExciseAmount = invoice.Items.Sum(item => item.ExciseAmount),
            voucherAmount = 0m,
            businessPartnerName = invoice.BusinessPartnerName,
            businessPartnerTin = invoice.BusinessPartnerTin,
            saleType = invoice.SaleType,
            discountType = invoice.DiscountType,
            taxType = "STANDARD",
            discountAmount = invoice.DiscountAmount,
            reference = string.Empty,
            groupReferenceId = invoice.GroupReferenceId ?? string.Empty,
            purchaseOrderReference = string.Empty,
            items = invoice.Items.Select(ToGraItem).ToArray()
        };
    }

    private static object ToGraItem(InvoiceItemCommand item)
    {
        return new
        {
            itemCode = item.ItemCode,
            itemCategory = item.ItemCategory,
            expireDate = item.ExpireDate?.ToString("yyyy-MM-dd") ?? string.Empty,
            description = item.Description,
            quantity = item.Quantity,
            unitPrice = item.UnitPrice,
            levyAmountA = item.LevyAmountA,
            levyAmountB = item.LevyAmountB,
            levyAmountC = item.LevyAmountC,
            levyAmountD = item.LevyAmountD,
            levyAmountE = item.LevyAmountE,
            discountAmount = item.DiscountAmount,
            exciseAmount = item.ExciseAmount,
            batchCode = item.BatchCode ?? string.Empty
        };
    }

    private static GraSubmissionResult ParseResponseBody(string responseBody)
    {
        try
        {
            using var document = JsonDocument.Parse(responseBody);
            return ParseResponse(document.RootElement);
        }
        catch (JsonException)
        {
            return new GraSubmissionResult(false, true, false, null, "GRA_RESPONSE_INVALID");
        }
    }

    private static GraSubmissionResult ParseResponse(JsonElement root)
    {
        if (!TryGetProperty(root, "response", out var response))
        {
            return new GraSubmissionResult(false, true, false, null, "GRA_RESPONSE_INVALID");
        }

        var status = GetString(response, "status");
        if (!string.Equals(status, "SUCCESS", StringComparison.OrdinalIgnoreCase))
        {
            return new GraSubmissionResult(false, false, true, null, "GRA_BUSINESS_REJECTION");
        }

        string? externalReference = null;
        if (TryGetProperty(response, "message", out var message) && message.ValueKind == JsonValueKind.Object)
        {
            externalReference = GetString(message, "ysdcrecnum")
                ?? GetString(message, "ysdcregsig")
                ?? GetString(message, "num");
        }

        return new GraSubmissionResult(true, false, false, externalReference, null);
    }

    private static string? GetString(JsonElement element, string name)
    {
        return TryGetProperty(element, name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static string? GetErrorCode(string? responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(responseBody);
            if (TryGetProperty(document.RootElement, "code", out var code) ||
                TryGetProperty(document.RootElement, "errorCode", out code))
            {
                return NormalizeErrorCode(code.ValueKind == JsonValueKind.String ? code.GetString() : code.GetRawText());
            }

            if (TryGetProperty(document.RootElement, "status", out var status))
            {
                return NormalizeErrorCode(status.ValueKind == JsonValueKind.String ? status.GetString() : status.GetRawText());
            }

            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? NormalizeErrorCode(string? value)
    {
        return value is { Length: 3 } && int.TryParse(value, out var numeric) && numeric >= 600
            ? $"E{value}"
            : value;
    }

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }
}
