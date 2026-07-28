using System.ComponentModel.DataAnnotations;

namespace InvoiceWorkflow.Api.Models;

public enum InvoiceStatus
{
    Processing,
    NeedsReview,
    Approved,
    Rejected
}

public class Invoice
{
    public Guid Id { get; set; } = Guid.NewGuid();
    [MaxLength(200)] public string OriginalFileName { get; set; } = string.Empty;
    [MaxLength(200)] public string VendorName { get; set; } = string.Empty;
    [MaxLength(100)] public string InvoiceNumber { get; set; } = string.Empty;
    public DateOnly? InvoiceDate { get; set; }
    public DateOnly? DueDate { get; set; }
    [MaxLength(100)] public string? PurchaseOrderNumber { get; set; }
    public decimal Subtotal { get; set; }
    public decimal Tax { get; set; }
    public decimal Total { get; set; }
    public double Confidence { get; set; }
    public InvoiceStatus Status { get; set; }
    public bool IsDuplicate { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<InvoiceLineItem> LineItems { get; set; } = [];
    public List<ValidationFinding> Findings { get; set; } = [];
}

public class InvoiceLineItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InvoiceId { get; set; }
    [MaxLength(300)] public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal => Math.Round(Quantity * UnitPrice, 2);
}

public class ValidationFinding
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InvoiceId { get; set; }
    [MaxLength(30)] public string Severity { get; set; } = "warning";
    [MaxLength(80)] public string Code { get; set; } = string.Empty;
    [MaxLength(500)] public string Message { get; set; } = string.Empty;
}

public record ExtractedInvoice(
    string VendorName,
    string InvoiceNumber,
    DateOnly? InvoiceDate,
    DateOnly? DueDate,
    string? PurchaseOrderNumber,
    decimal Subtotal,
    decimal Tax,
    decimal Total,
    double Confidence,
    IReadOnlyList<ExtractedLineItem> LineItems);

public record ExtractedLineItem(string Description, decimal Quantity, decimal UnitPrice);

public record UpdateInvoiceRequest(
    string VendorName,
    string InvoiceNumber,
    DateOnly? InvoiceDate,
    DateOnly? DueDate,
    string? PurchaseOrderNumber,
    decimal Subtotal,
    decimal Tax,
    decimal Total,
    List<ExtractedLineItem> LineItems);
