'use client';

/**
 * SmsCostPanel — LS-NOTIF-SMS-013
 *
 * Client component for SMS Cost Analytics interactive sections:
 *   - Cost Trends table (time-series cost points)
 *   - Provider Breakdown table
 *   - Tenant Breakdown table
 *   - Failure/Retry Breakdown table
 *   - Export trigger
 *
 * Receives all data as props from the server page.
 * Tab state is client-side only (no navigation).
 * Export calls the /notifications/v1/admin/sms/costs/export endpoint
 * directly from the browser via fetch (platform_session cookie).
 *
 * Security:
 *   - No credentials, phone numbers, or raw provider payloads are rendered.
 *   - Export is read-only; max 5 000 rows; client downloads as JSON.
 *   - All amounts are operational estimates, not invoice-grade billing data.
 */

import { useState } from 'react';
import type {
  SmsCostTrendResult,
  SmsCostProviderResult,
  SmsCostTenantResult,
  SmsCostFailureResult,
} from '@/lib/sms-cost-api';

// ── Helpers ───────────────────────────────────────────────────────────────────

function fmtCost(v: number | null | undefined, currency: string): string {
  if (v == null) return '—';
  return new Intl.NumberFormat('en-US', {
    style:    'currency',
    currency,
    minimumFractionDigits: 4,
    maximumFractionDigits: 4,
  }).format(v);
}

function fmtNum(v: number): string {
  return v.toLocaleString();
}

function shortDate(iso: string): string {
  return new Date(iso).toLocaleDateString('en-US', {
    month: 'short', day: 'numeric', year: 'numeric',
  });
}

// ── Shared table wrapper ──────────────────────────────────────────────────────

function TableWrap({ children }: { children: React.ReactNode }) {
  return (
    <div className="overflow-x-auto">
      <table className="w-full text-xs text-left text-slate-300">
        {children}
      </table>
    </div>
  );
}

function Th({ children, right }: { children: React.ReactNode; right?: boolean }) {
  return (
    <th className={`px-3 py-2 text-slate-400 font-medium whitespace-nowrap ${right ? 'text-right' : ''}`}>
      {children}
    </th>
  );
}

function Td({ children, right, mono }: { children: React.ReactNode; right?: boolean; mono?: boolean }) {
  return (
    <td className={`px-3 py-2 ${right ? 'text-right' : ''} ${mono ? 'font-mono tabular-nums' : ''}`}>
      {children}
    </td>
  );
}

function ErrorBanner({ msg }: { msg: string }) {
  return (
    <div className="p-3 rounded bg-red-900/30 border border-red-700/40 text-red-300 text-sm">
      {msg}
    </div>
  );
}

// ── Trends table ──────────────────────────────────────────────────────────────

function TrendsTable({
  data,
  currency,
}: {
  data: SmsCostTrendResult | null;
  currency: string;
}) {
  if (!data || data.points.length === 0) {
    return <p className="text-slate-500 text-sm py-4 text-center">No trend data for selected window.</p>;
  }

  return (
    <TableWrap>
      <thead>
        <tr className="border-b border-slate-700/60">
          <Th>Bucket</Th>
          <Th right>Attempts</Th>
          <Th right>Costed</Th>
          <Th right>Total Cost</Th>
          <Th right>Delivered</Th>
          <Th right>Failed</Th>
          <Th right>Retry</Th>
        </tr>
      </thead>
      <tbody>
        {data.points.map((p, i) => (
          <tr key={i} className="border-b border-slate-700/20 hover:bg-slate-700/20">
            <Td>{shortDate(p.bucketStart)}</Td>
            <Td right mono>{fmtNum(p.totalAttempts)}</Td>
            <Td right mono>{fmtNum(p.costedAttempts)}</Td>
            <Td right mono>{fmtCost(p.totalEffectiveCost, currency)}</Td>
            <Td right mono>{fmtCost(p.deliveredCost, currency)}</Td>
            <Td right mono>
              <span className={p.failedCost > 0 ? 'text-red-400' : ''}>
                {fmtCost(p.failedCost, currency)}
              </span>
            </Td>
            <Td right mono>
              <span className={p.retryCost > 0 ? 'text-amber-400' : ''}>
                {fmtCost(p.retryCost, currency)}
              </span>
            </Td>
          </tr>
        ))}
      </tbody>
    </TableWrap>
  );
}

