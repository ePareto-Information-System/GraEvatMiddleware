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
        ValidateDate(request.TransactionDate, nameof(request.TransactionDate));
        for (var index = 0; index < request.NoteLines.Count; index++)
        {
            ValidateDate(request.NoteLines[index].InvoiceDate, $"NoteLines[{index}].InvoiceDate");
        }

        var operation = request.NoteType == "DEBIT"
            ? TransactionOperation.DebitNote
            : TransactionOperation.CreditNote;
        return SubmitAsync(idempotencyKey, operation, request.NoteNumber, request.ToCommand(), cancellationToken);
    }

    [HttpPost("statements-of-account")]
    public Task<ActionResult<OperationTransactionResult>> SubmitStatement(
        [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
        StatementOfAccountRequest request,
        CancellationToken cancellationToken)
    {
        ValidateDate(request.TransactionDate, nameof(request.TransactionDate));
        for (var index = 0; index < request.Lines.Count; index++)
        {
            ValidateDate(request.Lines[index].TransactionDate, $"Lines[{index}].TransactionDate");
        }

        return SubmitAsync(idempotencyKey, TransactionOperation.StatementOfAccount,
            request.GroupReferenceId, request.ToCommand(), cancellationToken);
    }

    [HttpPost("inventory")]
    public Task<ActionResult<OperationTransactionResult>> SubmitInventory(
        [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
        InventorySubmissionRequest request,
        CancellationToken cancellationToken)
    {
        ValidateDate(request.InventoryDate, nameof(request.InventoryDate));
        return SubmitAsync(idempotencyKey, TransactionOperation.Inventory,
            $"INVENTORY-{request.InventoryDate:yyyyMMdd}", request.ToCommand(), cancellationToken);
    }

    [HttpGet("identifications/tin/{tin}")]
    public Task<ActionResult<IdentificationResult>> ValidateTin(string tin, CancellationToken cancellationToken) =>
        ValidateIdentificationAsync("tin", tin, cancellationToken);

    [HttpGet("identifications/ghana-card/{nationalId}")]
    public Task<ActionResult<IdentificationResult>> ValidateGhanaCard(
        string nationalId,
        CancellationToken cancellationToken) =>
        ValidateIdentificationAsync("nationalId", nationalId, cancellationToken);

    private async Task<ActionResult<IdentificationResult>> ValidateIdentificationAsync(
        string kind,
        string value,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length is < 3 or > 64)
        {
            ModelState.AddModelError(kind, "The identifier must contain 3 to 64 characters.");
            return UnprocessableEntity(CreateValidationProblem());
        }

        return Ok(await service.ValidateIdentificationAsync(new IdentificationQuery(
            User.GetTenantId(), kind, value, HttpContext.TraceIdentifier), cancellationToken));
    }

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
            return UnprocessableEntity(CreateValidationProblem());
        }

        var result = await service.SubmitAsync(new SubmitGraOperationCommand(
            User.GetTenantId(), operation, documentNumber, idempotencyKey,
            HttpContext.TraceIdentifier, payload), cancellationToken);
        HttpContext.Items[ExchangeLoggingMiddleware.TransactionIdItemKey] = result.Transaction.TransactionId;
        return result.IsReplay ? Ok(result.Transaction) : StatusCode(StatusCodes.Status201Created, result.Transaction);
    }

    private void ValidateDate(DateOnly value, string field)
    {
        if (value == default) ModelState.AddModelError(field, "A valid business date is required.");
    }

    private void ValidateDate(DateTimeOffset value, string field)
    {
        if (value == default) ModelState.AddModelError(field, "A valid business date is required.");
    }

    private ValidationProblemDetails CreateValidationProblem() => new(ModelState)
    {
        Status = StatusCodes.Status422UnprocessableEntity,
        Title = "Request validation failed.",
        Instance = HttpContext.Request.Path,
        Extensions =
        {
            ["code"] = "VALIDATION_FAILED",
            ["correlationId"] = HttpContext.TraceIdentifier
        }
    };
}
