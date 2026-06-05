import type { QueueSummary } from '@/types/control-center';

interface AnalyticsQueueTableProps {
  queue: QueueSummary;
}

/**
 * E19 — Queue backlog summary cards + breakdown table.
 * Shows role-queue and org-queue backlogs, queue age, and active user workload.
 */
export function AnalyticsQueueTable({ queue }: AnalyticsQueueTableProps) {
  const fmt    = (n: number) => n.toLocaleString();
  const age    = (h: number | null) =>
    h == null ? '—' : h < 24 ? `${h.toFixed(1)}h` : `${(h / 24).toFixed(1)}d`;
  const asOf   = new Date(queue.asOf).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });

  return (
    <div className="space-y-4">
      {/* Summary cards */}
      <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-5 gap-3">
        <div className="bg-white border border-gray-200 rounded-lg px-4 py-3">
          <div className="text-[11px] font-semibold uppercase tracking-wide text-gray-400">Role Queue</div>
          <div className="text-2xl font-bold text-gray-800 tabular-nums mt-1">{fmt(queue.roleQueueBacklog)}</div>
          <div className="text-[10px] text-gray-400">tasks in role queues</div>
        </div>
        <div className="bg-white border border-gray-200 rounded-lg px-4 py-3">
          <div className="text-[11px] font-semibold uppercase tracking-wide text-gray-400">Org Queue</div>
          <div className="text-2xl font-bold text-gray-800 tabular-nums mt-1">{fmt(queue.orgQueueBacklog)}</div>
          <div className="text-[10px] text-gray-400">tasks in org queues</div>
        </div>
        <div className="bg-white border border-gray-200 rounded-lg px-4 py-3">
          <div className="text-[11px] font-semibold uppercase tracking-wide text-gray-400">Unassigned</div>
          <div className={`text-2xl font-bold tabular-nums mt-1 ${queue.unassignedBacklog > 0 ? 'text-amber-700' : 'text-gray-800'}`}>
            {fmt(queue.unassignedBacklog)}
          </div>
          <div className="text-[10px] text-gray-400">awaiting assignment</div>
        </div>
        <div className="bg-white border border-gray-200 rounded-lg px-4 py-3">
          <div className="text-[11px] font-semibold uppercase tracking-wide text-gray-400">Oldest Queued</div>
          <div className="text-2xl font-bold text-gray-800 tabular-nums mt-1">{age(queue.oldestQueuedTaskAgeHours)}</div>
          <div className="text-[10px] text-gray-400">task age</div>
        </div>
        <div className={`rounded-lg px-4 py-3 border ${
          queue.overloadedUserCount > 0 ? 'bg-red-50 border-red-200' : 'bg-white border-gray-200'
        }`}>
          <div className={`text-[11px] font-semibold uppercase tracking-wide ${
            queue.overloadedUserCount > 0 ? 'text-red-500' : 'text-gray-400'
          }`}>
            Overloaded Users
          </div>
          <div className={`text-2xl font-bold tabular-nums mt-1 ${
            queue.overloadedUserCount > 0 ? 'text-red-800' : 'text-gray-800'
          }`}>
            {fmt(queue.overloadedUserCount)}
          </div>
          <div className="text-[10px] text-gray-400">≥{queue.overloadThreshold} active tasks</div>
        </div>
      </div>

      {/* Role queue breakdown */}
      {queue.roleQueueBreakdown.length > 0 && (
        <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
          <div className="px-4 py-3 border-b border-gray-100 flex items-center justify-between">
            <h4 className="text-sm font-semibold text-gray-700">Role Queue Breakdown</h4>
            <span className="text-xs text-gray-400">as of {asOf}</span>
          </div>
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b border-gray-100 text-xs text-gray-500 font-medium">
                <th className="text-left px-4 py-2">Role</th>
                <th className="text-right px-4 py-2">Open</th>
                <th className="text-right px-4 py-2">In Progress</th>
                <th className="text-right px-4 py-2">Total</th>
                <th className="text-right px-4 py-2">Overdue</th>
              </tr>
            </thead>
            <tbody>
              {queue.roleQueueBreakdown.map((r, i) => (
                <tr key={i} className="border-b border-gray-50 hover:bg-gray-50">
                  <td className="px-4 py-2 font-mono text-xs text-gray-700">{r.role}</td>
                  <td className="px-4 py-2 text-right tabular-nums text-gray-600">{fmt(r.openCount)}</td>
                  <td className="px-4 py-2 text-right tabular-nums text-blue-600">{fmt(r.inProgressCount)}</td>
                  <td className="px-4 py-2 text-right tabular-nums font-semibold text-gray-800">{fmt(r.totalCount)}</td>
                  <td className={`px-4 py-2 text-right tabular-nums font-bold ${
                    r.overdueCount > 0 ? 'text-red-700' : 'text-gray-400'
                  }`}>
                    {fmt(r.overdueCount)}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {/* Org queue breakdown */}
      {queue.orgQueueBreakdown.length > 0 && (
        <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
          <div className="px-4 py-3 border-b border-gray-100">
            <h4 className="text-sm font-semibold text-gray-700">Org Queue Breakdown</h4>
          </div>
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b border-gray-100 text-xs text-gray-500 font-medium">
                <th className="text-left px-4 py-2">Org ID</th>
                <th className="text-right px-4 py-2">Open</th>
                <th className="text-right px-4 py-2">In Progress</th>
                <th className="text-right px-4 py-2">Total</th>
                <th className="text-right px-4 py-2">Overdue</th>
              </tr>
            </thead>
            <tbody>
              {queue.orgQueueBreakdown.map((o, i) => (
                <tr key={i} className="border-b border-gray-50 hover:bg-gray-50">
                  <td className="px-4 py-2 font-mono text-xs text-gray-700 truncate max-w-[200px]">{o.orgId}</td>
                  <td className="px-4 py-2 text-right tabular-nums text-gray-600">{fmt(o.openCount)}</td>
                  <td className="px-4 py-2 text-right tabular-nums text-blue-600">{fmt(o.inProgressCount)}</td>
                  <td className="px-4 py-2 text-right tabular-nums font-semibold text-gray-800">{fmt(o.totalCount)}</td>
                  <td className={`px-4 py-2 text-right tabular-nums font-bold ${
                    o.overdueCount > 0 ? 'text-red-700' : 'text-gray-400'
                  }`}>
                    {fmt(o.overdueCount)}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
