namespace GraEvatMiddleware.Application.Abstractions;

public interface IGraEvatClient
{
    Task<GraSubmissionResult> SubmitAsync(
        GraSubmission submission,
        CancellationToken cancellationToken = default);
}

public sealed record GraSubmission(
    Guid TenantId,
    string Operation,
    string IdempotencyKey,
    object Payload);

public sealed record GraSubmissionResult(
    bool IsSuccessful,
    bool IsPending,
    string? ExternalReference,
    string? ErrorCode);
