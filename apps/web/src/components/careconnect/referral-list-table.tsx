import Link from 'next/link';
import type { ReferralSummary } from '@/types/careconnect';
import { formatTimestamp } from '@/lib/format-date';
import { StatusBadge, UrgencyBadge } from './status-badge';
import { ReferralQuickActions } from './referral-quick-actions';

interface ReferralListTableProps {
  referrals:  ReferralSummary[];
  totalCount: number;
  page:       number;
  pageSize:   number;
  isReferrer: boolean;
  isReceiver: boolean;
  orgId?:     string;
  currentQs?: string;
}

function rowHighlight(status: string): string {
  if (status === 'New')        return 'bg-blue-50/40 hover:bg-blue-50 border-l-4 border-l-blue-400';
  if (status === 'NewOpened')  return 'bg-sky-50/40 hover:bg-sky-50 border-l-4 border-l-sky-400';
  if (status === 'Accepted')   return 'hover:bg-gray-50 border-l-4 border-l-teal-400';
  if (status === 'InProgress') return 'bg-amber-50/30 hover:bg-amber-50/60 border-l-4 border-l-amber-400';
  return 'hover:bg-gray-50 border-l-4 border-l-transparent';
}

export function ReferralListTable({
  referrals,
  totalCount,
  page,
  pageSize,
  isReferrer,
  isReceiver,
  orgId,
  currentQs = '',
}: ReferralListTableProps) {
  if (referrals.length === 0) {
    return (
      <div className="bg-white border border-gray-200 rounded-lg p-12 text-center">
        <p className="text-sm font-medium text-gray-500">No referrals match your filters.</p>
        <p className="text-xs text-gray-400 mt-1">Try clearing your search or selecting a different status.</p>
      </div>
    );
  }

  return (
    <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
      <div className="overflow-x-auto">
        <table className="min-w-full divide-y divide-gray-100">
          <thead>
            <tr className="bg-gray-50">
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Client</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Provider</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide hidden sm:table-cell">Service</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide hidden lg:table-cell">Network</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide hidden md:table-cell">Urgency</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Status</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide hidden lg:table-cell">Created</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Actions</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-100">
            {referrals.map(r => (
              <tr key={r.id} className={`transition-colors ${rowHighlight(r.status)}`}>
                {/* Client */}
                <td className="px-4 py-3">
                  <p className="text-sm font-medium text-gray-900 truncate max-w-[160px]">
                    {r.clientFirstName} {r.clientLastName}
                  </p>
                  {r.caseNumber && (
                    <p className="text-xs text-gray-400 mt-0.5">#{r.caseNumber}</p>
                  )}
                </td>

                {/* Provider */}
                <td className="px-4 py-3">
                  <p className="text-sm text-gray-700 truncate max-w-[160px]">{r.providerName}</p>
                </td>

                {/* Service */}
                <td className="px-4 py-3 text-sm text-gray-600 hidden sm:table-cell">
                  <span className="truncate block max-w-[140px]">{r.requestedService}</span>
                </td>

                {/* Network */}
                <td className="px-4 py-3 hidden lg:table-cell">
                  {r.networkName ? (
                    <span className="inline-flex items-center gap-1 rounded-full bg-indigo-50 px-2 py-0.5 text-xs font-medium text-indigo-700 border border-indigo-100 truncate max-w-[140px]" title={r.networkName}>
                      <svg className="w-2.5 h-2.5 flex-shrink-0" fill="currentColor" viewBox="0 0 20 20">
                        <path d="M13 6a3 3 0 11-6 0 3 3 0 016 0zM18 8a2 2 0 11-4 0 2 2 0 014 0zM14 15a4 4 0 00-8 0v1h8v-1zM6 8a2 2 0 11-4 0 2 2 0 014 0zM16 18v-1a5.972 5.972 0 00-.75-2.906A3.005 3.005 0 0119 15v1h-3zM4.75 12.094A5.973 5.973 0 004 15v1H1v-1a3 3 0 013.75-2.906z" />
                      </svg>
                      {r.networkName}
                    </span>
                  ) : (
                    <span className="text-xs text-gray-300">—</span>
                  )}
                </td>

                {/* Urgency */}
                <td className="px-4 py-3 hidden md:table-cell">
                  <UrgencyBadge urgency={r.urgency} />
                </td>

                {/* Status */}
                <td className="px-4 py-3">
                  <StatusBadge status={r.status} />
                  {r.status === 'New' && (
                    <p className="text-[10px] text-blue-500 font-medium mt-0.5 leading-none">Unopened</p>
                  )}
                </td>

                {/* Created */}
                <td className="px-4 py-3 whitespace-nowrap hidden lg:table-cell">
                  {(() => { const ts = formatTimestamp(r.createdAtUtc); return (<><p className="text-xs text-gray-500">{ts.date}</p><p className="text-[11px] text-gray-400">{ts.time}</p></>); })()}
                </td>

                {/* Quick actions */}
                <td className="px-4 py-3">
                  <ReferralQuickActions
                    referral={r}
                    isReferrer={isReferrer && !!orgId && r.referringOrganizationId === orgId}
                    isReceiver={isReceiver && !!orgId && r.receivingOrganizationId === orgId}
                    contextQs={currentQs}
                  />
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {/* Footer / pagination */}
      {totalCount > 0 && (
        <div className="px-4 py-3 border-t border-gray-100 flex items-center justify-between">
          <p className="text-xs text-gray-400">
            Showing {(page - 1) * pageSize + 1}–{Math.min(page * pageSize, totalCount)} of {totalCount}
          </p>
          <div className="flex items-center gap-2">
            {page > 1 && (
              <Link
                href={currentQs ? `?${currentQs}&page=${page - 1}` : `?page=${page - 1}`}
                className="text-xs text-primary hover:underline"
              >
                ← Previous
              </Link>
            )}
            {page * pageSize < totalCount && (
              <Link
                href={currentQs ? `?${currentQs}&page=${page + 1}` : `?page=${page + 1}`}
                className="text-xs text-primary hover:underline"
              >
                Next →
              </Link>
            )}
          </div>
        </div>
      )}
    </div>
  );
}
