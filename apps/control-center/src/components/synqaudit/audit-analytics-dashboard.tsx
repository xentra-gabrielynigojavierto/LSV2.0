'use client';

import { useState, useTransition } from 'react';
import { useRouter }               from 'next/navigation';
import type {
  AuditAnalyticsSummary,
  AuditCategoryBreakdownItem,
  AuditSeverityBreakdownItem,
} from '@/types/control-center';

// ── Helpers ───────────────────────────────────────────────────────────────────

function fmt(n: number): string {
  return n.toLocaleString();
}

function pct(part: number, total: number): string {
  if (total === 0) return '0';
  return ((part / total) * 100).toFixed(1);
}

const CATEGORY_COLORS: Record<string, string> = {
  Security:       'bg-red-500',
  Access:         'bg-blue-500',
  Business:       'bg-indigo-500',
  Administrative: 'bg-purple-500',
  System:         'bg-gray-500',
  Compliance:     'bg-emerald-500',
  DataChange:     'bg-amber-500',
  Integration:    'bg-cyan-500',
  Performance:    'bg-orange-500',
};

const SEVERITY_COLORS: Record<string, string> = {
  Debug:    'bg-gray-300',
  Info:     'bg-blue-400',
  Notice:   'bg-indigo-400',
  Warn:     'bg-amber-400',
  Error:    'bg-red-500',
  Critical: 'bg-red-700',
  Alert:    'bg-rose-900',
};

// ── Sub-components ────────────────────────────────────────────────────────────

function KpiCard({
  label,
  value,
  icon,
  accent,
  sub,
}: {
  label:  string;
  value:  string | number;
  icon:   string;
  accent: string;
  sub?:   string;
}) {
  return (
    <div className="rounded-lg border border-gray-200 bg-white px-5 py-4">
      <div className="flex items-start gap-3">
        <span className={`mt-0.5 text-xl ${accent}`}>
          <i className={icon} />
        </span>
        <div className="min-w-0 flex-1">
          <p className="text-xs font-medium text-gray-500 uppercase tracking-wide">{label}</p>
          <p className={`text-2xl font-bold ${accent} mt-0.5`}>{fmt(Number(value))}</p>
          {sub && <p className="text-xs text-gray-400 mt-0.5">{sub}</p>}
        </div>
      </div>
    </div>
  );
}

function SectionHeader({ title, sub }: { title: string; sub?: string }) {
  return (
    <div className="mb-3">
      <h2 className="text-sm font-semibold text-gray-800">{title}</h2>
      {sub && <p className="text-xs text-gray-400 mt-0.5">{sub}</p>}
    </div>
  );
}

/** CSS bar chart for volume by day. */
function VolumeChart({ data }: { data: AuditAnalyticsSummary['volumeByDay'] }) {
  if (data.length === 0) {
    return (
      <div className="flex h-32 items-center justify-center rounded-lg border border-dashed border-gray-200 text-sm text-gray-400">
        No volume data in this window
      </div>
    );
  }

  const max = Math.max(...data.map((d) => d.count), 1);

  return (
    <div className="overflow-x-auto">
      <div className="flex items-end gap-0.5 h-32 min-w-full">
        {data.map((d) => {
          const heightPct = (d.count / max) * 100;
          const label     = d.date.slice(5); // "MM-DD"
          return (
            <div key={d.date} className="group relative flex-1 flex flex-col items-center justify-end min-w-[6px]">
              <div
                className="w-full bg-indigo-500 rounded-t hover:bg-indigo-600 transition-colors"
                style={{ height: `${Math.max(heightPct, 2)}%` }}
                title={`${d.date}: ${fmt(d.count)} events`}
              />
              {/* Show label every ~7 days to avoid clutter */}
              {data.length <= 35 && (
                <span className="text-[9px] text-gray-400 mt-1 rotate-45 origin-left hidden group-[&:nth-child(7n+1)]:block">
                  {label}
                </span>
              )}
            </div>
          );
        })}
      </div>
      <div className="flex justify-between mt-1 text-[10px] text-gray-400">
        <span>{data.at(0)?.date ?? ''}</span>
        <span>{data.at(-1)?.date ?? ''}</span>
      </div>
    </div>
  );
}

