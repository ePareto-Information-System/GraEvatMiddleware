using GraEvatMiddleware.Application.Operations;

namespace GraEvatMiddleware.Infrastructure.Monitoring;

internal sealed class OperationalMetrics : IRequestMetrics
{
    private long _started;
    private long _completed;
    private long _failed;
    private long _rateLimited;
    private long _totalDurationTicks;

    public void RequestStarted() => Interlocked.Increment(ref _started);

    public void RequestCompleted(int statusCode, TimeSpan duration)
    {
        Interlocked.Increment(ref _completed);
        Interlocked.Add(ref _totalDurationTicks, duration.Ticks);
        if (statusCode >= 500)
        {
            Interlocked.Increment(ref _failed);
        }

        if (statusCode == 429)
        {
            Interlocked.Increment(ref _rateLimited);
        }
    }

    public RequestMetricsSnapshot GetSnapshot()
    {
        var completed = Interlocked.Read(ref _completed);
        var totalTicks = Interlocked.Read(ref _totalDurationTicks);
        var averageMilliseconds = completed == 0
            ? 0
            : TimeSpan.FromTicks(totalTicks / completed).TotalMilliseconds;
        return new RequestMetricsSnapshot(
            Interlocked.Read(ref _started),
            completed,
            Interlocked.Read(ref _failed),
            Interlocked.Read(ref _rateLimited),
            Math.Round(averageMilliseconds, 2));
    }
}
