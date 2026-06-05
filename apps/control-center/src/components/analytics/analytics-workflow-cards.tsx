import type { WorkflowThroughput } from '@/types/control-center';

interface AnalyticsWorkflowCardsProps {
  throughput: WorkflowThroughput;
}

/**
 * E19 — Workflow throughput summary cards.
 * Shows started/completed/cancelled/failed counts, active workflows,
 * and cycle-time statistics for the reporting window.
 */
export function AnalyticsWorkflowCards({ throughput: t }: AnalyticsWorkflowCardsProps) {
  const fmt      = (n: number) => n.toLocaleString();
  const cycleHrs = (h: number | null) =>
    h == null ? '—' : h < 24 ? `${h.toFixed(1)}h` : `${(h / 24).toFixed(1)}d`;

  return (
    <div className="space-y-4">
      {/* Throughput row */}
      <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-5 gap-3">
        <div className="bg-white border border-gray-200 rounded-lg px-4 py-3">
          <div className="text-[11px] font-semibold uppercase tracking-wide text-gray-400">Started</div>
          <div className="text-2xl font-bold text-gray-800 tabular-nums mt-1">{fmt(t.startedInWindow)}</div>
          <div className="text-[10px] text-gray-400">{t.windowLabel}</div>
        </div>
        <div className="bg-emerald-50 border border-emerald-200 rounded-lg px-4 py-3">
          <div className="text-[11px] font-semibold uppercase tracking-wide text-emerald-600">Completed</div>
          <div className="text-2xl font-bold text-emerald-800 tabular-nums mt-1">{fmt(t.completedInWindow)}</div>
          <div className="text-[10px] text-emerald-500">{t.windowLabel}</div>
        </div>
        <div className="bg-white border border-gray-200 rounded-lg px-4 py-3">
          <div className="text-[11px] font-semibold uppercase tracking-wide text-gray-400">Cancelled</div>
          <div className="text-2xl font-bold text-gray-700 tabular-nums mt-1">{fmt(t.cancelledInWindow)}</div>
          <div className="text-[10px] text-gray-400">{t.windowLabel}</div>
        </div>
        <div className={`rounded-lg px-4 py-3 border ${
          t.failedInWindow > 0 ? 'bg-red-50 border-red-200' : 'bg-white border-gray-200'
        }`}>
          <div className={`text-[11px] font-semibold uppercase tracking-wide ${
            t.failedInWindow > 0 ? 'text-red-500' : 'text-gray-400'
          }`}>
            Failed
          </div>
          <div className={`text-2xl font-bold tabular-nums mt-1 ${
            t.failedInWindow > 0 ? 'text-red-800' : 'text-gray-700'
          }`}>
            {fmt(t.failedInWindow)}
          </div>
          <div className="text-[10px] text-gray-400">{t.windowLabel}</div>
        </div>
        <div className="bg-blue-50 border border-blue-200 rounded-lg px-4 py-3">
          <div className="text-[11px] font-semibold uppercase tracking-wide text-blue-600">Currently Active</div>
          <div className="text-2xl font-bold text-blue-800 tabular-nums mt-1">{fmt(t.currentlyActiveCount)}</div>
          <div className="text-[10px] text-blue-500">all time</div>
        </div>
      </div>

      {/* Cycle time */}
      <div className="grid grid-cols-2 gap-3">
        <div className="bg-white border border-gray-200 rounded-lg px-4 py-3">
          <div className="text-xs text-gray-500 font-medium">Avg Cycle Time ({t.windowLabel})</div>
          <div className="text-xl font-bold text-gray-800 tabular-nums mt-1">{cycleHrs(t.avgCycleTimeHours)}</div>
          <div className="text-[10px] text-gray-400">completedAt − createdAt</div>
        </div>
        <div className="bg-white border border-gray-200 rounded-lg px-4 py-3">
          <div className="text-xs text-gray-500 font-medium">Median Cycle Time ({t.windowLabel})</div>
          <div className="text-xl font-bold text-gray-800 tabular-nums mt-1">{cycleHrs(t.medianCycleTimeHours)}</div>
          <div className="text-[10px] text-gray-400">p50 of completions</div>
        </div>
      </div>

      {/* Product breakdown */}
      {t.byProduct.length > 0 && (
        <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
          <div className="px-4 py-3 border-b border-gray-100">
            <h4 className="text-sm font-semibold text-gray-700">By Product ({t.windowLabel})</h4>
          </div>
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b border-gray-100 text-xs text-gray-500 font-medium">
                <th className="text-left px-4 py-2">Product</th>
                <th className="text-right px-4 py-2">Started</th>
                <th className="text-right px-4 py-2">Completed</th>
                <th className="text-right px-4 py-2">Active</th>
              </tr>
            </thead>
            <tbody>
              {t.byProduct.map((p, i) => (
                <tr key={i} className="border-b border-gray-50 hover:bg-gray-50">
                  <td className="px-4 py-2 font-mono text-xs text-gray-700">{p.productKey}</td>
                  <td className="px-4 py-2 text-right tabular-nums text-gray-600">{p.startedCount.toLocaleString()}</td>
                  <td className="px-4 py-2 text-right tabular-nums text-emerald-700">{p.completedCount.toLocaleString()}</td>
                  <td className="px-4 py-2 text-right tabular-nums text-blue-700">{p.activeCount.toLocaleString()}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
