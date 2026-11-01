using Microsoft.AspNetCore.Mvc;

namespace GraEvatMiddleware.Api.Controllers.V1;

[ApiController]
[Route("api/v1/health")]
public sealed class HealthController : ControllerBase
{
    [HttpGet("live")]
    [ProducesResponseType<HealthResponse>(StatusCodes.Status200OK)]
    public ActionResult<HealthResponse> GetLiveness()
    {
        return Ok(new HealthResponse("healthy", DateTimeOffset.UtcNow));
    }
}

public sealed record HealthResponse(string Status, DateTimeOffset Timestamp);
