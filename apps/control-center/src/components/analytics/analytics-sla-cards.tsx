import type { SlaSummary } from '@/types/control-center';

interface AnalyticsSlaCardsProps {
  sla: SlaSummary;
}

/**
 * E19 — SLA performance summary cards.
 *
 * Displays active task SLA breakdown (on-track / at-risk / overdue), overdue
 * percentage, new breaches in the window, and on-time completions.
 * Overdue card links to the workflow queue for drill-down.
 */
export function AnalyticsSlaCards({ sla }: AnalyticsSlaCardsProps) {
  const fmt = (n: number) => n.toLocaleString();
  const pct = `${sla.overduePercentage.toFixed(1)}%`;

  return (
    <div className="space-y-4">
      <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-4 gap-3">
        {/* On-Track */}
        <div className="bg-emerald-50 border border-emerald-200 rounded-lg px-4 py-3">
          <div className="flex items-center gap-1.5 text-[11px] font-semibold uppercase tracking-wide text-emerald-700 opacity-70">
            <i className="ri-checkbox-circle-line text-[13px]" />
            On Track
          </div>
          <div className="text-2xl font-bold text-emerald-800 leading-none tabular-nums mt-1">
            {fmt(sla.activeOnTrackCount)}
          </div>
          <div className="text-[10px] text-emerald-600 opacity-60 mt-0.5">active tasks</div>
        </div>

        {/* At Risk */}
        <div className="bg-amber-50 border border-amber-200 rounded-lg px-4 py-3">
          <div className="flex items-center gap-1.5 text-[11px] font-semibold uppercase tracking-wide text-amber-700 opacity-70">
            <i className="ri-alarm-warning-line text-[13px]" />
            At Risk
          </div>
          <div className="text-2xl font-bold text-amber-800 leading-none tabular-nums mt-1">
            {fmt(sla.activeAtRiskCount)}
          </div>
          <div className="text-[10px] text-amber-600 opacity-60 mt-0.5">due soon</div>
        </div>

        {/* Overdue */}
        <div className={`rounded-lg px-4 py-3 border ${
          sla.activeOverdueCount > 0
            ? 'bg-red-50 border-red-200'
            : 'bg-gray-50 border-gray-200'
        }`}>
          <div className={`flex items-center gap-1.5 text-[11px] font-semibold uppercase tracking-wide opacity-70 ${
            sla.activeOverdueCount > 0 ? 'text-red-700' : 'text-gray-500'
          }`}>
            <i className="ri-error-warning-line text-[13px]" />
            Overdue
          </div>
          <div className={`text-2xl font-bold leading-none tabular-nums mt-1 ${
            sla.activeOverdueCount > 0 ? 'text-red-800' : 'text-gray-600'
          }`}>
            {fmt(sla.activeOverdueCount)}
          </div>
          <div className={`text-[10px] opacity-60 mt-0.5 ${
            sla.activeOverdueCount > 0 ? 'text-red-600' : 'text-gray-400'
          }`}>
            {pct} of active tasks
          </div>
        </div>

        {/* Overdue Age */}
        <div className="bg-white border border-gray-200 rounded-lg px-4 py-3">
          <div className="flex items-center gap-1.5 text-[11px] font-semibold uppercase tracking-wide text-gray-500 opacity-70">
            <i className="ri-timer-line text-[13px]" />
            Avg Overdue Age
          </div>
          <div className="text-2xl font-bold text-gray-800 leading-none tabular-nums mt-1">
            {sla.avgOverdueAgeDays != null
              ? `${sla.avgOverdueAgeDays.toFixed(1)}d`
              : '—'}
          </div>
          <div className="text-[10px] text-gray-400 opacity-60 mt-0.5">days past due</div>
        </div>
      </div>

      {/* Window stats */}
      <div className="grid grid-cols-2 sm:grid-cols-3 gap-3">
        <div className="bg-white border border-gray-200 rounded-lg px-4 py-3">
          <div className="text-xs text-gray-500 font-medium">New Breaches ({sla.windowLabel})</div>
          <div className={`text-xl font-bold mt-0.5 tabular-nums ${
            sla.breachedInWindow > 0 ? 'text-red-700' : 'text-gray-700'
          }`}>
            {fmt(sla.breachedInWindow)}
          </div>
        </div>
        <div className="bg-white border border-gray-200 rounded-lg px-4 py-3">
          <div className="text-xs text-gray-500 font-medium">Completed On-Time ({sla.windowLabel})</div>
          <div className="text-xl font-bold text-emerald-700 mt-0.5 tabular-nums">
            {fmt(sla.completedOnTimeInWindow)}
          </div>
        </div>
        <div className="bg-white border border-gray-200 rounded-lg px-4 py-3">
          <div className="text-xs text-gray-500 font-medium">Total Completed ({sla.windowLabel})</div>
          <div className="text-xl font-bold text-gray-700 mt-0.5 tabular-nums">
            {fmt(sla.completedInWindow)}
          </div>
        </div>
      </div>

      {/* Top overdue queues */}
      {sla.topOverdueQueues.length > 0 && (
        <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
          <div className="px-4 py-3 border-b border-gray-100">
            <h4 className="text-sm font-semibold text-gray-700">Top Overdue Queues</h4>
          </div>
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b border-gray-100 text-xs text-gray-500 font-medium">
                <th className="text-left px-4 py-2">Queue</th>
                <th className="text-left px-4 py-2">Type</th>
                <th className="text-right px-4 py-2">Overdue</th>
              </tr>
            </thead>
            <tbody>
              {sla.topOverdueQueues.map((q, i) => (
                <tr key={i} className="border-b border-gray-50 hover:bg-gray-50">
                  <td className="px-4 py-2 font-mono text-xs text-gray-700 truncate max-w-[180px]">{q.queueKey}</td>
                  <td className="px-4 py-2">
                    <span className={`inline-flex text-[10px] font-semibold px-1.5 py-0.5 rounded ${
                      q.queueType === 'Role'
                        ? 'bg-blue-50 text-blue-700'
                        : 'bg-purple-50 text-purple-700'
                    }`}>
                      {q.queueType}
                    </span>
                  </td>
                  <td className="px-4 py-2 text-right font-bold text-red-700 tabular-nums">{fmt(q.overdueCount)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
