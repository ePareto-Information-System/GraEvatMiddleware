using GraEvatMiddleware.Application.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GraEvatMiddleware.Infrastructure.Logging;

internal sealed class ExchangeLogRetentionWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<ExchangeLoggingOptions> options,
    ILogger<ExchangeLogRetentionWorker> logger) : BackgroundService
{
    private readonly ExchangeLoggingOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.RetentionCleanupEnabled)
        {
            logger.LogInformation("Exchange-log retention cleanup is disabled.");
            return;
        }

        var initialDelay = TimeSpan.FromSeconds(Math.Max(0, _options.RetentionCleanupInitialDelaySeconds));
        if (initialDelay > TimeSpan.Zero)
        {
            await Task.Delay(initialDelay, stoppingToken);
        }

        var interval = TimeSpan.FromMinutes(Math.Max(1, _options.RetentionCleanupIntervalMinutes));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var retentionService = scope.ServiceProvider.GetRequiredService<IExchangeLogRetentionService>();
                var deleted = await retentionService.DeleteExpiredAsync(DateTimeOffset.UtcNow, stoppingToken);
                logger.LogInformation(
                    "Exchange-log retention cleanup deleted {DeletedCount} expired records.",
                    deleted);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Exchange-log retention cleanup failed.");
            }

            await Task.Delay(interval, stoppingToken);
        }
    }
}
