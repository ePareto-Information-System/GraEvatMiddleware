using System.Text;
using System.Text.Json;
using GraEvatMiddleware.Application.Logging;

namespace GraEvatMiddleware.Infrastructure.Logging;

public sealed class SensitiveDataSanitizer : IExchangeContentSanitizer
{
    private const string Redacted = "[REDACTED]";
    private const string TruncatedSuffix = "...[TRUNCATED]";
    private static readonly HashSet<string> SensitiveNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "X-API-Key", "X-Admin-Key", "Authorization", "Cookie", "Set-Cookie", "security_key",
        "apiKey", "adminKey", "securityKey", "password", "secret", "token", "accessToken",
        "refreshToken", "connectionString"
    };

    public string SanitizeHeaders(IReadOnlyDictionary<string, string[]> headers)
    {
        var sanitized = headers.ToDictionary(
            item => item.Key,
            item => SensitiveNames.Contains(item.Key) ? new[] { Redacted } : item.Value,
            StringComparer.OrdinalIgnoreCase);
        return JsonSerializer.Serialize(sanitized);
    }

    public SanitizedContent SanitizeBody(string? body, string? contentType, int maximumCharacters)
    {
        if (string.IsNullOrEmpty(body)) return new SanitizedContent(body, false, IsJson(contentType));
        if (!IsJson(contentType)) return new SanitizedContent(null, false, false);

        string sanitized;
        try
        {
            using var document = JsonDocument.Parse(body);
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream)) WriteElement(document.RootElement, writer);
            sanitized = Encoding.UTF8.GetString(stream.ToArray());
        }
        catch (JsonException)
        {
            return new SanitizedContent("[INVALID JSON OMITTED]", false, true);
        }

        var limit = Math.Max(TruncatedSuffix.Length, maximumCharacters);
        return sanitized.Length <= limit
            ? new SanitizedContent(sanitized, false, true)
            : new SanitizedContent(sanitized[..(limit - TruncatedSuffix.Length)] + TruncatedSuffix, true, true);
    }

    private static bool IsJson(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType)) return false;
        var mediaType = contentType.Split(';', 2)[0].Trim();
        return mediaType.Equals("application/json", StringComparison.OrdinalIgnoreCase) ||
               mediaType.EndsWith("+json", StringComparison.OrdinalIgnoreCase);
    }

    private static void WriteElement(JsonElement element, Utf8JsonWriter writer)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject())
                {
                    writer.WritePropertyName(property.Name);
                    if (SensitiveNames.Contains(property.Name)) writer.WriteStringValue(Redacted);
                    else WriteElement(property.Value, writer);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray()) WriteElement(item, writer);
                writer.WriteEndArray();
                break;
            default:
                element.WriteTo(writer);
                break;
        }
    }
}
