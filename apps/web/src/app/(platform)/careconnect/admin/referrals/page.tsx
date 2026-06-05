/**
 * LSCC-01-004: Admin Referral Monitor.
 * Route: /careconnect/admin/referrals[?status=<str>&tenantId=<guid>]
 *
 * Server component — admin only (PlatformAdmin or TenantAdmin).
 * Cross-tenant view of all referrals with status, provider, and referrer
 * context.  Supports ?status filter via Next.js searchParams.
 */

import Link from 'next/link';
import { requireAdmin } from '@/lib/auth-guards';
import { careConnectServerApi } from '@/lib/careconnect-server-api';
import { ServerApiError } from '@/lib/server-api-client';
import type { AdminReferralItem, AdminReferralPage } from '@/types/careconnect';

export const dynamic = 'force-dynamic';


// Referral.ValidStatuses
const ALL_STATUSES = ['New', 'Accepted', 'InProgress', 'Completed', 'Declined', 'Cancelled'];

const STATUS_BADGE: Record<string, string> = {
  New:        'bg-blue-100 text-blue-800',
  Accepted:   'bg-indigo-100 text-indigo-800',
  InProgress: 'bg-yellow-100 text-yellow-800',
  Completed:  'bg-green-100 text-green-800',
  Declined:   'bg-red-100 text-red-800',
  Cancelled:  'bg-gray-100 text-gray-600',
};

const URGENCY_BADGE: Record<string, string> = {
  High:   'bg-red-100 text-red-800',
  Medium: 'bg-yellow-100 text-yellow-800',
  Low:    'bg-gray-100 text-gray-600',
};

function formatDate(iso: string) {
  return new Date(iso).toLocaleDateString('en-US', {
    month: 'short', day: 'numeric', year: 'numeric',
  });
}

function StatusBadge({ status }: { status: string }) {
  const cls = STATUS_BADGE[status] ?? 'bg-gray-100 text-gray-700';
  return (
    <span className={`inline-flex items-center px-2 py-0.5 rounded text-xs font-medium ${cls}`}>
      {status}
    </span>
  );
}

function UrgencyBadge({ urgency }: { urgency: string }) {
  const cls = URGENCY_BADGE[urgency] ?? 'bg-gray-100 text-gray-700';
  return (
    <span className={`inline-flex items-center px-2 py-0.5 rounded text-xs font-medium ${cls}`}>
      {urgency}
    </span>
  );
}

function EmptyState({ status }: { status?: string }) {
  return (
    <div className="bg-white rounded-xl border border-gray-200 p-12 text-center">
      <div className="w-14 h-14 bg-blue-50 rounded-full flex items-center justify-center mx-auto mb-4">
        <svg className="w-7 h-7 text-blue-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
        </svg>
      </div>
      <h3 className="text-base font-semibold text-gray-900 mb-1">
        {status ? `No ${status} referrals` : 'No referrals found'}
      </h3>
      <p className="text-sm text-gray-500">
        {status
          ? `There are currently no referrals with status "${status}".`
          : 'No referrals match the current filter.'}
      </p>
    </div>
  );
}

