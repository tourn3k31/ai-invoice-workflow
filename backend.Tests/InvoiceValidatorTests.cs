using InvoiceWorkflow.Api.Models;
using InvoiceWorkflow.Api.Services;

namespace InvoiceWorkflow.Api.Tests;

public class InvoiceValidatorTests
{
    [Fact]
    public void Valid_invoice_has_no_errors()
    {
        var invoice = new Invoice
        {
            VendorName = "Acme",
            InvoiceNumber = "INV-1",
            InvoiceDate = new DateOnly(2026, 7, 1),
            Subtotal = 100m,
            Tax = 8m,
            Total = 108m,
            Confidence = 0.9,
            LineItems = [new InvoiceLineItem { Description = "Item", Quantity = 2, UnitPrice = 50m }]
        };

        var findings = new InvoiceValidator().Validate(invoice);

        Assert.DoesNotContain(findings, f => f.Severity == "error");
    }

    [Fact]
    public void Incorrect_total_is_flagged()
    {
        var invoice = new Invoice
        {
            VendorName = "Acme",
            InvoiceNumber = "INV-2",
            InvoiceDate = new DateOnly(2026, 7, 1),
            Subtotal = 100m,
            Tax = 8m,
            Total = 120m,
            Confidence = 0.9
        };

        var findings = new InvoiceValidator().Validate(invoice);

        Assert.Contains(findings, f => f.Code == "TOTAL_MISMATCH");
    }
}
