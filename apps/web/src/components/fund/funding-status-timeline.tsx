import type { ApplicationStatusHistoryItem } from '@/types/fund';

interface FundingStatusTimelineProps {
  history: ApplicationStatusHistoryItem[];
}

const STATUS_DOT: Record<string, string> = {
  Draft:     'bg-gray-400',
  Submitted: 'bg-yellow-400',
  InReview:  'bg-blue-400',
  Approved:  'bg-green-500',
  Rejected:  'bg-red-400',
};

function formatDateTime(iso: string): string {
  return new Date(iso).toLocaleString('en-US', {
    month:   'short',
    day:     'numeric',
    year:    'numeric',
    hour:    'numeric',
    minute:  '2-digit',
    hour12:  true,
  });
}

export function FundingStatusTimeline({ history }: FundingStatusTimelineProps) {
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
          <p className="text-xs text-gray-400 mt-0.5">{formatDateTime(item.occurredAtUtc)}</p>
        </li>
      ))}
    </ol>
  );
}
