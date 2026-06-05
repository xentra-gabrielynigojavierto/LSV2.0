'use client';

import { useState, useTransition }   from 'react';
import { useRouter }                 from 'next/navigation';
import type { AuditAlertItem, AuditAlertListData, AlertStatus, AuditAlertSeverity } from '@/types/control-center';
import { auditAlertsClientApi }      from '@/lib/audit-alerts-client-api';

// ── Style maps ─────────────────────────────────────────────────────────────────

const SEV_STYLE: Record<AuditAlertSeverity, { badge: string; border: string; dot: string }> = {
  High:   { badge: 'bg-red-100 text-red-700 border-red-200',    border: 'border-l-red-500',    dot: 'bg-red-500' },
  Medium: { badge: 'bg-amber-100 text-amber-700 border-amber-200', border: 'border-l-amber-400', dot: 'bg-amber-400' },
  Low:    { badge: 'bg-blue-100 text-blue-700 border-blue-200',  border: 'border-l-blue-400',   dot: 'bg-blue-400' },
};

const STATUS_STYLE: Record<AlertStatus, string> = {
  Open:         'bg-red-50 text-red-700 border-red-200',
  Acknowledged: 'bg-amber-50 text-amber-700 border-amber-200',
  Resolved:     'bg-emerald-50 text-emerald-700 border-emerald-200',
};

// ── Helpers ───────────────────────────────────────────────────────────────────

function fmtUtc(iso: string | null | undefined): string {
  if (!iso) return '—';
  try {
    return new Date(iso).toLocaleString('en-GB', {
      day: '2-digit', month: 'short', year: 'numeric',
      hour: '2-digit', minute: '2-digit', timeZone: 'UTC', timeZoneName: 'short',
    });
  } catch { return iso; }
}

function relTime(iso: string): string {
  const diff = Date.now() - new Date(iso).getTime();
  const m = Math.floor(diff / 60000);
  if (m < 60)  return `${m}m ago`;
  const h = Math.floor(m / 60);
  if (h < 24)  return `${h}h ago`;
  const d = Math.floor(h / 24);
  return `${d}d ago`;
}

// ── AlertRow ──────────────────────────────────────────────────────────────────

interface AlertRowProps {
  item:          AuditAlertItem;
  onAcknowledge: (id: string) => void;
  onResolve:     (id: string) => void;
  loading:       boolean;
}

