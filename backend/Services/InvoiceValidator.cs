using InvoiceWorkflow.Api.Models;

namespace InvoiceWorkflow.Api.Services;

public sealed class InvoiceValidator
{
    public List<ValidationFinding> Validate(Invoice invoice)
    {
        var findings = new List<ValidationFinding>();

        void Add(string severity, string code, string message) => findings.Add(new ValidationFinding
        {
            InvoiceId = invoice.Id,
            Severity = severity,
            Code = code,
            Message = message
        });

        if (string.IsNullOrWhiteSpace(invoice.VendorName)) Add("error", "MISSING_VENDOR", "Vendor name is required.");
        if (string.IsNullOrWhiteSpace(invoice.InvoiceNumber)) Add("error", "MISSING_INVOICE_NUMBER", "Invoice number is required.");
        if (invoice.InvoiceDate is null) Add("error", "MISSING_INVOICE_DATE", "Invoice date is required.");
        if (invoice.Total <= 0) Add("error", "INVALID_TOTAL", "Total must be greater than zero.");

        var calculatedLineSubtotal = Math.Round(invoice.LineItems.Sum(x => x.Quantity * x.UnitPrice), 2);
        if (invoice.LineItems.Count > 0 && Math.Abs(calculatedLineSubtotal - invoice.Subtotal) > 0.01m)
            Add("error", "LINE_ITEMS_MISMATCH", $"Line items total {calculatedLineSubtotal:C} but subtotal is {invoice.Subtotal:C}.");

        var calculatedTotal = Math.Round(invoice.Subtotal + invoice.Tax, 2);
        if (Math.Abs(calculatedTotal - invoice.Total) > 0.01m)
            Add("error", "TOTAL_MISMATCH", $"Subtotal plus tax is {calculatedTotal:C}, not {invoice.Total:C}.");

        if (invoice.DueDate is not null && invoice.InvoiceDate is not null && invoice.DueDate < invoice.InvoiceDate)
            Add("warning", "DUE_DATE_BEFORE_INVOICE", "Due date occurs before the invoice date.");

        if (invoice.Confidence < 0.80)
            Add("warning", "LOW_CONFIDENCE", $"Extraction confidence is {invoice.Confidence:P0}; human review is recommended.");

        if (invoice.IsDuplicate)
            Add("error", "POSSIBLE_DUPLICATE", "An invoice with the same vendor and invoice number already exists.");

        return findings;
    }
}
