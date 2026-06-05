import Link from 'next/link';
import type { TenantSummary, TenantType } from '@/types/control-center';

interface TenantBreakdownCardProps {
  tenants: TenantSummary[];
  totalCount: number;
  error?: string | null;
}

const typeColors: Record<TenantType, string> = {
  LawFirm:    'bg-indigo-500',
  Provider:   'bg-emerald-500',
  Funder:     'bg-amber-500',
  LienOwner:  'bg-purple-500',
  Corporate:  'bg-blue-500',
  Government: 'bg-rose-500',
  Other:      'bg-gray-400',
};

export function TenantBreakdownCard({ tenants, totalCount, error }: TenantBreakdownCardProps) {
  if (error) {
    return (
      <div className="bg-white border border-gray-200 rounded-lg px-5 py-4">
        <h3 className="text-sm font-medium text-gray-700 mb-3">Tenant Distribution</h3>
        <p className="text-sm text-gray-400">Unable to load tenant data</p>
      </div>
    );
  }

  const byType = tenants.reduce<Record<string, number>>((acc, t) => {
    acc[t.type] = (acc[t.type] ?? 0) + 1;
    return acc;
  }, {});

  const byStatus = tenants.reduce<Record<string, number>>((acc, t) => {
    acc[t.status] = (acc[t.status] ?? 0) + 1;
    return acc;
  }, {});

  const sorted = Object.entries(byType).sort(([, a], [, b]) => b - a);

  return (
    <div className="bg-white border border-gray-200 rounded-lg">
      <div className="px-5 py-3 border-b border-gray-100 flex items-center justify-between">
        <h3 className="text-sm font-medium text-gray-700">Tenant Distribution</h3>
        <Link href="/tenants" className="text-xs text-blue-600 hover:text-blue-700">View all</Link>
      </div>

      <div className="px-5 py-4">
        <div className="flex items-center gap-3 mb-4">
          {Object.entries(byStatus).map(([status, count]) => (
            <div key={status} className="text-center">
              <p className="text-lg font-semibold text-gray-900">{count}</p>
              <p className="text-xs text-gray-500">{status}</p>
            </div>
          ))}
        </div>

        <div className="space-y-2">
          {sorted.map(([type, count]) => {
            const pct = totalCount > 0 ? Math.round((count / totalCount) * 100) : 0;
            return (
              <div key={type} className="flex items-center gap-3">
                <span className={`h-2.5 w-2.5 rounded-full shrink-0 ${typeColors[type as TenantType] ?? 'bg-gray-400'}`} />
                <span className="text-sm text-gray-700 w-24 truncate">{type}</span>
                <div className="flex-1 h-2 bg-gray-100 rounded-full overflow-hidden">
                  <div
                    className={`h-full rounded-full ${typeColors[type as TenantType] ?? 'bg-gray-400'}`}
                    style={{ width: `${Math.max(pct, 4)}%` }}
                  />
                </div>
                <span className="text-xs text-gray-500 w-8 text-right">{count}</span>
              </div>
            );
          })}
        </div>
      </div>
    </div>
  );
}
