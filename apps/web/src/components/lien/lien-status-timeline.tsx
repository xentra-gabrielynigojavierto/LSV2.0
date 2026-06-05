import type { LienStatusHistoryItem } from '@/types/lien';

interface LienStatusTimelineProps {
  history: LienStatusHistoryItem[];
}

const STATUS_DOT: Record<string, string> = {
  Draft:     'bg-gray-400',
  Offered:   'bg-blue-400',
  Sold:      'bg-green-500',
  Withdrawn: 'bg-red-400',
};

function formatDateTime(iso: string): string {
  return new Date(iso).toLocaleString('en-US', {
    month: 'short', day: 'numeric', year: 'numeric',
    hour: 'numeric', minute: '2-digit', hour12: true,
  });
}

export function LienStatusTimeline({ history }: LienStatusTimelineProps) {
  if (history.length === 0) {
    return <p className="text-sm text-gray-400">No status history available.</p>;
  }

  return (
    <ol className="relative border-l border-gray-200 space-y-4 ml-2">
      {history.map((item, idx) => (
        <li key={idx} className="pl-5">
          <span
            className={`absolute -left-1.5 top-1.5 mt-1 w-3 h-3 rounded-full border-2 border-white ${
              STATUS_DOT[item.status] ?? 'bg-gray-400'
            }`}
          />
          <p className="text-sm font-medium text-gray-900">{item.label}</p>
          {item.actorOrgName && (
            <p className="text-xs text-gray-500 mt-0.5">by {item.actorOrgName}</p>
          )}
          <p className="text-xs text-gray-400 mt-0.5">{formatDateTime(item.occurredAtUtc)}</p>
        </li>
      ))}
    </ol>
  );
}
