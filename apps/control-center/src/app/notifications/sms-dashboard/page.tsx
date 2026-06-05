/**
 * /notifications/sms-dashboard — LS-NOTIF-SMS-009
 *
 * Control Center SMS Dashboard — platform-admin read-only view.
 *
 * Consumes five Notification Service aggregate APIs from LS-NOTIF-SMS-008:
 *   /admin/sms/dashboard/summary   — KPI cards
 *   /admin/sms/dashboard/trends    — time-series chart
 *   /admin/sms/dashboard/failures  — failure breakdown table
 *   /admin/sms/dashboard/tenants   — tenant activity table
 *   /admin/sms/dashboard/providers — provider/config table
 *
 * Security:
 *   - requirePlatformAdmin() gates the entire page.
 *   - No credentials, SettingsJson, or phone numbers are rendered.
 *   - No action on this page triggers sends, retries, reconciliation, or
 *     provider calls — dashboard is entirely read-only.
 *
 * Filtering (URL params):
 *   ?window=7d|30d|90d       — date range preset (default: 30d)
 *   ?bucket=hour|day|week    — trend bucket size (default: day)
 *   ?ownership=all|tenant|platform — providerOwnershipMode filter (default: all)
 *
 * Each section loads independently via Promise.allSettled.
 * A section-level error banner is shown for each failed call; other
 * sections continue to render normally.
 */

import { requirePlatformAdmin }  from '@/lib/auth-guards';
import { CCShell }               from '@/components/shell/cc-shell';
import { smsDashboardApi }       from '@/lib/sms-dashboard-api';
import { SmsTrendChart }         from '@/components/notifications/sms-trend-chart';
import type {
  SmsDashboardSummary,
  SmsDashboardTrendResult,
  SmsDashboardFailureResult,
  SmsDashboardTenantResult,
  SmsDashboardProviderResult,
} from '@/lib/sms-dashboard-api';

export const dynamic = 'force-dynamic';

// ── Helpers ───────────────────────────────────────────────────────────────────

const WINDOW_OPTIONS = [
  { value: '7d',  label: 'Last 7 days',  days: 7  },
  { value: '30d', label: 'Last 30 days', days: 30 },
  { value: '90d', label: 'Last 90 days', days: 90 },
] as const;

const BUCKET_OPTIONS = [
  { value: 'hour', label: 'Hourly' },
  { value: 'day',  label: 'Daily'  },
  { value: 'week', label: 'Weekly' },
] as const;

const OWNERSHIP_OPTIONS = [
  { value: 'all',      label: 'All'      },
  { value: 'tenant',   label: 'Tenant'   },
  { value: 'platform', label: 'Platform' },
] as const;

function computeWindow(windowKey: string): { from: string; to: string } {
  const days = WINDOW_OPTIONS.find(o => o.value === windowKey)?.days ?? 30;
  const to   = new Date();
  const from = new Date(to.getTime() - days * 86_400_000);
  return { from: from.toISOString(), to: to.toISOString() };
}

function fmtN(n: number): string { return n.toLocaleString(); }

function fmtUtc(iso: string): string {
  try {
    return new Date(iso).toLocaleString('en-US', { timeZone: 'UTC', hour12: false });
  } catch {
    return iso;
  }
}

function fmtDate(iso: string): string {
  try {
    return new Date(iso).toLocaleDateString('en-US', { timeZone: 'UTC', month: 'short', day: 'numeric', year: 'numeric' });
  } catch {
    return iso;
  }
}

function pct(num: number, denom: number): string {
  if (denom === 0) return '—';
  return `${((num / denom) * 100).toFixed(1)}%`;
}

// ── Pill filter ───────────────────────────────────────────────────────────────

function FilterPill({
  label, value, current, paramName, otherParams,
}: {
  label: string;
  value: string;
  current: string;
  paramName: string;
  otherParams: string;
}) {
  const active = value === current;
  const qs = `${paramName}=${value}${otherParams ? `&${otherParams}` : ''}`;
  return (
    <a
      href={`?${qs}`}
      className={`px-3 py-1 rounded text-sm font-medium transition-colors ${
        active
          ? 'bg-gray-900 text-white'
          : 'bg-white border border-gray-200 text-gray-600 hover:border-gray-300'
      }`}
    >
      {label}
    </a>
  );
}

// ── Section error ─────────────────────────────────────────────────────────────

function SectionError({ label, message }: { label: string; message: string }) {
  return (
    <div className="rounded-lg border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-700">
      <i className="ri-error-warning-line mr-2" />
      <strong>{label}:</strong> {message}
    </div>
  );
}

