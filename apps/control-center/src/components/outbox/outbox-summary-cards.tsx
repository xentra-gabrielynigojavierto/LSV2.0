import type { OutboxSummary } from '@/types/control-center';

interface OutboxSummaryCardsProps {
  summary: OutboxSummary;
}

interface CardConfig {
  label:   string;
  value:   number;
  color:   string;
  icon:    string;
  hint?:   string;
}

/**
 * E17 — summary cards row for the outbox ops page. Shows grouped counts
 * (Pending, Processing, Failed, Dead Letters, Processed) so operators
 * can gauge async health at a glance.
 */
export function OutboxSummaryCards({ summary }: OutboxSummaryCardsProps) {
  const cards: CardConfig[] = [
    {
      label: 'Pending',
      value: summary.pendingCount,
      color: 'bg-amber-50 border-amber-200 text-amber-700',
      icon:  'ri-time-line',
      hint:  'Awaiting dispatch',
    },
    {
      label: 'Processing',
      value: summary.processingCount,
      color: 'bg-blue-50 border-blue-200 text-blue-700',
      icon:  'ri-loader-4-line',
      hint:  'Claimed by worker',
    },
    {
      label: 'Failed',
      value: summary.failedCount,
      color: summary.failedCount > 0
        ? 'bg-red-50 border-red-200 text-red-700'
        : 'bg-gray-50 border-gray-200 text-gray-500',
      icon:  'ri-error-warning-line',
      hint:  'Transient failures — still retrying',
    },
    {
      label: 'Dead Letters',
      value: summary.deadLetteredCount,
      color: summary.deadLetteredCount > 0
        ? 'bg-red-100 border-red-300 text-red-800'
        : 'bg-gray-50 border-gray-200 text-gray-500',
      icon:  'ri-skull-line',
      hint:  'Exhausted all retries — manual action required',
    },
    {
      label: 'Processed',
      value: summary.succeededCount,
      color: 'bg-green-50 border-green-200 text-green-700',
      icon:  'ri-checkbox-circle-line',
      hint:  'Successfully dispatched',
    },
  ];

  return (
    <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-5 gap-3">
      {cards.map(card => (
        <div
          key={card.label}
          className={`flex flex-col gap-1 rounded-lg border px-4 py-3 ${card.color}`}
          title={card.hint}
        >
          <div className="flex items-center gap-1.5 text-[11px] font-semibold uppercase tracking-wide opacity-70">
            <i className={`${card.icon} text-[13px]`} aria-hidden="true" />
            {card.label}
          </div>
          <div className="text-2xl font-bold leading-none tabular-nums">
            {card.value.toLocaleString()}
          </div>
          {card.hint && (
            <div className="text-[10px] opacity-60 truncate">{card.hint}</div>
          )}
        </div>
      ))}
    </div>
  );
}
