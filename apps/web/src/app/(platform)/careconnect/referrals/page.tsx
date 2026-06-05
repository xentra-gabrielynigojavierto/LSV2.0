import Link from 'next/link';
import { Suspense } from 'react';
import { requireOrg } from '@/lib/auth-guards';
import { ProductRole, OrgType } from '@/types';
import { checkCareConnectReceiverAccess } from '@/lib/careconnect-access';
import { ReferralAccessBlocked } from '@/components/careconnect/referral-access-blocked';
import { careConnectServerApi } from '@/lib/careconnect-server-api';
import { ServerApiError } from '@/lib/server-api-client';
import { ReferralListTable } from '@/components/careconnect/referral-list-table';
import { ReferralQueueToolbar } from '@/components/careconnect/referral-queue-toolbar';
import { isValidIsoDate, formatDisplayDate } from '@/lib/daterange';
import type { NetworkReferralItem } from '@/types/careconnect';

export const dynamic = 'force-dynamic';


interface ReferralsPageProps {
  searchParams: Promise<{
    status?:      string;
    urgency?:     string;
    providerId?:  string;
    createdFrom?: string;
    createdTo?:   string;
    page?:        string;
    search?:      string;
  }>;
}

// ── Status badge colours ───────────────────────────────────────────────────────

const STATUS_BADGE: Record<string, string> = {
  New:        'bg-blue-100 text-blue-800',
  NewOpened:  'bg-blue-50 text-blue-600',
  Accepted:   'bg-indigo-100 text-indigo-800',
  InProgress: 'bg-yellow-100 text-yellow-800',
  Completed:  'bg-green-100 text-green-800',
  Declined:   'bg-red-100 text-red-800',
  Cancelled:  'bg-gray-100 text-gray-500',
};

const URGENCY_BADGE: Record<string, string> = {
  Emergency: 'bg-red-100 text-red-800',
  Urgent:    'bg-orange-100 text-orange-800',
  Normal:    'bg-gray-100 text-gray-600',
  Low:       'bg-gray-50 text-gray-400',
};

function StatusBadge({ status }: { status: string }) {
  const cls = STATUS_BADGE[status] ?? 'bg-gray-100 text-gray-700';
  return (
    <span className={`inline-flex items-center px-2 py-0.5 rounded text-xs font-medium ${cls}`}>
      {status === 'NewOpened' ? 'Opened' : status}
    </span>
  );
}

function UrgencyBadge({ urgency }: { urgency: string }) {
  const cls = URGENCY_BADGE[urgency] ?? 'bg-gray-100 text-gray-600';
  return (
    <span className={`inline-flex items-center px-2 py-0.5 rounded text-xs font-medium ${cls}`}>
      {urgency}
    </span>
  );
}

function formatDate(iso: string) {
  return new Date(iso).toLocaleDateString('en-US', {
    month: 'short', day: 'numeric', year: 'numeric',
  });
}

// ── Network referral row (flat table) ─────────────────────────────────────────

function NetworkReferralRow({ item }: { item: NetworkReferralItem }) {
  const clientName     = [item.clientFirstName, item.clientLastName].filter(Boolean).join(' ') || '—';
  const providerDisplay = item.providerOrganizationName ?? item.providerName ?? '—';
  const lawFirm        = item.referrerName  ?? '—';
  const email          = item.referrerEmail ?? '—';

  return (
    <tr className="hover:bg-gray-50 transition-colors">
      <td className="px-4 py-3">
        <div className="text-sm font-medium text-gray-900">{clientName}</div>
        {item.caseNumber && (
          <div className="text-xs text-gray-400 mt-0.5">Case #{item.caseNumber}</div>
        )}
      </td>
      <td className="px-4 py-3">
        <div className="text-sm text-gray-800 max-w-[160px] truncate" title={lawFirm}>
          {lawFirm}
        </div>
      </td>
      <td className="px-4 py-3">
        <div className="text-sm text-gray-500 max-w-[180px] truncate" title={email}>
          {email}
        </div>
      </td>
      <td className="px-4 py-3">
        <div className="text-sm text-gray-800 max-w-[160px] truncate" title={providerDisplay}>
          {providerDisplay}
        </div>
      </td>
      <td className="px-4 py-3">
        <div className="text-sm text-gray-700 max-w-[140px] truncate" title={item.requestedService}>
          {item.requestedService}
        </div>
      </td>
      <td className="px-4 py-3">
        <StatusBadge status={item.status} />
      </td>
      <td className="px-4 py-3">
        <UrgencyBadge urgency={item.urgency} />
      </td>
      <td className="px-4 py-3">
        <span className="text-xs text-gray-400">{formatDate(item.createdAtUtc)}</span>
      </td>
      <td className="px-4 py-3 text-right">
        <Link
          href={`/careconnect/referrals/${item.id}`}
          className="text-sm text-primary hover:underline font-medium"
        >
          View →
        </Link>
      </td>
    </tr>
  );
}

