/**
 * /notifications/sms-costs — LS-NOTIF-SMS-013
 *
 * Control Center SMS Cost Analytics — platform-admin read-only view.
 *
 * Consumes six Notification Service SMS cost APIs:
 *   GET /admin/sms/costs/summary   — KPI cards
 *   GET /admin/sms/costs/trends    — time-series cost chart
 *   GET /admin/sms/costs/providers — per-provider cost breakdown
 *   GET /admin/sms/costs/tenants   — per-tenant cost breakdown
 *   GET /admin/sms/costs/failures  — failure/retry cost breakdown
 *   GET /admin/sms/costs/export    — export-ready rows (on-demand)
 *
 * Security:
 *   - requirePlatformAdmin() gates the entire page.
 *   - No credentials, SettingsJson, phone numbers, or raw payloads are rendered.
 *   - No action triggers sends, retries, reconciliation, or provider calls.
 *   - All cost values are operational estimates; not invoice-grade billing data.
 *
 * Filtering (URL params):
 *   ?window=7d|30d|90d          — date range preset (default: 30d)
 *   ?bucket=hour|day|week       — trend bucket (default: day)
 *   ?ownership=all|tenant|platform
 *   ?provider=<providerName>
 *
 * Sections load independently via Promise.allSettled.
 */

import { requirePlatformAdmin } from '@/lib/auth-guards';
import { CCShell }              from '@/components/shell/cc-shell';
import {
  getSmsCostSummary,
  getSmsCostTrends,
  getSmsCostProviders,
  getSmsCostTenants,
  getSmsCostFailures,
} from '@/lib/sms-cost-api';
import type {
  SmsCostSummary,
  SmsCostTrendResult,
  SmsCostProviderResult,
  SmsCostTenantResult,
  SmsCostFailureResult,
  SmsCostQuery,
} from '@/lib/sms-cost-api';
import { SmsCostPanel } from '@/components/sms-costs/cost-panel';

export const dynamic = 'force-dynamic';

// ── Window presets ────────────────────────────────────────────────────────────

const WINDOWS = [
  { value: '7d',  label: 'Last 7 days',  days: 7  },
  { value: '30d', label: 'Last 30 days', days: 30 },
  { value: '90d', label: 'Last 90 days', days: 90 },
] as const;
type WindowValue = typeof WINDOWS[number]['value'];

function isValidWindow(v: unknown): v is WindowValue {
  return typeof v === 'string' && WINDOWS.some(w => w.value === v);
}

// ── Helpers ───────────────────────────────────────────────────────────────────

function windowToFromTo(days: number): { from: string; to: string } {
  const to   = new Date();
  const from = new Date(to.getTime() - days * 24 * 60 * 60 * 1000);
  return {
    from: from.toISOString(),
    to:   to.toISOString(),
  };
}

function fmtCost(v: number, currency: string): string {
  return new Intl.NumberFormat('en-US', {
    style:    'currency',
    currency,
    minimumFractionDigits: 4,
    maximumFractionDigits: 4,
  }).format(v);
}

// ── Page ──────────────────────────────────────────────────────────────────────

interface PageProps {
  searchParams: Promise<Record<string, string | string[]>>;
}

