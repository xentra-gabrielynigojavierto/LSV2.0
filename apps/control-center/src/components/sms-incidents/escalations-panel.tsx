'use client';

/**
 * EscalationsPanel — LS-NOTIF-SMS-012
 *
 * Interactive client component for SMS escalation attempt history.
 *
 * Features:
 *   - Filter by status, channel type, severity, alert ID, policy ID
 *   - Retry failed escalation (ConfirmDialog guarded)
 *   - Summary breakdown cards (by status, by channel)
 *   - Pagination (offset-based)
 *   - router.refresh() after mutation
 *
 * Security:
 *   - TargetMasked is the only target field displayed — never raw URLs or emails.
 *   - No credentials, phone numbers, or raw provider payloads rendered.
 */

import { useState, useTransition, useCallback } from 'react';
import { useRouter }                            from 'next/navigation';
import { ConfirmDialog }                        from '@/components/ui/confirm-dialog';
import { smsIncidentsClientApi }                from '@/lib/sms-incidents-client-api';
import type {
  SmsAlertEscalationListResult,
  SmsEscalationSummaryDto,
  SmsAlertEscalationDto,
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

function StatusBadge({ status }: { status: string }) {
  const map: Record<string, string> = {
    sent:       'bg-emerald-100 text-emerald-800 border-emerald-200',
    failed:     'bg-red-100 text-red-800 border-red-200',
    pending:    'bg-amber-100 text-amber-800 border-amber-200',
    suppressed: 'bg-gray-100 text-gray-700 border-gray-200',
    skipped:    'bg-slate-100 text-slate-600 border-slate-200',
  };
  const cls = map[status] ?? 'bg-gray-100 text-gray-600 border-gray-200';
  return (
    <span className={`px-2 py-0.5 rounded-full text-[11px] font-semibold border capitalize ${cls}`}>
      {status}
    </span>
  );
}

function ChannelBadge({ channel }: { channel: string }) {
  const map: Record<string, string> = {
    email:                 'bg-sky-100 text-sky-800',
    teams_webhook:         'bg-purple-100 text-purple-800',
    slack_webhook:         'bg-green-100 text-green-800',
    pagerduty:             'bg-orange-100 text-orange-800',
    opsgenie:              'bg-rose-100 text-rose-800',
    internal_notification: 'bg-indigo-100 text-indigo-800',
  };
  const cls = map[channel] ?? 'bg-gray-100 text-gray-700';
  return (
    <span className={`px-2 py-0.5 rounded text-[11px] font-medium capitalize ${cls}`}>
      {channel.replace(/_/g, ' ')}
    </span>
  );
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

// ── Props ─────────────────────────────────────────────────────────────────────

interface EscalationsPanelProps {
  initialList:         SmsAlertEscalationListResult | null;
  initialSummary:      SmsEscalationSummaryDto | null;
  initialStatus?:      string;
  initialChannelType?: string;
  initialSeverity?:    string;
  initialAlertId?:     string;
  initialPolicyId?:    string;
  initialOffset:       number;
  pageSize:            number;
}

// ── Main Component ────────────────────────────────────────────────────────────

export function EscalationsPanel({
  initialList,
  initialSummary,
  initialStatus,
  initialChannelType,
  initialSeverity,
  initialAlertId,
  initialPolicyId,
  initialOffset,
  pageSize,
}: EscalationsPanelProps) {
  const router = useRouter();
  const [isPending, startTransition] = useTransition();

  const [retryTarget, setRetryTarget] = useState<SmsAlertEscalationDto | null>(null);
  const [actionErr,   setActionErr]   = useState<string | null>(null);
  const [actionOk,    setActionOk]    = useState<string | null>(null);

  const list    = initialList;
  const summary = initialSummary;
  const offset  = initialOffset;
  const total   = list?.total ?? 0;

  // ── Filter navigation ─────────────────────────────────────────────────────

  function navigate(params: Record<string, string | undefined>) {
    const sp = new URLSearchParams();
    const merged: Record<string, string | undefined> = {
      status:      initialStatus,
      channelType: initialChannelType,
      severity:    initialSeverity,
      alertId:     initialAlertId,
      policyId:    initialPolicyId,
      offset:      String(initialOffset),
      ...params,
    };
    for (const [k, v] of Object.entries(merged)) {
      if (v && v !== '0') sp.set(k, v);
    }
    router.push(`/notifications/sms-incidents/escalations?${sp.toString()}`);
  }

  // ── Retry action ──────────────────────────────────────────────────────────

  const handleRetry = useCallback(async () => {
    if (!retryTarget) return;
    const id = retryTarget.id;
    setRetryTarget(null);
    setActionErr(null);
    startTransition(async () => {
      const ok = await smsIncidentsClientApi.retryEscalation(id);
      if (ok) {
        setActionOk('Escalation retry initiated.');
        router.refresh();
      } else {
        setActionErr('Failed to retry escalation. It may not be eligible for retry.');
      }
    });
  }, [retryTarget, router]);

  // ── Render ────────────────────────────────────────────────────────────────

  return (
    <>
      {/* ── Retry confirm ───────────────────────────────────────────────── */}
      {retryTarget && (
        <ConfirmDialog
          title="Retry escalation?"
          description={`Retry the escalation attempt via ${retryTarget.channelType.replace(/_/g, ' ')} to ${retryTarget.targetMasked ?? '(masked)'}. Only eligible if the attempt failed.`}
          confirmLabel="Retry"
          variant="neutral"
          isPending={isPending}
          onConfirm={handleRetry}
          onCancel={() => setRetryTarget(null)}
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
        <div className="grid grid-cols-3 sm:grid-cols-6 gap-3">
          {[
            { label: 'Sent',       value: summary.sentCount,       color: 'text-emerald-700' },
            { label: 'Failed',     value: summary.failedCount,     color: summary.failedCount > 0 ? 'text-red-700' : '' },
            { label: 'Pending',    value: summary.pendingCount,    color: summary.pendingCount > 0 ? 'text-amber-700' : '' },
            { label: 'Suppressed', value: summary.suppressedCount, color: '' },
            { label: 'Skipped',    value: summary.skippedCount,    color: '' },
            { label: 'Total',      value: summary.totalCount,      color: '' },
          ].map(({ label, value, color }) => (
            <div key={label} className="bg-white border border-gray-200 rounded-lg px-3 py-2.5">
              <p className="text-[10px] font-medium text-gray-500 uppercase tracking-wide">{label}</p>
              <p className={`mt-0.5 text-lg font-bold tabular-nums ${color || 'text-gray-900'}`}>{fmtN(value)}</p>
            </div>
          ))}
        </div>
      )}

      {/* ── Channel breakdown ─────────────────────────────────────────── */}
      {summary && Object.keys(summary.byChannel).length > 0 && (
        <div className="bg-white border border-gray-200 rounded-lg px-4 py-3">
          <p className="text-xs font-semibold text-gray-600 mb-2">By Channel</p>
          <div className="flex flex-wrap gap-3">
            {Object.entries(summary.byChannel).map(([ch, count]) => (
              <div key={ch} className="flex items-center gap-1.5">
                <ChannelBadge channel={ch} />
                <span className="text-sm font-semibold tabular-nums text-gray-700">{fmtN(count)}</span>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* ── Filters ───────────────────────────────────────────────────── */}
      <div className="flex flex-wrap items-center gap-3">
        <select
          value={initialStatus ?? ''}
          onChange={e => navigate({ status: e.target.value || undefined, offset: '0' })}
          className="text-sm border border-gray-200 rounded-md px-3 py-1.5 bg-white focus:outline-none focus:ring-2 focus:ring-indigo-300"
          aria-label="Filter by status"
        >
          <option value="">All statuses</option>
          <option value="sent">Sent</option>
          <option value="failed">Failed</option>
          <option value="pending">Pending</option>
          <option value="suppressed">Suppressed</option>
          <option value="skipped">Skipped</option>
        </select>

        <select
          value={initialChannelType ?? ''}
          onChange={e => navigate({ channelType: e.target.value || undefined, offset: '0' })}
          className="text-sm border border-gray-200 rounded-md px-3 py-1.5 bg-white focus:outline-none focus:ring-2 focus:ring-indigo-300"
          aria-label="Filter by channel"
        >
          <option value="">All channels</option>
          <option value="email">Email</option>
          <option value="teams_webhook">Teams Webhook</option>
          <option value="slack_webhook">Slack Webhook</option>
          <option value="pagerduty">PagerDuty</option>
          <option value="opsgenie">OpsGenie</option>
          <option value="internal_notification">Internal Notification</option>
        </select>

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
          {fmtN(total)} record{total !== 1 ? 's' : ''}
        </span>
      </div>

      {/* ── Table ─────────────────────────────────────────────────────── */}
      {!list || list.items.length === 0 ? (
        <div className="bg-white border border-gray-200 rounded-lg px-6 py-10 text-center text-sm text-gray-400">
          <i className="ri-send-plane-2-line text-3xl block mb-2 text-gray-300" aria-hidden />
          No escalation records match the current filters.
        </div>
      ) : (
        <div className="rounded-lg border border-gray-200 bg-white overflow-hidden">
          <div className="overflow-x-auto">
            <table className="min-w-full divide-y divide-gray-100 text-sm">
              <thead className="bg-gray-50 text-xs text-gray-500 uppercase tracking-wide">
                <tr>
                  <th className="px-4 py-2.5 text-left font-medium">Channel</th>
                  <th className="px-4 py-2.5 text-left font-medium">Target (masked)</th>
                  <th className="px-4 py-2.5 text-left font-medium">Severity</th>
                  <th className="px-4 py-2.5 text-left font-medium">Status</th>
                  <th className="px-4 py-2.5 text-right font-medium">Attempts</th>
                  <th className="px-4 py-2.5 text-left font-medium">Failure</th>
                  <th className="px-4 py-2.5 text-right font-medium">Sent At</th>
                  <th className="px-4 py-2.5 text-right font-medium">Next Retry</th>
                  <th className="px-4 py-2.5 text-right font-medium">Actions</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {list.items.map(esc => (
                  <tr key={esc.id} className="hover:bg-gray-50">
                    <td className="px-4 py-3">
                      <ChannelBadge channel={esc.channelType} />
                    </td>
                    <td className="px-4 py-3 font-mono text-[11px] text-gray-500">
                      {esc.targetMasked ?? '—'}
                    </td>
                    <td className="px-4 py-3">
                      <SeverityBadge severity={esc.severity} />
                    </td>
                    <td className="px-4 py-3">
                      <StatusBadge status={esc.status} />
                    </td>
                    <td className="px-4 py-3 text-right tabular-nums text-gray-700">
                      {fmtN(esc.attemptCount)}
                    </td>
                    <td className="px-4 py-3 max-w-[200px]">
                      {esc.failureReason ? (
                        <span className="text-xs text-red-700 truncate block" title={esc.failureReason}>
                          {esc.failureReason}
                        </span>
                      ) : (
                        <span className="text-gray-400 italic text-xs">—</span>
                      )}
                    </td>
                    <td className="px-4 py-3 text-right font-mono text-[11px] text-gray-400 whitespace-nowrap">
                      {fmtUtc(esc.sentAt)}
                    </td>
                    <td className="px-4 py-3 text-right font-mono text-[11px] text-gray-400 whitespace-nowrap">
                      {fmtUtc(esc.nextRetryAt)}
                    </td>
                    <td className="px-4 py-3 text-right">
                      {esc.status === 'failed' ? (
                        <button
                          onClick={() => setRetryTarget(esc)}
                          disabled={isPending}
                          className="px-2.5 py-1 text-xs font-medium rounded-md bg-amber-50 text-amber-700 hover:bg-amber-100 border border-amber-200 disabled:opacity-50 transition-colors"
                        >
                          Retry
                        </button>
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
        TargetMasked is the only target field shown — raw webhook URLs and emails are never exposed.
        All timestamps UTC.
      </div>
    </>
  );
}
