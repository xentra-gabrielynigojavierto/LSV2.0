'use client';

import { useState, useTransition } from 'react';
import { useRouter }               from 'next/navigation';
import type { AuditAnomalyData, AuditAnomalyItem } from '@/types/control-center';

// ── Severity config ────────────────────────────────────────────────────────────

const SEVERITY_CONFIG = {
  High: {
    badge:  'bg-red-100 text-red-700 border-red-200',
    border: 'border-l-red-500',
    icon:   'ri-error-warning-fill text-red-500',
    dot:    'bg-red-500',
  },
  Medium: {
    badge:  'bg-amber-100 text-amber-700 border-amber-200',
    border: 'border-l-amber-400',
    icon:   'ri-alert-fill text-amber-500',
    dot:    'bg-amber-400',
  },
  Low: {
    badge:  'bg-blue-100 text-blue-700 border-blue-200',
    border: 'border-l-blue-400',
    icon:   'ri-information-fill text-blue-400',
    dot:    'bg-blue-400',
  },
} as const;

type Sev = keyof typeof SEVERITY_CONFIG;

// ── Helper ─────────────────────────────────────────────────────────────────────

function formatUtcShort(iso: string): string {
  if (!iso) return '';
  try {
    const d = new Date(iso);
    return d.toLocaleString('en-GB', {
      day:    '2-digit',
      month:  'short',
      hour:   '2-digit',
      minute: '2-digit',
      timeZone: 'UTC',
      timeZoneName: 'short',
    });
  } catch {
    return iso;
  }
}

// ── AnomalyCard ────────────────────────────────────────────────────────────────

function AnomalyCard({ item }: { item: AuditAnomalyItem }) {
  const cfg = SEVERITY_CONFIG[item.severity as Sev] ?? SEVERITY_CONFIG.Low;

  return (
    <div className={`rounded-lg border border-gray-200 bg-white border-l-4 ${cfg.border} px-5 py-4`}>
      <div className="flex items-start gap-3">
        <span className={`text-xl mt-0.5 ${cfg.icon}`}>
          <i className={cfg.icon.split(' ')[0]} />
        </span>
        <div className="flex-1 min-w-0">
          <div className="flex items-center gap-2 flex-wrap">
            <span className="text-sm font-semibold text-gray-800">{item.title}</span>
            <span className={`inline-flex items-center rounded-full border px-2 py-0.5 text-[10px] font-medium uppercase ${cfg.badge}`}>
              {item.severity}
            </span>
            <span className="text-[10px] font-mono text-gray-400 bg-gray-100 rounded px-1.5 py-0.5">
              {item.ruleKey}
            </span>
          </div>

          <p className="mt-1.5 text-xs text-gray-600 leading-relaxed">{item.description}</p>

          {/* Metric row */}
          <div className="mt-3 flex flex-wrap gap-3 text-xs">
            <div className="flex flex-col items-center rounded bg-gray-50 border border-gray-100 px-3 py-1.5 min-w-[80px]">
              <span className="text-gray-400 text-[10px] uppercase tracking-wide">Recent 24h</span>
              <span className="font-bold text-gray-800 text-base mt-0.5">{item.recentValue.toLocaleString()}</span>
            </div>
            {item.baselineValue !== null && (
              <div className="flex flex-col items-center rounded bg-gray-50 border border-gray-100 px-3 py-1.5 min-w-[80px]">
                <span className="text-gray-400 text-[10px] uppercase tracking-wide">Baseline ref</span>
                <span className="font-bold text-gray-800 text-base mt-0.5">
                  {typeof item.baselineValue === 'number' && item.baselineValue % 1 !== 0
                    ? item.baselineValue.toFixed(1)
                    : item.baselineValue.toLocaleString()}
                </span>
              </div>
            )}
            <div className="flex flex-col items-center rounded bg-gray-50 border border-gray-100 px-3 py-1.5 min-w-[80px]">
              <span className="text-gray-400 text-[10px] uppercase tracking-wide">Actual</span>
              <span className="font-bold text-gray-800 text-base mt-0.5">{item.actualValue.toFixed(1)}</span>
            </div>
            <div className="flex flex-col items-center rounded bg-gray-50 border border-gray-100 px-3 py-1.5 min-w-[80px]">
              <span className="text-gray-400 text-[10px] uppercase tracking-wide">Threshold</span>
              <span className="font-bold text-gray-400 text-base mt-0.5">{item.threshold}</span>
            </div>
          </div>

          {/* Context tags */}
          {(item.affectedActorId || item.affectedTenantId || item.affectedEventType) && (
            <div className="mt-2 flex flex-wrap gap-1.5 text-[10px]">
              {item.affectedActorId && (
                <span className="rounded bg-indigo-50 border border-indigo-200 px-2 py-0.5 text-indigo-700 font-mono">
                  Actor: {item.affectedActorName ?? item.affectedActorId}
                </span>
              )}
              {item.affectedTenantId && (
                <span className="rounded bg-purple-50 border border-purple-200 px-2 py-0.5 text-purple-700 font-mono">
                  Tenant: {item.affectedTenantId}
                </span>
              )}
              {item.affectedEventType && (
                <span className="rounded bg-gray-100 border border-gray-200 px-2 py-0.5 text-gray-600 font-mono">
                  {item.affectedEventType}
                </span>
              )}
            </div>
          )}

          {/* Drill-down */}
          <div className="mt-3">
            <a
              href={item.drillDownPath}
              className="inline-flex items-center gap-1 rounded bg-indigo-50 px-3 py-1 text-xs font-medium text-indigo-600 hover:bg-indigo-100 transition-colors"
            >
              <i className="ri-search-eye-line" />
              Investigate
            </a>
          </div>
        </div>
      </div>
    </div>
  );
}

