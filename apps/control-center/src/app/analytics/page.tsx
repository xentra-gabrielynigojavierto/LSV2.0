import { Suspense }               from 'react';
import { requirePlatformAdmin }  from '@/lib/auth-guards';
import { controlCenterServerApi } from '@/lib/control-center-api';
import { CCShell }                from '@/components/shell/cc-shell';
import { AnalyticsSlaCards }      from '@/components/analytics/analytics-sla-cards';
import { AnalyticsQueueTable }    from '@/components/analytics/analytics-queue-table';
import { AnalyticsWorkflowCards } from '@/components/analytics/analytics-workflow-cards';
import { AnalyticsOutboxCards }   from '@/components/analytics/analytics-outbox-cards';
import { AnalyticsPlatformTable } from '@/components/analytics/analytics-platform-table';
import type {

  AnalyticsDashboardSummary,
  PlatformAnalyticsSummary,
} from '@/types/control-center';

export const dynamic = 'force-dynamic';

interface AnalyticsPageProps {
  searchParams: Promise<{ window?: string }>;
}

const WINDOW_OPTIONS = [
  { value: 'today', label: 'Today'      },
  { value: '7d',    label: 'Last 7 Days'  },
  { value: '30d',   label: 'Last 30 Days' },
] as const;

/**
 * /analytics — E19 Platform Admin Analytics Dashboard.
 *
 * Access: PlatformAdmin only.
 *
 * Surfaces SLA performance, queue backlog, workflow throughput, and outbox
 * reliability metrics for the platform operator. Each section links to the
 * operational surface where corrective action can be taken.
 *
 * Data: Flow GET /api/v1/admin/analytics/* endpoints via the gateway.
 *   - /summary     — unified dashboard (SLA + queue + workflows + assignment + outbox)
 *   - /platform    — cross-tenant aggregations (PlatformAdmin only)
 * Cache: 30 s per request (tag: cc:analytics), revalidated on each page load.
 */