// ── Provider table ────────────────────────────────────────────────────────────

function ProvidersTable({
  data,
  currency,
}: {
  data: SmsCostProviderResult | null;
  currency: string;
}) {
  if (!data || data.items.length === 0) {
    return <p className="text-slate-500 text-sm py-4 text-center">No provider data.</p>;
  }

  return (
    <>
      <div className="flex justify-between items-center mb-3 text-xs text-slate-400">
        <span>{data.totalProviderConfigs} provider config(s)</span>
        <span>Grand total: <span className="text-white font-medium">{fmtCost(data.grandTotalEffectiveCost, currency)}</span></span>
      </div>
      <TableWrap>
        <thead>
          <tr className="border-b border-slate-700/60">
            <Th>Provider</Th>
            <Th>Ownership</Th>
            <Th right>Attempts</Th>
            <Th right>Delivered</Th>
            <Th right>Failed</Th>
            <Th right>Total Cost</Th>
            <Th right>Cost/Delivered</Th>
          </tr>
        </thead>
        <tbody>
          {data.items.map((p, i) => (
            <tr key={i} className="border-b border-slate-700/20 hover:bg-slate-700/20">
              <Td>
                <span className="font-medium text-white">{p.provider}</span>
                {p.providerConfigId && (
                  <span className="ml-1 text-slate-500 text-xs">
                    ({p.providerConfigId.slice(0, 8)}…)
                  </span>
                )}
              </Td>
              <Td>
                <span className={`text-xs px-1.5 py-0.5 rounded ${
                  p.providerOwnershipMode === 'tenant'
                    ? 'bg-blue-500/20 text-blue-300'
                    : p.providerOwnershipMode === 'platform'
                    ? 'bg-purple-500/20 text-purple-300'
                    : 'bg-slate-600/30 text-slate-400'
                }`}>
                  {p.providerOwnershipMode}
                </span>
              </Td>
              <Td right mono>{fmtNum(p.totalAttempts)}</Td>
              <Td right mono><span className="text-emerald-400">{fmtNum(p.deliveredAttempts)}</span></Td>
              <Td right mono><span className={p.failedAttempts > 0 ? 'text-red-400' : ''}>{fmtNum(p.failedAttempts)}</span></Td>
              <Td right mono>{fmtCost(p.totalEffectiveCost, currency)}</Td>
              <Td right mono>{fmtCost(p.costPerDeliveredMessage, currency)}</Td>
            </tr>
          ))}
        </tbody>
      </TableWrap>
    </>
  );
}

// ── Tenant table ──────────────────────────────────────────────────────────────

function TenantsTable({
  data,
  currency,
}: {
  data: SmsCostTenantResult | null;
  currency: string;
}) {
  if (!data || data.items.length === 0) {
    return <p className="text-slate-500 text-sm py-4 text-center">No tenant data.</p>;
  }

  return (
    <>
      <div className="flex justify-between items-center mb-3 text-xs text-slate-400">
        <span>{data.totalTenants} tenant(s)</span>
        <span>Grand total: <span className="text-white font-medium">{fmtCost(data.grandTotalEffectiveCost, currency)}</span></span>
      </div>
      <TableWrap>
        <thead>
          <tr className="border-b border-slate-700/60">
            <Th>Tenant ID</Th>
            <Th right>Attempts</Th>
            <Th right>Delivered</Th>
            <Th right>Failed</Th>
            <Th right>Total Cost</Th>
            <Th right>Cost/Delivered</Th>
            <Th>Last Activity</Th>
          </tr>
        </thead>
        <tbody>
          {data.items.map((t, i) => (
            <tr key={i} className="border-b border-slate-700/20 hover:bg-slate-700/20">
              <Td>
                <span className="font-mono text-xs text-slate-300">
                  {t.tenantId ?? <span className="text-slate-500">—</span>}
                </span>
              </Td>
              <Td right mono>{fmtNum(t.totalAttempts)}</Td>
              <Td right mono><span className="text-emerald-400">{fmtNum(t.deliveredAttempts)}</span></Td>
              <Td right mono><span className={t.failedAttempts > 0 ? 'text-red-400' : ''}>{fmtNum(t.failedAttempts)}</span></Td>
              <Td right mono>{fmtCost(t.totalEffectiveCost, currency)}</Td>
              <Td right mono>{fmtCost(t.costPerDeliveredMessage, currency)}</Td>
              <Td>{t.latestActivityAt ? shortDate(t.latestActivityAt) : '—'}</Td>
            </tr>
          ))}
        </tbody>
      </TableWrap>
    </>
  );
}

