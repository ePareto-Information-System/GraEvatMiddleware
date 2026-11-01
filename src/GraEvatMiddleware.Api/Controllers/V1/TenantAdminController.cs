using GraEvatMiddleware.Api.Authentication;
using GraEvatMiddleware.Api.Contracts;
using GraEvatMiddleware.Application.Tenants;
using Microsoft.AspNetCore.Mvc;

namespace GraEvatMiddleware.Api.Controllers.V1;

[ApiController]
[Route("api/v1/admin/tenants")]
[AdminKey]
public sealed class TenantAdminController(ITenantManagementService tenantService) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<TenantRegistrationResult>(StatusCodes.Status201Created)]
    public async Task<ActionResult<TenantRegistrationResult>> Register(
        RegisterTenantRequest request,
        CancellationToken cancellationToken)
    {
        var result = await tenantService.RegisterAsync(
            new RegisterTenantCommand(
                request.Name,
                request.Slug,
                request.TaxpayerReference,
                request.GraSecurityKeySecretName,
                HttpContext.TraceIdentifier),
            cancellationToken);
        return Created($"/api/v1/admin/tenants/{result.TenantId}", result);
    }

    [HttpPost("{tenantId:guid}/credentials")]
    [ProducesResponseType<IssuedCredentialResult>(StatusCodes.Status201Created)]
    public async Task<ActionResult<IssuedCredentialResult>> IssueCredential(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var result = await tenantService.IssueCredentialAsync(
            tenantId,
            HttpContext.TraceIdentifier,
            cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpDelete("{tenantId:guid}/credentials/{credentialId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RevokeCredential(
        Guid tenantId,
        Guid credentialId,
        CancellationToken cancellationToken)
    {
        await tenantService.RevokeCredentialAsync(
            tenantId,
            credentialId,
            HttpContext.TraceIdentifier,
            cancellationToken);
        return NoContent();
    }

    [HttpPost("{tenantId:guid}/activate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Activate(Guid tenantId, CancellationToken cancellationToken)
    {
        await tenantService.ActivateAsync(tenantId, HttpContext.TraceIdentifier, cancellationToken);
        return NoContent();
    }

    [HttpPost("{tenantId:guid}/suspend")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Suspend(Guid tenantId, CancellationToken cancellationToken)
    {
        await tenantService.SuspendAsync(tenantId, HttpContext.TraceIdentifier, cancellationToken);
        return NoContent();
    }
}
