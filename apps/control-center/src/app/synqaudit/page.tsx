import { isRedirectError }         from 'next/dist/client/components/redirect-error';
import { requirePlatformAdmin }    from '@/lib/auth-guards';
import { controlCenterServerApi }  from '@/lib/control-center-api';
import { CCShell }                 from '@/components/shell/cc-shell';
import { formatUtc }               from '@/components/synqaudit/synqaudit-badges';
import { SeverityBadge, OutcomeBadge } from '@/components/synqaudit/synqaudit-badges';

export const dynamic = 'force-dynamic';

/**
 * /synqaudit — SynqAudit overview page.
 *
 * Shows a summary of recent audit activity: recent events, event counts by
 * severity, quick-nav cards to sub-sections.
 */
export default async function SynqAuditOverviewPage() {
  const session = await requirePlatformAdmin();

  let recentEvents: Awaited<ReturnType<typeof controlCenterServerApi.auditCanonical.list>>['items'] = [];
  let totalCount   = 0;
  let fetchError:  string | null = null;

  try {
    const result = await controlCenterServerApi.auditCanonical.list({
      page:     1,
      pageSize: 20,
    });
    recentEvents = result.items;
    totalCount   = result.totalCount;
  } catch (err) {
    if (isRedirectError(err)) throw err;
    fetchError = err instanceof Error ? err.message : 'Could not reach the audit service.';
  }

  // Count by severity from recent window
  const bySeverity: Record<string, number> = {};
  for (const e of recentEvents) {
    const s = e.severity.toLowerCase();
    bySeverity[s] = (bySeverity[s] ?? 0) + 1;
  }

  const statCards = [
    { label: 'Total (all time)',    value: totalCount.toLocaleString(), color: 'text-indigo-700' },
    { label: 'Critical (recent)',   value: (bySeverity['critical'] ?? 0).toString(), color: 'text-red-700'  },
    { label: 'Errors (recent)',     value: (bySeverity['error']    ?? 0).toString(), color: 'text-red-600'  },
    { label: 'Warnings (recent)',   value: (bySeverity['warn']     ?? 0).toString(), color: 'text-amber-600'},
  ];

  const quickNavCards = [
    {
      href:  '/synqaudit/investigation',
      icon:  'ri-search-eye-line',
      title: 'Investigation',
      desc:  'Filter, search, and drill into the canonical event stream.',
      color: 'border-indigo-200 hover:border-indigo-400',
    },
    {
      href:  '/synqaudit/trace',
      icon:  'ri-git-branch-line',
      title: 'Trace Viewer',
      desc:  'Reconstruct request flows across services by correlation ID.',
      color: 'border-violet-200 hover:border-violet-400',
    },
    {
      href:  '/synqaudit/exports',
      icon:  'ri-download-cloud-line',
      title: 'Exports',
      desc:  'Submit async export jobs in JSON, CSV, or NDJSON format.',
      color: 'border-green-200 hover:border-green-400',
    },
    {
      href:  '/synqaudit/integrity',
      icon:  'ri-fingerprint-line',
      title: 'Integrity',
      desc:  'Generate and verify HMAC-SHA256 hash chain checkpoints.',
      color: 'border-blue-200 hover:border-blue-400',
    },
    {
      href:  '/synqaudit/permissions',
      icon:  'ri-key-2-line',
      title: 'Permissions',
      desc:  'Browse permission-change events — role, group, and product assignments with before/after state.',
      color: 'border-indigo-200 hover:border-indigo-400',
    },
    {
      href:  '/synqaudit/legal-holds',
      icon:  'ri-scales-3-line',
      title: 'Legal Holds',
      desc:  'Place, view, and release legal holds on audit records.',
      color: 'border-amber-200 hover:border-amber-400',
    },
  ];

  return (
    <CCShell userEmail={session.email}>
      <div className="space-y-6">

        {/* Header */}
        <div>
          <div className="flex items-center gap-3">
            <h1 className="text-xl font-semibold text-gray-900">SynqAudit</h1>
            <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full bg-green-50 border border-green-300 text-[11px] font-semibold text-green-700">
              <span className="h-1.5 w-1.5 rounded-full bg-green-500 animate-pulse" />
              LIVE
            </span>
          </div>
          <p className="text-sm text-gray-500 mt-0.5">
            Platform-wide canonical audit trail — powered by the Audit Event Service.
          </p>
        </div>

        {/* Error banner */}
        {fetchError && (
          <div className="rounded-lg border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-700">
            <strong>Audit service unreachable:</strong> {fetchError}
          </div>
        )}

        {/* Stat cards */}
        <div className="grid grid-cols-2 sm:grid-cols-4 gap-4">
          {statCards.map(card => (
            <div key={card.label} className="rounded-lg border border-gray-200 bg-white px-4 py-3">
              <p className="text-xs text-gray-500 mb-1">{card.label}</p>
              <p className={`text-2xl font-bold ${card.color}`}>{card.value}</p>
            </div>
          ))}
        </div>

        {/* Quick nav */}
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3">
          {quickNavCards.map(card => (
            <a
              key={card.href}
              href={card.href}
              className={`flex items-start gap-3 rounded-lg border bg-white px-4 py-3.5 transition-all group ${card.color}`}
            >
              <i className={`${card.icon} text-xl text-gray-400 group-hover:text-indigo-600 transition-colors mt-0.5`} />
              <div>
                <p className="text-sm font-semibold text-gray-700 group-hover:text-indigo-700">{card.title}</p>
                <p className="text-xs text-gray-500 mt-0.5">{card.desc}</p>
              </div>
            </a>
          ))}
        </div>

        {/* Recent events */}
        {recentEvents.length > 0 && (
          <div className="rounded-lg border border-gray-200 bg-white overflow-hidden">
            <div className="px-4 py-3 border-b border-gray-100 bg-gray-50 flex items-center justify-between">
              <h2 className="text-sm font-semibold text-gray-700">Recent Events</h2>
              <a href="/synqaudit/investigation" className="text-xs text-indigo-600 hover:text-indigo-800 font-medium">
                View all →
              </a>
            </div>
            <table className="min-w-full divide-y divide-gray-100 text-sm">
              <thead className="bg-gray-50 text-xs text-gray-500 uppercase tracking-wide">
                <tr>
                  <th className="px-4 py-2.5 text-left font-medium">Time (UTC)</th>
                  <th className="px-4 py-2.5 text-left font-medium">Severity</th>
                  <th className="px-4 py-2.5 text-left font-medium">Event Type</th>
                  <th className="px-4 py-2.5 text-left font-medium">Actor</th>
                  <th className="px-4 py-2.5 text-left font-medium">Outcome</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {recentEvents.map(e => (
                  <tr key={e.id} className="hover:bg-gray-50">
                    <td className="px-4 py-2 text-gray-500 whitespace-nowrap font-mono text-[11px]">
                      {formatUtc(e.occurredAtUtc)}
                    </td>
                    <td className="px-4 py-2">
                      <SeverityBadge value={e.severity} />
                    </td>
                    <td className="px-4 py-2 text-gray-700 font-mono text-[11px] whitespace-nowrap">
                      {e.eventType}
                    </td>
                    <td className="px-4 py-2 text-xs text-gray-600 whitespace-nowrap">
                      {e.actorLabel ?? e.actorId ?? <span className="text-gray-400 italic">system</span>}
                    </td>
                    <td className="px-4 py-2">
                      <OutcomeBadge value={e.outcome} />
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

      </div>
    </CCShell>
  );
}