// ── Failures table ────────────────────────────────────────────────────────────

function FailuresTable({
  data,
  currency,
}: {
  data: SmsCostFailureResult | null;
  currency: string;
}) {
  if (!data || data.items.length === 0) {
    return <p className="text-slate-500 text-sm py-4 text-center">No failure data for selected window.</p>;
  }

  return (
    <>
      <div className="flex flex-wrap gap-4 mb-3 text-xs text-slate-400">
        <span>Total failed: <span className="text-red-400 font-medium">{fmtNum(data.totalFailedAttempts)}</span></span>
        <span>Failed cost: <span className="text-red-400 font-medium">{fmtCost(data.totalFailedCost, currency)}</span></span>
        <span>Retry cost: <span className="text-amber-400 font-medium">{fmtCost(data.totalRetryCost, currency)}</span></span>
      </div>
      <TableWrap>
        <thead>
          <tr className="border-b border-slate-700/60">
            <Th>Category</Th>
            <Th>Type</Th>
            <Th right>Count</Th>
            <Th right>Costed</Th>
            <Th right>Total Cost</Th>
            <Th>Last Seen</Th>
          </tr>
        </thead>
        <tbody>
          {data.items.map((f, i) => (
            <tr key={i} className="border-b border-slate-700/20 hover:bg-slate-700/20">
              <Td>
                <span className="font-mono text-xs">{f.failureCategory}</span>
              </Td>
              <Td>
                {f.isRetry ? (
                  <span className="text-xs px-1.5 py-0.5 rounded bg-amber-500/20 text-amber-300">retry</span>
                ) : (
                  <span className="text-xs px-1.5 py-0.5 rounded bg-red-500/20 text-red-300">failed</span>
                )}
              </Td>
              <Td right mono>{fmtNum(f.count)}</Td>
              <Td right mono>{fmtNum(f.costedCount)}</Td>
              <Td right mono>
                <span className={f.totalEffectiveCost > 0 ? (f.isRetry ? 'text-amber-400' : 'text-red-400') : ''}>
                  {fmtCost(f.totalEffectiveCost, currency)}
                </span>
              </Td>
              <Td>{f.latestOccurrenceAt ? shortDate(f.latestOccurrenceAt) : '—'}</Td>
            </tr>
          ))}
        </tbody>
      </TableWrap>
    </>
  );
}

// ── Export panel ──────────────────────────────────────────────────────────────

