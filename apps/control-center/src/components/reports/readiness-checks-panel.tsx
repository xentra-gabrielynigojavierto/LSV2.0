import type { ReportsReadinessCheck } from '@/types/control-center';

interface ReadinessChecksPanelProps {
  checks: ReportsReadinessCheck[];
}

export function ReadinessChecksPanel({ checks }: ReadinessChecksPanelProps) {
  if (checks.length === 0) return null;

  const okCount   = checks.filter(c => c.status === 'ok').length;
  const mockCount = checks.filter(c => c.status === 'mock').length;
  const failCount = checks.filter(c => c.status === 'fail').length;

  return (
    <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
      <div className="px-5 py-3.5 border-b border-gray-100 bg-gray-50 flex items-center justify-between">
        <div>
          <h2 className="text-xs font-semibold uppercase tracking-wide text-gray-500">
            Readiness Checks
          </h2>
          <p className="text-[11px] text-gray-400 mt-0.5">
            Adapter and dependency probes
          </p>
        </div>
        <div className="flex items-center gap-2">
          {okCount > 0 && (
            <span className="text-xs text-green-600 font-medium">{okCount} ok</span>
          )}
          {mockCount > 0 && (
            <span className="text-xs text-blue-600 font-medium">{mockCount} mock</span>
          )}
          {failCount > 0 && (
            <span className="text-xs text-red-600 font-medium">{failCount} fail</span>
          )}
        </div>
      </div>

      <div className="divide-y divide-gray-100">
        {checks.map(check => (
          <div key={check.name} className="flex items-center gap-3 px-5 py-3">
            <span className={`h-2 w-2 rounded-full shrink-0 ${
              check.status === 'ok'   ? 'bg-green-500' :
              check.status === 'mock' ? 'bg-blue-400'  :
                                        'bg-red-600'
            }`} />
            <span className="flex-1 text-sm text-gray-900 font-mono">
              {check.name}
            </span>
            <span className={`inline-flex items-center px-2 py-0.5 rounded text-xs font-semibold ${
              check.status === 'ok'   ? 'bg-green-100 text-green-700' :
              check.status === 'mock' ? 'bg-blue-100 text-blue-700'   :
                                        'bg-red-100 text-red-700'
            }`}>
              {check.status}
            </span>
          </div>
        ))}
      </div>
    </div>
  );
}
