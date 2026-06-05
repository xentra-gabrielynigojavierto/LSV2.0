import { formatDateTime } from '@/lib/lien-utils';

interface TimelineEvent {
  action: string;
  timestamp: string;
  actor: string;
  note?: string;
}

interface ActivityTimelineProps {
  events: TimelineEvent[];
  title?: string;
}

export function ActivityTimeline({ events, title = 'Activity History' }: ActivityTimelineProps) {
  return (
    <div className="bg-white border border-gray-200 rounded-xl p-5">
      <h3 className="text-sm font-semibold text-gray-800 mb-4">{title}</h3>
      {events.length === 0 ? (
        <p className="text-sm text-gray-400">No activity yet.</p>
      ) : (
        <div className="relative">
          <div className="absolute left-[7px] top-2 bottom-2 w-px bg-gray-200" />
          <ul className="space-y-4">
            {events.map((event, i) => (
              <li key={i} className="relative pl-6">
                <div className="absolute left-0 top-1.5 w-[15px] h-[15px] rounded-full bg-white border-2 border-gray-300 z-10" />
                <div>
                  <p className="text-sm text-gray-700 font-medium">{event.action}</p>
                  {event.note && <p className="text-xs text-gray-500 mt-0.5">{event.note}</p>}
                  <p className="text-xs text-gray-400 mt-0.5">
                    {event.actor} &middot; {formatDateTime(event.timestamp)}
                  </p>
                </div>
              </li>
            ))}
          </ul>
        </div>
      )}
    </div>
  );
}
