'use client';

import { useState, useTransition } from 'react';
import type { AuditExport, AuditExportFormat } from '@/types/control-center';

interface ExportResult {
  export: AuditExport;
  error?: never;
}

interface ExportError {
  error:  string;
  export?: never;
}

/**
 * ExportRequestForm — controlled form for creating SynqAudit export jobs.
 *
 * Calls the API route POST /api/synqaudit/exports (which proxies to the audit
 * service via controlCenterServerApi.auditExports.create).
 */
export function ExportRequestForm() {
  const [, startTransition] = useTransition();
  const [result, setResult] = useState<ExportResult | ExportError | null>(null);

  const [format,                 setFormat]                = useState<AuditExportFormat>('Json');
  const [tenantId,               setTenantId]              = useState('');
  const [eventType,              setEventType]             = useState('');
  const [category,               setCategory]              = useState('');
  const [severity,               setSeverity]              = useState('');
  const [correlationId,          setCorrelationId]         = useState('');
  const [dateFrom,               setDateFrom]              = useState('');
  const [dateTo,                 setDateTo]                = useState('');
  const [includeStateSnapshots,  setIncludeStateSnapshots] = useState(false);
  const [includeTags,            setIncludeTags]           = useState(true);
  const [submitting,             setSubmitting]            = useState(false);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setSubmitting(true);
    setResult(null);

    startTransition(() => {});

    try {
      const body = {
        format,
        tenantId:              tenantId              || undefined,
        eventType:             eventType             || undefined,
        category:              category              || undefined,
        severity:              severity              || undefined,
        correlationId:         correlationId         || undefined,
        dateFrom:              dateFrom              || undefined,
        dateTo:                dateTo                || undefined,
        includeStateSnapshots: includeStateSnapshots || undefined,
        includeTags:           includeTags,
      };

      const res = await fetch('/api/synqaudit/exports', {
        method:  'POST',
        headers: { 'Content-Type': 'application/json' },
        body:    JSON.stringify(body),
      });

      if (!res.ok) {
        const err = await res.json().catch(() => ({ message: `HTTP ${res.status}` })) as { message?: string };
        setResult({ error: err.message ?? `HTTP ${res.status}` });
      } else {
        const data = await res.json() as AuditExport;
        setResult({ export: data });
      }
    } catch (err) {
      setResult({ error: err instanceof Error ? err.message : 'Request failed' });
    } finally {
      setSubmitting(false);
    }
  }

  const inputCls = 'w-full h-9 rounded-md border border-gray-300 px-3 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500 bg-white';
  const labelCls = 'block text-xs font-medium text-gray-600 mb-1';

  return (
    <div className="space-y-6">
      <form onSubmit={handleSubmit} className="rounded-lg border border-gray-200 bg-white p-6 space-y-5">
        <h3 className="text-sm font-semibold text-gray-700">New Export Job</h3>

        {/* Format */}
        <div className="flex gap-4">
          {(['Json', 'Csv', 'Ndjson'] as AuditExportFormat[]).map(f => (
            <label key={f} className="flex items-center gap-2 cursor-pointer">
              <input
                type="radio"
                name="format"
                value={f}
                checked={format === f}
                onChange={() => setFormat(f)}
                className="text-indigo-600 focus:ring-indigo-500"
              />
              <span className="text-sm font-medium text-gray-700">{f}</span>
            </label>
          ))}
        </div>

        {/* Filters */}
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
          <div>
            <label className={labelCls}>Tenant ID (optional)</label>
            <input type="text" placeholder="tenant-uuid" value={tenantId} onChange={e => setTenantId(e.target.value)} className={inputCls} />
          </div>
          <div>
            <label className={labelCls}>Event Type (optional)</label>
            <input type="text" placeholder="identity.user.login…" value={eventType} onChange={e => setEventType(e.target.value)} className={inputCls} />
          </div>
          <div>
            <label className={labelCls}>Category</label>
            <select value={category} onChange={e => setCategory(e.target.value)} className={inputCls}>
              <option value="">All categories</option>
              <option value="security">Security</option>
              <option value="access">Access</option>
              <option value="business">Business</option>
              <option value="administrative">Administrative</option>
              <option value="compliance">Compliance</option>
              <option value="dataChange">Data Change</option>
            </select>
          </div>
          <div>
            <label className={labelCls}>Min Severity</label>
            <select value={severity} onChange={e => setSeverity(e.target.value)} className={inputCls}>
              <option value="">Any</option>
              <option value="info">Info</option>
              <option value="warn">Warn</option>
              <option value="error">Error</option>
              <option value="critical">Critical</option>
            </select>
          </div>
          <div>
            <label className={labelCls}>Correlation ID</label>
            <input type="text" placeholder="req-xxxxxxxx" value={correlationId} onChange={e => setCorrelationId(e.target.value)} className={inputCls} />
          </div>
          <div>
            <label className={labelCls}>Date From</label>
            <input type="date" value={dateFrom} onChange={e => setDateFrom(e.target.value)} className={inputCls} />
          </div>
          <div>
            <label className={labelCls}>Date To</label>
            <input type="date" value={dateTo} onChange={e => setDateTo(e.target.value)} className={inputCls} />
          </div>
        </div>

        {/* Options */}
        <div className="flex items-center gap-6">
          <label className="flex items-center gap-2 cursor-pointer">
            <input
              type="checkbox"
              checked={includeStateSnapshots}
              onChange={e => setIncludeStateSnapshots(e.target.checked)}
              className="rounded text-indigo-600 focus:ring-indigo-500"
            />
            <span className="text-sm text-gray-700">Include Before/After state snapshots</span>
          </label>
          <label className="flex items-center gap-2 cursor-pointer">
            <input
              type="checkbox"
              checked={includeTags}
              onChange={e => setIncludeTags(e.target.checked)}
              className="rounded text-indigo-600 focus:ring-indigo-500"
            />
            <span className="text-sm text-gray-700">Include tags</span>
          </label>
        </div>

        <div className="flex items-center gap-3 pt-1">
          <button
            type="submit"
            disabled={submitting}
            className="h-9 px-5 text-sm font-semibold text-white bg-indigo-600 hover:bg-indigo-700 disabled:opacity-50 disabled:cursor-not-allowed rounded-md transition-colors"
          >
            {submitting ? 'Submitting…' : 'Submit Export Job'}
          </button>
        </div>
      </form>

      {/* Result */}
      {result && !result.error && result.export && (
        <ExportJobCard job={result.export} />
      )}

      {result?.error && (
        <div className="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
          {result.error}
        </div>
      )}
    </div>
  );
}

