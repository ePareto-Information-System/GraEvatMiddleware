using GraEvatMiddleware.Application.Health;
using Microsoft.AspNetCore.Mvc;

namespace GraEvatMiddleware.Api.Controllers.V1;

[ApiController]
[Route("api/v1/health")]
public sealed class HealthController(IReadinessService readinessService) : ControllerBase
{
    [HttpGet("live")]
    [ProducesResponseType<HealthResponse>(StatusCodes.Status200OK)]
    public ActionResult<HealthResponse> GetLiveness()
    {
        return Ok(new HealthResponse("healthy", DateTimeOffset.UtcNow));
    }

    [HttpGet("ready")]
    [ProducesResponseType<ReadinessResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ReadinessResponse>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<ReadinessResponse>> GetReadiness(CancellationToken cancellationToken)
    {
        var result = await readinessService.CheckAsync(cancellationToken);
        var response = new ReadinessResponse(
            result.IsReady ? "ready" : "not_ready",
            result.Database,
            result.Gra,
            result.GraErrorCode,
            result.Timestamp);
        return result.IsReady
            ? Ok(response)
            : StatusCode(StatusCodes.Status503ServiceUnavailable, response);
    }
}

public sealed record HealthResponse(string Status, DateTimeOffset Timestamp);

public sealed record ReadinessResponse(
    string Status,
    string Database,
    string Gra,
    string? GraErrorCode,
    DateTimeOffset Timestamp);
