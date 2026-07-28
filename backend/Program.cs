using InvoiceWorkflow.Api.Data;
using InvoiceWorkflow.Api.Models;
using InvoiceWorkflow.Api.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=invoices.db"));

builder.Services.AddScoped<IInvoiceExtractor, DemoInvoiceExtractor>();
builder.Services.AddSingleton<InvoiceValidator>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy
            .WithOrigins(
                "http://localhost:5173",
                "https://kind-water-00536e710.7.azurestaticapps.net"
            )
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors("Frontend");

app.MapGet("/api/invoices", async (AppDbContext db) =>
{
    var invoices = await db.Invoices
        .Include(i => i.LineItems)
        .Include(i => i.Findings)
        .ToListAsync();

    return Results.Ok(
        invoices.OrderByDescending(i => i.CreatedAt)
    );
});

app.MapGet("/api/invoices/{id:guid}", async (
    Guid id,
    AppDbContext db) =>
{
    var invoice = await db.Invoices
        .Include(i => i.LineItems)
        .Include(i => i.Findings)
        .FirstOrDefaultAsync(i => i.Id == id);

    return invoice is null
        ? Results.NotFound()
        : Results.Ok(invoice);
});

app.MapPost("/api/invoices/upload", async (
    IFormFile file,
    AppDbContext db,
    IInvoiceExtractor extractor,
    InvoiceValidator validator,
    CancellationToken ct) =>
{
    if (file.Length == 0)
    {
        return Results.BadRequest(new
        {
            message = "File is empty."
        });
    }

    if (file.Length > 5_000_000)
    {
        return Results.BadRequest(new
        {
            message = "File exceeds the 5 MB demo limit."
        });
    }

    if (!Path.GetExtension(file.FileName)
        .Equals(".txt", StringComparison.OrdinalIgnoreCase))
    {
        return Results.BadRequest(new
        {
            message =
                "This starter accepts .txt invoices. Add a production extractor for PDF and image files."
        });
    }

    await using var stream = file.OpenReadStream();

    var extracted = await extractor.ExtractAsync(
        stream,
        file.FileName,
        ct
    );

    var duplicate = await db.Invoices.AnyAsync(
        i =>
            i.VendorName == extracted.VendorName &&
            i.InvoiceNumber == extracted.InvoiceNumber &&
            i.Status != InvoiceStatus.Rejected,
        ct
    );

    var invoice = new Invoice
    {
        OriginalFileName = Path.GetFileName(file.FileName),
        VendorName = extracted.VendorName,
        InvoiceNumber = extracted.InvoiceNumber,
        InvoiceDate = extracted.InvoiceDate,
        DueDate = extracted.DueDate,
        PurchaseOrderNumber = extracted.PurchaseOrderNumber,
        Subtotal = extracted.Subtotal,
        Tax = extracted.Tax,
        Total = extracted.Total,
        Confidence = extracted.Confidence,
        IsDuplicate = duplicate,
        Status = InvoiceStatus.Processing
    };

    invoice.LineItems = extracted.LineItems
        .Select(x => new InvoiceLineItem
        {
            InvoiceId = invoice.Id,
            Description = x.Description,
            Quantity = x.Quantity,
            UnitPrice = x.UnitPrice
        })
        .ToList();

    invoice.Findings = validator.Validate(invoice);

    invoice.Status =
        invoice.Findings.Any(f => f.Severity == "error") ||
        invoice.Confidence < 0.80
            ? InvoiceStatus.NeedsReview
            : InvoiceStatus.Approved;

    db.Invoices.Add(invoice);
    await db.SaveChangesAsync(ct);

    return Results.Created(
        $"/api/invoices/{invoice.Id}",
        invoice
    );
}).DisableAntiforgery();

app.MapPut("/api/invoices/{id:guid}", async (
    Guid id,
    UpdateInvoiceRequest request,
    AppDbContext db,
    InvoiceValidator validator) =>
{
    var invoice = await db.Invoices
        .Include(i => i.LineItems)
        .Include(i => i.Findings)
        .FirstOrDefaultAsync(i => i.Id == id);

    if (invoice is null)
    {
        return Results.NotFound();
    }

    invoice.VendorName = request.VendorName.Trim();
    invoice.InvoiceNumber = request.InvoiceNumber.Trim();
    invoice.InvoiceDate = request.InvoiceDate;
    invoice.DueDate = request.DueDate;

    invoice.PurchaseOrderNumber =
        string.IsNullOrWhiteSpace(request.PurchaseOrderNumber)
            ? null
            : request.PurchaseOrderNumber.Trim();

    invoice.Subtotal = request.Subtotal;
    invoice.Tax = request.Tax;
    invoice.Total = request.Total;
    invoice.UpdatedAt = DateTimeOffset.UtcNow;

    db.InvoiceLineItems.RemoveRange(invoice.LineItems);
    db.ValidationFindings.RemoveRange(invoice.Findings);

    invoice.LineItems = request.LineItems
        .Select(x => new InvoiceLineItem
        {
            InvoiceId = invoice.Id,
            Description = x.Description,
            Quantity = x.Quantity,
            UnitPrice = x.UnitPrice
        })
        .ToList();

    invoice.Findings = validator.Validate(invoice);
    invoice.Status = InvoiceStatus.NeedsReview;

    await db.SaveChangesAsync();

    return Results.Ok(invoice);
});

app.MapPost("/api/invoices/{id:guid}/approve", async (
    Guid id,
    AppDbContext db) =>
{
    var invoice = await db.Invoices
        .Include(i => i.Findings)
        .FirstOrDefaultAsync(i => i.Id == id);

    if (invoice is null)
    {
        return Results.NotFound();
    }

    if (invoice.Findings.Any(f => f.Severity == "error"))
    {
        return Results.BadRequest(new
        {
            message = "Resolve validation errors before approval."
        });
    }

    invoice.Status = InvoiceStatus.Approved;
    invoice.UpdatedAt = DateTimeOffset.UtcNow;

    await db.SaveChangesAsync();

    return Results.Ok(invoice);
});

app.MapPost("/api/invoices/{id:guid}/reject", async (
    Guid id,
    AppDbContext db) =>
{
    var invoice = await db.Invoices.FindAsync(id);

    if (invoice is null)
    {
        return Results.NotFound();
    }

    invoice.Status = InvoiceStatus.Rejected;
    invoice.UpdatedAt = DateTimeOffset.UtcNow;

    await db.SaveChangesAsync();

    return Results.Ok(invoice);
});

app.MapGet("/api/metrics", async (AppDbContext db) =>
{
    var invoices = await db.Invoices
        .AsNoTracking()
        .ToListAsync();

    return Results.Ok(new
    {
        totalProcessed = invoices.Count,

        approved = invoices.Count(
            i => i.Status == InvoiceStatus.Approved
        ),

        needsReview = invoices.Count(
            i => i.Status == InvoiceStatus.NeedsReview
        ),

        duplicates = invoices.Count(
            i => i.IsDuplicate
        ),

        averageConfidence =
            invoices.Count == 0
                ? 0
                : Math.Round(
                    invoices.Average(i => i.Confidence),
                    2
                ),

        totalValue = invoices
            .Where(i => i.Status == InvoiceStatus.Approved)
            .Sum(i => i.Total)
    });
});

app.Run();

public partial class Program;