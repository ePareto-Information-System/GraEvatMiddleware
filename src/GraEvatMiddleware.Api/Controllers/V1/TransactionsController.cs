using GraEvatMiddleware.Api.Authentication;
using GraEvatMiddleware.Application.Invoices;
using GraEvatMiddleware.Application.Transactions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GraEvatMiddleware.Api.Controllers.V1;

[ApiController]
[Authorize]
[Route("api/v1/transactions")]
public sealed class TransactionsController(ITransactionQueryService transactionQueryService) : ControllerBase
{
    [HttpGet("{transactionId:guid}")]
    [ProducesResponseType<TransactionResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TransactionResult>> Find(
        Guid transactionId,
        CancellationToken cancellationToken)
    {
        var result = await transactionQueryService.FindAsync(
            User.GetTenantId(),
            transactionId,
            cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }
}
