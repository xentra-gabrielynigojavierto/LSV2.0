import Link from 'next/link';
import type { MonitoringSummary } from '@/types/control-center';

interface SystemStatusCardProps {
  data: MonitoringSummary | null;
  error?: string | null;
}

const statusConfig = {
  Healthy:  { dot: 'bg-emerald-500', bg: 'bg-emerald-50', border: 'border-emerald-200', text: 'text-emerald-700', label: 'All Systems Operational' },
  Degraded: { dot: 'bg-amber-500',   bg: 'bg-amber-50',   border: 'border-amber-200',   text: 'text-amber-700',   label: 'Degraded Performance' },
  Down:     { dot: 'bg-red-500',     bg: 'bg-red-50',     border: 'border-red-200',     text: 'text-red-700',     label: 'System Outage' },
} as const;

export function SystemStatusCard({ data, error }: SystemStatusCardProps) {
  if (error) {
    return (
      <div className="bg-white border border-gray-200 rounded-lg px-5 py-4">
        <div className="flex items-center justify-between mb-3">
          <h3 className="text-sm font-medium text-gray-700">System Health</h3>
          <Link href="/monitoring" className="text-xs text-blue-600 hover:text-blue-700">View details</Link>
        </div>
        <p className="text-sm text-gray-400">Unable to load health data</p>
      </div>
    );
  }

  if (!data) return null;

  const status = data.system.status;
  const cfg = statusConfig[status] ?? statusConfig.Down;
  const criticalAlerts = data.alerts.filter(a => a.severity === 'Critical').length;
  const warningAlerts = data.alerts.filter(a => a.severity === 'Warning').length;
  const healthyCount = data.integrations.filter(i => i.status === 'Healthy').length;
  const totalServices = data.integrations.length;

  return (
    <div className={`${cfg.bg} border ${cfg.border} rounded-lg px-5 py-4`}>
      <div className="flex items-center justify-between mb-3">
        <h3 className="text-sm font-medium text-gray-700">System Health</h3>
        <Link href="/monitoring" className="text-xs text-blue-600 hover:text-blue-700">View details</Link>
      </div>

      <div className="flex items-center gap-2 mb-2">
        <span className={`h-2.5 w-2.5 rounded-full ${cfg.dot} animate-pulse`} />
        <span className={`text-sm font-semibold ${cfg.text}`}>{cfg.label}</span>
      </div>

      <div className="flex items-center gap-4 text-xs text-gray-500">
        <span>{healthyCount}/{totalServices} services healthy</span>
        {criticalAlerts > 0 && (
          <span className="text-red-600 font-medium">{criticalAlerts} critical</span>
        )}
        {warningAlerts > 0 && (
          <span className="text-amber-600 font-medium">{warningAlerts} warning{warningAlerts > 1 ? 's' : ''}</span>
        )}
      </div>
    </div>
  );
}
