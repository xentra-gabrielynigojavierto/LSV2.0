'use client';

/**
 * AlertsPanel — LS-NOTIF-SMS-012
 *
 * Interactive client component for SMS operational alert management.
 *
 * Features:
 *   - Filter by status, severity, and alert type
 *   - Resolve alert (with optional resolution note)
 *   - Suppress alert (configurable duration: 15m / 1h / 4h / 24h / 7d)
 *   - Trigger manual evaluation cycle
 *   - Pagination (offset-based, page size configurable)
 *   - router.refresh() after each mutation for server-side re-fetch
 *
 * Security:
 *   - No credentials, raw targets, or phone numbers rendered.
 *   - Evaluate action is guarded by ConfirmDialog.
 */

import { useState, useTransition, useCallback } from 'react';
import { useRouter }                            from 'next/navigation';
import { ConfirmDialog }                        from '@/components/ui/confirm-dialog';
import { smsIncidentsClientApi }                from '@/lib/sms-incidents-client-api';
import type {
  SmsAlertListResult,
  SmsAlertSummaryDto,
  SmsAlertDto,
}                                               from '@/lib/sms-incidents-api';

// ── Helpers ───────────────────────────────────────────────────────────────────

function fmtN(n: number | null | undefined): string {
  if (n == null) return '—';
  return n.toLocaleString();
}

function fmtUtc(s: string | null | undefined): string {
  if (!s) return '—';
  try { return new Date(s).toLocaleString('en-US', { timeZone: 'UTC', hour12: false }); }
  catch { return s; }
}

function SeverityBadge({ severity }: { severity: string }) {
  const cls = severity === 'critical'
    ? 'bg-red-100 text-red-800 border-red-200'
    : 'bg-amber-100 text-amber-800 border-amber-200';
  return (
    <span className={`px-2 py-0.5 rounded-full text-[11px] font-semibold border uppercase tracking-wide ${cls}`}>
      {severity}
    </span>
  );
}

function StatusBadge({ status }: { status: string }) {
  const map: Record<string, string> = {
    active:     'bg-red-100 text-red-800 border-red-200',
    resolved:   'bg-emerald-100 text-emerald-800 border-emerald-200',
    suppressed: 'bg-gray-100 text-gray-700 border-gray-200',
  };
  const cls = map[status] ?? 'bg-gray-100 text-gray-700 border-gray-200';
  return (
    <span className={`px-2 py-0.5 rounded-full text-[11px] font-semibold border capitalize ${cls}`}>
      {status}
    </span>
  );
}

// ── Props ─────────────────────────────────────────────────────────────────────

interface AlertsPanelProps {
  initialList:      SmsAlertListResult | null;
  initialSummary:   SmsAlertSummaryDto | null;
  initialStatus?:   string;
  initialSeverity?: string;
  initialAlertType?: string;
  initialOffset:    number;
  pageSize:         number;
}

// ── Dialogs ───────────────────────────────────────────────────────────────────

type DialogState =
  | { kind: 'resolve';  alert: SmsAlertDto }
  | { kind: 'suppress'; alert: SmsAlertDto }
  | { kind: 'evaluate' }
  | null;

const SUPPRESS_OPTIONS = [
  { label: '15 minutes',  value: 15   },
  { label: '1 hour',      value: 60   },
  { label: '4 hours',     value: 240  },
  { label: '24 hours',    value: 1440 },
  { label: '7 days',      value: 10080 },
];

// ── Main Component ────────────────────────────────────────────────────────────

