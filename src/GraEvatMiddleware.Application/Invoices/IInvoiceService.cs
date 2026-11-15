namespace GraEvatMiddleware.Application.Invoices;

public interface IInvoiceService
{
    Task<InvoiceSubmissionResult> SubmitAsync(SubmitInvoiceCommand command, CancellationToken cancellationToken = default);
}
