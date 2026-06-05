import Link from 'next/link';
import type { OutboxAnalyticsSummary } from '@/types/control-center';

interface AnalyticsOutboxCardsProps {
  outbox: OutboxAnalyticsSummary;
}

/**
 * E19 — Outbox reliability analytics cards.
 * Extends E17 outbox summary with window-scoped trends and event-type breakdown.
 * Dead-letter card links to the outbox ops page for drill-down.
 */
export function AnalyticsOutboxCards({ outbox }: AnalyticsOutboxCardsProps) {
  const fmt = (n: number) => n.toLocaleString();

  return (
    <div className="space-y-4">
      {/* Current state health */}
      <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-5 gap-3">
        <div className="bg-amber-50 border border-amber-200 rounded-lg px-4 py-3">
          <div className="flex items-center gap-1.5 text-[11px] font-semibold uppercase tracking-wide text-amber-700 opacity-70">
            <i className="ri-time-line" />Pending
          </div>
          <div className="text-2xl font-bold text-amber-800 tabular-nums mt-1">{fmt(outbox.pendingCount)}</div>
          <div className="text-[10px] text-amber-600 opacity-60">awaiting dispatch</div>
        </div>

        <div className="bg-blue-50 border border-blue-200 rounded-lg px-4 py-3">
          <div className="flex items-center gap-1.5 text-[11px] font-semibold uppercase tracking-wide text-blue-700 opacity-70">
            <i className="ri-loader-4-line" />Processing
          </div>
          <div className="text-2xl font-bold text-blue-800 tabular-nums mt-1">{fmt(outbox.processingCount)}</div>
          <div className="text-[10px] text-blue-600 opacity-60">claimed by worker</div>
        </div>

        <div className={`rounded-lg px-4 py-3 border ${
          outbox.failedCount > 0 ? 'bg-red-50 border-red-200' : 'bg-gray-50 border-gray-200'
        }`}>
          <div className={`flex items-center gap-1.5 text-[11px] font-semibold uppercase tracking-wide opacity-70 ${
            outbox.failedCount > 0 ? 'text-red-700' : 'text-gray-400'
          }`}>
            <i className="ri-error-warning-line" />Failed
          </div>
          <div className={`text-2xl font-bold tabular-nums mt-1 ${
            outbox.failedCount > 0 ? 'text-red-800' : 'text-gray-600'
          }`}>
            {fmt(outbox.failedCount)}
          </div>
          <div className="text-[10px] opacity-60 text-gray-400">still retrying</div>
        </div>

        <Link href="/operations/outbox?status=DeadLettered" className="block">
          <div className={`rounded-lg px-4 py-3 border h-full hover:opacity-90 transition-opacity ${
            outbox.deadLetteredCount > 0
              ? 'bg-red-100 border-red-300'
              : 'bg-gray-50 border-gray-200'
          }`}>
            <div className={`flex items-center gap-1.5 text-[11px] font-semibold uppercase tracking-wide opacity-70 ${
              outbox.deadLetteredCount > 0 ? 'text-red-800' : 'text-gray-400'
            }`}>
              <i className="ri-skull-line" />Dead Letters
            </div>
            <div className={`text-2xl font-bold tabular-nums mt-1 ${
              outbox.deadLetteredCount > 0 ? 'text-red-900' : 'text-gray-600'
            }`}>
              {fmt(outbox.deadLetteredCount)}
            </div>
            <div className="text-[10px] opacity-60 text-red-500">manual action needed ↗</div>
          </div>
        </Link>

        <div className="bg-emerald-50 border border-emerald-200 rounded-lg px-4 py-3">
          <div className="flex items-center gap-1.5 text-[11px] font-semibold uppercase tracking-wide text-emerald-700 opacity-70">
            <i className="ri-checkbox-circle-line" />Succeeded
          </div>
          <div className="text-2xl font-bold text-emerald-800 tabular-nums mt-1">{fmt(outbox.succeededCount)}</div>
          <div className="text-[10px] text-emerald-600 opacity-60">all time</div>
        </div>
      </div>

      {/* Window trend */}
      <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
        <div className="bg-white border border-gray-200 rounded-lg px-4 py-3">
          <div className="text-xs text-gray-500 font-medium">Created ({outbox.windowLabel})</div>
          <div className="text-xl font-bold text-gray-800 tabular-nums mt-0.5">{fmt(outbox.createdInWindow)}</div>
        </div>
        <div className="bg-white border border-gray-200 rounded-lg px-4 py-3">
          <div className="text-xs text-gray-500 font-medium">Succeeded ({outbox.windowLabel})</div>
          <div className="text-xl font-bold text-emerald-700 tabular-nums mt-0.5">{fmt(outbox.succeededInWindow)}</div>
        </div>
        <div className="bg-white border border-gray-200 rounded-lg px-4 py-3">
          <div className="text-xs text-gray-500 font-medium">Failed ({outbox.windowLabel})</div>
          <div className={`text-xl font-bold tabular-nums mt-0.5 ${outbox.failedInWindow > 0 ? 'text-red-700' : 'text-gray-700'}`}>
            {fmt(outbox.failedInWindow)}
          </div>
        </div>
        <div className="bg-white border border-gray-200 rounded-lg px-4 py-3">
          <div className="text-xs text-gray-500 font-medium">Dead-lettered ({outbox.windowLabel})</div>
          <div className={`text-xl font-bold tabular-nums mt-0.5 ${outbox.deadLetteredInWindow > 0 ? 'text-red-800' : 'text-gray-700'}`}>
            {fmt(outbox.deadLetteredInWindow)}
          </div>
        </div>
      </div>

      {/* Failed by event type */}
      {outbox.failedByEventType.length > 0 && (
        <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
          <div className="px-4 py-3 border-b border-gray-100">
            <h4 className="text-sm font-semibold text-gray-700">Failed / Dead-lettered by Event Type</h4>
          </div>
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b border-gray-100 text-xs text-gray-500 font-medium">
                <th className="text-left px-4 py-2">Event Type</th>
                <th className="text-right px-4 py-2">Failed</th>
                <th className="text-right px-4 py-2">Dead-lettered</th>
                <th className="text-right px-4 py-2">Total</th>
              </tr>
            </thead>
            <tbody>
              {outbox.failedByEventType.map((e, i) => (
                <tr key={i} className="border-b border-gray-50 hover:bg-gray-50">
                  <td className="px-4 py-2 font-mono text-xs text-gray-700">{e.eventType}</td>
                  <td className={`px-4 py-2 text-right tabular-nums ${e.failedCount > 0 ? 'text-red-600' : 'text-gray-400'}`}>
                    {fmt(e.failedCount)}
                  </td>
                  <td className={`px-4 py-2 text-right tabular-nums font-semibold ${e.deadLettered > 0 ? 'text-red-800' : 'text-gray-400'}`}>
                    {fmt(e.deadLettered)}
                  </td>
                  <td className="px-4 py-2 text-right tabular-nums font-bold text-gray-800">{fmt(e.totalUnhealthy)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
