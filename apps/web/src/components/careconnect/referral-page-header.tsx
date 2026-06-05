import type { ReferralDetail } from '@/types/careconnect';
import { StatusBadge, UrgencyBadge } from './status-badge';

interface ReferralPageHeaderProps {
  referral: ReferralDetail;
}

function formatDate(iso: string | undefined): string {
  if (!iso) return '—';
  return new Date(iso).toLocaleDateString('en-US', {
    month: 'short',
    day:   'numeric',
    year:  'numeric',
  });
}

export function ReferralPageHeader({ referral }: ReferralPageHeaderProps) {
  return (
    <div className="bg-white border border-gray-200 rounded-lg px-6 py-5">
      <div className="flex items-start justify-between gap-4 flex-wrap">
        {/* Identity */}
        <div className="min-w-0">
          <h1 className="text-xl font-semibold text-gray-900 leading-tight truncate">
            {referral.clientFirstName} {referral.clientLastName}
          </h1>
          <div className="flex items-center gap-3 mt-1 flex-wrap">
            {referral.caseNumber && (
              <span className="text-sm text-gray-500">Case #{referral.caseNumber}</span>
            )}
            <span className="text-sm text-gray-500">{referral.providerName}</span>
            <span className="text-xs text-gray-400">Created {formatDate(referral.createdAtUtc)}</span>
          </div>
        </div>

        {/* Status + urgency pills */}
        <div className="flex items-center gap-2 shrink-0">
          <UrgencyBadge urgency={referral.urgency} />
          <StatusBadge status={referral.status} size="md" />
        </div>
      </div>

      {/* Quick-scan row: service */}
      <div className="mt-3 pt-3 border-t border-gray-100 flex items-center gap-6 flex-wrap">
        <div className="text-xs text-gray-500">
          <span className="font-medium text-gray-700">{referral.requestedService}</span>
          <span className="ml-1 text-gray-400">service requested</span>
        </div>
      </div>
    </div>
  );
}
