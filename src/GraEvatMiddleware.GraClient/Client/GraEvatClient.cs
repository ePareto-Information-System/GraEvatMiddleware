using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GraEvatMiddleware.Application.Abstractions;
using GraEvatMiddleware.Application.Invoices;
using GraEvatMiddleware.GraClient.Configuration;
using Microsoft.Extensions.Options;

namespace GraEvatMiddleware.GraClient.Client;

internal sealed class GraEvatClient(
    HttpClient httpClient,
    IGraSecretProvider secretProvider,
    IOptions<GraApiOptions> options) : IGraEvatClient
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

        httpClient.BaseAddress ??= baseUri;
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"vsdc/api/v1/taxpayer/{Uri.EscapeDataString(submission.TaxpayerReference)}/invoice");
        request.Headers.Add("security_key", securityKey);
        request.Content = JsonContent.Create(ToGraPayload(submission.Invoice));

        using var response = await httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

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

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        return ParseResponse(document.RootElement);
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