// ── Network referrals view ─────────────────────────────────────────────────────

const ALL_STATUSES = ['New', 'Accepted', 'InProgress', 'Completed', 'Declined', 'Cancelled'];

async function NetworkReferralsView({
  status,
  search,
}: {
  status?: string;
  search?: string;
}) {
  let data = null;
  let fetchError: string | null = null;

  try {
    data = await careConnectServerApi.networkReferrals.list({
      page:     1,
      pageSize: 200,
      status:   status || undefined,
      search:   search || undefined,
    });
  } catch (err) {
    fetchError = err instanceof ServerApiError ? err.message : 'Failed to load network referrals.';
  }

  const hasMore = data ? data.total > data.items.length : false;

  return (
    <div className="space-y-4">
      {/* Status filter pills */}
      <div className="flex flex-wrap gap-2">
        <Link
          href="/careconnect/referrals"
          className={`px-3 py-1.5 rounded-full text-xs font-medium border transition-colors ${
            !status
              ? 'bg-primary text-white border-primary'
              : 'bg-white text-gray-600 border-gray-200 hover:border-gray-400'
          }`}
        >
          All
        </Link>
        {ALL_STATUSES.map((s) => (
          <Link
            key={s}
            href={`/careconnect/referrals?status=${s}${search ? `&search=${encodeURIComponent(search)}` : ''}`}
            className={`px-3 py-1.5 rounded-full text-xs font-medium border transition-colors ${
              status === s
                ? 'bg-primary text-white border-primary'
                : 'bg-white text-gray-600 border-gray-200 hover:border-gray-400'
            }`}
          >
            {s}
          </Link>
        ))}
      </div>

      {/* Error */}
      {fetchError && (
        <div className="bg-red-50 border border-red-200 rounded-lg px-4 py-3 text-sm text-red-700">
          Unable to load network referrals. {fetchError}
        </div>
      )}

      {/* Summary line */}
      {data && (
        <p className="text-xs text-gray-400">
          {data.total === 0
            ? (status || search ? 'No referrals match your filters.' : 'No referrals have been sent through your network yet.')
            : `${data.total} referral${data.total !== 1 ? 's' : ''}${status ? ` · Status: ${status}` : ''}`}
        </p>
      )}

      {/* Flat table */}
      {data && data.items.length > 0 && (
        <div className="bg-white rounded-xl border border-gray-200 overflow-hidden">
          <div className="overflow-x-auto">
            <table className="w-full text-left">
              <thead>
                <tr className="border-b border-gray-100 bg-gray-50">
                  <th className="px-4 py-3 text-xs font-semibold text-gray-400 uppercase tracking-wide">Client</th>
                  <th className="px-4 py-3 text-xs font-semibold text-gray-400 uppercase tracking-wide">Law Firm</th>
                  <th className="px-4 py-3 text-xs font-semibold text-gray-400 uppercase tracking-wide">Email</th>
                  <th className="px-4 py-3 text-xs font-semibold text-gray-400 uppercase tracking-wide">Provider</th>
                  <th className="px-4 py-3 text-xs font-semibold text-gray-400 uppercase tracking-wide">Service</th>
                  <th className="px-4 py-3 text-xs font-semibold text-gray-400 uppercase tracking-wide">Status</th>
                  <th className="px-4 py-3 text-xs font-semibold text-gray-400 uppercase tracking-wide">Urgency</th>
                  <th className="px-4 py-3 text-xs font-semibold text-gray-400 uppercase tracking-wide">Date</th>
                  <th className="px-4 py-3" />
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-50">
                {data.items.map((item) => (
                  <NetworkReferralRow key={item.id} item={item} />
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* Empty state */}
      {data && data.items.length === 0 && !fetchError && (
        <div className="bg-white rounded-xl border border-gray-200 p-12 text-center">
          <div className="w-14 h-14 bg-gray-50 rounded-full flex items-center justify-center mx-auto mb-4">
            <svg className="w-7 h-7 text-gray-300" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5}
                d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
            </svg>
          </div>
          <h3 className="text-base font-semibold text-gray-900 mb-1">
            {status || search ? 'No matching referrals' : 'No referrals yet'}
          </h3>
          <p className="text-sm text-gray-500">
            {status || search
              ? 'Try clearing your filters to see all referrals.'
              : 'Referrals sent by law firms to providers in your network will appear here.'}
          </p>
          {(status || search) && (
            <Link
              href="/careconnect/referrals"
              className="mt-3 inline-block text-sm text-primary hover:underline"
            >
              Clear filters
            </Link>
          )}
        </div>
      )}

      {/* Pagination notice */}
      {hasMore && (
        <p className="text-xs text-gray-400 text-center">
          Showing first {data?.items.length} of {data?.total} referrals.
        </p>
      )}
    </div>
  );
}

// ── Page ───────────────────────────────────────────────────────────────────────

export default async function ReferralsPage({ searchParams }: ReferralsPageProps) {
  const searchParamsData = await searchParams;
  const session = await requireOrg();

  // LawFirm org type is intrinsically a referrer even if the explicit product role
  // hasn't been assigned yet — mirrors how LienOwner implies NetworkManager.
  const isReferrer        = session.productRoles.includes(ProductRole.CareConnectReferrer)
                          || session.orgType === OrgType.LawFirm;
  const isReceiver        = session.productRoles.includes(ProductRole.CareConnectReceiver);
  const isNetworkManager  = session.productRoles.includes(ProductRole.CareConnectNetworkManager)
                          || session.orgType === OrgType.LienOwner;

  // LSCC-01-002-02: Enforce the admin-controlled access model.
  if (!isReferrer && !isReceiver && !isNetworkManager) {
    const readiness = checkCareConnectReceiverAccess(session);
    return <ReferralAccessBlocked reason={readiness.reason} />;
  }

  // ── Network manager view ───────────────────────────────────────────────────
  // Lien companies with the network manager role see all referrals flowing through
  // their network, grouped by law firm. This takes priority over any referrer/receiver
  // role the account may also hold — lien companies do not send referrals themselves.

  if (isNetworkManager) {
    const searchText = searchParamsData.search?.trim() || undefined;

    return (
      <div className="space-y-5">
        {/* Header */}
        <div className="flex items-center justify-between">
          <div>
            <h1 className="text-xl font-semibold text-gray-900">Network Referrals</h1>
            <p className="text-sm text-gray-500 mt-0.5">
              All referrals from law firms to providers in your network.
            </p>
          </div>
        </div>

        {/* Search bar */}
        <Suspense fallback={null}>
          <ReferralQueueToolbar
            currentSearch={searchText ?? ''}
            currentStatus={searchParamsData.status ?? ''}
          />
        </Suspense>

        {/* Network grouped view */}
        <NetworkReferralsView
          status={searchParamsData.status || undefined}
          search={searchText}
        />

        <div className="pt-1">
          <Link href="/careconnect/dashboard" className="text-xs text-gray-400 hover:text-gray-600 transition-colors">
            ← Back to Dashboard
          </Link>
        </div>
      </div>
    );
  }

  // ── Standard referrer / receiver view ─────────────────────────────────────

  const page = Math.max(1, parseInt(searchParamsData.page ?? '1') || 1);

  const createdFrom = (searchParamsData.createdFrom && isValidIsoDate(searchParamsData.createdFrom))
    ? searchParamsData.createdFrom : undefined;
  const createdTo   = (searchParamsData.createdTo && isValidIsoDate(searchParamsData.createdTo))
    ? searchParamsData.createdTo : undefined;

  const searchText = searchParamsData.search?.trim() || undefined;

  let result = null;
  let fetchError: string | null = null;

  try {
    result = await careConnectServerApi.referrals.search({
      status:      searchParamsData.status     || undefined,
      urgency:     searchParamsData.urgency    || undefined,
      providerId:  searchParamsData.providerId || undefined,
      clientName:  searchText,
      createdFrom,
      createdTo,
      page,
      pageSize: 20,
    });
  } catch (err) {
    fetchError = err instanceof ServerApiError ? err.message : 'Failed to load referrals.';
  }

  const heading = isReferrer ? 'Sent Referrals' : 'Referral Inbox';

  const hasDateFilter = !!(createdFrom || createdTo);

  const qsParts: string[] = [];
  if (searchParamsData.status)    qsParts.push(`status=${encodeURIComponent(searchParamsData.status)}`);
  if (searchParamsData.search)    qsParts.push(`search=${encodeURIComponent(searchParamsData.search)}`);
  if (searchParamsData.createdFrom) qsParts.push(`createdFrom=${searchParamsData.createdFrom}`);
  if (searchParamsData.createdTo)   qsParts.push(`createdTo=${searchParamsData.createdTo}`);
  const currentQs = qsParts.join('&');

  return (
    <div className="space-y-4">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-semibold text-gray-900">{heading}</h1>
          <p className="text-sm text-gray-500 mt-0.5">
            {isReferrer ? 'Referrals you have sent to providers.' : 'Referrals waiting for your action.'}
          </p>
        </div>
        {isReferrer && (
          <Link
            href="/careconnect/providers"
            className="bg-primary text-white text-sm font-medium px-4 py-2 rounded-md hover:opacity-90 transition-opacity shrink-0"
          >
            + New Referral
          </Link>
        )}
      </div>

      {/* Active date filter indicator */}
      {hasDateFilter && (
        <div className="flex items-center gap-2 text-xs text-blue-700 bg-blue-50 border border-blue-100 rounded px-3 py-2">
          <span>&#x1F4C5;</span>
          <span>
            Filtered to{' '}
            {createdFrom ? formatDisplayDate(createdFrom) : 'start'}
            {' → '}
            {createdTo ? formatDisplayDate(createdTo) : 'today'}
          </span>
          <Link
            href="/careconnect/referrals"
            className="ml-2 text-blue-500 hover:text-blue-700 underline"
          >
            Clear
          </Link>
        </div>
      )}

      {/* Search + filter toolbar */}
      <Suspense fallback={null}>
        <ReferralQueueToolbar
          currentSearch={searchText ?? ''}
          currentStatus={searchParamsData.status ?? ''}
        />
      </Suspense>

      {/* Error */}
      {fetchError && (
        <div className="bg-red-50 border border-red-200 rounded-lg px-4 py-3 text-sm text-red-700">
          Unable to load referrals. {fetchError}
        </div>
      )}

      {/* Results summary when filtering */}
      {result && (searchText || searchParamsData.status) && (
        <p className="text-xs text-gray-400">
          {result.totalCount === 0
            ? 'No referrals match your filters.'
            : `${result.totalCount} referral${result.totalCount !== 1 ? 's' : ''} found`}
        </p>
      )}

      {/* Referral table */}
      {result && (
        <ReferralListTable
          referrals={result.items}
          totalCount={result.totalCount}
          page={result.page}
          pageSize={result.pageSize}
          isReferrer={isReferrer}
          isReceiver={isReceiver}
          orgId={session.orgId}
          currentQs={currentQs}
        />
      )}

      {/* Back to dashboard */}
      <div className="pt-1">
        <Link href="/careconnect/dashboard" className="text-xs text-gray-400 hover:text-gray-600 transition-colors">
          ← Back to Dashboard
        </Link>
      </div>
    </div>
  );
}
