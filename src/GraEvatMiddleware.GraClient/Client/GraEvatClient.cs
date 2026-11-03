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

        if (!Uri.TryCreate(_options.BaseUrl, UriKind.Absolute, out var baseUri))
        {
            return new GraSubmissionResult(false, true, false, null, "GRA_BASE_URL_INVALID");
        }

        var securityKey = await secretProvider.GetSecretAsync(
            submission.SecurityKeySecretName,
            cancellationToken);
        if (string.IsNullOrWhiteSpace(securityKey))
        {
            return new GraSubmissionResult(false, true, false, null, "GRA_SECRET_NOT_CONFIGURED");
        }

        httpClient.BaseAddress ??= new Uri(baseUri.AbsoluteUri.TrimEnd('/') + "/");
        var relativeUrl = $"vsdc/api/v1/taxpayer/{Uri.EscapeDataString(submission.TaxpayerReference)}/invoice";
        var payload = ToGraPayload(submission.Invoice);
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            relativeUrl);
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

    private static GraSubmissionResult MapResponse(HttpResponseMessage response, string? responseBody)
    {
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            return new GraSubmissionResult(false, false, true, null, "GRA_AUTHENTICATION_FAILED");
        }

        if ((int)response.StatusCode >= 500)
        {
            return new GraSubmissionResult(false, true, false, null, "GRA_SERVICE_UNAVAILABLE");
        }

        if (!response.IsSuccessStatusCode)
        {
            return new GraSubmissionResult(false, false, true, null, "GRA_REQUEST_REJECTED");
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
            businessPartnerName = invoice.BusinessPartnerName,
            businessPartnerTin = invoice.BusinessPartnerTin,
            saleType = invoice.SaleType,
            discountType = invoice.DiscountType,
            discountAmount = invoice.DiscountAmount,
            groupReferenceId = invoice.GroupReferenceId,
            itemList = invoice.Items.Select(ToGraItem).ToArray()
        };
    }

    private static object ToGraItem(InvoiceItemCommand item)
    {
        return new
        {
            itemCode = item.ItemCode,
            itemCategory = item.ItemCategory,
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
            batchCode = item.BatchCode,
            expireDate = item.ExpireDate?.ToString("yyyy-MM-dd")
        };
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