function AlertRow({ item, onAcknowledge, onResolve, loading }: AlertRowProps) {
  const sev = SEV_STYLE[item.severity] ?? SEV_STYLE.Low;

  // Parse context JSON for display
  let ctx: Record<string, unknown> | null = null;
  try { if (item.contextJson) ctx = JSON.parse(item.contextJson); } catch { /* ignore */ }

  return (
    <div className={`rounded-lg border border-gray-200 bg-white border-l-4 ${sev.border} px-5 py-4`}>
      <div className="flex items-start gap-3">
        <span className={`mt-1 h-2 w-2 shrink-0 rounded-full ${sev.dot}`} />
        <div className="flex-1 min-w-0">
          {/* Header row */}
          <div className="flex flex-wrap items-center gap-2">
            <span className="text-sm font-semibold text-gray-800">{item.title}</span>
            <span className={`inline-flex items-center rounded-full border px-2 py-0.5 text-[10px] font-medium uppercase ${sev.badge}`}>
              {item.severity}
            </span>
            <span className={`inline-flex items-center rounded-full border px-2 py-0.5 text-[10px] font-medium ${STATUS_STYLE[item.status]}`}>
              {item.status}
            </span>
            <span className="text-[10px] font-mono text-gray-400 bg-gray-100 rounded px-1.5 py-0.5">
              {item.ruleKey}
            </span>
            {item.detectionCount > 1 && (
              <span className="text-[10px] text-gray-500 bg-gray-50 border border-gray-200 rounded px-1.5 py-0.5">
                {item.detectionCount}× detected
              </span>
            )}
          </div>

          {/* Description */}
          <p className="mt-1.5 text-xs text-gray-600 leading-relaxed">{item.description}</p>

          {/* Context */}
          {ctx && (
            <div className="mt-2 flex flex-wrap gap-2 text-[10px]">
              {['recentValue', 'baselineValue', 'actualValue', 'threshold'].map((k) =>
                ctx![k] !== undefined ? (
                  <span key={k} className="rounded bg-gray-50 border border-gray-200 px-2 py-0.5 text-gray-500 font-mono">
                    {k.replace(/([A-Z])/g, ' $1').trim()}: <strong className="text-gray-700">{String(ctx![k])}</strong>
                  </span>
                ) : null
              )}
              {typeof ctx['affectedActorName'] === 'string' && (
                <span className="rounded bg-indigo-50 border border-indigo-200 px-2 py-0.5 text-indigo-700 font-mono">
                  Actor: {ctx['affectedActorName']}
                </span>
              )}
              {typeof ctx['affectedTenantId'] === 'string' && (
                <span className="rounded bg-purple-50 border border-purple-200 px-2 py-0.5 text-purple-700 font-mono">
                  Tenant: {ctx['affectedTenantId']}
                </span>
              )}
              {typeof ctx['affectedEventType'] === 'string' && (
                <span className="rounded bg-gray-100 border border-gray-200 px-2 py-0.5 text-gray-600 font-mono">
                  {ctx['affectedEventType']}
                </span>
              )}
            </div>
          )}

          {/* Tenant / scope */}
          {(item.tenantId || item.scopeType === 'Platform') && (
            <div className="mt-1.5 text-[10px] text-gray-400">
              {item.scopeType === 'Platform'
                ? 'Platform-wide alert'
                : `Tenant: ${item.tenantId}`}
            </div>
          )}

          {/* Timeline */}
          <div className="mt-2 flex flex-wrap gap-3 text-[10px] text-gray-400">
            <span>First detected: {relTime(item.firstDetectedAtUtc)} ({fmtUtc(item.firstDetectedAtUtc)})</span>
            {item.firstDetectedAtUtc !== item.lastDetectedAtUtc && (
              <span>Last: {relTime(item.lastDetectedAtUtc)}</span>
            )}
            {item.acknowledgedBy && (
              <span className="text-amber-600">Acknowledged by {item.acknowledgedBy} · {relTime(item.acknowledgedAtUtc!)}</span>
            )}
            {item.resolvedBy && (
              <span className="text-emerald-600">Resolved by {item.resolvedBy} · {relTime(item.resolvedAtUtc!)}</span>
            )}
          </div>

          {/* Actions */}
          <div className="mt-3 flex flex-wrap gap-2">
            {item.drillDownPath && (
              <a
                href={item.drillDownPath}
                className="inline-flex items-center gap-1 rounded bg-indigo-50 px-3 py-1 text-xs font-medium text-indigo-600 hover:bg-indigo-100 transition-colors"
              >
                <i className="ri-search-eye-line" />
                Investigate
              </a>
            )}
            <a
              href="/synqaudit/anomalies"
              className="inline-flex items-center gap-1 rounded bg-gray-50 px-3 py-1 text-xs font-medium text-gray-500 hover:bg-gray-100 transition-colors border border-gray-200"
            >
              <i className="ri-alarm-warning-line" />
              Anomaly View
            </a>
            {item.status !== 'Acknowledged' && item.status !== 'Resolved' && (
              <button
                onClick={() => onAcknowledge(item.alertId)}
                disabled={loading}
                className="inline-flex items-center gap-1 rounded bg-amber-50 px-3 py-1 text-xs font-medium text-amber-700 hover:bg-amber-100 disabled:opacity-50 transition-colors border border-amber-200"
              >
                <i className="ri-eye-line" />
                Acknowledge
              </button>
            )}
            {item.status !== 'Resolved' && (
              <button
                onClick={() => onResolve(item.alertId)}
                disabled={loading}
                className="inline-flex items-center gap-1 rounded bg-emerald-50 px-3 py-1 text-xs font-medium text-emerald-700 hover:bg-emerald-100 disabled:opacity-50 transition-colors border border-emerald-200"
              >
                <i className="ri-check-double-line" />
                Resolve
              </button>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}

// ── Main Panel ────────────────────────────────────────────────────────────────

interface Props {
  initialData:     AuditAlertListData | null;
  initialStatus:   string | undefined;
  initialTenantId: string | undefined;
}

export function AuditAlertPanel({ initialData, initialStatus, initialTenantId }: Props) {
  const router = useRouter();
  const [isPending, startTransition] = useTransition();

  const [data,       setData]       = useState<AuditAlertListData | null>(initialData);
  const [tenantId,   setTenantId]   = useState(initialTenantId ?? '');
  const [statusFilt, setStatusFilt] = useState<string>(initialStatus ?? '');
  const [actionBusy, setActionBusy] = useState(false);
  const [evalResult, setEvalResult] = useState<string | null>(null);

  // ── Filter ────────────────────────────────────────────────────────────────

  function applyFilter() {
    const qs = new URLSearchParams();
    if (tenantId)   qs.set('tenantId', tenantId);
    if (statusFilt) qs.set('status',   statusFilt);
    startTransition(() => {
      router.push(`/synqaudit/alerts${qs.size > 0 ? `?${qs.toString()}` : ''}`);
    });
  }

  // ── Evaluate ──────────────────────────────────────────────────────────────

  async function handleEvaluate() {
    setActionBusy(true);
    setEvalResult(null);
    try {
      const result = await auditAlertsClientApi.evaluate({ tenantId: tenantId || undefined });
      if (result) {
        setEvalResult(
          `Evaluation complete — ${result.anomaliesDetected} anomalies detected. ` +
          `${result.alertsCreated} created, ${result.alertsRefreshed} refreshed, ${result.alertsSuppressed} suppressed.`
        );
        // Reload alert list
        const refreshed = await auditAlertsClientApi.list({
          status: statusFilt || undefined,
          tenantId: tenantId || undefined,
          limit: 100,
        });
        setData(refreshed);
      }
    } finally {
      setActionBusy(false);
    }
  }

  // ── Acknowledge ───────────────────────────────────────────────────────────

  async function handleAcknowledge(alertId: string) {
    setActionBusy(true);
    try {
      await auditAlertsClientApi.acknowledge(alertId);
      const refreshed = await auditAlertsClientApi.list({
        status: statusFilt || undefined,
        tenantId: tenantId || undefined,
        limit: 100,
      });
      setData(refreshed);
    } finally {
      setActionBusy(false);
    }
  }

  // ── Resolve ───────────────────────────────────────────────────────────────

  async function handleResolve(alertId: string) {
    setActionBusy(true);
    try {
      await auditAlertsClientApi.resolve(alertId);
      const refreshed = await auditAlertsClientApi.list({
        status: statusFilt || undefined,
        tenantId: tenantId || undefined,
        limit: 100,
      });
      setData(refreshed);
    } finally {
      setActionBusy(false);
    }
  }

  const busy = isPending || actionBusy;

  return (
    <div className="space-y-5">
      {/* ── Controls ─────────────────────────────────────────────────────── */}
      <div className="rounded-lg border border-gray-200 bg-white px-5 py-4">
        <div className="flex flex-wrap items-end gap-3">
          <div>
            <label className="block text-xs font-medium text-gray-500 mb-1">Status</label>
            <select
              value={statusFilt}
              onChange={(e) => setStatusFilt(e.target.value)}
              className="rounded border border-gray-300 px-2 py-1 text-sm text-gray-700 focus:outline-none focus:ring-1 focus:ring-indigo-400"
            >
              <option value="">All statuses</option>
              <option value="Open">Open</option>
              <option value="Acknowledged">Acknowledged</option>
              <option value="Resolved">Resolved</option>
            </select>
          </div>
          <div>
            <label className="block text-xs font-medium text-gray-500 mb-1">Tenant ID</label>
            <input
              type="text"
              placeholder="All tenants"
              value={tenantId}
              onChange={(e) => setTenantId(e.target.value)}
              className="rounded border border-gray-300 px-2 py-1 text-sm text-gray-700 w-52 focus:outline-none focus:ring-1 focus:ring-indigo-400"
            />
          </div>
          <button
            onClick={applyFilter}
            disabled={busy}
            className="rounded bg-gray-100 px-3 py-1.5 text-sm font-medium text-gray-700 hover:bg-gray-200 disabled:opacity-60 transition-colors border border-gray-200"
          >
            {isPending ? 'Loading…' : 'Filter'}
          </button>
          <button
            onClick={handleEvaluate}
            disabled={busy}
            className="rounded bg-indigo-600 px-4 py-1.5 text-sm font-medium text-white hover:bg-indigo-700 disabled:opacity-60 transition-colors flex items-center gap-1.5"
          >
            <i className="ri-play-line" />
            {actionBusy ? 'Evaluating…' : 'Evaluate Now'}
          </button>
        </div>
        {evalResult && (
          <p className="mt-2 text-xs text-indigo-700 bg-indigo-50 border border-indigo-100 rounded px-3 py-1.5">
            {evalResult}
          </p>
        )}
      </div>

      {/* ── Summary counters ──────────────────────────────────────────────── */}
      {data && (
        <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
          <div className="rounded-lg border border-gray-200 bg-white px-4 py-3 text-center">
            <p className="text-xs text-gray-500 uppercase tracking-wide">Total</p>
            <p className={`text-2xl font-bold mt-0.5 ${(data.openCount + data.acknowledgedCount) > 0 ? 'text-red-600' : 'text-emerald-600'}`}>
              {data.openCount + data.acknowledgedCount + data.resolvedCount}
            </p>
          </div>
          <div className="rounded-lg border border-red-100 bg-red-50 px-4 py-3 text-center">
            <p className="text-xs text-red-500 uppercase tracking-wide">Open</p>
            <p className="text-2xl font-bold text-red-700 mt-0.5">{data.openCount}</p>
          </div>
          <div className="rounded-lg border border-amber-100 bg-amber-50 px-4 py-3 text-center">
            <p className="text-xs text-amber-500 uppercase tracking-wide">Acknowledged</p>
            <p className="text-2xl font-bold text-amber-600 mt-0.5">{data.acknowledgedCount}</p>
          </div>
          <div className="rounded-lg border border-emerald-100 bg-emerald-50 px-4 py-3 text-center">
            <p className="text-xs text-emerald-500 uppercase tracking-wide">Resolved</p>
            <p className="text-2xl font-bold text-emerald-600 mt-0.5">{data.resolvedCount}</p>
          </div>
        </div>
      )}

      {/* ── No data ───────────────────────────────────────────────────────── */}
      {!data && (
        <div className="rounded-lg border border-dashed border-gray-200 bg-gray-50 px-6 py-12 text-center">
          <i className="ri-notification-badge-line text-3xl text-gray-300" />
          <p className="mt-2 text-sm text-gray-500">Alert data unavailable.</p>
        </div>
      )}

      {/* ── Alert list ────────────────────────────────────────────────────── */}
      {data && (
        <>
          {data.alerts.length === 0 ? (
            <div className="rounded-lg border border-emerald-200 bg-emerald-50 px-6 py-10 text-center">
              <i className="ri-shield-check-fill text-4xl text-emerald-400" />
              <p className="mt-3 text-sm font-medium text-emerald-700">No alerts found</p>
              <p className="text-xs text-emerald-600 mt-1">
                {statusFilt
                  ? `No ${statusFilt.toLowerCase()} alerts.`
                  : 'Click "Evaluate Now" to run anomaly detection and generate alerts from current conditions.'}
              </p>
            </div>
          ) : (
            <div className="space-y-3">
              {data.alerts.map((item) => (
                <AlertRow
                  key={item.alertId}
                  item={item}
                  onAcknowledge={handleAcknowledge}
                  onResolve={handleResolve}
                  loading={busy}
                />
              ))}
            </div>
          )}

          {/* ── Lifecycle guide ───────────────────────────────────────────── */}
          <details className="rounded-lg border border-gray-200 bg-white">
            <summary className="cursor-pointer px-5 py-3 text-xs font-medium text-gray-600 hover:text-gray-800 flex items-center gap-2">
              <i className="ri-information-line" />
              Alert lifecycle and deduplication rules
            </summary>
            <div className="border-t border-gray-100 px-5 py-4 text-xs text-gray-600 space-y-2">
              <p><strong>Evaluation:</strong> Click "Evaluate Now" to run all 7 anomaly detection rules and upsert alert records.</p>
              <p><strong>Deduplication:</strong> Each anomaly condition has a deterministic fingerprint. Re-detecting the same condition while an alert is Open or Acknowledged refreshes it (increments detection count) rather than creating a duplicate.</p>
              <p><strong>Cooldown:</strong> After an alert is Resolved, re-detection within 1 hour is suppressed. After 1 hour, a new alert is created.</p>
              <p><strong>Acknowledge:</strong> Marks the alert as seen. The condition may still be active.</p>
              <p><strong>Resolve:</strong> Marks the alert as handled. Starts the 1-hour cooldown window.</p>
              <p><strong>Recurrence:</strong> If the same condition reappears after the cooldown, a new alert record is created (preserving the history of the previous episode).</p>
              <p><strong>Notifications:</strong> Deferred to a future ticket. Alert records are available for integration with external notification systems.</p>
            </div>
          </details>
        </>
      )}
    </div>
  );
}
