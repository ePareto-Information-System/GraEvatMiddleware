using System.Text.Json;
using GraEvatMiddleware.Infrastructure.Logging;

namespace GraEvatMiddleware.UnitTests;

public sealed class SensitiveDataSanitizerTests
{
    private readonly SensitiveDataSanitizer _sanitizer = new();

    [Fact]
    public void SanitizeHeaders_RedactsSensitiveValuesCaseInsensitively()
    {
        var result = _sanitizer.SanitizeHeaders(new Dictionary<string, string[]>
        {
            ["Content-Type"] = ["application/json"],
            ["x-api-key"] = ["evat.secret"],
            ["AUTHORIZATION"] = ["Bearer secret"],
            ["security_key"] = ["gra-secret"]
        });

        using var document = JsonDocument.Parse(result);
        Assert.Equal("application/json", document.RootElement.GetProperty("Content-Type")[0].GetString());
        Assert.Equal("[REDACTED]", document.RootElement.GetProperty("x-api-key")[0].GetString());
        Assert.Equal("[REDACTED]", document.RootElement.GetProperty("AUTHORIZATION")[0].GetString());
        Assert.Equal("[REDACTED]", document.RootElement.GetProperty("security_key")[0].GetString());
        Assert.DoesNotContain("evat.secret", result, StringComparison.Ordinal);
    }

    [Fact]
    public void SanitizeBody_RedactsNestedSensitivePropertiesCaseInsensitively()
    {
        const string body = """
            {
              "invoiceNumber": "INV-001",
              "credential": {
                "ApiKey": "raw-key",
                "nested": [{ "PASSWORD": "secret" }]
              },
              "security_key": "gra-key"
            }
            """;

        var result = _sanitizer.SanitizeBody(body, "application/json", 10_000);

        Assert.True(result.IsSupported);
        Assert.False(result.IsTruncated);
        Assert.Contains("INV-001", result.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("raw-key", result.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("gra-key", result.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("\"PASSWORD\":\"secret\"", result.Content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("[REDACTED]", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void SanitizeBody_TruncatesOnlyAfterRedaction()
    {
        const string body = "{\"apiKey\":\"must-not-leak\",\"description\":\"abcdefghijklmnopqrstuvwxyz\"}";

        var result = _sanitizer.SanitizeBody(body, "application/json", 35);

        Assert.True(result.IsTruncated);
        Assert.DoesNotContain("must-not-leak", result.Content, StringComparison.Ordinal);
        Assert.EndsWith("...[TRUNCATED]", result.Content, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("application/octet-stream")]
    [InlineData("image/png")]
    [InlineData(null)]
    public void SanitizeBody_DoesNotCaptureUnsupportedContentTypes(string? contentType)
    {
        var result = _sanitizer.SanitizeBody("binary-or-unknown", contentType, 1_000);

        Assert.False(result.IsSupported);
        Assert.Null(result.Content);
        Assert.False(result.IsTruncated);
    }
}
