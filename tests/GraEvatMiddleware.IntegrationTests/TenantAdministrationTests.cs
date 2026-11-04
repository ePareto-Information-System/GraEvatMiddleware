using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GraEvatMiddleware.Api.Contracts;
using GraEvatMiddleware.Application.Tenants;
using GraEvatMiddleware.Domain.Tenants;
using GraEvatMiddleware.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GraEvatMiddleware.IntegrationTests;

public sealed class TenantAdministrationTests(GraEvatApiFactory factory) : IClassFixture<GraEvatApiFactory>
{
    [Fact]
    public async Task RotateCredential_RevokesOldKeyAndReturnsUsableReplacement()
    {
        using var client = factory.CreateClient();
        var tenant = await RegisterTenantAsync(client);
        using var rotate = AdminRequest(
            HttpMethod.Post,
            $"/api/v1/admin/tenants/{tenant.TenantId}/credentials/{tenant.Credential.CredentialId}/rotate");
        using var rotateResponse = await client.SendAsync(rotate);

        Assert.Equal(HttpStatusCode.Created, rotateResponse.StatusCode);
        var replacement = await rotateResponse.Content.ReadFromJsonAsync<IssuedCredentialResult>();
        Assert.NotNull(replacement);
        Assert.NotEqual(tenant.Credential.CredentialId, replacement.CredentialId);

        using var oldKeyResponse = await LookupAsync(client, tenant.Credential.ApiKey);
        using var newKeyResponse = await LookupAsync(client, replacement.ApiKey);
        Assert.Equal(HttpStatusCode.Unauthorized, oldKeyResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, newKeyResponse.StatusCode);
    }

    [Fact]
    public async Task TerminateTenant_RevokesCredentialsAndCannotBeReactivated()
    {
        using var client = factory.CreateClient();
        var tenant = await RegisterTenantAsync(client);
        using var terminate = AdminRequest(
            HttpMethod.Post,
            $"/api/v1/admin/tenants/{tenant.TenantId}/terminate");
        using var terminateResponse = await client.SendAsync(terminate);
        Assert.Equal(HttpStatusCode.NoContent, terminateResponse.StatusCode);

        using var tenantResponse = await LookupAsync(client, tenant.Credential.ApiKey);
        Assert.Equal(HttpStatusCode.Unauthorized, tenantResponse.StatusCode);

        using var activate = AdminRequest(
            HttpMethod.Post,
            $"/api/v1/admin/tenants/{tenant.TenantId}/activate");
        using var activateResponse = await client.SendAsync(activate);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, activateResponse.StatusCode);
    }

    [Fact]
    public async Task UpdateConfiguration_ChangesOnlyNamedTenantAndCreatesAuditEvent()
    {
        using var client = factory.CreateClient();
        var tenant = await RegisterTenantAsync(client);
        using var update = AdminRequest(
            HttpMethod.Put,
            $"/api/v1/admin/tenants/{tenant.TenantId}/configuration",
            new UpdateTenantConfigurationRequest
            {
                TaxpayerReference = $"UPDATED-{Guid.NewGuid():N}"[..20],
                GraSecurityKeySecretName = "updated-gra-secret"
            });
        using var response = await client.SendAsync(update);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var saved = await dbContext.Tenants.SingleAsync(item => item.Id == tenant.TenantId);
        Assert.StartsWith("UPDATED-", saved.TaxpayerReference, StringComparison.Ordinal);
        Assert.Equal("updated-gra-secret", saved.GraSecurityKeySecretName);
        Assert.True(await dbContext.AuditEvents.AnyAsync(item =>
            item.TenantId == tenant.TenantId && item.Action == "tenant.configuration_updated"));
    }

    [Fact]
    public async Task AuditLookup_ReturnsOnlyAuthenticatedTenantEvents()
    {
        using var client = factory.CreateClient();
        var first = await RegisterTenantAsync(client);
        var second = await RegisterTenantAsync(client);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/audit?limit=100");
        request.Headers.Add("X-API-Key", first.Credential.ApiKey);
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var events = json.RootElement.EnumerateArray().ToList();
        Assert.NotEmpty(events);
        Assert.All(events, item => Assert.Equal(first.TenantId, item.GetProperty("tenantId").GetGuid()));
        Assert.DoesNotContain(events, item => item.GetProperty("tenantId").GetGuid() == second.TenantId);
    }

    private static async Task<TenantRegistrationResult> RegisterTenantAsync(HttpClient client)
    {
        var suffix = Guid.NewGuid().ToString("N")[..10];
        using var request = AdminRequest(HttpMethod.Post, "/api/v1/admin/tenants", new RegisterTenantRequest
        {
            Name = $"Tenant {suffix}",
            Slug = $"tenant-{suffix}",
            TaxpayerReference = $"TIN-{suffix}",
            GraSecurityKeySecretName = $"gra-secret-{suffix}"
        });
        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TenantRegistrationResult>())!;
    }

    private static HttpRequestMessage AdminRequest(HttpMethod method, string url, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Add("X-Admin-Key", "integration-test-admin-key");
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return request;
    }

    private static Task<HttpResponseMessage> LookupAsync(HttpClient client, string apiKey)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/transactions/{Guid.NewGuid()}");
        request.Headers.Add("X-API-Key", apiKey);
        return client.SendAsync(request);
    }
}