function ExportJobCard({ job }: { job: AuditExport }) {
  const statusColor: Record<string, string> = {
    Pending:    'bg-amber-50  text-amber-700  border-amber-300',
    Processing: 'bg-blue-50   text-blue-700   border-blue-300',
    Completed:  'bg-green-50  text-green-700  border-green-300',
    Failed:     'bg-red-50    text-red-700    border-red-300',
  };
  const cls = statusColor[job.status] ?? 'bg-gray-100 text-gray-600 border-gray-200';

  return (
    <div className="rounded-lg border border-gray-200 bg-white p-5 space-y-3">
      <div className="flex items-center gap-3">
        <h4 className="text-sm font-semibold text-gray-700">Export Job Created</h4>
        <span className={`inline-flex items-center px-2 py-0.5 rounded-full text-[10px] font-semibold uppercase border ${cls}`}>
          {job.status}
        </span>
      </div>

      <div className="grid grid-cols-2 gap-x-6 gap-y-1.5 text-[11px]">
        <Row label="Export ID">{job.exportId}</Row>
        <Row label="Format">{job.format}</Row>
        {job.recordCount !== undefined && <Row label="Records">{job.recordCount.toLocaleString()}</Row>}
        <Row label="Created">{job.createdAtUtc}</Row>
        {job.completedAtUtc && <Row label="Completed">{job.completedAtUtc}</Row>}
        {job.errorMessage   && <Row label="Error">{job.errorMessage}</Row>}
      </div>

      {job.downloadUrl && job.status === 'Completed' && (
        <a
          href={job.downloadUrl}
          target="_blank"
          rel="noopener noreferrer"
          className="inline-flex items-center gap-1.5 text-sm text-indigo-600 hover:text-indigo-800 font-medium"
        >
          <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
            <path strokeLinecap="round" strokeLinejoin="round" d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-4l-4 4m0 0l-4-4m4 4V4" />
          </svg>
          Download Export
        </a>
      )}

      {(job.status === 'Pending' || job.status === 'Processing') && (
        <p className="text-[11px] text-gray-500">
          The export is being processed. Refresh this page or poll{' '}
          <span className="font-mono text-gray-700">GET /audit-service/audit/exports/{job.exportId}</span>{' '}
          to check progress.
        </p>
      )}
    </div>
  );
}

function Row({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div>
      <span className="text-gray-400">{label}: </span>
      <span className="text-gray-700 font-mono">{children}</span>
    </div>
  );
}
