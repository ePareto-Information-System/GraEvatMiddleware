using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace GraEvatMiddleware.IntegrationTests;

public sealed class OperationalReadinessTests(GraEvatApiFactory factory) : IClassFixture<GraEvatApiFactory>
{
    [Fact]
    public async Task OperationsStatus_RejectsMissingAdminKey()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/v1/admin/operations/status");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task OperationsStatus_ReportsRequestAndRetrySignals()
    {
        using var client = factory.CreateClient();
        const string correlationId = "operations-observability-test";
        using var livenessRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/health/live");
        livenessRequest.Headers.Add("X-Correlation-ID", correlationId);
        using var livenessResponse = await client.SendAsync(livenessRequest);
        livenessResponse.EnsureSuccessStatusCode();

        using var statusRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/operations/status");
        statusRequest.Headers.Add("X-Admin-Key", "integration-test-admin-key");
        using var statusResponse = await client.SendAsync(statusRequest);
        statusResponse.EnsureSuccessStatusCode();
        var status = await statusResponse.Content.ReadFromJsonAsync<OperationsStatusResponse>();

        Assert.NotNull(status);
        Assert.True(status.Requests.Completed >= 1);
        Assert.Equal(0, status.Retries.Exhausted);
        Assert.Equal(0, status.Retries.ExpiredLeases);
        Assert.Equal(correlationId, livenessResponse.Headers.GetValues("X-Correlation-ID").Single());
    }

    [Fact]
    public void RetentionCleanup_IsRegisteredAsHostedService()
    {
        var hostedServices = factory.Services.GetServices<IHostedService>();

        Assert.Contains(hostedServices, service =>
            service.GetType().Name == "ExchangeLogRetentionWorker");
    }

    private sealed record OperationsStatusResponse(
        RequestMetricsResponse Requests,
        RetryMetricsResponse Retries,
        DateTimeOffset Timestamp);

    private sealed record RequestMetricsResponse(
        long Started,
        long Completed,
        long Failed,
        long RateLimited,
        double AverageDurationMilliseconds);

    private sealed record RetryMetricsResponse(
        int Scheduled,
        int Processing,
        int ReconciliationPending,
        int Exhausted,
        int ExpiredLeases);
}