export default async function SmsCostsPage({ searchParams }: PageProps) {
  const session = await requirePlatformAdmin();
  const sp      = await searchParams;

  const rawWindow = Array.isArray(sp.window) ? sp.window[0] : sp.window;
  const rawBucket = Array.isArray(sp.bucket) ? sp.bucket[0] : sp.bucket;
  const rawOwn    = Array.isArray(sp.ownership) ? sp.ownership[0] : sp.ownership;
  const rawProv   = Array.isArray(sp.provider) ? sp.provider[0] : sp.provider;

  const windowVal = isValidWindow(rawWindow) ? rawWindow : '30d';
  const bucket    = ['hour', 'day', 'week'].includes(rawBucket ?? '') ? rawBucket! : 'day';
  const ownership = ['tenant', 'platform'].includes(rawOwn ?? '') ? rawOwn : undefined;
  const provider  = rawProv ?? undefined;

  const selectedDays = WINDOWS.find(w => w.value === windowVal)!.days;
  const { from, to } = windowToFromTo(selectedDays);

  const baseQuery: SmsCostQuery = {
    from,
    to,
    bucket,
    ...(ownership ? { providerOwnershipMode: ownership } : {}),
    ...(provider  ? { provider }              : {}),
  };

  const [summaryRes, trendsRes, providersRes, tenantsRes, failuresRes] =
    await Promise.allSettled([
      getSmsCostSummary(baseQuery),
      getSmsCostTrends({ ...baseQuery, bucket }),
      getSmsCostProviders(baseQuery),
      getSmsCostTenants(baseQuery),
      getSmsCostFailures(baseQuery),
    ]);

  const summary   = summaryRes.status   === 'fulfilled' ? summaryRes.value   : null;
  const trends    = trendsRes.status    === 'fulfilled' ? trendsRes.value    : null;
  const providers = providersRes.status === 'fulfilled' ? providersRes.value : null;
  const tenants   = tenantsRes.status   === 'fulfilled' ? tenantsRes.value   : null;
  const failures  = failuresRes.status  === 'fulfilled' ? failuresRes.value  : null;

  const currency = summary?.currency ?? 'USD';

  // ── KPI cards ──────────────────────────────────────────────────────────────

  const kpis = summary ? [
    {
      label:    'Total Effective Cost',
      value:    fmtCost(summary.totalEffectiveCost, currency),
      sub:      `${summary.costedAttempts.toLocaleString()} costed attempts`,
      accent:   'blue' as const,
    },
    {
      label:    'Delivered Cost',
      value:    fmtCost(summary.deliveredCost, currency),
      sub:      `${summary.deliveredCount.toLocaleString()} delivered`,
      accent:   'green' as const,
    },
    {
      label:    'Failed / Retry Cost',
      value:    fmtCost(summary.failedCost + summary.retryCost, currency),
      sub:      `${summary.failedCount.toLocaleString()} failed`,
      accent:   'red' as const,
    },
    {
      label:    'Cost per Delivered SMS',
      value:    summary.costPerDeliveredMessage != null
                  ? fmtCost(summary.costPerDeliveredMessage, currency)
                  : '—',
      sub:      'effective / delivered',
      accent:   'purple' as const,
    },
  ] : null;

  return (
    <CCShell userEmail={session.email}>
      {/* Header */}
      <div className="flex items-center justify-between mb-6">
        <div>
          <h1 className="text-xl font-semibold text-white">SMS Cost Analytics</h1>
          <p className="text-sm text-slate-400 mt-0.5">
            Operational cost estimates — not invoice-grade billing data.
            Amounts are configurable per-provider estimates in {currency}.
          </p>
        </div>
        <span className="text-xs px-2 py-1 rounded bg-blue-500/20 text-blue-300 font-medium">
          READ-ONLY
        </span>
      </div>

      {/* Window / filter bar */}
      <div className="flex flex-wrap gap-2 mb-6">
        {WINDOWS.map(w => (
          <a
            key={w.value}
            href={`/notifications/sms-costs?window=${w.value}&bucket=${bucket}${ownership ? `&ownership=${ownership}` : ''}${provider ? `&provider=${provider}` : ''}`}
            className={[
              'px-3 py-1.5 rounded text-xs font-medium transition-colors',
              windowVal === w.value
                ? 'bg-blue-600 text-white'
                : 'bg-slate-700 text-slate-300 hover:bg-slate-600',
            ].join(' ')}
          >
            {w.label}
          </a>
        ))}
        <div className="ml-auto flex gap-2">
          {(['hour', 'day', 'week'] as const).map(b => (
            <a
              key={b}
              href={`/notifications/sms-costs?window=${windowVal}&bucket=${b}${ownership ? `&ownership=${ownership}` : ''}${provider ? `&provider=${provider}` : ''}`}
              className={[
                'px-3 py-1.5 rounded text-xs font-medium transition-colors',
                bucket === b
                  ? 'bg-slate-500 text-white'
                  : 'bg-slate-700 text-slate-300 hover:bg-slate-600',
              ].join(' ')}
            >
              {b}
            </a>
          ))}
        </div>
      </div>

      {/* KPI cards */}
      {summaryRes.status === 'rejected' && (
        <div className="mb-4 p-3 rounded bg-red-900/30 border border-red-700/40 text-red-300 text-sm">
          Failed to load cost summary.
        </div>
      )}
      {kpis && (
        <div className="grid grid-cols-2 lg:grid-cols-4 gap-4 mb-6">
          {kpis.map(k => (
            <div
              key={k.label}
              className="rounded-lg bg-slate-800/60 border border-slate-700/40 p-4"
            >
              <p className="text-xs text-slate-400 mb-1">{k.label}</p>
              <p className={[
                'text-lg font-semibold tabular-nums',
                k.accent === 'blue'   ? 'text-blue-300'   :
                k.accent === 'green'  ? 'text-emerald-400' :
                k.accent === 'red'    ? 'text-red-400'    :
                'text-purple-300',
              ].join(' ')}>{k.value}</p>
              <p className="text-xs text-slate-500 mt-0.5">{k.sub}</p>
            </div>
          ))}
        </div>
      )}

      {/* Cost source info bar */}
      {summary && (
        <div className="flex flex-wrap gap-4 mb-6 p-3 rounded-lg bg-slate-800/40 border border-slate-700/30 text-xs text-slate-400">
          <span>
            <span className="text-blue-300 font-medium">{summary.estimatedCostCount.toLocaleString()}</span> estimated
          </span>
          <span>
            <span className="text-emerald-400 font-medium">{summary.providerReconciledCount.toLocaleString()}</span> provider-reconciled
          </span>
          <span>
            <span className="text-slate-500 font-medium">{summary.unavailableCount.toLocaleString()}</span> unavailable
          </span>
          <span className="ml-auto">
            Tenant: <span className="text-white font-medium">{fmtCost(summary.tenantOwnedCost, currency)}</span>
            &nbsp;·&nbsp;
            Platform: <span className="text-white font-medium">{fmtCost(summary.platformOwnedCost, currency)}</span>
          </span>
        </div>
      )}

      {/* Interactive panels — client component handles trends table + all breakdowns */}
      <SmsCostPanel
        trends={trends}
        trendsError={trendsRes.status === 'rejected'}
        providers={providers}
        providersError={providersRes.status === 'rejected'}
        tenants={tenants}
        tenantsError={tenantsRes.status === 'rejected'}
        failures={failures}
        failuresError={failuresRes.status === 'rejected'}
        currency={currency}
        windowVal={windowVal}
        bucket={bucket}
      />
    </CCShell>
  );
}
