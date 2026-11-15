using System.Net;
using System.Text.Json;

namespace GraEvatMiddleware.IntegrationTests;

public sealed class RateLimitingTests
{
    [Fact]
    public async Task RequestsBeyondConfiguredLimit_ReturnTooManyRequests()
    {
        using var factory = new GraEvatApiFactory(new Dictionary<string, string?>
        {
            ["RateLimiting:Enabled"] = "true",
            ["RateLimiting:PermitLimit"] = "2",
            ["RateLimiting:WindowSeconds"] = "60"
        });
        using var client = factory.CreateClient();

        using var first = await client.GetAsync("/api/v1/health/live");
        using var second = await client.GetAsync("/api/v1/health/live");
        using var rejected = await client.GetAsync("/api/v1/health/live");

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.Equal("application/problem+json", rejected.Content.Headers.ContentType?.MediaType);
        using var problem = JsonDocument.Parse(await rejected.Content.ReadAsStringAsync());
        Assert.Equal("RATE_LIMIT_EXCEEDED", problem.RootElement.GetProperty("code").GetString());
    }
}
