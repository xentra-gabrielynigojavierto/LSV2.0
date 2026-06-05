/**
 * LSCC-011: Activation Funnel Analytics
 * Route: /careconnect/admin/analytics/activation
 * Auth:  Admin only (TenantAdmin | PlatformAdmin)
 *
 * Server component — data fetched at request time.
 * Date filter is URL-based (?days=7|30|90) for shareable links.
 */

import Link from 'next/link';
import { Suspense } from 'react';
import { requireAdmin } from '@/lib/auth-guards';
import { careConnectServerApi } from '@/lib/careconnect-server-api';
import { ServerApiError } from '@/lib/server-api-client';
import type { ActivationFunnelMetrics } from '@/types/careconnect';
import { DateFilter } from './date-filter';

export const dynamic = 'force-dynamic';


// ── Helpers ───────────────────────────────────────────────────────────────────

function pct(rate: number | null): string {
  if (rate === null) return '—';
  return `${Math.round(rate * 100)}%`;
}

function num(n: number | null | undefined): string {
  if (n === null || n === undefined) return '—';
  return n.toLocaleString();
}

// ── Summary card ──────────────────────────────────────────────────────────────

function MetricCard({
  label,
  value,
  sub,
  accent,
  href,
}: {
  label:   string;
  value:   string | number;
  sub?:    string;
  accent?: 'green' | 'blue' | 'amber' | 'gray';
  href?:   string;
}) {
  const accentClass = {
    green: 'bg-green-50 border-green-200',
    blue:  'bg-blue-50  border-blue-200',
    amber: 'bg-amber-50 border-amber-200',
    gray:  'bg-gray-50  border-gray-200',
  }[accent ?? 'gray'];

  const valueClass = {
    green: 'text-green-700',
    blue:  'text-blue-700',
    amber: 'text-amber-700',
    gray:  'text-gray-900',
  }[accent ?? 'gray'];

  const inner = (
    <div className={`rounded-xl border px-5 py-4 ${accentClass} ${href ? 'hover:shadow-sm transition-shadow' : ''}`}>
      <p className="text-xs font-medium text-gray-500 uppercase tracking-wide mb-1">{label}</p>
      <p className={`text-2xl font-bold ${valueClass}`}>{value}</p>
      {sub && <p className="text-xs text-gray-500 mt-0.5">{sub}</p>}
    </div>
  );

  return href ? <Link href={href}>{inner}</Link> : inner;
}

// ── Funnel row ────────────────────────────────────────────────────────────────

function FunnelRow({
  stage,
  count,
  rate,
  rateLabel,
  note,
  barWidth,
  color,
}: {
  stage:      string;
  count:      number | null;
  rate?:      number | null;
  rateLabel?: string;
  note?:      string;
  barWidth:   number;   // 0–100
  color:      string;
}) {
  return (
    <div className="flex items-center gap-4 py-3 border-b border-gray-100 last:border-0">
      <div className="w-44 shrink-0">
        <p className="text-sm font-medium text-gray-900">{stage}</p>
        {note && <p className="text-xs text-gray-400 mt-0.5">{note}</p>}
      </div>
      <div className="flex-1">
        <div className="h-5 bg-gray-100 rounded-full overflow-hidden">
          <div
            className={`h-full rounded-full transition-all ${color}`}
            style={{ width: `${Math.max(barWidth, barWidth > 0 ? 2 : 0)}%` }}
          />
        </div>
      </div>
      <div className="w-16 text-right">
        <span className="text-sm font-semibold text-gray-900">
          {count === null ? '—' : num(count)}
        </span>
      </div>
      {rate !== undefined && (
        <div className="w-14 text-right">
          <span className="text-xs text-gray-500">{pct(rate ?? null)}</span>
          {rateLabel && <p className="text-xs text-gray-400">{rateLabel}</p>}
        </div>
      )}
    </div>
  );
}

// ── Page ──────────────────────────────────────────────────────────────────────

interface PageProps {
  searchParams?: Promise<{ days?: string; startDate?: string; endDate?: string }>;
}