// ── Empty state ───────────────────────────────────────────────────────────────

function EmptyState({ message }: { message: string }) {
  return (
    <div className="rounded-lg border border-gray-200 bg-white px-6 py-10 text-center">
      <i className="ri-inbox-line text-3xl text-gray-300 mb-2 block" />
      <p className="text-sm text-gray-500">{message}</p>
    </div>
  );
}

// ── Section wrapper ───────────────────────────────────────────────────────────

function Section({ icon, title, children, link }: {
  icon: string;
  title: string;
  children: React.ReactNode;
  link?: { href: string; label: string };
}) {
  return (
    <section>
      <div className="flex items-center justify-between mb-3">
        <h2 className="text-base font-semibold text-gray-800 flex items-center gap-2">
          <i className={`${icon} text-indigo-500`} />
          {title}
        </h2>
        {link && (
          <a href={link.href} className="text-xs text-indigo-600 hover:text-indigo-800 hover:underline font-medium">
            {link.label} →
          </a>
        )}
      </div>
      {children}
    </section>
  );
}

// ── KPI card ──────────────────────────────────────────────────────────────────

function KpiCard({ label, value, color }: { label: string; value: string; color: string }) {
  return (
    <div className="rounded-lg border border-gray-200 bg-white px-4 py-3">
      <p className="text-xs text-gray-500 mb-1 truncate">{label}</p>
      <p className={`text-2xl font-bold tabular-nums ${color}`}>{value}</p>
    </div>
  );
}

// ── Status badge ──────────────────────────────────────────────────────────────

function OwnershipBadge({ mode }: { mode: string }) {
  const cfg: Record<string, string> = {
    tenant:   'bg-indigo-50 text-indigo-700 border-indigo-200',
    platform: 'bg-sky-50 text-sky-700 border-sky-200',
  };
  return (
    <span className={`inline-flex items-center px-2 py-0.5 rounded-full text-[11px] font-semibold border ${cfg[mode] ?? 'bg-gray-50 text-gray-600 border-gray-200'}`}>
      {mode}
    </span>
  );
}

// ── Page ──────────────────────────────────────────────────────────────────────

interface PageProps {
  searchParams: Promise<{
    window?:    string;
    bucket?:    string;
    ownership?: string;
  }>;
}

