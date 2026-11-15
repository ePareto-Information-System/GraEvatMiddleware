using GraEvatMiddleware.Api.Authentication;
using GraEvatMiddleware.Api.Contracts;
using GraEvatMiddleware.Api.Middleware;
using GraEvatMiddleware.Application.Invoices;
using GraEvatMiddleware.Domain.Transactions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GraEvatMiddleware.Api.Controllers.V1;

[ApiController]
[Authorize]
[Route("api/v1")]
public sealed class FiscalOperationsController(IFiscalTransactionService service) : ControllerBase
{
    [HttpPost("refunds")]
    public Task<ActionResult<TransactionResult>> SubmitRefund(
        [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
        RefundRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Reference))
        {
            ModelState.AddModelError(nameof(request.Reference), "Reference is required for refunds.");
        }

        var flag = string.Equals(request.RefundType, "PARTIAL", StringComparison.Ordinal)
            ? "PARTIAL_REFUND"
            : "REFUND";
        return SubmitDocumentAsync(
            idempotencyKey,
            request,
            TransactionOperation.InvoiceRefund,
            flag,
            cancellationToken);
    }

    [HttpPost("purchases")]
    public Task<ActionResult<TransactionResult>> SubmitPurchase(
        [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
        FiscalDocumentRequest request,
        CancellationToken cancellationToken) =>
        SubmitDocumentAsync(
            idempotencyKey,
            request,
            TransactionOperation.Purchase,
            "PURCHASE",
            cancellationToken);

    [HttpPost("purchase-returns")]
    public Task<ActionResult<TransactionResult>> SubmitPurchaseReturn(
        [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
        FiscalDocumentRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Reference))
        {
            ModelState.AddModelError(nameof(request.Reference), "Reference is required for purchase returns.");
        }

        return SubmitDocumentAsync(
            idempotencyKey,
            request,
            TransactionOperation.PurchaseReturn,
            "PURCHASE_RETURN",
            cancellationToken);
    }

    [HttpPost("refund-cancellations")]
    public Task<ActionResult<TransactionResult>> SubmitRefundCancellation(
        [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
        CancellationRequest request,
        CancellationToken cancellationToken) =>
        SubmitCancellationAsync(
            idempotencyKey,
            request,
            TransactionOperation.RefundCancellation,
            "REFUND_CANCELATION",
            cancellationToken);

    [HttpPost("purchase-return-cancellations")]
    public Task<ActionResult<TransactionResult>> SubmitPurchaseReturnCancellation(
        [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
        CancellationRequest request,
        CancellationToken cancellationToken) =>
        SubmitCancellationAsync(
            idempotencyKey,
            request,
            TransactionOperation.PurchaseReturnCancellation,
            "PURCHASE_RETURN_CANCELATION",
            cancellationToken);

    private async Task<ActionResult<TransactionResult>> SubmitDocumentAsync(
        string idempotencyKey,
        FiscalDocumentRequest request,
        TransactionOperation operation,
        string graFlag,
        CancellationToken cancellationToken)
    {
        if (!ValidateIdempotencyKey(idempotencyKey) || !ModelState.IsValid)
        {
            return UnprocessableEntity(CreateValidationProblem());
        }

        var result = await service.SubmitDocumentAsync(
            new SubmitFiscalDocumentCommand(
                User.GetTenantId(),
                operation,
                graFlag,
                idempotencyKey,
                HttpContext.TraceIdentifier,
                request.ToCommand()),
            cancellationToken);
        return CreateResult(result);
    }

    private async Task<ActionResult<TransactionResult>> SubmitCancellationAsync(
        string idempotencyKey,
        CancellationRequest request,
        TransactionOperation operation,
        string graFlag,
        CancellationToken cancellationToken)
    {
        if (!ValidateIdempotencyKey(idempotencyKey) || !ModelState.IsValid)
        {
            return UnprocessableEntity(CreateValidationProblem());
        }

        var result = await service.SubmitCancellationAsync(
            new SubmitCancellationCommand(
                User.GetTenantId(),
                operation,
                graFlag,
                idempotencyKey,
                HttpContext.TraceIdentifier,
                request.ToCommand()),
            cancellationToken);
        return CreateResult(result);
    }

    private bool ValidateIdempotencyKey(string idempotencyKey)
    {
        if (!string.IsNullOrWhiteSpace(idempotencyKey) && idempotencyKey.Length is >= 8 and <= 128)
        {
            return true;
        }

        ModelState.AddModelError("Idempotency-Key", "Idempotency-Key must contain 8 to 128 characters.");
        return false;
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

    private ActionResult<TransactionResult> CreateResult(InvoiceSubmissionResult result)
    {
        HttpContext.Items[ExchangeLoggingMiddleware.TransactionIdItemKey] = result.Transaction.TransactionId;
        return result.IsReplay
            ? Ok(result.Transaction)
            : StatusCode(StatusCodes.Status201Created, result.Transaction);
    }
}
