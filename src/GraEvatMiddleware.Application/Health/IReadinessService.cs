namespace GraEvatMiddleware.Application.Health;

public interface IReadinessService
{
    Task<ReadinessResult> CheckAsync(CancellationToken cancellationToken = default);
}

public sealed record ReadinessResult(
    bool IsReady,
    string Database,
    string Gra,
    string? GraErrorCode,
    DateTimeOffset Timestamp);