function ExportPanel({ currency }: { currency: string }) {
  const [loading, setLoading] = useState(false);
  const [error,   setError]   = useState<string | null>(null);
  const [rows,    setRows]    = useState<number | null>(null);

  async function handleExport() {
    setLoading(true);
    setError(null);
    setRows(null);
    try {
      const res = await fetch(
        '/api/notifications/v1/admin/sms/costs/export?limit=5000',
        { credentials: 'include' },
      );
      if (!res.ok) throw new Error(`HTTP ${res.status}`);
      const data = await res.json();
      setRows(data.totalRows ?? 0);
      const blob   = new Blob([JSON.stringify(data, null, 2)], { type: 'application/json' });
      const url    = URL.createObjectURL(blob);
      const anchor = document.createElement('a');
      anchor.href     = url;
      anchor.download = `sms-cost-export-${new Date().toISOString().slice(0, 10)}.json`;
      anchor.click();
      URL.revokeObjectURL(url);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Export failed');
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="flex flex-col gap-3">
      <p className="text-sm text-slate-400">
        Download up to 5 000 rows of cost metadata as JSON. Each row is one SMS attempt.
        No credentials, phone numbers, or raw provider payloads are included.
        Amounts are operational estimates in {currency}.
      </p>
      <div className="flex items-center gap-3">
        <button
          onClick={handleExport}
          disabled={loading}
          className="px-4 py-2 rounded bg-blue-600 hover:bg-blue-500 disabled:opacity-50 text-white text-sm font-medium transition-colors"
        >
          {loading ? 'Exporting…' : 'Export JSON'}
        </button>
        {rows != null && (
          <span className="text-xs text-emerald-400">{fmtNum(rows)} rows exported.</span>
        )}
        {error && (
          <span className="text-xs text-red-400">{error}</span>
        )}
      </div>
    </div>
  );
}

// ── Tabs ──────────────────────────────────────────────────────────────────────

type Tab = 'trends' | 'providers' | 'tenants' | 'failures' | 'export';

const TABS: { id: Tab; label: string }[] = [
  { id: 'trends',    label: 'Cost Trends'  },
  { id: 'providers', label: 'By Provider'  },
  { id: 'tenants',   label: 'By Tenant'    },
  { id: 'failures',  label: 'Failures'     },
  { id: 'export',    label: 'Export'       },
];

// ── Main panel ────────────────────────────────────────────────────────────────

interface SmsCostPanelProps {
  trends:         SmsCostTrendResult | null;
  trendsError:    boolean;
  providers:      SmsCostProviderResult | null;
  providersError: boolean;
  tenants:        SmsCostTenantResult | null;
  tenantsError:   boolean;
  failures:       SmsCostFailureResult | null;
  failuresError:  boolean;
  currency:       string;
  windowVal:      string;
  bucket:         string;
}

export function SmsCostPanel({
  trends,
  trendsError,
  providers,
  providersError,
  tenants,
  tenantsError,
  failures,
  failuresError,
  currency,
  windowVal,
  bucket,
}: SmsCostPanelProps) {
  const [activeTab, setActiveTab] = useState<Tab>('trends');

  return (
    <div className="rounded-lg bg-slate-800/60 border border-slate-700/40">
      {/* Tab bar */}
      <div className="flex border-b border-slate-700/60 overflow-x-auto">
        {TABS.map(t => (
          <button
            key={t.id}
            onClick={() => setActiveTab(t.id)}
            className={[
              'px-4 py-3 text-sm font-medium whitespace-nowrap transition-colors',
              activeTab === t.id
                ? 'text-white border-b-2 border-blue-500'
                : 'text-slate-400 hover:text-slate-200',
            ].join(' ')}
          >
            {t.label}
          </button>
        ))}
      </div>

      {/* Tab content */}
      <div className="p-4">
        {activeTab === 'trends' && (
          trendsError
            ? <ErrorBanner msg="Failed to load cost trends." />
            : <TrendsTable data={trends} currency={currency} />
        )}

        {activeTab === 'providers' && (
          providersError
            ? <ErrorBanner msg="Failed to load provider breakdown." />
            : <ProvidersTable data={providers} currency={currency} />
        )}

        {activeTab === 'tenants' && (
          tenantsError
            ? <ErrorBanner msg="Failed to load tenant breakdown." />
            : <TenantsTable data={tenants} currency={currency} />
        )}

        {activeTab === 'failures' && (
          failuresError
            ? <ErrorBanner msg="Failed to load failure cost breakdown." />
            : <FailuresTable data={failures} currency={currency} />
        )}

        {activeTab === 'export' && (
          <ExportPanel currency={currency} />
        )}
      </div>

      {/* Footer note */}
      <div className="px-4 pb-3 text-xs text-slate-600">
        Window: {windowVal} · Bucket: {bucket} · Currency: {currency}
        &nbsp;·&nbsp;All amounts are per-provider configured estimates.
      </div>
    </div>
  );
}
