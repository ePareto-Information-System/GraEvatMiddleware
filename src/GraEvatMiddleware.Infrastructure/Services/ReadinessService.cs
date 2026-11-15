using GraEvatMiddleware.Application.Abstractions;
using GraEvatMiddleware.Application.Health;
using GraEvatMiddleware.Domain.Tenants;
using GraEvatMiddleware.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace GraEvatMiddleware.Infrastructure.Services;

internal sealed class ReadinessService(
    AppDbContext dbContext,
    IGraEvatClient graClient,
    IConfiguration configuration,
    ILogger<ReadinessService> logger) : IReadinessService
{
    public async Task<ReadinessResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        var timestamp = DateTimeOffset.UtcNow;
        try
        {
            if (!await dbContext.Database.CanConnectAsync(cancellationToken))
            {
                return new ReadinessResult(false, "unavailable", "not_checked", null, timestamp);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                "Database readiness check failed with exception type {ExceptionType}.",
                exception.GetType().Name);
            return new ReadinessResult(false, "unavailable", "not_checked", null, timestamp);
        }

        if (!configuration.GetValue<bool>("GraApi:Enabled"))
        {
            return new ReadinessResult(true, "healthy", "disabled", null, timestamp);
        }

        var tenant = await dbContext.Tenants
            .AsNoTracking()
            .Where(item => item.Status == TenantStatus.Active)
            .OrderBy(item => item.CreatedAt)
            .Select(item => new { item.TaxpayerReference, item.GraSecurityKeySecretName })
            .FirstOrDefaultAsync(cancellationToken);
        if (tenant is null)
        {
            return new ReadinessResult(false, "healthy", "not_configured", "GRA_TENANT_NOT_CONFIGURED", timestamp);
        }

        try
        {
            var result = await graClient.CheckHealthAsync(
                new GraHealthCheck(tenant.TaxpayerReference, tenant.GraSecurityKeySecretName),
                cancellationToken);
            if (result.IsHealthy)
            {
                return new ReadinessResult(true, "healthy", "healthy", null, timestamp);
            }

            logger.LogWarning("GRA readiness check failed with code {ErrorCode}.", result.ErrorCode);
            return new ReadinessResult(false, "healthy", "unavailable", result.ErrorCode, timestamp);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("GRA readiness check timed out.");
            return new ReadinessResult(false, "healthy", "unavailable", "GRA_HEALTH_TIMEOUT", timestamp);
        }
        catch (HttpRequestException)
        {
            logger.LogWarning("GRA readiness check encountered a communication failure.");
            return new ReadinessResult(false, "healthy", "unavailable", "GRA_HEALTH_COMMUNICATION_FAILURE", timestamp);
        }
    }
}
