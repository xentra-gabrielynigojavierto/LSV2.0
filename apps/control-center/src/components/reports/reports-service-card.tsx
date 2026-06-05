import type { ReportsServiceStatus } from '@/types/control-center';

interface ReportsServiceCardProps {
  status:     ReportsServiceStatus;
  latencyMs?: number;
  checkedAt:  string;
}

const STATUS_CONFIG: Record<ReportsServiceStatus, {
  bg:    string;
  ring:  string;
  dot:   string;
  text:  string;
  label: string;
}> = {
  online:   { bg: 'bg-green-50',  ring: 'ring-green-200', dot: 'bg-green-500', text: 'text-green-700', label: 'Reports Service Online'  },
  degraded: { bg: 'bg-amber-50',  ring: 'ring-amber-200', dot: 'bg-amber-500', text: 'text-amber-700', label: 'Reports Service Degraded' },
  offline:  { bg: 'bg-red-50',    ring: 'ring-red-200',   dot: 'bg-red-600',   text: 'text-red-700',   label: 'Reports Service Offline'  },
};

export function ReportsServiceCard({ status, latencyMs, checkedAt }: ReportsServiceCardProps) {
  const cfg = STATUS_CONFIG[status];
  const since = formatTime(checkedAt);

  return (
    <div className={`rounded-xl ring-1 ${cfg.bg} ${cfg.ring} px-6 py-5 flex items-center gap-4`}>
      <span className="relative flex h-4 w-4 shrink-0">
        {status === 'online' && (
          <span className={`animate-ping absolute inline-flex h-full w-full rounded-full ${cfg.dot} opacity-40`} />
        )}
        <span className={`relative inline-flex rounded-full h-4 w-4 ${cfg.dot}`} />
      </span>

      <div className="flex-1 min-w-0">
        <p className={`text-base font-semibold ${cfg.text}`}>{cfg.label}</p>
        <p className="text-xs text-gray-500 mt-0.5">
          {latencyMs !== undefined && (
            <>Latency: <span className="font-medium">{latencyMs} ms</span>{' · '}</>
          )}
          Last checked {since}
        </p>
      </div>

      <span className={`inline-flex items-center px-2.5 py-1 rounded-full text-xs font-semibold border shrink-0 ${
        status === 'online'  ? 'bg-green-100 text-green-700 border-green-300' :
        status === 'degraded' ? 'bg-amber-100 text-amber-700 border-amber-300' :
                                'bg-red-100 text-red-700 border-red-300'
      }`}>
        {status === 'online' ? 'Online' : status === 'degraded' ? 'Degraded' : 'Offline'}
      </span>
    </div>
  );
}

function formatTime(iso: string): string {
  try {
    return new Date(iso).toLocaleTimeString('en-US', {
      hour: '2-digit', minute: '2-digit', second: '2-digit',
      hour12: false, timeZone: 'UTC', timeZoneName: 'short',
    });
  } catch {
    return iso;
  }
}