export function AlertsPanel({
  initialList,
  initialSummary,
  initialStatus,
  initialSeverity,
  initialAlertType,
  initialOffset,
  pageSize,
}: AlertsPanelProps) {
  const router = useRouter();
  const [isPending, startTransition] = useTransition();

  const [dialog,        setDialog]       = useState<DialogState>(null);
  const [actionErr,     setActionErr]    = useState<string | null>(null);
  const [actionOk,      setActionOk]     = useState<string | null>(null);
  const [resolutionNote, setResolutionNote] = useState('');
  const [suppressMins,  setSuppressMins]  = useState(60);

  const list    = initialList;
  const summary = initialSummary;
  const offset  = initialOffset;
  const total   = list?.total ?? 0;

  // ── Filter navigation ─────────────────────────────────────────────────────

  function navigate(params: Record<string, string | undefined>) {
    const sp = new URLSearchParams();
    const merged = {
      status: initialStatus, severity: initialSeverity, alertType: initialAlertType,
      offset: String(initialOffset),
      ...params,
    };
    for (const [k, v] of Object.entries(merged)) {
      if (v && v !== '0') sp.set(k, v);
    }
    router.push(`/notifications/sms-incidents/alerts?${sp.toString()}`);
  }

  // ── Actions ───────────────────────────────────────────────────────────────

  const handleResolve = useCallback(async () => {
    if (dialog?.kind !== 'resolve') return;
    const alertId = dialog.alert.id;
    setDialog(null);
    setActionErr(null);
    startTransition(async () => {
      const ok = await smsIncidentsClientApi.resolveAlert(alertId, resolutionNote.trim() || undefined);
      if (ok) {
        setActionOk('Alert resolved.');
        setResolutionNote('');
        router.refresh();
      } else {
        setActionErr('Failed to resolve alert. It may already be resolved.');
      }
    });
  }, [dialog, resolutionNote, router]);

  const handleSuppress = useCallback(async () => {
    if (dialog?.kind !== 'suppress') return;
    const alertId = dialog.alert.id;
    setDialog(null);
    setActionErr(null);
    startTransition(async () => {
      const ok = await smsIncidentsClientApi.suppressAlert(alertId, suppressMins);
      if (ok) {
        setActionOk(`Alert suppressed for ${SUPPRESS_OPTIONS.find(o => o.value === suppressMins)?.label ?? `${suppressMins} min`}.`);
        router.refresh();
      } else {
        setActionErr('Failed to suppress alert.');
      }
    });
  }, [dialog, suppressMins, router]);

  const handleEvaluate = useCallback(async () => {
    setDialog(null);
    setActionErr(null);
    startTransition(async () => {
      const ok = await smsIncidentsClientApi.evaluateAlerts();
      if (ok) {
        setActionOk('Evaluation cycle triggered. Refresh to see new alerts.');
        router.refresh();
      } else {
        setActionErr('Failed to trigger evaluation cycle.');
      }
    });
  }, [router]);

  // ── Render ────────────────────────────────────────────────────────────────

  return (
    <>
      {/* ── Dialogs ─────────────────────────────────────────────────────── */}

      {dialog?.kind === 'resolve' && (
        <ConfirmDialog
          title={`Resolve alert?`}
          description={`This will mark the alert "${dialog.alert.alertType}" as resolved. You can optionally add a note.`}
          confirmLabel="Resolve"
          variant="neutral"
          isPending={isPending}
          onConfirm={handleResolve}
          onCancel={() => setDialog(null)}
        />
      )}

      {dialog?.kind === 'suppress' && (
        <ConfirmDialog
          title="Suppress alert?"
          description={`Suppression stops this alert condition from re-triggering for the selected period. Duration: ${
            SUPPRESS_OPTIONS.find(o => o.value === suppressMins)?.label ?? `${suppressMins} min`
          }.`}
          confirmLabel="Suppress"
          variant="warning"
          isPending={isPending}
          onConfirm={handleSuppress}
          onCancel={() => setDialog(null)}
        />
      )}

      {dialog?.kind === 'evaluate' && (
        <ConfirmDialog
          title="Trigger evaluation cycle?"
          description="This runs one SMS alert evaluation cycle immediately. It creates or updates alerts based on current delivery data. No notifications or SMS messages are sent."
          confirmLabel="Run Evaluation"
          variant="neutral"
          isPending={isPending}
          onConfirm={handleEvaluate}
          onCancel={() => setDialog(null)}
        />
      )}

      {/* ── Status banner ─────────────────────────────────────────────── */}
      {actionOk && (
        <div className="flex items-center gap-2 px-4 py-3 rounded-lg bg-emerald-50 border border-emerald-200 text-sm text-emerald-800">
          <i className="ri-checkbox-circle-line" aria-hidden /> {actionOk}
          <button onClick={() => setActionOk(null)} className="ml-auto text-emerald-600 hover:text-emerald-800" aria-label="Dismiss">
            <i className="ri-close-line" aria-hidden />
          </button>
        </div>
      )}
      {actionErr && (
        <div className="flex items-center gap-2 px-4 py-3 rounded-lg bg-red-50 border border-red-200 text-sm text-red-700">
          <i className="ri-error-warning-line" aria-hidden /> {actionErr}
          <button onClick={() => setActionErr(null)} className="ml-auto text-red-500 hover:text-red-700" aria-label="Dismiss">
            <i className="ri-close-line" aria-hidden />
          </button>
        </div>
      )}

      {/* ── Summary cards ─────────────────────────────────────────────── */}
      {summary && (
        <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
          {[
            { label: 'Active',     value: summary.activeCount,         color: summary.activeCount > 0 ? 'text-red-700' : '' },
            { label: 'Critical',   value: summary.criticalActiveCount, color: summary.criticalActiveCount > 0 ? 'text-red-700' : '' },
            { label: 'Warning',    value: summary.warningActiveCount,  color: summary.warningActiveCount > 0 ? 'text-amber-700' : '' },
            { label: 'Resolved',   value: summary.resolvedCount,       color: 'text-emerald-700' },
          ].map(({ label, value, color }) => (
            <div key={label} className="bg-white border border-gray-200 rounded-lg px-4 py-3">
              <p className="text-xs font-medium text-gray-500 uppercase tracking-wide">{label}</p>
              <p className={`mt-0.5 text-xl font-bold tabular-nums ${color || 'text-gray-900'}`}>{fmtN(value)}</p>
            </div>
          ))}
        </div>
      )}

      {/* ── Toolbar ───────────────────────────────────────────────────── */}
      <div className="flex flex-wrap items-center gap-3">
        {/* Status filter */}
        <select
          value={initialStatus ?? ''}
          onChange={e => navigate({ status: e.target.value || undefined, offset: '0' })}
          className="text-sm border border-gray-200 rounded-md px-3 py-1.5 bg-white focus:outline-none focus:ring-2 focus:ring-indigo-300"
          aria-label="Filter by status"
        >
          <option value="">All statuses</option>
          <option value="active">Active</option>
          <option value="resolved">Resolved</option>
          <option value="suppressed">Suppressed</option>
        </select>

        {/* Severity filter */}
        <select
          value={initialSeverity ?? ''}
          onChange={e => navigate({ severity: e.target.value || undefined, offset: '0' })}
          className="text-sm border border-gray-200 rounded-md px-3 py-1.5 bg-white focus:outline-none focus:ring-2 focus:ring-indigo-300"
          aria-label="Filter by severity"
        >
          <option value="">All severities</option>
          <option value="warning">Warning</option>
          <option value="critical">Critical</option>
        </select>

        <span className="ml-auto text-sm text-gray-500 tabular-nums">
          {fmtN(total)} alert{total !== 1 ? 's' : ''}
        </span>

        <button
          onClick={() => setDialog({ kind: 'evaluate' })}
          disabled={isPending}
          className="flex items-center gap-1.5 px-3 py-1.5 text-sm font-medium rounded-md bg-indigo-600 text-white hover:bg-indigo-700 disabled:opacity-50 transition-colors"
        >
          <i className="ri-refresh-line" aria-hidden />
          Run Evaluation
        </button>
      </div>

      {/* ── Resolve note input (only visible if resolve dialog open → actually inline) ── */}
      {/* Note: We handle resolve note as a pre-dialog input field below the table */}

      {/* ── Alert table ───────────────────────────────────────────────── */}
      {!list || list.items.length === 0 ? (
        <div className="bg-white border border-gray-200 rounded-lg px-6 py-10 text-center text-sm text-gray-400">
          <i className="ri-checkbox-circle-line text-3xl block mb-2 text-gray-300" aria-hidden />
          No alerts match the current filters.
        </div>
      ) : (
        <div className="rounded-lg border border-gray-200 bg-white overflow-hidden">
          <div className="overflow-x-auto">
            <table className="min-w-full divide-y divide-gray-100 text-sm">
              <thead className="bg-gray-50 text-xs text-gray-500 uppercase tracking-wide">
                <tr>
                  <th className="px-4 py-2.5 text-left font-medium">Type</th>
                  <th className="px-4 py-2.5 text-left font-medium">Severity</th>
                  <th className="px-4 py-2.5 text-left font-medium">Status</th>
                  <th className="px-4 py-2.5 text-left font-medium">Message</th>
                  <th className="px-4 py-2.5 text-right font-medium">Occurrences</th>
                  <th className="px-4 py-2.5 text-right font-medium">First Seen</th>
                  <th className="px-4 py-2.5 text-right font-medium">Last Seen</th>
                  <th className="px-4 py-2.5 text-right font-medium">Actions</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {list.items.map(alert => (
                  <tr key={alert.id} className="hover:bg-gray-50">
                    <td className="px-4 py-3">
                      <span className="font-mono text-[11px] text-gray-600 bg-gray-100 px-1.5 py-0.5 rounded">
                        {alert.alertType}
                      </span>
                    </td>
                    <td className="px-4 py-3">
                      <SeverityBadge severity={alert.severity} />
                    </td>
                    <td className="px-4 py-3">
                      <StatusBadge status={alert.status} />
                    </td>
                    <td className="px-4 py-3 max-w-xs">
                      <p className="text-xs text-gray-700 truncate" title={alert.message}>{alert.message}</p>
                      {alert.tenantId && (
                        <p className="text-[10px] text-gray-400 font-mono mt-0.5">
                          tenant: {alert.tenantId}
                        </p>
                      )}
                    </td>
                    <td className="px-4 py-3 text-right tabular-nums text-gray-700 font-semibold">
                      {fmtN(alert.occurrenceCount)}
                    </td>
                    <td className="px-4 py-3 text-right font-mono text-[11px] text-gray-400 whitespace-nowrap">
                      {fmtUtc(alert.firstObservedAt)}
                    </td>
                    <td className="px-4 py-3 text-right font-mono text-[11px] text-gray-400 whitespace-nowrap">
                      {fmtUtc(alert.lastObservedAt)}
                    </td>
                    <td className="px-4 py-3 text-right">
                      {alert.status === 'active' ? (
                        <div className="flex items-center justify-end gap-2">
                          <button
                            onClick={() => { setResolutionNote(''); setDialog({ kind: 'resolve', alert }); }}
                            disabled={isPending}
                            className="px-2.5 py-1 text-xs font-medium rounded-md bg-emerald-50 text-emerald-700 hover:bg-emerald-100 border border-emerald-200 disabled:opacity-50 transition-colors"
                          >
                            Resolve
                          </button>
                          <button
                            onClick={() => { setSuppressMins(60); setDialog({ kind: 'suppress', alert }); }}
                            disabled={isPending}
                            className="px-2.5 py-1 text-xs font-medium rounded-md bg-gray-50 text-gray-600 hover:bg-gray-100 border border-gray-200 disabled:opacity-50 transition-colors"
                          >
                            Suppress
                          </button>
                        </div>
                      ) : (
                        <span className="text-xs text-gray-400 italic">—</span>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* ── Resolve note input (shown inline when resolving) ─────────────
          We embed it here so the admin can type before clicking Resolve.
          The ConfirmDialog is shown after they click the row button.
          The note textarea is shown below the table so it's visible before confirming.
      ─────────────────────────────────────────────────────────────────── */}
      {dialog?.kind === 'resolve' && (
        <div className="bg-white border border-gray-200 rounded-lg p-4 space-y-2">
          <label className="block text-sm font-medium text-gray-700">
            Resolution note (optional)
          </label>
          <textarea
            value={resolutionNote}
            onChange={e => setResolutionNote(e.target.value.slice(0, 1000))}
            rows={2}
            maxLength={1000}
            placeholder="Describe why this alert is being resolved…"
            className="w-full text-sm border border-gray-200 rounded-md px-3 py-2 focus:outline-none focus:ring-2 focus:ring-indigo-300 resize-none"
          />
          <p className="text-xs text-gray-400 text-right">{resolutionNote.length}/1000</p>
        </div>
      )}

      {/* ── Suppress duration picker (inline before confirm) ───────────── */}
      {dialog?.kind === 'suppress' && (
        <div className="bg-white border border-gray-200 rounded-lg p-4 space-y-2">
          <label className="block text-sm font-medium text-gray-700">
            Suppress duration
          </label>
          <div className="flex flex-wrap gap-2">
            {SUPPRESS_OPTIONS.map(opt => (
              <button
                key={opt.value}
                onClick={() => setSuppressMins(opt.value)}
                className={[
                  'px-3 py-1.5 text-xs font-medium rounded-md border transition-colors',
                  suppressMins === opt.value
                    ? 'bg-indigo-600 text-white border-indigo-600'
                    : 'bg-white text-gray-700 border-gray-200 hover:bg-gray-50',
                ].join(' ')}
              >
                {opt.label}
              </button>
            ))}
          </div>
        </div>
      )}

      {/* ── Pagination ────────────────────────────────────────────────── */}
      {total > pageSize && (
        <div className="flex items-center justify-between text-sm text-gray-500">
          <span>
            Showing {offset + 1}–{Math.min(offset + pageSize, total)} of {fmtN(total)}
          </span>
          <div className="flex gap-2">
            <button
              disabled={offset === 0}
              onClick={() => navigate({ offset: String(Math.max(0, offset - pageSize)) })}
              className="px-3 py-1.5 rounded-md border border-gray-200 bg-white hover:bg-gray-50 disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
            >
              Previous
            </button>
            <button
              disabled={offset + pageSize >= total}
              onClick={() => navigate({ offset: String(offset + pageSize) })}
              className="px-3 py-1.5 rounded-md border border-gray-200 bg-white hover:bg-gray-50 disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
            >
              Next
            </button>
          </div>
        </div>
      )}

      <div className="text-xs text-gray-400 text-right border-t border-gray-100 pt-3">
        Read from Notification Service. No credentials or phone numbers are rendered. All timestamps UTC.
      </div>
    </>
  );
}
