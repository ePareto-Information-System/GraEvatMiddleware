namespace GraEvatMiddleware.Application.Retry;

public sealed class GraRetryOptions
{
    public const string SectionName = "GraRetry";

    public bool Enabled { get; init; }
    public bool WorkerEnabled { get; init; }
    public int MaximumAttempts { get; init; } = 5;
    public int InitialDelaySeconds { get; init; } = 30;
    public double BackoffMultiplier { get; init; } = 2;
    public int MaximumDelaySeconds { get; init; } = 3600;
    public int PollIntervalSeconds { get; init; } = 15;
    public int LeaseSeconds { get; init; } = 120;
}
