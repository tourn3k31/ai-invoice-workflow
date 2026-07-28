using System.Globalization;
using System.Text.RegularExpressions;
using InvoiceWorkflow.Api.Models;

namespace InvoiceWorkflow.Api.Services;

public sealed class DemoInvoiceExtractor : IInvoiceExtractor
{
    public async Task<ExtractedInvoice> ExtractAsync(Stream stream, string fileName, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream);
        var text = await reader.ReadToEndAsync(cancellationToken);

        string Get(string label)
        {
            var match = Regex.Match(text, $@"^{Regex.Escape(label)}\s*:\s*(.+)$", RegexOptions.Multiline | RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value.Trim() : string.Empty;
        }

        decimal Money(string label) => decimal.TryParse(Get(label), NumberStyles.Number, CultureInfo.InvariantCulture, out var value) ? value : 0m;
        DateOnly? Date(string label) => DateOnly.TryParse(Get(label), CultureInfo.InvariantCulture, DateTimeStyles.None, out var value) ? value : null;

        var lineItems = new List<ExtractedLineItem>();
        foreach (Match match in Regex.Matches(text, @"^Item\s*:\s*(.*?)\s*\|\s*([0-9.]+)\s*\|\s*([0-9.]+)\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase))
        {
            if (decimal.TryParse(match.Groups[2].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var quantity) &&
                decimal.TryParse(match.Groups[3].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var unitPrice))
            {
                lineItems.Add(new ExtractedLineItem(match.Groups[1].Value.Trim(), quantity, unitPrice));
            }
        }

        var requiredFound = new[] { Get("Vendor"), Get("Invoice Number"), Get("Invoice Date"), Get("Total") }.Count(x => !string.IsNullOrWhiteSpace(x));
        var confidence = Math.Round(0.55 + requiredFound * 0.1 + Math.Min(lineItems.Count, 3) * 0.02, 2);

        return new ExtractedInvoice(
            Get("Vendor"),
            Get("Invoice Number"),
            Date("Invoice Date"),
            Date("Due Date"),
            NullIfEmpty(Get("PO Number")),
            Money("Subtotal"),
            Money("Tax"),
            Money("Total"),
            Math.Min(confidence, 0.95),
            lineItems);
    }

    private static string? NullIfEmpty(string value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
