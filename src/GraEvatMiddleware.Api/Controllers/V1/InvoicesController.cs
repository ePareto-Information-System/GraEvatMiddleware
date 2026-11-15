using GraEvatMiddleware.Api.Authentication;
using GraEvatMiddleware.Api.Contracts;
using GraEvatMiddleware.Api.Middleware;
using GraEvatMiddleware.Application.Invoices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GraEvatMiddleware.Api.Controllers.V1;

[ApiController]
[Authorize]
[Route("api/v1/invoices")]
public sealed class InvoicesController(IInvoiceService invoiceService) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<TransactionResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<TransactionResult>(StatusCodes.Status201Created)]
    public async Task<ActionResult<TransactionResult>> Submit(
        [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
        InvoiceRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length is < 8 or > 128)
        {
            ModelState.AddModelError("Idempotency-Key", "Idempotency-Key must contain 8 to 128 characters.");
            return UnprocessableEntity(new ValidationProblemDetails(ModelState));
        }

        var result = await invoiceService.SubmitAsync(
            new SubmitInvoiceCommand(
                User.GetTenantId(),
                idempotencyKey,
                HttpContext.TraceIdentifier,
                request.ToCommand()),
            cancellationToken);

        HttpContext.Items[ExchangeLoggingMiddleware.TransactionIdItemKey] = result.Transaction.TransactionId;

        return result.IsReplay
            ? Ok(result.Transaction)
            : StatusCode(StatusCodes.Status201Created, result.Transaction);
    }
}
