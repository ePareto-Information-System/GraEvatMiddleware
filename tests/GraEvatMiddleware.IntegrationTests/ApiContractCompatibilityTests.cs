using System.Text.Json;
using System.Text.RegularExpressions;

namespace GraEvatMiddleware.IntegrationTests;

public sealed partial class ApiContractCompatibilityTests(GraEvatApiFactory factory)
    : IClassFixture<GraEvatApiFactory>
{
    [Fact]
    public async Task MaintainedOpenApiPaths_MatchRuntimeSwaggerPaths()
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/swagger/v1/swagger.json");
        response.EnsureSuccessStatusCode();
        using var runtimeDocument = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var runtimePaths = runtimeDocument.RootElement.GetProperty("paths")
            .EnumerateObject()
            .Select(item => item.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();

        var repositoryRoot = FindRepositoryRoot();
        var maintainedPaths = File.ReadLines(Path.Combine(repositoryRoot, "docs", "openapi-v1.yaml"))
            .Select(line => PathLine().Match(line))
            .Where(match => match.Success)
            .Select(match => match.Groups[1].Value)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(24, runtimePaths.Length);
        Assert.Equal(runtimePaths, maintainedPaths);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "docs", "openapi-v1.yaml")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }

    [GeneratedRegex("^  (/api/v1/[^:]+):\\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex PathLine();
}
