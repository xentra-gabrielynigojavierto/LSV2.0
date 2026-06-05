import type { NotifStatus } from '@/lib/notifications-api';

const MAP: Record<NotifStatus, { label: string; cls: string }> = {
  accepted:   { label: 'Accepted',   cls: 'bg-blue-50   text-blue-700   border-blue-200'   },
  processing: { label: 'Processing', cls: 'bg-indigo-50 text-indigo-700 border-indigo-200' },
  sent:       { label: 'Sent',       cls: 'bg-green-50  text-green-700  border-green-200'  },
  failed:     { label: 'Failed',     cls: 'bg-red-50    text-red-700    border-red-200'    },
  blocked:    { label: 'Blocked',    cls: 'bg-amber-50  text-amber-700  border-amber-200'  },
};

export function NotificationStatusBadge({ status }: { status: string }) {
  const cfg = MAP[status as NotifStatus] ?? { label: status, cls: 'bg-gray-50 text-gray-600 border-gray-200' };
  return (
    <span className={`inline-flex items-center px-2 py-0.5 rounded-full text-[11px] font-semibold border ${cfg.cls}`}>
      {cfg.label}
    </span>
  );
}
