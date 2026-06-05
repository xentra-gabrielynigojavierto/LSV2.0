import Link from 'next/link';
import { requireOrg } from '@/lib/auth-guards';
import { ProductRole } from '@/types';
import { fundServerApi } from '@/lib/fund-server-api';
import { ServerApiError } from '@/lib/server-api-client';
import { FundingApplicationListTable } from '@/components/fund/funding-application-list-table';

export const dynamic = 'force-dynamic';


interface ApplicationsPageProps {
  searchParams: Promise<{ status?: string }>;
}

/**
 * /fund/applications — Application list.
 *
 * Access: SYNQFUND_REFERRER or SYNQFUND_FUNDER.
 *
 * UX shaping by role:
 *   - SYNQFUND_REFERRER (law firm): heading = "My Applications" + New Application button.
 *   - SYNQFUND_FUNDER:              heading = "Assigned Applications" (no create button).
 *
 * The backend scopes results to the caller's org automatically.
 * The status filter is applied on this page (backend supports ?status= query param).
 */
export default async function ApplicationsPage({ searchParams }: ApplicationsPageProps) {
  const searchParamsData = await searchParams;
  const session = await requireOrg();

  const isReferrer = session.productRoles.includes(ProductRole.SynqFundReferrer);
  const isFunder   = session.productRoles.includes(ProductRole.SynqFundFunder);

  if (!isReferrer && !isFunder) {
    return (
      <div className="bg-yellow-50 border border-yellow-200 rounded-lg px-4 py-3 text-sm text-yellow-700">
        You do not have a SynqFund role. Contact your administrator to gain access.
      </div>
    );
  }

  let applications = null;
  let fetchError: string | null = null;

  try {
    applications = await fundServerApi.applications.search({
      status: searchParamsData.status || undefined,
    });
  } catch (err) {
    fetchError = err instanceof ServerApiError ? err.message : 'Failed to load applications.';
  }

  const heading = isReferrer ? 'My Applications' : 'Assigned Applications';

  const STATUS_FILTERS = isReferrer
    ? ['', 'Draft', 'Submitted', 'InReview', 'Approved', 'Rejected']
    : ['', 'Submitted', 'InReview', 'Approved', 'Rejected'];

  return (
    <div className="space-y-4">
      {/* Header */}
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold text-gray-900">{heading}</h1>
        {isReferrer && (
          <Link
            href="/fund/applications/new"
            className="bg-primary text-white text-sm font-medium px-4 py-2 rounded-md hover:opacity-90 transition-opacity"
          >
            New Application
          </Link>
        )}
      </div>

      {/* Status filter chips */}
      <div className="flex items-center gap-2 flex-wrap">
        {STATUS_FILTERS.map(s => (
          <Link
            key={s}
            href={s ? `/fund/applications?status=${s}` : '/fund/applications'}
            className={`text-sm px-3 py-1 rounded-full border transition-colors ${
              (searchParamsData.status ?? '') === s
                ? 'bg-primary text-white border-primary'
                : 'bg-white text-gray-600 border-gray-200 hover:border-gray-400'
            }`}
          >
            {s === 'InReview' ? 'In Review' : s || 'All'}
          </Link>
        ))}
      </div>

      {/* Error */}
      {fetchError && (
        <div className="bg-red-50 border border-red-200 rounded-lg px-4 py-3 text-sm text-red-700">
          {fetchError}
        </div>
      )}

      {/* Table */}
      {applications && (
        <FundingApplicationListTable applications={applications} />
      )}
    </div>
  );
}
