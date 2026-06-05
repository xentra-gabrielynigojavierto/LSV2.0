import type { PlatformAnalyticsSummary } from '@/types/control-center';

interface AnalyticsPlatformTableProps {
  platform: PlatformAnalyticsSummary;
}

/**
 * E19 — Platform-scoped cross-tenant analytics table.
 * Shows platform-wide totals and per-tenant rankings for overdue rate,
 * active workflow count, and outbox health.
 * Only rendered for PlatformAdmin users.
 */
export function AnalyticsPlatformTable({ platform: p }: AnalyticsPlatformTableProps) {
  const fmt = (n: number) => n.toLocaleString();

  return (
    <div className="space-y-4">
      {/* Platform totals */}
      <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-5 gap-3">
        <div className="bg-blue-50 border border-blue-200 rounded-lg px-4 py-3">
          <div className="text-[11px] font-semibold uppercase tracking-wide text-blue-600">Active Workflows</div>
          <div className="text-2xl font-bold text-blue-800 tabular-nums mt-1">{fmt(p.totalActiveWorkflows)}</div>
          <div className="text-[10px] text-blue-500">across all tenants</div>
        </div>
        <div className="bg-white border border-gray-200 rounded-lg px-4 py-3">
          <div className="text-[11px] font-semibold uppercase tracking-wide text-gray-400">Active Tasks</div>
          <div className="text-2xl font-bold text-gray-800 tabular-nums mt-1">{fmt(p.totalActiveTasks)}</div>
          <div className="text-[10px] text-gray-400">across all tenants</div>
        </div>
        <div className={`rounded-lg px-4 py-3 border ${p.totalOverdueTasks > 0 ? 'bg-red-50 border-red-200' : 'bg-white border-gray-200'}`}>
          <div className={`text-[11px] font-semibold uppercase tracking-wide ${p.totalOverdueTasks > 0 ? 'text-red-500' : 'text-gray-400'}`}>
            Overdue Tasks
          </div>
          <div className={`text-2xl font-bold tabular-nums mt-1 ${p.totalOverdueTasks > 0 ? 'text-red-800' : 'text-gray-800'}`}>
            {fmt(p.totalOverdueTasks)}
          </div>
          <div className="text-[10px] text-gray-400">across all tenants</div>
        </div>
        <div className={`rounded-lg px-4 py-3 border ${p.totalFailedOutbox > 0 ? 'bg-amber-50 border-amber-200' : 'bg-white border-gray-200'}`}>
          <div className={`text-[11px] font-semibold uppercase tracking-wide ${p.totalFailedOutbox > 0 ? 'text-amber-600' : 'text-gray-400'}`}>
            Failed Outbox
          </div>
          <div className={`text-2xl font-bold tabular-nums mt-1 ${p.totalFailedOutbox > 0 ? 'text-amber-800' : 'text-gray-800'}`}>
            {fmt(p.totalFailedOutbox)}
          </div>
          <div className="text-[10px] text-gray-400">total failed messages</div>
        </div>
        <div className={`rounded-lg px-4 py-3 border ${p.totalDeadLettered > 0 ? 'bg-red-100 border-red-300' : 'bg-white border-gray-200'}`}>
          <div className={`text-[11px] font-semibold uppercase tracking-wide ${p.totalDeadLettered > 0 ? 'text-red-700' : 'text-gray-400'}`}>
            Dead Letters
          </div>
          <div className={`text-2xl font-bold tabular-nums mt-1 ${p.totalDeadLettered > 0 ? 'text-red-900' : 'text-gray-800'}`}>
            {fmt(p.totalDeadLettered)}
          </div>
          <div className="text-[10px] text-gray-400">total dead-lettered</div>
        </div>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-4">
        {/* Top tenants by overdue */}
        {p.topTenantsByOverdue.length > 0 && (
          <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
            <div className="px-4 py-3 border-b border-gray-100">
              <h4 className="text-sm font-semibold text-gray-700">Top Tenants — Overdue Rate</h4>
            </div>
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-gray-100 text-xs text-gray-500 font-medium">
                  <th className="text-left px-4 py-2">Tenant ID</th>
                  <th className="text-right px-4 py-2">Overdue</th>
                  <th className="text-right px-4 py-2">Rate</th>
                </tr>
              </thead>
              <tbody>
                {p.topTenantsByOverdue.map((t, i) => (
                  <tr key={i} className="border-b border-gray-50 hover:bg-gray-50">
                    <td className="px-4 py-2 font-mono text-[11px] text-gray-600 truncate max-w-[120px]">{t.tenantId}</td>
                    <td className="px-4 py-2 text-right font-bold text-red-700 tabular-nums">{fmt(t.overdueCount)}</td>
                    <td className="px-4 py-2 text-right tabular-nums text-gray-600">{t.overdueRate.toFixed(1)}%</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

        {/* Top tenants by active workflows */}
        {p.topTenantsByActiveWorkflows.length > 0 && (
          <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
            <div className="px-4 py-3 border-b border-gray-100">
              <h4 className="text-sm font-semibold text-gray-700">Top Tenants — Active Workflows</h4>
            </div>
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-gray-100 text-xs text-gray-500 font-medium">
                  <th className="text-left px-4 py-2">Tenant ID</th>
                  <th className="text-right px-4 py-2">Active</th>
                </tr>
              </thead>
              <tbody>
                {p.topTenantsByActiveWorkflows.map((t, i) => (
                  <tr key={i} className="border-b border-gray-50 hover:bg-gray-50">
                    <td className="px-4 py-2 font-mono text-[11px] text-gray-600 truncate max-w-[160px]">{t.tenantId}</td>
                    <td className="px-4 py-2 text-right font-bold text-blue-700 tabular-nums">{fmt(t.activeCount)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

        {/* Outbox health by tenant */}
        {p.outboxHealthByTenant.length > 0 && (
          <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
            <div className="px-4 py-3 border-b border-gray-100">
              <h4 className="text-sm font-semibold text-gray-700">Outbox Health by Tenant</h4>
            </div>
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-gray-100 text-xs text-gray-500 font-medium">
                  <th className="text-left px-4 py-2">Tenant ID</th>
                  <th className="text-right px-4 py-2">Failed</th>
                  <th className="text-right px-4 py-2">Dead Letters</th>
                </tr>
              </thead>
              <tbody>
                {p.outboxHealthByTenant.map((t, i) => (
                  <tr key={i} className="border-b border-gray-50 hover:bg-gray-50">
                    <td className="px-4 py-2 font-mono text-[11px] text-gray-600 truncate max-w-[120px]">{t.tenantId}</td>
                    <td className={`px-4 py-2 text-right tabular-nums font-semibold ${t.failedCount > 0 ? 'text-red-600' : 'text-gray-400'}`}>
                      {fmt(t.failedCount)}
                    </td>
                    <td className={`px-4 py-2 text-right tabular-nums font-bold ${t.deadLettered > 0 ? 'text-red-900' : 'text-gray-400'}`}>
                      {fmt(t.deadLettered)}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

      <div className="text-xs text-gray-400 text-right">
        As of {new Date(p.asOf).toLocaleString()} UTC · {p.windowLabel}
      </div>
    </div>
  );
}
