using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace GraEvatMiddleware.Infrastructure.Authentication;

internal sealed class ApiKeyMaterialService(IOptions<SecurityOptions> options)
{
    private readonly byte[] _pepper = Encoding.UTF8.GetBytes(
        string.IsNullOrWhiteSpace(options.Value.ApiKeyPepper)
            ? throw new InvalidOperationException("Security:ApiKeyPepper must be configured.")
            : options.Value.ApiKeyPepper);

    public IssuedApiKey Generate()
    {
        var prefix = Convert.ToHexString(RandomNumberGenerator.GetBytes(6)).ToLowerInvariant();
        var secret = ToBase64Url(RandomNumberGenerator.GetBytes(32));
        var rawKey = $"evat.{prefix}.{secret}";
        return new IssuedApiKey(rawKey, prefix, Hash(rawKey));
    }

    public string? ExtractPrefix(string rawKey)
    {
        var parts = rawKey.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length == 3 && parts[0] == "evat" && parts[1].Length == 12
            ? parts[1]
            : null;
    }

    public bool Verify(string rawKey, string expectedHash)
    {
        var actual = Convert.FromBase64String(Hash(rawKey));
        var expected = Convert.FromBase64String(expectedHash);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    private string Hash(string rawKey)
    {
        using var hmac = new HMACSHA256(_pepper);
        return Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(rawKey)));
    }

    private static string ToBase64Url(byte[] value)
    {
        return Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}

internal sealed record IssuedApiKey(string RawKey, string Prefix, string Hash);
