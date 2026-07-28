# AI Invoice Workflow

A portfolio-ready invoice-processing application that demonstrates a real business workflow:

- Upload invoice text files
- Extract structured invoice fields
- Validate totals and required fields
- Detect duplicate invoices
- Route low-confidence invoices for human review
- Edit and approve extracted results
- Export approved invoice data as JSON
- Track processing statistics

The repository runs without a paid AI account by using a deterministic demo extractor. The backend uses an extractor interface so a hosted document-AI or multimodal model can be added later without changing the UI or workflow.

## Stack

- React + TypeScript + Vite
- ASP.NET Core 8 Minimal API
- Entity Framework Core + SQLite
- xUnit tests

## Run locally

### Prerequisites

- Node.js 20+
- .NET 8 SDK

### Backend

```bash
cd backend
dotnet restore
dotnet run
```

Backend: `http://localhost:5074`
Swagger: `http://localhost:5074/swagger`

### Frontend

```bash
cd frontend
npm install
npm run dev
```

Frontend: `http://localhost:5173`

## Try the app

Upload one of the files in `/samples`.

- `invoice-acme-1001.txt` is a valid invoice.
- `invoice-acme-1001-duplicate.txt` should be identified as a duplicate after the first file is processed.
- `invoice-bad-total.txt` contains an incorrect total and should require review.

## Invoice text format

```text
Vendor: Acme Office Supply
Invoice Number: INV-1001
Invoice Date: 2026-07-15
Due Date: 2026-08-14
PO Number: PO-5501
Item: Printer Paper | 5 | 8.00
Item: Ink Cartridge | 2 | 25.00
Subtotal: 90.00
Tax: 8.10
Total: 98.10
```

## Portfolio talking points

- Designed an end-to-end document-processing workflow rather than a single AI API call.
- Added deterministic financial validation and duplicate detection around model output.
- Implemented confidence-based human review and correction tracking.
- Persisted invoices, line items, validation findings, status, and audit timestamps.
- Added automated tests for total validation and duplicate-key behavior.

## Recommended production upgrades

1. Replace `DemoInvoiceExtractor` with a hosted document-AI implementation.
2. Store uploaded files in cloud object storage.
3. Add authentication and role-based approval.
4. Add malware scanning and file-type verification.
5. Add structured logs, tracing, retries, timeout policies, and usage-cost tracking.
6. Add a labeled evaluation set and an extraction-accuracy dashboard.