function ReferralRow({ item }: { item: AdminReferralItem }) {
  return (
    <tr className="hover:bg-gray-50 transition-colors">
      <td className="px-4 py-3">
        <div className="text-sm font-mono text-gray-400">{item.id.slice(0, 8)}…</div>
        <div className="text-xs text-gray-500 mt-0.5">{formatDate(item.createdAtUtc)}</div>
      </td>
      <td className="px-4 py-3">
        <StatusBadge status={item.status} />
      </td>
      <td className="px-4 py-3">
        <UrgencyBadge urgency={item.urgency} />
      </td>
      <td className="px-4 py-3">
        <div className="text-sm text-gray-900 max-w-[180px] truncate" title={item.requestedService}>
          {item.requestedService}
        </div>
      </td>
      <td className="px-4 py-3">
        {item.providerName ? (
          <div>
            <div className="text-sm text-gray-900">{item.providerName}</div>
            {item.providerEmail && (
              <div className="text-xs text-gray-500">{item.providerEmail}</div>
            )}
          </div>
        ) : (
          <span className="text-xs text-gray-400">—</span>
        )}
      </td>
      <td className="px-4 py-3">
        {item.referrerName || item.referrerEmail ? (
          <div>
            {item.referrerName && <div className="text-sm text-gray-900">{item.referrerName}</div>}
            {item.referrerEmail && <div className="text-xs text-gray-500">{item.referrerEmail}</div>}
          </div>
        ) : (
          <span className="text-xs text-gray-400">—</span>
        )}
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

interface PageProps {
  searchParams: Promise<{ status?: string; tenantId?: string }>;
}

export default async function AdminReferralMonitorPage({ searchParams }: PageProps) {
  await requireAdmin();

  const { status, tenantId } = await searchParams;

  let data: AdminReferralPage | null = null;
  let errorMessage: string | null = null;

  try {
    data = await careConnectServerApi.adminDashboard.getReferrals({
      page:     1,
      pageSize: 50,
      status:   status || undefined,
      tenantId: tenantId || undefined,
    });
  } catch (err) {
    if (err instanceof ServerApiError) {
      errorMessage = `Failed to load referrals (HTTP ${err.status}).`;
    } else {
      errorMessage = 'Failed to load referrals. Please try again.';
    }
  }

  const items = data?.items ?? [];

  return (
    <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
      {/* Header */}
      <div className="mb-6 flex items-center justify-between">
        <div>
          <h1 className="text-xl font-semibold text-gray-900">Referral Monitor</h1>
          <p className="text-sm text-gray-500 mt-0.5">
            Cross-tenant referral overview — all statuses, all tenants
          </p>
        </div>
        <div className="flex items-center gap-3">
          {data && (
            <span className="text-sm text-gray-500">
              {data.total.toLocaleString()} referral{data.total !== 1 ? 's' : ''}
            </span>
          )}
          <Link
            href="/careconnect/admin/dashboard"
            className="text-sm text-gray-500 hover:text-gray-700"
          >
            ← Dashboard
          </Link>
        </div>
      </div>

      {/* Status filter pills */}
      <div className="flex flex-wrap gap-2 mb-5">
        <Link
          href="/careconnect/admin/referrals"
          className={`px-3 py-1 rounded-full text-xs font-medium border transition-colors ${
            !status
              ? 'bg-gray-900 text-white border-gray-900'
              : 'bg-white text-gray-600 border-gray-200 hover:border-gray-400'
          }`}
        >
          All
        </Link>
        {ALL_STATUSES.map((s) => (
          <Link
            key={s}
            href={`/careconnect/admin/referrals?status=${s}`}
            className={`px-3 py-1 rounded-full text-xs font-medium border transition-colors ${
              status === s
                ? 'bg-gray-900 text-white border-gray-900'
                : 'bg-white text-gray-600 border-gray-200 hover:border-gray-400'
            }`}
          >
            {s}
          </Link>
        ))}
      </div>

      {/* Error */}
      {errorMessage && (
        <div className="bg-red-50 border border-red-200 rounded-lg px-4 py-3 text-sm text-red-700 mb-6">
          {errorMessage}
        </div>
      )}

      {/* Empty state */}
      {!errorMessage && items.length === 0 && <EmptyState status={status} />}

      {/* Referral table */}
      {items.length > 0 && (
        <div className="bg-white rounded-xl border border-gray-200 overflow-hidden">
          <div className="overflow-x-auto">
            <table className="w-full text-left">
              <thead>
                <tr className="border-b border-gray-200 bg-gray-50">
                  <th className="px-4 py-3 text-xs font-semibold text-gray-500 uppercase tracking-wide">Referral</th>
                  <th className="px-4 py-3 text-xs font-semibold text-gray-500 uppercase tracking-wide">Status</th>
                  <th className="px-4 py-3 text-xs font-semibold text-gray-500 uppercase tracking-wide">Urgency</th>
                  <th className="px-4 py-3 text-xs font-semibold text-gray-500 uppercase tracking-wide">Service</th>
                  <th className="px-4 py-3 text-xs font-semibold text-gray-500 uppercase tracking-wide">Provider</th>
                  <th className="px-4 py-3 text-xs font-semibold text-gray-500 uppercase tracking-wide">Referrer</th>
                  <th className="px-4 py-3" />
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {items.map((item) => (
                  <ReferralRow key={item.id} item={item} />
                ))}
              </tbody>
            </table>
          </div>
          {data && data.total > items.length && (
            <div className="border-t border-gray-100 px-4 py-3 text-sm text-gray-500 text-center">
              Showing {items.length} of {data.total.toLocaleString()} referrals
            </div>
          )}
        </div>
      )}
    </div>
  );
}
