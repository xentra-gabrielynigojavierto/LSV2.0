/**
 * LSCC-01-005: Referral Performance Metrics Dashboard.
 * Route: /careconnect/admin/performance[?days=7|since=<ISO>]
 *
 * Server component — admin only (PlatformAdmin or TenantAdmin).
 *
 * Sections:
 *   1. Time-window selector (24h / 7d / 30d presets)
 *   2. Summary metric cards
 *   3. Aging distribution (unaccepted New referrals)
 *   4. Provider performance table
 */

import Link from 'next/link';
import { requireAdmin } from '@/lib/auth-guards';
import { careConnectServerApi } from '@/lib/careconnect-server-api';
import { ServerApiError } from '@/lib/server-api-client';
import type {

  ReferralPerformanceResult,
  ProviderPerformanceRow,
} from '@/types/careconnect';

export const dynamic = 'force-dynamic';


// ── Helpers ────────────────────────────────────────────────────────────────────

function fmtRate(rate: number) {
  return `${(rate * 100).toFixed(1)}%`;
}

function fmtTta(hours: number | null) {
  if (hours === null) return '—';
  if (hours < 1) return `${Math.round(hours * 60)}m`;
  if (hours < 48) return `${hours.toFixed(1)}h`;
  return `${(hours / 24).toFixed(1)}d`;
}

function fmtDate(iso: string) {
  return new Date(iso).toLocaleDateString('en-US', {
    month: 'short', day: 'numeric', year: 'numeric',
  });
}

// ── Sub-components ─────────────────────────────────────────────────────────────

function SummaryCard({
  label, value, sub, accent = 'default',
}: {
  label:   string;
  value:   string;
  sub?:    string;
  accent?: 'default' | 'green' | 'amber' | 'blue';
}) {
  const colors = {
    default: 'bg-white border-gray-200 text-gray-900',
    green:   'bg-green-50 border-green-200 text-green-900',
    amber:   'bg-amber-50 border-amber-200 text-amber-900',
    blue:    'bg-blue-50 border-blue-200 text-blue-900',
  }[accent];

  return (
    <div className={`rounded-xl border p-5 ${colors}`}>
      <p className="text-xs font-semibold uppercase tracking-wide opacity-60">{label}</p>
      <p className="mt-2 text-3xl font-bold">{value}</p>
      {sub && <p className="mt-1 text-xs opacity-50">{sub}</p>}
    </div>
  );
}

function AgingBar({ label, count, total }: { label: string; count: number; total: number }) {
  const pct = total > 0 ? (count / total) * 100 : 0;
  const urgencyColor = label.startsWith('3+') ? 'bg-red-400'
    : label.startsWith('1–3') ? 'bg-orange-400'
    : label.startsWith('1–24') ? 'bg-yellow-400'
    : 'bg-blue-400';

  return (
    <div className="flex items-center gap-3">
      <span className="text-xs text-gray-500 w-20 shrink-0">{label}</span>
      <div className="flex-1 bg-gray-100 rounded-full h-2.5 overflow-hidden">
        <div
          className={`h-2.5 rounded-full ${urgencyColor} transition-all`}
          style={{ width: `${pct.toFixed(1)}%` }}
        />
      </div>
      <span className="text-xs font-semibold text-gray-800 w-8 text-right">{count}</span>
    </div>
  );
}