// ── Main panel ─────────────────────────────────────────────────────────────────

interface Props {
  data:             AuditAnomalyData | null;
  initialTenantId:  string | undefined;
}

export function AuditAnomalyPanel({ data, initialTenantId }: Props) {
  const router = useRouter();
  const [isPending, startTransition] = useTransition();
  const [tenantId, setTenantId]      = useState(initialTenantId ?? '');

  function applyFilter() {
    const qs = new URLSearchParams();
    if (tenantId) qs.set('tenantId', tenantId);
    startTransition(() => {
      router.push(`/synqaudit/anomalies${qs.size > 0 ? `?${qs.toString()}` : ''}`);
    });
  }

  const highCount   = data?.anomalies.filter((a) => a.severity === 'High').length   ?? 0;
  const medCount    = data?.anomalies.filter((a) => a.severity === 'Medium').length ?? 0;
  const lowCount    = data?.anomalies.filter((a) => a.severity === 'Low').length    ?? 0;
  const totalCount  = data?.totalAnomalies ?? 0;

  return (
    <div className="space-y-5">
      {/* ── Filter bar ─────────────────────────────────────────────────────── */}
      <div className="rounded-lg border border-gray-200 bg-white px-5 py-4">
        <div className="flex flex-wrap items-end gap-3">
          <div>
            <label className="block text-xs font-medium text-gray-500 mb-1">Tenant ID</label>
            <input
              type="text"
              placeholder="All tenants (platform admin)"
              value={tenantId}
              onChange={(e) => setTenantId(e.target.value)}
              className="rounded border border-gray-300 px-2 py-1 text-sm text-gray-700 w-64 focus:outline-none focus:ring-1 focus:ring-indigo-400"
            />
          </div>
          <button
            onClick={applyFilter}
            disabled={isPending}
            className="rounded bg-indigo-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-indigo-700 disabled:opacity-60 transition-colors"
          >
            {isPending ? 'Loading…' : 'Refresh'}
          </button>
        </div>
        {data && (
          <p className="mt-2 text-xs text-gray-400">
            Evaluated at {formatUtcShort(data.evaluatedAt)}
            {' · '}Recent: {formatUtcShort(data.recentWindowFrom)} → {formatUtcShort(data.recentWindowTo)}
            {' · '}Baseline: {formatUtcShort(data.baselineWindowFrom)} → {formatUtcShort(data.baselineWindowTo)}
            {data.effectiveTenantId ? ` · Tenant: ${data.effectiveTenantId}` : ' · All tenants'}
          </p>
        )}
      </div>

      {/* ── No data ─────────────────────────────────────────────────────────── */}
      {!data && (
        <div className="rounded-lg border border-dashed border-gray-200 bg-gray-50 px-6 py-12 text-center">
          <i className="ri-alarm-warning-line text-3xl text-gray-300" />
          <p className="mt-2 text-sm text-gray-500">Anomaly data unavailable.</p>
        </div>
      )}

      {data && (
        <>
          {/* ── Summary bar ───────────────────────────────────────────────── */}
          <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
            <div className="rounded-lg border border-gray-200 bg-white px-4 py-3 text-center">
              <p className="text-xs text-gray-500 uppercase tracking-wide">Total</p>
              <p className={`text-2xl font-bold mt-0.5 ${totalCount > 0 ? 'text-red-600' : 'text-emerald-600'}`}>
                {totalCount}
              </p>
            </div>
            <div className="rounded-lg border border-red-100 bg-red-50 px-4 py-3 text-center">
              <p className="text-xs text-red-500 uppercase tracking-wide">High</p>
              <p className="text-2xl font-bold text-red-700 mt-0.5">{highCount}</p>
            </div>
            <div className="rounded-lg border border-amber-100 bg-amber-50 px-4 py-3 text-center">
              <p className="text-xs text-amber-500 uppercase tracking-wide">Medium</p>
              <p className="text-2xl font-bold text-amber-600 mt-0.5">{medCount}</p>
            </div>
            <div className="rounded-lg border border-blue-100 bg-blue-50 px-4 py-3 text-center">
              <p className="text-xs text-blue-500 uppercase tracking-wide">Low</p>
              <p className="text-2xl font-bold text-blue-600 mt-0.5">{lowCount}</p>
            </div>
          </div>

          {/* ── Empty state ──────────────────────────────────────────────────── */}
          {totalCount === 0 && (
            <div className="rounded-lg border border-emerald-200 bg-emerald-50 px-6 py-10 text-center">
              <i className="ri-shield-check-fill text-4xl text-emerald-400" />
              <p className="mt-3 text-sm font-medium text-emerald-700">No anomalies detected</p>
              <p className="text-xs text-emerald-600 mt-1">
                All 7 detection rules evaluated the last 24h of activity — nothing crossed a threshold.
              </p>
              <p className="text-xs text-emerald-500 mt-3">
                Thresholds: denial spike 3×, actor/tenant/event-type concentration, governance burst 3×, severity escalation 10%
              </p>
            </div>
          )}

          {/* ── Anomaly cards ─────────────────────────────────────────────── */}
          {totalCount > 0 && (
            <div className="space-y-3">
              {data.anomalies.map((item) => (
                <AnomalyCard key={item.ruleKey} item={item} />
              ))}
            </div>
          )}

          {/* ── Rule documentation ───────────────────────────────────────────── */}
          <details className="rounded-lg border border-gray-200 bg-white">
            <summary className="cursor-pointer px-5 py-3 text-xs font-medium text-gray-600 hover:text-gray-800 flex items-center gap-2">
              <i className="ri-information-line" />
              Detection rules evaluated ({data.anomalies.length} of 7 fired)
            </summary>
            <div className="border-t border-gray-100 px-5 py-4">
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-2 text-xs text-gray-600">
                {[
                  { key: 'DENIAL_SPIKE',          sev: 'High',   desc: 'Denial events 3× the 7-day daily average (≥5 events)' },
                  { key: 'ACTOR_CONCENTRATION',    sev: 'Medium', desc: 'One actor ≥30% of all events in 24h (≥20 events)' },
                  { key: 'TENANT_CONCENTRATION',   sev: 'Medium', desc: 'One tenant ≥40% of platform events in 24h (≥50 events, platform admin)' },
                  { key: 'GOVERNANCE_BURST',       sev: 'Medium', desc: 'Governance events 3× the 7-day daily average (≥3 events)' },
                  { key: 'EXPORT_SPIKE',           sev: 'Medium', desc: 'Audit access/export events 3× daily average (≥5 events)' },
                  { key: 'SEVERITY_ESCALATION',    sev: 'High',   desc: 'Critical/Alert events >10 absolute or ≥10% of total in 24h' },
                  { key: 'EVENTTYPE_CONCENTRATION', sev: 'Low',   desc: 'One event type ≥50% of all events in 24h (≥30 events)' },
                ].map((rule) => {
                  const fired  = data.anomalies.some((a) => a.ruleKey === rule.key);
                  const sevCfg = SEVERITY_CONFIG[rule.sev as Sev];
                  return (
                    <div key={rule.key} className={`flex items-start gap-2 rounded border p-2 ${fired ? 'border-red-200 bg-red-50' : 'border-gray-100 bg-gray-50'}`}>
                      <span className={`mt-0.5 h-2 w-2 shrink-0 rounded-full ${fired ? sevCfg.dot : 'bg-gray-300'}`} />
                      <div>
                        <span className="font-mono font-medium text-gray-700">{rule.key}</span>
                        <span className={`ml-1.5 inline-flex rounded-full border px-1.5 py-px text-[9px] uppercase font-medium ${sevCfg.badge}`}>{rule.sev}</span>
                        <p className="text-gray-500 mt-0.5">{rule.desc}</p>
                      </div>
                    </div>
                  );
                })}
              </div>
            </div>
          </details>
        </>
      )}
    </div>
  );
}
