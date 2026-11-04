using GraEvatMiddleware.Api.Authentication;
using GraEvatMiddleware.Application.Auditing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GraEvatMiddleware.Api.Controllers.V1;

[ApiController]
[Authorize]
[Route("api/v1/audit")]
public sealed class AuditController(IAuditQueryService auditQueryService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<AuditEventResult>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AuditEventResult>>> List(
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var events = await auditQueryService.ListAsync(User.GetTenantId(), limit, cancellationToken);
        return Ok(events);
    }
}
