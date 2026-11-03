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
        Assert.Contains("X-Admin-Key", await documentResponse.Content.ReadAsStringAsync());
        Assert.Contains("X-API-Key", await documentResponse.Content.ReadAsStringAsync());
    }
}
