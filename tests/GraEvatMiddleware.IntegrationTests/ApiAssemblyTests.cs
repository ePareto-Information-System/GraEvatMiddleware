using System.Net.Http.Json;
using System.Text.Json;

namespace GraEvatMiddleware.IntegrationTests;

public sealed class ApiAssemblyTests(GraEvatApiFactory factory) : IClassFixture<GraEvatApiFactory>
{
    [Fact]
    public void ApiProject_IsAvailableToIntegrationTests()
    {
        Assert.Equal("GraEvatMiddleware.Api", typeof(Program).Assembly.GetName().Name);
    }

    [Fact]
    public async Task SwaggerUi_AndDocument_AreAvailableForTesting()
    {
        using var client = factory.CreateClient();

        using var uiResponse = await client.GetAsync("/swagger/index.html");
        using var documentResponse = await client.GetAsync("/swagger/v1/swagger.json");

        uiResponse.EnsureSuccessStatusCode();
        documentResponse.EnsureSuccessStatusCode();
        Assert.Contains("GRA eVAT Middleware API", await uiResponse.Content.ReadAsStringAsync());
        var swaggerJson = await documentResponse.Content.ReadAsStringAsync();
        Assert.Contains("X-Admin-Key", swaggerJson);
        Assert.Contains("X-API-Key", swaggerJson);
        using var swagger = JsonDocument.Parse(swaggerJson);
        var transactionResponses = swagger.RootElement
            .GetProperty("paths")
            .GetProperty("/api/v1/transactions/{transactionId}")
            .GetProperty("get")
            .GetProperty("responses");
        Assert.True(transactionResponses.TryGetProperty("401", out _));
    }

    [Fact]
    public async Task Readiness_ReportsDatabaseReadyAndDisabledGraWithoutCallingUpstream()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/v1/health/ready");
        var body = await response.Content.ReadAsStringAsync();

        response.EnsureSuccessStatusCode();
        Assert.Contains("\"status\":\"ready\"", body, StringComparison.Ordinal);
        Assert.Contains("\"database\":\"healthy\"", body, StringComparison.Ordinal);
        Assert.Contains("\"gra\":\"disabled\"", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Readiness_WhenGraEnabledWithoutActiveTenant_ReturnsServiceUnavailable()
    {
        await using var enabledFactory = new GraEvatApiFactory(new Dictionary<string, string?>
        {
            ["GraApi:Enabled"] = "true",
            ["GraApi:Environment"] = "Staging",
            ["GraApi:BaseUrl"] = "https://gra.example/",
            ["GraApi:AllowedHosts:0"] = "gra.example"
        });
        using var client = enabledFactory.CreateClient();

        using var response = await client.GetAsync("/api/v1/health/ready");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(System.Net.HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Contains("\"status\":\"not_ready\"", body, StringComparison.Ordinal);
        Assert.Contains("\"gra\":\"not_configured\"", body, StringComparison.Ordinal);
        Assert.Contains("GRA_TENANT_NOT_CONFIGURED", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Readiness_WhenGraEnabledWithActiveTenant_ReportsHealthy()
    {
        await using var enabledFactory = new GraEvatApiFactory(new Dictionary<string, string?>
        {
            ["GraApi:Enabled"] = "true",
            ["GraApi:Environment"] = "Staging",
            ["GraApi:BaseUrl"] = "https://gra.example/",
            ["GraApi:AllowedHosts:0"] = "gra.example"
        });
        using var client = enabledFactory.CreateClient();
        using var registerRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/admin/tenants")
        {
            Content = JsonContent.Create(new
            {
                name = "Readiness Tenant",
                slug = "readiness-tenant",
                taxpayerReference = "TIN-READY",
                graSecurityKeySecretName = "gra-readiness-secret"
            })
        };
        registerRequest.Headers.Add("X-Admin-Key", "integration-test-admin-key");
        using var registration = await client.SendAsync(registerRequest);
        registration.EnsureSuccessStatusCode();

        using var response = await client.GetAsync("/api/v1/health/ready");
        var body = await response.Content.ReadAsStringAsync();

        response.EnsureSuccessStatusCode();
        Assert.Contains("\"status\":\"ready\"", body, StringComparison.Ordinal);
        Assert.Contains("\"gra\":\"healthy\"", body, StringComparison.Ordinal);
    }
}
