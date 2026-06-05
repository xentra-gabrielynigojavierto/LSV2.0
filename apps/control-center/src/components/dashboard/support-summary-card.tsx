import Link from 'next/link';
import type { SupportCase } from '@/types/control-center';

interface SupportSummaryCardProps {
  cases: SupportCase[];
  totalCount: number;
  error?: string | null;
}

const statusDot: Record<string, string> = {
  Open:          'bg-blue-500',
  Investigating: 'bg-amber-500',
  Resolved:      'bg-emerald-500',
  Closed:        'bg-gray-400',
};

const priorityBadge: Record<string, string> = {
  High:   'bg-red-100 text-red-700',
  Medium: 'bg-amber-100 text-amber-700',
  Low:    'bg-gray-100 text-gray-600',
};

export function SupportSummaryCard({ cases, totalCount, error }: SupportSummaryCardProps) {
  const openCount = cases.filter(c => c.status === 'Open' || c.status === 'Investigating').length;

  return (
    <div className="bg-white border border-gray-200 rounded-lg">
      <div className="px-5 py-3 border-b border-gray-100 flex items-center justify-between">
        <div className="flex items-center gap-2">
          <h3 className="text-sm font-medium text-gray-700">Support Cases</h3>
          {openCount > 0 && (
            <span className="inline-flex items-center text-[10px] font-semibold px-1.5 py-0.5 rounded-full bg-blue-100 text-blue-700">
              {openCount} open
            </span>
          )}
        </div>
        <Link href="/support" className="text-xs text-blue-600 hover:text-blue-700">View all ({totalCount})</Link>
      </div>

      {error ? (
        <div className="px-5 py-6 text-center">
          <p className="text-sm text-gray-400">Unable to load support data</p>
        </div>
      ) : cases.length === 0 ? (
        <div className="px-5 py-6 text-center">
          <p className="text-sm text-gray-400">No support cases</p>
        </div>
      ) : (
        <div className="divide-y divide-gray-50">
          {cases.slice(0, 5).map((c) => (
            <Link key={c.id} href={`/support/${c.id}`} className="px-5 py-3 flex items-center gap-3 hover:bg-gray-50 transition-colors block">
              <span className={`h-2 w-2 rounded-full shrink-0 ${statusDot[c.status] ?? 'bg-gray-400'}`} />
              <div className="min-w-0 flex-1">
                <p className="text-sm text-gray-800 truncate">{c.title}</p>
                <p className="text-xs text-gray-400 mt-0.5">{c.tenantName} · {c.status}</p>
              </div>
              <span className={`text-[10px] font-semibold px-1.5 py-0.5 rounded shrink-0 ${priorityBadge[c.priority] ?? 'bg-gray-100 text-gray-500'}`}>
                {c.priority}
              </span>
            </Link>
          ))}
        </div>
      )}
    </div>
  );
}
