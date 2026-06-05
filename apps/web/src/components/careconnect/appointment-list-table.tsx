import Link from 'next/link';
import type { AppointmentSummary } from '@/types/careconnect';
import { StatusBadge } from './status-badge';

interface AppointmentListTableProps {
  appointments: AppointmentSummary[];
  totalCount:   number;
  page:         number;
  pageSize:     number;
}

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

export function AppointmentListTable({
  appointments,
  totalCount,
  page,
  pageSize,
}: AppointmentListTableProps) {
  if (appointments.length === 0) {
    return (
      <div className="bg-white border border-gray-200 rounded-lg p-10 text-center">
        <p className="text-sm text-gray-400">No appointments found.</p>
      </div>
    );
  }

  const start = (page - 1) * pageSize + 1;
  const end   = Math.min(page * pageSize, totalCount);

  return (
    <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
      <div className="overflow-x-auto">
        <table className="min-w-full divide-y divide-gray-100">
          <thead>
            <tr className="bg-gray-50">
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Client</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Provider</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Service</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Scheduled</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Duration</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Status</th>
              <th className="px-4 py-3" />
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-100">
            {appointments.map(appt => (
              <tr key={appt.id} className="hover:bg-gray-50 transition-colors">
                <td className="px-4 py-3">
                  <p className="text-sm font-medium text-gray-900">
                    {appt.clientFirstName} {appt.clientLastName}
                  </p>
                  {appt.caseNumber && (
                    <p className="text-xs text-gray-400 mt-0.5">#{appt.caseNumber}</p>
                  )}
                </td>
                <td className="px-4 py-3 text-sm text-gray-700">{appt.providerName}</td>
                <td className="px-4 py-3 text-sm text-gray-600">{appt.serviceType ?? '—'}</td>
                <td className="px-4 py-3 text-xs text-gray-500 whitespace-nowrap">
                  {formatDateTime(appt.scheduledAtUtc)}
                </td>
                <td className="px-4 py-3 text-xs text-gray-400 whitespace-nowrap">
                  {appt.durationMinutes} min
                </td>
                <td className="px-4 py-3">
                  <StatusBadge status={appt.status} />
                </td>
                <td className="px-4 py-3 text-right">
                  <Link
                    href={`/careconnect/appointments/${appt.id}`}
                    className="text-xs text-primary font-medium hover:underline whitespace-nowrap"
                  >
                    View →
                  </Link>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {/* Footer */}
      <div className="px-4 py-3 border-t border-gray-100 flex items-center justify-between">
        <p className="text-xs text-gray-400">
          Showing {start}–{end} of {totalCount}
        </p>
        <div className="flex items-center gap-2">
          {page > 1 && (
            <Link href={`?page=${page - 1}`} className="text-xs text-primary hover:underline">
              ← Previous
            </Link>
          )}
          {page * pageSize < totalCount && (
            <Link href={`?page=${page + 1}`} className="text-xs text-primary hover:underline">
              Next →
            </Link>
          )}
        </div>
      </div>
    </div>
  );
}