export default async function AnalyticsPage({ searchParams }: AnalyticsPageProps) {
  const sp     = await searchParams;
  const window = (sp.window ?? '7d') as 'today' | '7d' | '30d';

  const session = await requirePlatformAdmin();

  let summary: AnalyticsDashboardSummary | null = null;
  let platform: PlatformAnalyticsSummary | null = null;
  let summaryError: string | null = null;
  let platformError: string | null = null;

  try {
    summary = await controlCenterServerApi.analytics.getDashboardSummary(window);
  } catch (e) {
    summaryError = e instanceof Error ? e.message : 'Failed to load analytics';
  }

  try {
    platform = await controlCenterServerApi.analytics.getPlatformSummary(window);
  } catch (e) {
    platformError = e instanceof Error ? e.message : 'Failed to load platform analytics';
  }

  const windowLabel = WINDOW_OPTIONS.find(o => o.value === window)?.label ?? 'Last 7 Days';

  return (
    <CCShell userEmail={session.email}>
      <div className="px-6 py-6 max-w-7xl mx-auto space-y-8">
        {/* Header */}
        <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
          <div>
            <h1 className="text-xl font-semibold text-gray-900">Analytics</h1>
            <p className="text-sm text-gray-500 mt-1">
              Operational health across SLA, queues, workflows, and async reliability
            </p>
          </div>

          {/* Window selector */}
          <div className="flex items-center gap-2">
            {WINDOW_OPTIONS.map(opt => (
              <a
                key={opt.value}
                href={`?window=${opt.value}`}
                className={`px-3 py-1.5 rounded text-sm font-medium transition-colors ${
                  window === opt.value
                    ? 'bg-gray-900 text-white'
                    : 'bg-white border border-gray-200 text-gray-600 hover:border-gray-300'
                }`}
              >
                {opt.label}
              </a>
            ))}
          </div>
        </div>

        {summaryError ? (
          <div className="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
            <i className="ri-error-warning-line mr-2" />
            {summaryError}
          </div>
        ) : summary ? (
          <>
            {/* SLA Performance */}
            <section>
              <div className="flex items-center justify-between mb-3">
                <h2 className="text-base font-semibold text-gray-800 flex items-center gap-2">
                  <i className="ri-alarm-warning-line text-amber-500" />
                  SLA Performance
                </h2>
                <span className="text-xs text-gray-400">{windowLabel}</span>
              </div>
              <AnalyticsSlaCards sla={summary.sla} />
            </section>

            {/* Queue / Workload */}
            <section>
              <div className="flex items-center justify-between mb-3">
                <h2 className="text-base font-semibold text-gray-800 flex items-center gap-2">
                  <i className="ri-stack-line text-blue-500" />
                  Queue &amp; Workload
                </h2>
                <span className="text-xs text-gray-400">Current state</span>
              </div>
              <AnalyticsQueueTable queue={summary.queue} />
            </section>

            {/* Workflow Throughput */}
            <section>
              <div className="flex items-center justify-between mb-3">
                <h2 className="text-base font-semibold text-gray-800 flex items-center gap-2">
                  <i className="ri-flow-chart text-emerald-500" />
                  Workflow Throughput
                </h2>
                <span className="text-xs text-gray-400">{windowLabel}</span>
              </div>
              <AnalyticsWorkflowCards throughput={summary.workflows} />
            </section>

            {/* Assignment Distribution */}
            <section>
              <div className="flex items-center justify-between mb-3">
                <h2 className="text-base font-semibold text-gray-800 flex items-center gap-2">
                  <i className="ri-user-settings-line text-violet-500" />
                  Assignment Distribution
                </h2>
                <span className="text-xs text-gray-400">{windowLabel}</span>
              </div>
              <div className="space-y-4">
                {/* Mode distribution */}
                <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
                  {[
                    { label: 'Direct User', value: summary.assignment.directUserCount, icon: 'ri-user-line', color: 'text-blue-700' },
                    { label: 'Role Queue',  value: summary.assignment.roleQueueCount,  icon: 'ri-group-line', color: 'text-violet-700' },
                    { label: 'Org Queue',   value: summary.assignment.orgQueueCount,   icon: 'ri-building-line', color: 'text-purple-700' },
                    { label: 'Unassigned',  value: summary.assignment.unassignedCount, icon: 'ri-user-unfollow-line', color: summary.assignment.unassignedCount > 0 ? 'text-amber-700' : 'text-gray-500' },
                  ].map(card => (
                    <div key={card.label} className="bg-white border border-gray-200 rounded-lg px-4 py-3">
                      <div className="flex items-center gap-1.5 text-[11px] font-semibold uppercase tracking-wide text-gray-400">
                        <i className={`${card.icon} text-[13px]`} />
                        {card.label}
                      </div>
                      <div className={`text-2xl font-bold tabular-nums mt-1 ${card.color}`}>
                        {card.value.toLocaleString()}
                      </div>
                    </div>
                  ))}
                </div>
                <div className="bg-white border border-gray-200 rounded-lg px-4 py-3 flex items-center gap-4">
                  <div>
                    <div className="text-xs text-gray-500 font-medium">Assigned ({windowLabel})</div>
                    <div className="text-xl font-bold text-gray-800 tabular-nums">{summary.assignment.assignedInWindow.toLocaleString()}</div>
                  </div>
                  <div className="flex-1 text-[11px] text-gray-400 leading-relaxed">
                    {summary.assignment.assumptionNote}
                  </div>
                </div>

                {/* Top assignees by workload */}
                {summary.assignment.topAssigneesByActiveLoad.length > 0 && (
                  <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
                    <div className="px-4 py-3 border-b border-gray-100">
                      <h4 className="text-sm font-semibold text-gray-700">Top Assignees by Active Load</h4>
                    </div>
                    <table className="w-full text-sm">
                      <thead>
                        <tr className="border-b border-gray-100 text-xs text-gray-500 font-medium">
                          <th className="text-left px-4 py-2">User ID</th>
                          <th className="text-right px-4 py-2">Open</th>
                          <th className="text-right px-4 py-2">In Progress</th>
                          <th className="text-right px-4 py-2">Total Active</th>
                        </tr>
                      </thead>
                      <tbody>
                        {summary.assignment.topAssigneesByActiveLoad.slice(0, 10).map((u, i) => (
                          <tr key={i} className="border-b border-gray-50 hover:bg-gray-50">
                            <td className="px-4 py-2 font-mono text-xs text-gray-700 truncate max-w-[200px]">{u.userId}</td>
                            <td className="px-4 py-2 text-right tabular-nums text-gray-600">{u.openCount.toLocaleString()}</td>
                            <td className="px-4 py-2 text-right tabular-nums text-blue-600">{u.inProgressCount.toLocaleString()}</td>
                            <td className="px-4 py-2 text-right tabular-nums font-bold text-gray-800">{u.activeTaskCount.toLocaleString()}</td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                )}
              </div>
            </section>

            {/* Outbox Reliability */}
            <section>
              <div className="flex items-center justify-between mb-3">
                <h2 className="text-base font-semibold text-gray-800 flex items-center gap-2">
                  <i className="ri-inbox-archive-line text-orange-500" />
                  Async Reliability (Outbox)
                </h2>
                <a href="/operations/outbox" className="text-xs text-blue-600 hover:text-blue-700 hover:underline">
                  View outbox ops →
                </a>
              </div>
              <AnalyticsOutboxCards outbox={summary.outbox} />
            </section>
          </>
        ) : null}

        {/* Platform Cross-tenant Summary — PlatformAdmin only */}
        <section>
          <div className="flex items-center justify-between mb-3">
            <h2 className="text-base font-semibold text-gray-800 flex items-center gap-2">
              <i className="ri-global-line text-indigo-500" />
              Platform — Cross-Tenant View
            </h2>
            <span className="text-xs text-gray-400">{windowLabel}</span>
          </div>
          {platformError ? (
            <div className="rounded-lg border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-700">
              <i className="ri-error-warning-line mr-2" />
              {platformError}
            </div>
          ) : platform ? (
            <AnalyticsPlatformTable platform={platform} />
          ) : null}
        </section>

        {/* Footer */}
        <div className="text-xs text-gray-400 text-right border-t border-gray-100 pt-4">
          Analytics are read-only derived metrics. All figures reflect UTC time boundaries.
          {summary && (
            <> Generated at {new Date(summary.generatedAt).toLocaleString()}.</>
          )}
        </div>
      </div>
    </CCShell>
  );
}
