import type { AppointmentStatusHistoryItem } from '@/types/careconnect';

interface AppointmentTimelineProps {
  history: AppointmentStatusHistoryItem[];
}

const STATUS_DOT: Record<string, string> = {
  Scheduled:  'bg-blue-400',
  Confirmed:  'bg-green-400',
  Cancelled:  'bg-red-400',
  Completed:  'bg-green-600',
  NoShow:     'bg-orange-400',
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

export function AppointmentTimeline({ history }: AppointmentTimelineProps) {
  if (history.length === 0) {
    return (
      <p className="text-sm text-gray-400">No status history available.</p>
    );
  }

  const sorted = [...history].sort(
    (a, b) => new Date(b.changedAtUtc).getTime() - new Date(a.changedAtUtc).getTime(),
  );

  return (
    <ol className="relative border-l border-gray-200 space-y-4 ml-2">
      {sorted.map((item, idx) => (
        <li key={idx} className="pl-5">
          {/* Dot */}
          <span
            className={`absolute -left-1.5 top-1.5 mt-1 w-3 h-3 rounded-full border-2 border-white ${
              STATUS_DOT[item.status] ?? 'bg-gray-400'
            }`}
          />

          <div>
            <p className="text-sm font-medium text-gray-900">{item.status}</p>
            <p className="text-xs text-gray-400 mt-0.5">
              {formatDateTime(item.changedAtUtc)}
              {item.changedByName ? ` · ${item.changedByName}` : ''}
            </p>
            {item.notes && (
              <p className="text-xs text-gray-500 mt-1 whitespace-pre-wrap">{item.notes}</p>
            )}
          </div>
        </li>
      ))}
    </ol>
  );
}
