namespace GraEvatMiddleware.IntegrationTests;

public sealed class ApiAssemblyTests
{
    [Fact]
    public void ApiProject_IsAvailableToIntegrationTests()
    {
        Assert.Equal("GraEvatMiddleware.Api", typeof(Program).Assembly.GetName().Name);
    }
}
