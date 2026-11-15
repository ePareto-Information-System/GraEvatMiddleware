namespace GraEvatMiddleware.Application.Invoices;

public interface IFiscalTransactionService
{
    Task<InvoiceSubmissionResult> SubmitDocumentAsync(
        SubmitFiscalDocumentCommand command,
        CancellationToken cancellationToken = default);

    Task<InvoiceSubmissionResult> SubmitCancellationAsync(
        SubmitCancellationCommand command,
        CancellationToken cancellationToken = default);
}
