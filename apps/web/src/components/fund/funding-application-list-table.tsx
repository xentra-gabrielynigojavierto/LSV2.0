import Link from 'next/link';
import type { FundingApplicationSummary } from '@/types/fund';
import { FundingStatusBadge } from './funding-status-badge';

interface FundingApplicationListTableProps {
  applications: FundingApplicationSummary[];
}

function formatCurrency(amount?: number): string {
  if (amount == null) return '—';
  return new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD', maximumFractionDigits: 0 })
    .format(amount);
}

function formatDate(iso: string): string {
  return new Date(iso).toLocaleDateString('en-US', {
    month: 'short',
    day:   'numeric',
    year:  'numeric',
  });
}

export function FundingApplicationListTable({ applications }: FundingApplicationListTableProps) {
  if (applications.length === 0) {
    return (
      <div className="bg-white border border-gray-200 rounded-lg p-10 text-center">
        <p className="text-sm text-gray-400">No applications found.</p>
      </div>
    );
  }

  return (
    <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
      <div className="overflow-x-auto">
        <table className="min-w-full divide-y divide-gray-100">
          <thead>
            <tr className="bg-gray-50">
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Application #</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Applicant</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Case type</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Requested</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Status</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Created</th>
              <th className="px-4 py-3" />
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-100">
            {applications.map(app => (
              <tr key={app.id} className="hover:bg-gray-50 transition-colors">
                <td className="px-4 py-3">
                  <span className="text-xs font-mono text-gray-600">{app.applicationNumber}</span>
                </td>
                <td className="px-4 py-3">
                  <p className="text-sm font-medium text-gray-900">
                    {app.applicantFirstName} {app.applicantLastName}
                  </p>
                  <p className="text-xs text-gray-400 mt-0.5">{app.email}</p>
                </td>
                <td className="px-4 py-3 text-sm text-gray-600">{app.caseType ?? '—'}</td>
                <td className="px-4 py-3 text-sm text-gray-700">{formatCurrency(app.requestedAmount)}</td>
                <td className="px-4 py-3">
                  <FundingStatusBadge status={app.status} />
                </td>
                <td className="px-4 py-3 text-xs text-gray-400 whitespace-nowrap">
                  {formatDate(app.createdAtUtc)}
                </td>
                <td className="px-4 py-3 text-right">
                  <Link
                    href={`/fund/applications/${app.id}`}
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
    </div>
  );
}