export default async function ActivationAnalyticsPage({ searchParams }: PageProps) {
  await requireAdmin();

  const resolvedParams = await searchParams;
  const daysParam = resolvedParams?.days ?? '30';
  const days      = parseInt(daysParam, 10) || 30;

  let metrics: ActivationFunnelMetrics | null = null;
  let errorMessage: string | null = null;

  try {
    metrics = await careConnectServerApi.analytics.getFunnel({ days });
  } catch (err) {
    if (err instanceof ServerApiError) {
      errorMessage = `Failed to load analytics (${err.status}).`;
    } else {
      errorMessage = 'Failed to load analytics. Please try again.';
    }
  }

  const c = metrics?.counts;
  const r = metrics?.rates;

  // Normalise funnel bar widths relative to referralsSent
  const maxCount = Math.max(c?.referralsSent ?? 0, 1);
  const bar = (n: number | null) =>
    n === null ? 0 : Math.round(((n / maxCount) * 100));

  return (
    <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8 space-y-8">

      {/* Header */}
      <div className="flex items-start justify-between flex-wrap gap-4">
        <div>
          <h1 className="text-xl font-semibold text-gray-900">Activation Funnel Analytics</h1>
          <p className="text-sm text-gray-500 mt-0.5">
            Provider activation performance from referral to accepted.
          </p>
        </div>
        <div className="flex items-center gap-3">
          <Suspense>
            <DateFilter activeDays={String(days)} />
          </Suspense>
          <Link
            href="/careconnect/admin/activations"
            className="text-sm text-primary hover:underline font-medium"
          >
            View Queue →
          </Link>
        </div>
      </div>

      {/* Error state */}
      {errorMessage && (
        <div className="rounded-lg bg-red-50 border border-red-200 px-4 py-3 text-sm text-red-700">
          {errorMessage}
        </div>
      )}

      {/* Empty state */}
      {metrics?.isEmpty && !errorMessage && (
        <div className="rounded-xl border border-gray-200 bg-white p-12 text-center">
          <p className="text-base font-semibold text-gray-900 mb-1">No activation activity</p>
          <p className="text-sm text-gray-500">
            No referrals or activation requests found for the selected period.
          </p>
        </div>
      )}

      {/* Metrics */}
      {metrics && !metrics.isEmpty && (
        <>
          {/* Summary cards */}
          <div className="grid grid-cols-2 sm:grid-cols-4 gap-4">
            <MetricCard
              label="Referrals Sent"
              value={num(c?.referralsSent)}
              accent="gray"
            />
            <MetricCard
              label="Activation Started"
              value={num(c?.activationStarted)}
              sub={`${pct(r?.activationRate ?? null)} of sent`}
              accent="blue"
            />
            <MetricCard
              label="Auto-Provisioned"
              value={num(c?.autoProvisionSucceeded)}
              sub={`${pct(r?.autoProvisionSuccessRate ?? null)} of started`}
              accent="green"
            />
            <MetricCard
              label="Referrals Accepted"
              value={num(c?.referralsAccepted)}
              sub={`${pct(r?.referralAcceptanceRate ?? null)} of sent`}
              accent="green"
              href={`/careconnect/referrals?status=Accepted`}
            />
          </div>

          {/* Funnel breakdown */}
          <div className="bg-white rounded-xl border border-gray-200 p-6">
            <h2 className="text-sm font-semibold text-gray-900 mb-4">Funnel Breakdown</h2>

            <FunnelRow
              stage="Referrals Sent"
              count={c?.referralsSent ?? null}
              barWidth={100}
              color="bg-gray-400"
            />
            <FunnelRow
              stage="Referral Viewed"
              count={null}
              barWidth={0}
              color="bg-indigo-400"
              note="Audit-log only — not queryable"
              rate={null}
              rateLabel="of sent"
            />
            <FunnelRow
              stage="Activation Started"
              count={c?.activationStarted ?? null}
              barWidth={bar(c?.activationStarted ?? null)}
              color="bg-blue-400"
              rate={r?.activationRate ?? null}
              rateLabel="of sent"
            />
            <FunnelRow
              stage="Auto-Provisioned"
              count={c?.autoProvisionSucceeded ?? null}
              barWidth={bar(c?.autoProvisionSucceeded ?? null)}
              color="bg-green-400"
              rate={r?.autoProvisionSuccessRate ?? null}
              rateLabel="of started"
            />
            <FunnelRow
              stage="Fallback to Queue"
              count={c?.fallbackPending ?? null}
              barWidth={bar(c?.fallbackPending ?? null)}
              color="bg-amber-400"
              rate={r?.fallbackRate ?? null}
              rateLabel="of started"
              note="Still pending in activation queue"
            />
            <FunnelRow
              stage="Admin Approved"
              count={c?.adminApproved ?? null}
              barWidth={bar(c?.adminApproved ?? null)}
              color="bg-purple-400"
            />
            <FunnelRow
              stage="Referrals Accepted"
              count={c?.referralsAccepted ?? null}
              barWidth={bar(c?.referralsAccepted ?? null)}
              color="bg-emerald-500"
              rate={r?.referralAcceptanceRate ?? null}
              rateLabel="of sent"
            />
          </div>

          {/* Supporting counts + drilldowns */}
          <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
            <MetricCard
              label="Pending in Queue"
              value={num(c?.totalPendingSnapshot)}
              sub="Awaiting admin review"
              accent="amber"
              href="/careconnect/admin/activations"
            />
            <MetricCard
              label="Total Approved"
              value={num(c?.totalApprovedSnapshot)}
              sub="All-time"
              accent="green"
            />
            <MetricCard
              label="Overall Approval Rate"
              value={pct(r?.overallApprovalRate ?? null)}
              sub="Auto + admin / started"
              accent="blue"
            />
          </div>

          {/* Data source note */}
          <p className="text-xs text-gray-400 text-center">
            Metrics derived from Referrals + Activation Requests. ReferralViewed and
            direct AutoProvision events are audit-log only and shown as —.{' '}
            Fallback Pending is a proxy: requests created in range still awaiting approval.
          </p>
        </>
      )}
    </div>
  );
}