function ProviderTable({ rows }: { rows: ProviderPerformanceRow[] }) {
  if (rows.length === 0) {
    return (
      <div className="bg-white rounded-xl border border-gray-200 p-10 text-center">
        <p className="text-sm text-gray-500">No provider activity in this window.</p>
      </div>
    );
  }

  return (
    <div className="bg-white rounded-xl border border-gray-200 overflow-hidden">
      <div className="overflow-x-auto">
        <table className="w-full text-left">
          <thead>
            <tr className="border-b border-gray-200 bg-gray-50">
              <th className="px-4 py-3 text-xs font-semibold text-gray-500 uppercase tracking-wide">Provider</th>
              <th className="px-4 py-3 text-xs font-semibold text-gray-500 uppercase tracking-wide text-right">Total</th>
              <th className="px-4 py-3 text-xs font-semibold text-gray-500 uppercase tracking-wide text-right">Accepted</th>
              <th className="px-4 py-3 text-xs font-semibold text-gray-500 uppercase tracking-wide text-right">Accept rate</th>
              <th className="px-4 py-3 text-xs font-semibold text-gray-500 uppercase tracking-wide text-right">Avg TTA</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-100">
            {rows.map((row) => {
              const rateCls = row.acceptanceRate >= 0.7
                ? 'text-green-700'
                : row.acceptanceRate >= 0.4
                ? 'text-yellow-700'
                : 'text-red-700';

              return (
                <tr key={row.providerId} className="hover:bg-gray-50 transition-colors">
                  <td className="px-4 py-3">
                    <span className="text-sm font-medium text-gray-900">{row.providerName}</span>
                  </td>
                  <td className="px-4 py-3 text-right">
                    <span className="text-sm text-gray-700">{row.totalReferrals}</span>
                  </td>
                  <td className="px-4 py-3 text-right">
                    <span className="text-sm text-gray-700">{row.acceptedReferrals}</span>
                  </td>
                  <td className="px-4 py-3 text-right">
                    <span className={`text-sm font-medium ${rateCls}`}>
                      {fmtRate(row.acceptanceRate)}
                    </span>
                  </td>
                  <td className="px-4 py-3 text-right">
                    <span className="text-sm text-gray-700">{fmtTta(row.avgTimeToAcceptHours)}</span>
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </div>
  );
}

// ── Page ───────────────────────────────────────────────────────────────────────

interface PageProps {
  searchParams: Promise<{ days?: string; since?: string }>;
}

const PRESETS = [
  { label: '24h',  days: 1  },
  { label: '7d',   days: 7  },
  { label: '30d',  days: 30 },
];

export default async function PerformancePage({ searchParams }: PageProps) {
  await requireAdmin();

  const params  = await searchParams;
  const days    = params.since ? undefined : Math.max(1, Number(params.days ?? 7));
  const since   = params.since;
  const activeDays = days ?? 7;

  let data: ReferralPerformanceResult | null = null;
  let errorMessage: string | null = null;

  try {
    data = await careConnectServerApi.adminPerformance.getMetrics({
      days:  days,
      since: since,
    });
  } catch (err) {
    if (err instanceof ServerApiError) {
      errorMessage = `Failed to load performance metrics (HTTP ${err.status}).`;
    } else {
      errorMessage = 'Failed to load performance metrics. Please try again.';
    }
  }

  const aging   = data?.aging;
  const summary = data?.summary;

  return (
    <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
      {/* Header */}
      <div className="mb-6 flex items-center justify-between">
        <div>
          <h1 className="text-xl font-semibold text-gray-900">Referral Performance</h1>
          <p className="text-sm text-gray-500 mt-0.5">
            {data
              ? `${fmtDate(data.windowFrom)} – ${fmtDate(data.windowTo)}`
              : 'Performance metrics for the selected window'}
          </p>
        </div>
        <Link href="/careconnect/admin/dashboard" className="text-sm text-gray-500 hover:text-gray-700">
          ← Dashboard
        </Link>
      </div>

      {/* Time-window presets */}
      <div className="flex gap-2 mb-6">
        {PRESETS.map(({ label, days: d }) => (
          <Link
            key={d}
            href={`/careconnect/admin/performance?days=${d}`}
            className={`px-3 py-1 rounded-full text-xs font-medium border transition-colors ${
              activeDays === d && !since
                ? 'bg-gray-900 text-white border-gray-900'
                : 'bg-white text-gray-600 border-gray-200 hover:border-gray-400'
            }`}
          >
            {label}
          </Link>
        ))}
      </div>

      {/* Error */}
      {errorMessage && (
        <div className="bg-red-50 border border-red-200 rounded-lg px-4 py-3 text-sm text-red-700 mb-6">
          {errorMessage}
        </div>
      )}

      {data && (
        <>
          {/* ── 1. Summary cards ─────────────────────────────────────────────── */}
          <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-5 gap-4 mb-8">
            <SummaryCard
              label="Total referrals"
              value={summary!.totalReferrals.toLocaleString()}
            />
            <SummaryCard
              label="Accepted"
              value={summary!.acceptedReferrals.toLocaleString()}
              accent="green"
            />
            <SummaryCard
              label="Acceptance rate"
              value={fmtRate(summary!.acceptanceRate)}
              accent={summary!.acceptanceRate >= 0.5 ? 'green' : 'amber'}
            />
            <SummaryCard
              label="Avg time to accept"
              value={fmtTta(summary!.avgTimeToAcceptHours)}
              sub={summary!.avgTimeToAcceptHours !== null ? 'from created to accepted' : undefined}
              accent="blue"
            />
            <SummaryCard
              label="Currently New"
              value={summary!.currentNewReferrals.toLocaleString()}
              sub="awaiting acceptance"
              accent={summary!.currentNewReferrals > 10 ? 'amber' : 'default'}
            />
          </div>

          {/* ── 2. Aging distribution ─────────────────────────────────────────── */}
          <div className="mb-8">
            <h2 className="text-sm font-semibold text-gray-600 uppercase tracking-wide mb-3">
              New Referral Aging
              <span className="ml-2 font-normal text-gray-400 normal-case">
                (all currently-New referrals, not filtered by window)
              </span>
            </h2>

            {aging!.total === 0 ? (
              <div className="bg-white rounded-xl border border-gray-200 p-8 text-center">
                <p className="text-sm text-gray-500">No referrals currently in New status.</p>
              </div>
            ) : (
              <div className="bg-white rounded-xl border border-gray-200 p-5 space-y-3">
                <AgingBar label="&lt; 1 hour"  count={aging!.lt1h}   total={aging!.total} />
                <AgingBar label="1–24 hours" count={aging!.h1to24} total={aging!.total} />
                <AgingBar label="1–3 days"   count={aging!.d1to3}  total={aging!.total} />
                <AgingBar label="3+ days"    count={aging!.gt3d}   total={aging!.total} />
                <p className="text-xs text-gray-400 pt-1">
                  {aging!.total} referral{aging!.total !== 1 ? 's' : ''} waiting
                  {aging!.gt3d > 0 && (
                    <span className="ml-1 font-semibold text-red-600">
                      — {aging!.gt3d} stuck 3+ days
                    </span>
                  )}
                </p>
              </div>
            )}
          </div>

          {/* ── 3. Provider performance table ─────────────────────────────────── */}
          <div>
            <h2 className="text-sm font-semibold text-gray-600 uppercase tracking-wide mb-3">
              Provider Responsiveness
              <span className="ml-2 font-normal text-gray-400 normal-case">
                sorted by total referrals
              </span>
            </h2>
            <ProviderTable rows={data.providers} />
          </div>
        </>
      )}

      {/* Empty state when no error but all metrics are zero */}
      {data && summary!.totalReferrals === 0 && !errorMessage && (
        <div className="mt-6 bg-blue-50 border border-blue-100 rounded-lg px-4 py-3 text-sm text-blue-700">
          No referrals were created in this window. Try a wider time range using the presets above.
        </div>
      )}
    </div>
  );
}
