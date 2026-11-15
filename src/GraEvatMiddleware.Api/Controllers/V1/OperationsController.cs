using GraEvatMiddleware.Api.Authentication;
using GraEvatMiddleware.Application.Operations;
using Microsoft.AspNetCore.Mvc;

namespace GraEvatMiddleware.Api.Controllers.V1;

[ApiController]
[Route("api/v1/admin/operations")]
[AdminKey]
public sealed class OperationsController(IOperationalStatusService operationalStatusService) : ControllerBase
{
    [HttpGet("status")]
    [ProducesResponseType<OperationalStatus>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<OperationalStatus>> GetStatus(CancellationToken cancellationToken)
    {
        return Ok(await operationalStatusService.GetAsync(cancellationToken));
    }
}
