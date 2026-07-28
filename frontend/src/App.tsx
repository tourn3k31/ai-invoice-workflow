import { FormEvent, useEffect, useMemo, useState } from 'react';

const API = 'http://localhost:5074/api';

type Finding = { id: string; severity: string; code: string; message: string };
type LineItem = { id?: string; description: string; quantity: number; unitPrice: number; lineTotal?: number };
type Invoice = {
  id: string; originalFileName: string; vendorName: string; invoiceNumber: string;
  invoiceDate?: string; dueDate?: string; purchaseOrderNumber?: string;
  subtotal: number; tax: number; total: number; confidence: number;
  status: number; isDuplicate: boolean; createdAt: string;
  lineItems: LineItem[]; findings: Finding[];
};
type Metrics = { totalProcessed: number; approved: number; needsReview: number; duplicates: number; averageConfidence: number; totalValue: number };

const statusName = ['Processing', 'Needs review', 'Approved', 'Rejected'];

export default function App() {
  const [invoices, setInvoices] = useState<Invoice[]>([]);
  const [selected, setSelected] = useState<Invoice | null>(null);
  const [metrics, setMetrics] = useState<Metrics | null>(null);
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState('');

  async function refresh() {
    const [invoiceResponse, metricsResponse] = await Promise.all([fetch(`${API}/invoices`), fetch(`${API}/metrics`)]);
    const invoiceData = await invoiceResponse.json();
    setInvoices(invoiceData);
    setMetrics(await metricsResponse.json());
    if (selected) setSelected(invoiceData.find((x: Invoice) => x.id === selected.id) ?? null);
  }

  useEffect(() => { refresh().catch(() => setMessage('Could not reach the backend. Start the .NET API on port 5074.')); }, []);

  async function upload(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = event.currentTarget;
    const input = form.elements.namedItem('invoice') as HTMLInputElement;
    if (!input.files?.[0]) return;
    setBusy(true); setMessage('');
    const body = new FormData(); body.append('file', input.files[0]);
    const response = await fetch(`${API}/invoices/upload`, { method: 'POST', body });
    const result = await response.json();
    if (!response.ok) setMessage(result.message ?? 'Upload failed.');
    else { setSelected(result); setMessage('Invoice processed.'); form.reset(); await refresh(); }
    setBusy(false);
  }

  async function action(name: 'approve' | 'reject') {
    if (!selected) return;
    const response = await fetch(`${API}/invoices/${selected.id}/${name}`, { method: 'POST' });
    const result = await response.json();
    setMessage(response.ok ? `Invoice ${name}d.` : result.message ?? 'Action failed.');
    await refresh();
  }

  const approvedRate = useMemo(() => metrics && metrics.totalProcessed ? Math.round(metrics.approved / metrics.totalProcessed * 100) : 0, [metrics]);

  return <main className="shell">
    <header>
      <div><p className="eyebrow">Portfolio Project</p><h1>AI Invoice Workflow</h1><p>Extract, validate, review, approve, and measure invoice processing.</p></div>
      <form onSubmit={upload} className="upload">
        <input name="invoice" type="file" accept=".txt" required />
        <button disabled={busy}>{busy ? 'Processing…' : 'Upload invoice'}</button>
      </form>
    </header>

    {message && <div className="message">{message}</div>}

    <section className="metrics">
      <Metric label="Processed" value={metrics?.totalProcessed ?? 0} />
      <Metric label="Approved" value={metrics?.approved ?? 0} />
      <Metric label="Needs review" value={metrics?.needsReview ?? 0} />
      <Metric label="Approval rate" value={`${approvedRate}%`} />
      <Metric label="Approved value" value={(metrics?.totalValue ?? 0).toLocaleString('en-US', { style: 'currency', currency: 'USD' })} />
    </section>

    <section className="workspace">
      <aside>
        <h2>Invoices</h2>
        {invoices.length === 0 && <p className="muted">Upload a sample invoice to begin.</p>}
        {invoices.map(invoice => <button className={`invoice-row ${selected?.id === invoice.id ? 'active' : ''}`} key={invoice.id} onClick={() => setSelected(invoice)}>
          <strong>{invoice.vendorName || 'Unknown vendor'}</strong>
          <span>{invoice.invoiceNumber || 'No number'} · {invoice.total.toLocaleString('en-US', { style: 'currency', currency: 'USD' })}</span>
          <small>{statusName[invoice.status]}</small>
        </button>)}
      </aside>

      <article>
        {!selected ? <div className="empty"><h2>No invoice selected</h2><p>Choose an invoice to inspect its extraction and validation results.</p></div> : <>
          <div className="title-row"><div><p className="eyebrow">{selected.originalFileName}</p><h2>{selected.vendorName}</h2></div><span className={`badge s${selected.status}`}>{statusName[selected.status]}</span></div>
          <div className="detail-grid">
            <Field label="Invoice number" value={selected.invoiceNumber} />
            <Field label="Invoice date" value={selected.invoiceDate ?? '—'} />
            <Field label="Due date" value={selected.dueDate ?? '—'} />
            <Field label="PO number" value={selected.purchaseOrderNumber ?? '—'} />
            <Field label="Confidence" value={`${Math.round(selected.confidence * 100)}%`} />
            <Field label="Duplicate" value={selected.isDuplicate ? 'Yes' : 'No'} />
          </div>

          <h3>Line items</h3>
          <table><thead><tr><th>Description</th><th>Qty</th><th>Unit price</th><th>Total</th></tr></thead>
          <tbody>{selected.lineItems.map((item, index) => <tr key={item.id ?? index}><td>{item.description}</td><td>{item.quantity}</td><td>{item.unitPrice.toLocaleString('en-US', { style: 'currency', currency: 'USD' })}</td><td>{(item.quantity * item.unitPrice).toLocaleString('en-US', { style: 'currency', currency: 'USD' })}</td></tr>)}</tbody></table>

          <div className="totals"><span>Subtotal <strong>{selected.subtotal.toLocaleString('en-US', { style: 'currency', currency: 'USD' })}</strong></span><span>Tax <strong>{selected.tax.toLocaleString('en-US', { style: 'currency', currency: 'USD' })}</strong></span><span>Total <strong>{selected.total.toLocaleString('en-US', { style: 'currency', currency: 'USD' })}</strong></span></div>

          <h3>Validation</h3>
          {selected.findings.length === 0 ? <p className="success">No validation findings.</p> : selected.findings.map(f => <div className={`finding ${f.severity}`} key={f.id}><strong>{f.code}</strong><span>{f.message}</span></div>)}

          <div className="actions"><button className="secondary" onClick={() => action('reject')}>Reject</button><button onClick={() => action('approve')}>Approve</button></div>
        </>}
      </article>
    </section>
  </main>;
}

function Metric({ label, value }: { label: string; value: string | number }) { return <div className="metric"><span>{label}</span><strong>{value}</strong></div>; }
function Field({ label, value }: { label: string; value: string }) { return <div className="field"><span>{label}</span><strong>{value}</strong></div>; }
