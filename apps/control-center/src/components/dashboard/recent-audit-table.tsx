import Link from 'next/link';
import type { CanonicalAuditEvent } from '@/types/control-center';

interface RecentAuditTableProps {
  events: CanonicalAuditEvent[];
  error?: string | null;
}

function formatRelativeTime(iso: string): string {
  const diff = Date.now() - new Date(iso).getTime();
  const mins = Math.floor(diff / 60000);
  if (mins < 1) return 'just now';
  if (mins < 60) return `${mins}m ago`;
  const hours = Math.floor(mins / 60);
  if (hours < 24) return `${hours}h ago`;
  const days = Math.floor(hours / 24);
  return `${days}d ago`;
}

const severityColor: Record<string, string> = {
  Critical: 'bg-red-100 text-red-700',
  High:     'bg-orange-100 text-orange-700',
  Medium:   'bg-amber-100 text-amber-700',
  Low:      'bg-gray-100 text-gray-600',
  Info:     'bg-blue-50 text-blue-600',
};

export function RecentAuditTable({ events, error }: RecentAuditTableProps) {
  return (
    <div className="bg-white border border-gray-200 rounded-lg">
      <div className="px-5 py-3 border-b border-gray-100 flex items-center justify-between">
        <h3 className="text-sm font-medium text-gray-700">Recent Audit Events</h3>
        <Link href="/audit-logs" className="text-xs text-blue-600 hover:text-blue-700">View all</Link>
      </div>

      {error ? (
        <div className="px-5 py-6 text-center">
          <p className="text-sm text-gray-400">Unable to load audit data</p>
        </div>
      ) : events.length === 0 ? (
        <div className="px-5 py-6 text-center">
          <p className="text-sm text-gray-400">No recent audit events</p>
        </div>
      ) : (
        <div className="divide-y divide-gray-50">
          {events.map((evt) => (
            <div key={evt.id} className="px-5 py-3 flex items-start gap-3">
              <div className="min-w-0 flex-1">
                <p className="text-sm text-gray-800 truncate">{evt.description}</p>
                <div className="flex items-center gap-2 mt-1">
                  {evt.actorLabel && (
                    <span className="text-xs text-gray-500">{evt.actorLabel}</span>
                  )}
                  {evt.category && (
                    <span className="text-xs text-gray-400">· {evt.category}</span>
                  )}
                </div>
              </div>
              <div className="flex items-center gap-2 shrink-0">
                {evt.severity && (
                  <span className={`text-[10px] font-semibold px-1.5 py-0.5 rounded ${severityColor[evt.severity] ?? 'bg-gray-100 text-gray-500'}`}>
                    {evt.severity}
                  </span>
                )}
                <span className="text-xs text-gray-400 whitespace-nowrap">
                  {formatRelativeTime(evt.occurredAtUtc)}
                </span>
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