/** Percentage-bar breakdown row. */
function BreakdownRow({
  label,
  count,
  total,
  color,
  href,
}: {
  label:  string;
  count:  number;
  total:  number;
  color:  string;
  href?:  string;
}) {
  const p = Number(pct(count, total));

  return (
    <div className="flex items-center gap-2 py-1.5">
      <div className="w-24 shrink-0 text-xs text-gray-700 truncate" title={label}>
        {href ? (
          <a href={href} className="hover:underline text-indigo-600">{label}</a>
        ) : (
          label
        )}
      </div>
      <div className="flex-1 h-2 bg-gray-100 rounded-full overflow-hidden">
        <div className={`h-full rounded-full ${color}`} style={{ width: `${p}%` }} />
      </div>
      <div className="w-16 shrink-0 text-right text-xs text-gray-500">
        {fmt(count)} <span className="text-gray-300">({pct(count, total)}%)</span>
      </div>
    </div>
  );
}

// ── Main component ────────────────────────────────────────────────────────────

interface Props {
  summary:         AuditAnalyticsSummary | null;
  initialFrom:     string;
  initialTo:       string;
  initialCategory: string | undefined;
  initialTenantId: string | undefined;
}

export function AuditAnalyticsDashboard({
  summary,
  initialFrom,
  initialTo,
  initialCategory,
  initialTenantId,
}: Props) {
  const router = useRouter();
  const [isPending, startTransition] = useTransition();

  // Filter state
  const [from,     setFrom]     = useState(initialFrom.slice(0, 10));
  const [to,       setTo]       = useState(initialTo.slice(0, 10));
  const [category, setCategory] = useState(initialCategory ?? '');
  const [tenantId, setTenantId] = useState(initialTenantId ?? '');

  function applyFilters() {
    const qs = new URLSearchParams();
    if (from)     qs.set('from',     new Date(from).toISOString());
    if (to)       qs.set('to',       new Date(to + 'T23:59:59Z').toISOString());
    if (category) qs.set('category', category);
    if (tenantId) qs.set('tenantId', tenantId);
    startTransition(() => {
      router.push(`/synqaudit/analytics?${qs.toString()}`);
    });
  }

  const categories = [
    'Security', 'Access', 'Business', 'Administrative',
    'System', 'Compliance', 'DataChange', 'Integration', 'Performance',
  ];

  const total = summary?.totalEvents ?? 0;

  if (!summary) {
    return (
      <div className="rounded-lg border border-dashed border-gray-200 bg-gray-50 px-6 py-12 text-center">
        <i className="ri-bar-chart-box-line text-3xl text-gray-300" />
        <p className="mt-2 text-sm text-gray-500">No analytics data available.</p>
        <p className="text-xs text-gray-400 mt-1">
          Analytics will populate once audit events are ingested.
        </p>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      {/* ── Filter bar ─────────────────────────────────────────────────────── */}
      <div className="rounded-lg border border-gray-200 bg-white px-5 py-4">
        <div className="flex flex-wrap items-end gap-3">
          <div>
            <label className="block text-xs font-medium text-gray-500 mb-1">From</label>
            <input
              type="date"
              value={from}
              onChange={(e) => setFrom(e.target.value)}
              className="rounded border border-gray-300 px-2 py-1 text-sm text-gray-700 focus:outline-none focus:ring-1 focus:ring-indigo-400"
            />
          </div>
          <div>
            <label className="block text-xs font-medium text-gray-500 mb-1">To</label>
            <input
              type="date"
              value={to}
              onChange={(e) => setTo(e.target.value)}
              className="rounded border border-gray-300 px-2 py-1 text-sm text-gray-700 focus:outline-none focus:ring-1 focus:ring-indigo-400"
            />
          </div>
          <div>
            <label className="block text-xs font-medium text-gray-500 mb-1">Category</label>
            <select
              value={category}
              onChange={(e) => setCategory(e.target.value)}
              className="rounded border border-gray-300 px-2 py-1 text-sm text-gray-700 focus:outline-none focus:ring-1 focus:ring-indigo-400"
            >
              <option value="">All categories</option>
              {categories.map((c) => (
                <option key={c} value={c}>{c}</option>
              ))}
            </select>
          </div>
          <div>
            <label className="block text-xs font-medium text-gray-500 mb-1">Tenant ID</label>
            <input
              type="text"
              placeholder="All tenants"
              value={tenantId}
              onChange={(e) => setTenantId(e.target.value)}
              className="rounded border border-gray-300 px-2 py-1 text-sm text-gray-700 w-44 focus:outline-none focus:ring-1 focus:ring-indigo-400"
            />
          </div>
          <button
            onClick={applyFilters}
            disabled={isPending}
            className="rounded bg-indigo-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-indigo-700 disabled:opacity-60 transition-colors"
          >
            {isPending ? 'Loading…' : 'Apply'}
          </button>
        </div>
        <p className="mt-2 text-xs text-gray-400">
          Window: {summary.from.slice(0, 10)} → {summary.to.slice(0, 10)}
          {summary.effectiveTenantId
            ? ` · Tenant: ${summary.effectiveTenantId}`
            : ' · All tenants'}
          {' · Max window: 90 days'}
        </p>
      </div>

      {/* ── KPI cards ──────────────────────────────────────────────────────── */}
      <div className="grid grid-cols-2 lg:grid-cols-4 gap-4">
        <KpiCard
          label="Total Events"
          value={summary.totalEvents}
          icon="ri-pulse-line"
          accent="text-indigo-600"
          sub="in selected window"
        />
        <KpiCard
          label="Security Events"
          value={summary.securityEventCount}
          icon="ri-shield-keyhole-line"
          accent="text-red-600"
          sub={total > 0 ? `${pct(summary.securityEventCount, total)}% of total` : undefined}
        />
        <KpiCard
          label="Access Denials"
          value={summary.denialEventCount}
          icon="ri-forbid-line"
          accent="text-amber-600"
          sub="events containing .denied / .deny"
        />
        <KpiCard
          label="Governance Actions"
          value={summary.governanceEventCount}
          icon="ri-scales-3-line"
          accent="text-emerald-600"
          sub="legal holds, integrity, exports"
        />
      </div>

      {/* ── Volume trend ───────────────────────────────────────────────────── */}
      <div className="rounded-lg border border-gray-200 bg-white px-5 py-4">
        <SectionHeader
          title="Event Volume by Day"
          sub="Total events per calendar day (UTC) in the selected window"
        />
        <VolumeChart data={summary.volumeByDay} />
      </div>

      {/* ── Category + Severity breakdowns ─────────────────────────────────── */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
        <div className="rounded-lg border border-gray-200 bg-white px-5 py-4">
          <SectionHeader title="By Category" sub="Event count per audit category" />
          {summary.byCategory.length === 0 ? (
            <p className="text-sm text-gray-400">No data</p>
          ) : (
            <div>
              {summary.byCategory.map((item: AuditCategoryBreakdownItem) => (
                <BreakdownRow
                  key={item.category}
                  label={item.category}
                  count={item.count}
                  total={total}
                  color={CATEGORY_COLORS[item.category] ?? 'bg-gray-400'}
                  href={`/synqaudit/investigation?category=${encodeURIComponent(item.category)}`}
                />
              ))}
            </div>
          )}
        </div>

        <div className="rounded-lg border border-gray-200 bg-white px-5 py-4">
          <SectionHeader title="By Severity" sub="Event count per severity level" />
          {summary.bySeverity.length === 0 ? (
            <p className="text-sm text-gray-400">No data</p>
          ) : (
            <div>
              {summary.bySeverity.map((item: AuditSeverityBreakdownItem) => (
                <BreakdownRow
                  key={item.severity}
                  label={item.severity}
                  count={item.count}
                  total={total}
                  color={SEVERITY_COLORS[item.severity] ?? 'bg-gray-400'}
                />
              ))}
            </div>
          )}
        </div>
      </div>

      {/* ── Top Event Types ─────────────────────────────────────────────────── */}
      <div className="rounded-lg border border-gray-200 bg-white px-5 py-4">
        <SectionHeader
          title="Top Event Types"
          sub="15 most frequent event type codes in the selected window"
        />
        {summary.topEventTypes.length === 0 ? (
          <p className="text-sm text-gray-400">No data</p>
        ) : (
          <div className="divide-y divide-gray-100">
            {summary.topEventTypes.map((item, i) => (
              <div key={item.eventType} className="flex items-center gap-3 py-2 text-sm">
                <span className="w-5 text-right text-xs text-gray-400 shrink-0">{i + 1}</span>
                <span className="flex-1 font-mono text-xs text-gray-700 truncate" title={item.eventType}>
                  <a
                    href={`/synqaudit/investigation?eventType=${encodeURIComponent(item.eventType)}`}
                    className="hover:underline text-indigo-600"
                  >
                    {item.eventType}
                  </a>
                </span>
                <span className="shrink-0 text-xs font-medium text-gray-700">{fmt(item.count)}</span>
                <div className="w-20 h-1.5 bg-gray-100 rounded-full overflow-hidden shrink-0">
                  <div
                    className="h-full bg-indigo-400 rounded-full"
                    style={{ width: `${pct(item.count, summary.topEventTypes[0]?.count ?? 1)}%` }}
                  />
                </div>
              </div>
            ))}
          </div>
        )}
      </div>

      {/* ── Top Actors + Top Tenants ────────────────────────────────────────── */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
        {/* Top Actors */}
        <div className="rounded-lg border border-gray-200 bg-white px-5 py-4">
          <SectionHeader
            title="Top Actors"
            sub="10 most active authenticated actors"
          />
          {summary.topActors.length === 0 ? (
            <p className="text-sm text-gray-400">No actor data</p>
          ) : (
            <div className="divide-y divide-gray-100">
              {summary.topActors.map((actor, i) => (
                <div key={actor.actorId} className="flex items-center gap-3 py-2 text-sm">
                  <span className="w-5 text-right text-xs text-gray-400 shrink-0">{i + 1}</span>
                  <div className="flex-1 min-w-0">
                    <a
                      href={`/synqaudit/investigation?actorId=${encodeURIComponent(actor.actorId)}`}
                      className="block text-xs font-medium text-indigo-600 hover:underline truncate"
                      title={actor.actorId}
                    >
                      {actor.actorName ?? actor.actorId}
                    </a>
                    {actor.actorName && (
                      <span className="block font-mono text-[10px] text-gray-400 truncate">{actor.actorId}</span>
                    )}
                  </div>
                  <span className="shrink-0 text-xs font-medium text-gray-700">{fmt(actor.count)}</span>
                </div>
              ))}
            </div>
          )}
        </div>

        {/* Top Tenants (platform admin only) */}
        <div className="rounded-lg border border-gray-200 bg-white px-5 py-4">
          <SectionHeader
            title="Top Tenants"
            sub={
              summary.topTenants !== null
                ? '10 most active tenants by event count'
                : 'Available to platform administrators only'
            }
          />
          {summary.topTenants === null ? (
            <div className="flex items-center gap-2 rounded border border-dashed border-gray-200 bg-gray-50 px-4 py-6 text-sm text-gray-400">
              <i className="ri-lock-line" />
              Cross-tenant analytics require platform admin scope.
            </div>
          ) : summary.topTenants.length === 0 ? (
            <p className="text-sm text-gray-400">No tenant data</p>
          ) : (
            <div className="divide-y divide-gray-100">
              {summary.topTenants.map((t, i) => (
                <div key={t.tenantId} className="flex items-center gap-3 py-2 text-sm">
                  <span className="w-5 text-right text-xs text-gray-400 shrink-0">{i + 1}</span>
                  <span className="flex-1 font-mono text-xs text-gray-700 truncate" title={t.tenantId}>
                    {t.tenantId}
                  </span>
                  <span className="shrink-0 text-xs font-medium text-gray-700">{fmt(t.count)}</span>
                  <div className="w-20 h-1.5 bg-gray-100 rounded-full overflow-hidden shrink-0">
                    <div
                      className="h-full bg-purple-400 rounded-full"
                      style={{ width: `${pct(t.count, summary.topTenants![0]?.count ?? 1)}%` }}
                    />
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>
      </div>

      {/* ── Investigation links footer ───────────────────────────────────────── */}
      <div className="rounded-lg border border-indigo-100 bg-indigo-50 px-5 py-4 text-sm text-indigo-700">
        <p className="font-medium mb-1">
          <i className="ri-search-eye-line mr-1" />
          From analytics to investigation
        </p>
        <p className="text-xs text-indigo-600">
          Click any category, event type, or actor name above to open the Investigation viewer
          pre-filtered to that dimension.
        </p>
        <div className="mt-3 flex flex-wrap gap-2">
          <a
            href="/synqaudit/investigation"
            className="inline-flex items-center gap-1 rounded bg-indigo-100 px-3 py-1 text-xs font-medium text-indigo-700 hover:bg-indigo-200 transition-colors"
          >
            <i className="ri-search-eye-line" /> Open Investigation
          </a>
          <a
            href="/synqaudit/integrity"
            className="inline-flex items-center gap-1 rounded bg-indigo-100 px-3 py-1 text-xs font-medium text-indigo-700 hover:bg-indigo-200 transition-colors"
          >
            <i className="ri-fingerprint-line" /> Integrity Checks
          </a>
          <a
            href="/synqaudit/legal-holds"
            className="inline-flex items-center gap-1 rounded bg-indigo-100 px-3 py-1 text-xs font-medium text-indigo-700 hover:bg-indigo-200 transition-colors"
          >
            <i className="ri-scales-3-line" /> Legal Holds
          </a>
          <a
            href="/synqaudit/exports"
            className="inline-flex items-center gap-1 rounded bg-indigo-100 px-3 py-1 text-xs font-medium text-indigo-700 hover:bg-indigo-200 transition-colors"
          >
            <i className="ri-download-cloud-line" /> Exports
          </a>
        </div>
      </div>
    </div>
  );
}
