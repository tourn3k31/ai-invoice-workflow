using InvoiceWorkflow.Api.Models;

namespace InvoiceWorkflow.Api.Services;

public interface IInvoiceExtractor
{
    Task<ExtractedInvoice> ExtractAsync(Stream stream, string fileName, CancellationToken cancellationToken);
}