export default async function SmsDashboardPage({ searchParams }: PageProps) {
  const session = await requirePlatformAdmin();
  const sp      = await searchParams;

  const windowKey  = WINDOW_OPTIONS.some(o => o.value === sp.window)  ? sp.window!  : '30d';
  const bucketKey  = BUCKET_OPTIONS.some(o => o.value === sp.bucket)  ? sp.bucket!  : 'day';
  const ownership  = OWNERSHIP_OPTIONS.some(o => o.value === sp.ownership && sp.ownership !== 'all')
    ? sp.ownership!
    : undefined;

  const { from, to } = computeWindow(windowKey);

  const baseQuery = {
    from,
    to,
    ...(ownership ? { providerOwnershipMode: ownership } : {}),
  };

  // Build passthrough query string for filter pills
  const otherWindow    = `bucket=${bucketKey}${ownership ? `&ownership=${sp.ownership}` : ''}`;
  const otherBucket    = `window=${windowKey}${ownership ? `&ownership=${sp.ownership}` : ''}`;
  const otherOwnership = `window=${windowKey}&bucket=${bucketKey}`;

  // ── Fetch all sections in parallel — failures are isolated per section ────
  const [summaryRes, trendsRes, failuresRes, tenantsRes, providersRes] = await Promise.allSettled([
    smsDashboardApi.getSummary(baseQuery),
    smsDashboardApi.getTrends({ ...baseQuery, bucket: bucketKey }),
    smsDashboardApi.getFailures(baseQuery),
    smsDashboardApi.getTenants(baseQuery),
    smsDashboardApi.getProviders(baseQuery),
  ]);

  const summary:   SmsDashboardSummary | null         = summaryRes.status   === 'fulfilled' ? summaryRes.value   : null;
  const trends:    SmsDashboardTrendResult | null     = trendsRes.status    === 'fulfilled' ? trendsRes.value    : null;
  const failures:  SmsDashboardFailureResult | null   = failuresRes.status  === 'fulfilled' ? failuresRes.value  : null;
  const tenants:   SmsDashboardTenantResult | null    = tenantsRes.status   === 'fulfilled' ? tenantsRes.value   : null;
  const providers: SmsDashboardProviderResult | null  = providersRes.status === 'fulfilled' ? providersRes.value : null;

  const summaryErr   = summaryRes.status   === 'rejected' ? String((summaryRes.reason   as Error).message ?? summaryRes.reason)   : null;
  const trendsErr    = trendsRes.status    === 'rejected' ? String((trendsRes.reason    as Error).message ?? trendsRes.reason)    : null;
  const failuresErr  = failuresRes.status  === 'rejected' ? String((failuresRes.reason  as Error).message ?? failuresRes.reason)  : null;
  const tenantsErr   = tenantsRes.status   === 'rejected' ? String((tenantsRes.reason   as Error).message ?? tenantsRes.reason)   : null;
  const providersErr = providersRes.status === 'rejected' ? String((providersRes.reason as Error).message ?? providersRes.reason) : null;

  const windowLabel = WINDOW_OPTIONS.find(o => o.value === windowKey)?.label ?? 'Last 30 days';

  return (
    <CCShell userEmail={session.email}>
      <div className="px-6 py-6 max-w-7xl mx-auto space-y-8">

        {/* ── Header ─────────────────────────────────────────────────────── */}
        <div className="flex flex-col sm:flex-row sm:items-start sm:justify-between gap-4">
          <div>
            <h1 className="text-xl font-semibold text-gray-900">SMS Dashboard</h1>
            <p className="text-sm text-gray-500 mt-0.5">
              Read-only SMS delivery, reconciliation, and provider metrics.{' '}
              <span className="text-gray-400">Notification Service is the source of truth — no data is stored here.</span>
            </p>
          </div>
          {/* Date window pills */}
          <div className="flex items-center gap-2 flex-wrap shrink-0">
            {WINDOW_OPTIONS.map(opt => (
              <FilterPill
                key={opt.value}
                label={opt.label}
                value={opt.value}
                current={windowKey}
                paramName="window"
                otherParams={otherWindow}
              />
            ))}
          </div>
        </div>

        {/* ── Secondary filters ───────────────────────────────────────────── */}
        <div className="flex flex-wrap items-center gap-4">
          <div className="flex items-center gap-2">
            <span className="text-xs text-gray-500 font-medium uppercase tracking-wide">Ownership</span>
            {OWNERSHIP_OPTIONS.map(opt => (
              <FilterPill
                key={opt.value}
                label={opt.label}
                value={opt.value}
                current={sp.ownership ?? 'all'}
                paramName="ownership"
                otherParams={otherOwnership}
              />
            ))}
          </div>
        </div>

        {/* ── KPI Summary ─────────────────────────────────────────────────── */}
        <Section icon="ri-bar-chart-2-line" title={`Delivery Overview — ${windowLabel}`}>
          {summaryErr ? (
            <SectionError label="Summary unavailable" message={summaryErr} />
          ) : summary ? (
            <div className="space-y-4">
              {/* Row 1: delivery counts */}
              <div>
                <p className="text-[11px] font-semibold text-gray-400 uppercase tracking-wide mb-2">Delivery</p>
                <div className="grid grid-cols-2 sm:grid-cols-4 lg:grid-cols-6 gap-3">
                  <KpiCard label="Total Attempts"  value={fmtN(summary.totalAttempts)}  color="text-gray-900"     />
                  <KpiCard label="Sent"             value={fmtN(summary.sentCount)}       color="text-sky-700"      />
                  <KpiCard label="Delivered"        value={fmtN(summary.deliveredCount)}  color="text-emerald-700"  />
                  <KpiCard label="Failed"           value={fmtN(summary.failedCount)}     color="text-red-700"      />
                  <KpiCard label="Dead Letter"      value={fmtN(summary.deadLetterCount)} color="text-red-900"      />
                  <KpiCard label="Pending / Other"  value={fmtN(summary.pendingCount + summary.processingCount + summary.sendingCount + summary.retryingCount)} color="text-amber-700" />
                </div>
              </div>

              {/* Row 2: delivery rate + cardinality */}
              <div className="grid grid-cols-2 sm:grid-cols-4 lg:grid-cols-6 gap-3">
                <KpiCard label="Delivery Rate"     value={pct(summary.deliveredCount, summary.totalAttempts)}  color="text-emerald-700" />
                <KpiCard label="Tenant-Owned"      value={fmtN(summary.tenantOwnedCount)}    color="text-indigo-700"  />
                <KpiCard label="Platform-Owned"    value={fmtN(summary.platformOwnedCount)}  color="text-sky-700"     />
                <KpiCard label="Unique Tenants"    value={fmtN(summary.uniqueTenantCount)}   color="text-gray-700"    />
                <KpiCard label="Unique Providers"  value={fmtN(summary.uniqueProviderCount)} color="text-gray-700"    />
                <KpiCard label="Provider Configs"  value={fmtN(summary.uniqueProviderConfigCount)} color="text-gray-700" />
              </div>

              {/* Row 3: reconciliation */}
              <div>
                <p className="text-[11px] font-semibold text-gray-400 uppercase tracking-wide mb-2">Reconciliation</p>
                <div className="grid grid-cols-2 sm:grid-cols-4 lg:grid-cols-7 gap-3">
                  <KpiCard label="Reconciled"          value={fmtN(summary.reconciledTotal)}               color="text-indigo-700"  />
                  <KpiCard label="Never Reconciled"    value={fmtN(summary.neverReconciled)}               color="text-amber-700"   />
                  <KpiCard label="Updated"             value={fmtN(summary.reconciliationUpdated)}         color="text-emerald-700" />
                  <KpiCard label="No Change"           value={fmtN(summary.reconciliationNoChange)}        color="text-gray-600"    />
                  <KpiCard label="Lookup Failed"       value={fmtN(summary.reconciliationLookupFailed)}    color="text-red-700"     />
                  <KpiCard label="Skipped"             value={fmtN(summary.reconciliationSkipped)}         color="text-gray-500"    />
                  <KpiCard label="Config Failed"       value={fmtN(summary.reconciliationProviderConfigFailed)} color="text-orange-700" />
                </div>
              </div>

              {/* Window bounds */}
              {(summary.earliestAt || summary.latestAt) && (
                <p className="text-[11px] text-gray-400">
                  Window: {summary.earliestAt ? fmtDate(summary.earliestAt) : '—'} → {summary.latestAt ? fmtDate(summary.latestAt) : '—'} UTC
                </p>
              )}
            </div>
          ) : (
            <EmptyState message="No SMS activity in this period." />
          )}
        </Section>

        {/* ── Trend Chart ─────────────────────────────────────────────────── */}
        <Section icon="ri-line-chart-line" title="Delivery Trend">
          {/* Bucket filter pills */}
          <div className="flex items-center gap-2 mb-4">
            <span className="text-xs text-gray-500 font-medium">Bucket:</span>
            {BUCKET_OPTIONS.map(opt => (
              <FilterPill
                key={opt.value}
                label={opt.label}
                value={opt.value}
                current={bucketKey}
                paramName="bucket"
                otherParams={otherBucket}
              />
            ))}
          </div>

          {trendsErr ? (
            <SectionError label="Trend data unavailable" message={trendsErr} />
          ) : trends ? (
            <div className="rounded-lg border border-gray-200 bg-white p-4">
              <SmsTrendChart points={trends.points} bucket={trends.bucket} />
              {trends.points.length > 0 && (
                <p className="text-[10px] text-gray-400 mt-2">
                  {trends.points.length} {trends.bucket}-bucket{trends.points.length !== 1 ? 's' : ''} ·
                  {fmtDate(trends.windowFrom)} → {fmtDate(trends.windowTo)} UTC ·
                  Reconciliation trend is based on CreatedAt bucket, not LastReconciledAt.
                </p>
              )}
            </div>
          ) : (
            <EmptyState message="No trend data for this period." />
          )}
        </Section>

        {/* ── Failure Breakdown ───────────────────────────────────────────── */}
        <Section icon="ri-error-warning-line" title="Failure Breakdown"
          link={{ href: '/notifications/log', label: 'View delivery log' }}>
          {failuresErr ? (
            <SectionError label="Failure data unavailable" message={failuresErr} />
          ) : failures && failures.items.length > 0 ? (
            <div className="rounded-lg border border-gray-200 bg-white overflow-hidden">
              <div className="px-4 py-3 border-b border-gray-100 bg-gray-50 flex items-center justify-between">
                <span className="text-sm font-semibold text-gray-700">
                  {fmtN(failures.totalFailedAttempts)} failed attempt{failures.totalFailedAttempts !== 1 ? 's' : ''}
                </span>
              </div>
              <div className="overflow-x-auto">
                <table className="min-w-full divide-y divide-gray-100 text-sm">
                  <thead className="bg-gray-50 text-xs text-gray-500 uppercase tracking-wide">
                    <tr>
                      <th className="px-4 py-2.5 text-left font-medium">Failure Category</th>
                      <th className="px-4 py-2.5 text-left font-medium">Error Code</th>
                      <th className="px-4 py-2.5 text-right font-medium">Count</th>
                      <th className="px-4 py-2.5 text-right font-medium">% of Failures</th>
                      <th className="px-4 py-2.5 text-right font-medium">Latest</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-gray-100">
                    {failures.items.map((item, i) => (
                      <tr key={i} className="hover:bg-gray-50">
                        <td className="px-4 py-2.5">
                          <span className="font-mono text-xs text-gray-800 bg-gray-100 rounded px-1.5 py-0.5">
                            {item.failureCategory}
                          </span>
                        </td>
                        <td className="px-4 py-2.5 font-mono text-xs text-gray-500">
                          {item.errorCode ?? <span className="italic text-gray-400">—</span>}
                        </td>
                        <td className="px-4 py-2.5 text-right tabular-nums font-semibold text-red-700">
                          {fmtN(item.count)}
                        </td>
                        <td className="px-4 py-2.5 text-right tabular-nums text-gray-500 text-xs">
                          {pct(item.count, failures.totalFailedAttempts)}
                        </td>
                        <td className="px-4 py-2.5 text-right font-mono text-[11px] text-gray-400 whitespace-nowrap">
                          {fmtUtc(item.latestOccurrenceAt)}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          ) : failures ? (
            <EmptyState message="No failures in this period." />
          ) : null}
        </Section>

        {/* ── Tenant Breakdown ─────────────────────────────────────────────── */}
        <Section icon="ri-building-2-line" title="Tenant Activity">
          <div className="rounded-lg border border-amber-100 bg-amber-50 px-3 py-2 text-xs text-amber-700 mb-3">
            <i className="ri-information-line mr-1" />
            Tenant names are not available — Notification Service returns tenant IDs only.
            Future enhancement: enrich with names from Identity service.
          </div>

          {tenantsErr ? (
            <SectionError label="Tenant data unavailable" message={tenantsErr} />
          ) : tenants && tenants.items.length > 0 ? (
            <div className="rounded-lg border border-gray-200 bg-white overflow-hidden">
              <div className="px-4 py-3 border-b border-gray-100 bg-gray-50">
                <span className="text-sm font-semibold text-gray-700">
                  {fmtN(tenants.totalTenants)} tenant{tenants.totalTenants !== 1 ? 's' : ''} with SMS activity
                </span>
              </div>
              <div className="overflow-x-auto">
                <table className="min-w-full divide-y divide-gray-100 text-sm">
                  <thead className="bg-gray-50 text-xs text-gray-500 uppercase tracking-wide">
                    <tr>
                      <th className="px-4 py-2.5 text-left font-medium">Tenant ID</th>
                      <th className="px-4 py-2.5 text-right font-medium">Total</th>
                      <th className="px-4 py-2.5 text-right font-medium">Delivered</th>
                      <th className="px-4 py-2.5 text-right font-medium">Failed</th>
                      <th className="px-4 py-2.5 text-right font-medium">Pending</th>
                      <th className="px-4 py-2.5 text-right font-medium">Reconciled</th>
                      <th className="px-4 py-2.5 text-right font-medium">Never Recon.</th>
                      <th className="px-4 py-2.5 text-right font-medium">Tenant-Owned</th>
                      <th className="px-4 py-2.5 text-right font-medium">Latest</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-gray-100">
                    {tenants.items.map((t, i) => (
                      <tr key={i} className="hover:bg-gray-50">
                        <td className="px-4 py-2.5">
                          {t.tenantId
                            ? (
                              <a href={`/tenants/${t.tenantId}`} className="font-mono text-xs text-indigo-600 hover:underline">
                                {t.tenantId}
                              </a>
                            )
                            : <span className="font-mono text-xs text-gray-400 italic">—</span>
                          }
                        </td>
                        <td className="px-4 py-2.5 text-right tabular-nums font-semibold text-gray-800">{fmtN(t.totalAttempts)}</td>
                        <td className="px-4 py-2.5 text-right tabular-nums text-emerald-700">{fmtN(t.deliveredCount)}</td>
                        <td className="px-4 py-2.5 text-right tabular-nums text-red-700">{fmtN(t.failedCount)}</td>
                        <td className="px-4 py-2.5 text-right tabular-nums text-amber-700">{fmtN(t.pendingCount)}</td>
                        <td className="px-4 py-2.5 text-right tabular-nums text-indigo-700">{fmtN(t.reconciledTotal)}</td>
                        <td className="px-4 py-2.5 text-right tabular-nums text-gray-500">{fmtN(t.neverReconciled)}</td>
                        <td className="px-4 py-2.5 text-right tabular-nums text-indigo-600">{fmtN(t.tenantOwnedCount)}</td>
                        <td className="px-4 py-2.5 text-right font-mono text-[11px] text-gray-400 whitespace-nowrap">
                          {fmtUtc(t.latestActivityAt)}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          ) : tenants ? (
            <EmptyState message="No tenant SMS activity in this period." />
          ) : null}
        </Section>

        {/* ── Provider Breakdown ───────────────────────────────────────────── */}
        <Section icon="ri-plug-line" title="Provider Activity"
          link={{ href: '/notifications/providers', label: 'Manage providers' }}>
          {providersErr ? (
            <SectionError label="Provider data unavailable" message={providersErr} />
          ) : providers && providers.items.length > 0 ? (
            <div className="rounded-lg border border-gray-200 bg-white overflow-hidden">
              <div className="px-4 py-3 border-b border-gray-100 bg-gray-50">
                <span className="text-sm font-semibold text-gray-700">
                  {fmtN(providers.totalProviderConfigs)} provider config{providers.totalProviderConfigs !== 1 ? 's' : ''} active
                </span>
              </div>
              <div className="overflow-x-auto">
                <table className="min-w-full divide-y divide-gray-100 text-sm">
                  <thead className="bg-gray-50 text-xs text-gray-500 uppercase tracking-wide">
                    <tr>
                      <th className="px-4 py-2.5 text-left font-medium">Provider</th>
                      <th className="px-4 py-2.5 text-left font-medium">Config ID</th>
                      <th className="px-4 py-2.5 text-left font-medium">Ownership</th>
                      <th className="px-4 py-2.5 text-right font-medium">Total</th>
                      <th className="px-4 py-2.5 text-right font-medium">Sent</th>
                      <th className="px-4 py-2.5 text-right font-medium">Delivered</th>
                      <th className="px-4 py-2.5 text-right font-medium">Failed</th>
                      <th className="px-4 py-2.5 text-right font-medium">Reconciled</th>
                      <th className="px-4 py-2.5 text-right font-medium">Lookup Failed</th>
                      <th className="px-4 py-2.5 text-right font-medium">Latest</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-gray-100">
                    {providers.items.map((p, i) => (
                      <tr key={i} className="hover:bg-gray-50">
                        <td className="px-4 py-2.5 font-semibold text-gray-700 capitalize">{p.provider || '—'}</td>
                        <td className="px-4 py-2.5">
                          {p.providerConfigId
                            ? <span className="font-mono text-[11px] text-gray-500">{p.providerConfigId}</span>
                            : <span className="text-gray-400 italic text-xs">—</span>
                          }
                        </td>
                        <td className="px-4 py-2.5">
                          <OwnershipBadge mode={p.providerOwnershipMode} />
                        </td>
                        <td className="px-4 py-2.5 text-right tabular-nums font-semibold text-gray-800">{fmtN(p.totalAttempts)}</td>
                        <td className="px-4 py-2.5 text-right tabular-nums text-sky-700">{fmtN(p.sentCount)}</td>
                        <td className="px-4 py-2.5 text-right tabular-nums text-emerald-700">{fmtN(p.deliveredCount)}</td>
                        <td className="px-4 py-2.5 text-right tabular-nums text-red-700">{fmtN(p.failedCount)}</td>
                        <td className="px-4 py-2.5 text-right tabular-nums text-indigo-700">{fmtN(p.reconciledTotal)}</td>
                        <td className="px-4 py-2.5 text-right tabular-nums text-orange-700">{fmtN(p.reconciliationLookupFailed)}</td>
                        <td className="px-4 py-2.5 text-right font-mono text-[11px] text-gray-400 whitespace-nowrap">
                          {fmtUtc(p.latestActivityAt)}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          ) : providers ? (
            <EmptyState message="No provider SMS activity in this period." />
          ) : null}
        </Section>

        {/* ── Footer ─────────────────────────────────────────────────────── */}
        <div className="text-xs text-gray-400 text-right border-t border-gray-100 pt-4">
          Read-only. Notification Service owns all SMS aggregation — no data is stored in Control Center.
          All timestamps are UTC.
        </div>

      </div>
    </CCShell>
  );
}
