import Link from 'next/link';
import { formatRate } from '@/lib/careconnect-metrics';
import type { ProviderPerformanceRow } from '@/lib/careconnect-metrics';

interface ProviderPerformanceProps {
  rows: ProviderPerformanceRow[];
  from: string;
  to:   string;
  /** true if source data was capped at 200 items */
  isCapped: boolean;
}

/** Server-renderable provider performance table. Sorted by referrals received DESC (done by computeProviderPerformance). */
export function ProviderPerformanceTable({ rows, from, to, isCapped }: ProviderPerformanceProps) {
  if (rows.length === 0) {
    return (
      <p className="text-sm text-gray-400 py-4 text-center">
        No provider activity found for this date range.
      </p>
    );
  }

  return (
    <div className="space-y-2">
      {isCapped && (
        <p className="text-xs text-amber-600 bg-amber-50 border border-amber-100 rounded px-3 py-1.5">
          Showing top providers from the first 200 referrals in this range. Broaden your date range for full accuracy.
        </p>
      )}

      <div className="overflow-x-auto">
        <table className="w-full text-sm">
          <thead>
            <tr className="border-b border-gray-100">
              <th className="text-left text-xs font-medium text-gray-400 pb-2 pr-4">Provider</th>
              <th className="text-right text-xs font-medium text-gray-400 pb-2 px-3">Referrals</th>
              <th className="text-right text-xs font-medium text-gray-400 pb-2 px-3">Acceptance</th>
              <th className="text-right text-xs font-medium text-gray-400 pb-2 pl-3">Appts Completed</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-50">
            {rows.map(row => (
              <tr key={row.providerId} className="hover:bg-gray-50 transition-colors">
                <td className="py-2.5 pr-4">
                  <Link
                    href={`/careconnect/providers/${row.providerId}`}
                    className="font-medium text-gray-900 hover:text-primary transition-colors truncate block max-w-[180px]"
                    title={row.providerName}
                  >
                    {row.providerName}
                  </Link>
                </td>
                <td className="py-2.5 px-3 text-right">
                  <Link
                    href={`/careconnect/referrals?providerId=${row.providerId}&createdFrom=${from}&createdTo=${to}`}
                    className="text-gray-700 hover:text-primary transition-colors tabular-nums"
                  >
                    {row.referralsReceived}
                  </Link>
                </td>
                <td className="py-2.5 px-3 text-right">
                  <span className={`font-medium ${
                    row.acceptanceRate >= 0.7 ? 'text-green-600' :
                    row.acceptanceRate >= 0.4 ? 'text-yellow-600' :
                    row.referralsReceived === 0 ? 'text-gray-300' :
                    'text-red-500'
                  }`}>
                    {formatRate(row.acceptanceRate)}
                  </span>
                </td>
                <td className="py-2.5 pl-3 text-right">
                  <Link
                    href={`/careconnect/appointments?providerId=${row.providerId}&from=${from}&to=${to}`}
                    className="text-gray-700 hover:text-primary transition-colors tabular-nums"
                  >
                    {row.appointmentsCompleted}
                  </Link>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
