using GraEvatMiddleware.Api.Authentication;
using GraEvatMiddleware.Api.Contracts;
using GraEvatMiddleware.Api.Middleware;
using GraEvatMiddleware.Application.Operations;
using GraEvatMiddleware.Domain.Transactions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GraEvatMiddleware.Api.Controllers.V1;

[ApiController]
[Authorize]
[Route("api/v1")]
public sealed class RemainingGraOperationsController(IGraOperationService service) : ControllerBase
{
    [HttpPost("credit-debit-notes")]
    public Task<ActionResult<OperationTransactionResult>> SubmitNote(
        [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
        CreditDebitNoteRequest request,
        CancellationToken cancellationToken)
    {
        var operation = request.NoteType == "DEBIT"
            ? TransactionOperation.DebitNote
            : TransactionOperation.CreditNote;
        return SubmitAsync(idempotencyKey, operation, request.NoteNumber, request.ToCommand(), cancellationToken);
    }

    [HttpPost("statements-of-account")]
    public Task<ActionResult<OperationTransactionResult>> SubmitStatement(
        [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
        StatementOfAccountRequest request,
        CancellationToken cancellationToken) =>
        SubmitAsync(idempotencyKey, TransactionOperation.StatementOfAccount,
            request.GroupReferenceId, request.ToCommand(), cancellationToken);

    [HttpPost("inventory")]
    public Task<ActionResult<OperationTransactionResult>> SubmitInventory(
        [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
        InventorySubmissionRequest request,
        CancellationToken cancellationToken) =>
        SubmitAsync(idempotencyKey, TransactionOperation.Inventory,
            $"INVENTORY-{request.InventoryDate:yyyyMMdd}", request.ToCommand(), cancellationToken);

    [HttpGet("identifications/tin/{tin}")]
    public Task<IdentificationResult> ValidateTin(string tin, CancellationToken cancellationToken) =>
        service.ValidateIdentificationAsync(new IdentificationQuery(
            User.GetTenantId(), "tin", tin, HttpContext.TraceIdentifier), cancellationToken);

    [HttpGet("identifications/ghana-card/{nationalId}")]
    public Task<IdentificationResult> ValidateGhanaCard(string nationalId, CancellationToken cancellationToken) =>
        service.ValidateIdentificationAsync(new IdentificationQuery(
            User.GetTenantId(), "nationalId", nationalId, HttpContext.TraceIdentifier), cancellationToken);

    private async Task<ActionResult<OperationTransactionResult>> SubmitAsync(
        string idempotencyKey,
        TransactionOperation operation,
        string documentNumber,
        object payload,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length is < 8 or > 128)
        {
            ModelState.AddModelError("Idempotency-Key", "Idempotency-Key must contain 8 to 128 characters.");
        }

        if (!ModelState.IsValid)
        {
            return UnprocessableEntity(new ValidationProblemDetails(ModelState)
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Title = "Request validation failed.",
                Instance = HttpContext.Request.Path,
                Extensions =
                {
                    ["code"] = "VALIDATION_FAILED",
                    ["correlationId"] = HttpContext.TraceIdentifier
                }
            });
        }

        var result = await service.SubmitAsync(new SubmitGraOperationCommand(
            User.GetTenantId(), operation, documentNumber, idempotencyKey,
            HttpContext.TraceIdentifier, payload), cancellationToken);
        HttpContext.Items[ExchangeLoggingMiddleware.TransactionIdItemKey] = result.Transaction.TransactionId;
        return result.IsReplay ? Ok(result.Transaction) : StatusCode(StatusCodes.Status201Created, result.Transaction);
    }
}
