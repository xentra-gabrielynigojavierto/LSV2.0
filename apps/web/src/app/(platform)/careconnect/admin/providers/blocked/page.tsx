/**
 * LSCC-01-004: Blocked Provider Queue.
 * Route: /careconnect/admin/providers/blocked
 *
 * Server component — admin only (PlatformAdmin or TenantAdmin).
 * Lists providers (grouped by userId + failureReason) whose access-readiness
 * check failed within the last 7 days. Each row links to the provisioning
 * page so the admin can remediate immediately.
 */

import Link from 'next/link';
import { requireAdmin } from '@/lib/auth-guards';
import { careConnectServerApi } from '@/lib/careconnect-server-api';
import { ServerApiError } from '@/lib/server-api-client';
import type { BlockedProviderLogItem, BlockedProviderLogPage } from '@/types/careconnect';

export const dynamic = 'force-dynamic';


function formatTs(iso: string) {
  return new Date(iso).toLocaleString('en-US', {
    month: 'short', day: 'numeric',
    hour: 'numeric', minute: '2-digit', hour12: true,
  });
}

function reasonBadge(reason: string) {
  const cls = reason.includes('role') || reason.includes('provision')
    ? 'bg-amber-100 text-amber-800'
    : 'bg-red-100 text-red-800';
  return (
    <span className={`inline-flex items-center px-2 py-0.5 rounded text-xs font-medium ${cls}`}>
      {reason}
    </span>
  );
}

function EmptyState() {
  return (
    <div className="bg-white rounded-xl border border-gray-200 p-12 text-center">
      <div className="w-14 h-14 bg-green-100 rounded-full flex items-center justify-center mx-auto mb-4">
        <svg className="w-7 h-7 text-green-600" fill="none" viewBox="0 0 24 24" stroke="currentColor">
          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 13l4 4L19 7" />
        </svg>
      </div>
      <h3 className="text-base font-semibold text-gray-900 mb-1">No blocked providers in the last 7 days</h3>
      <p className="text-sm text-gray-500">
        All providers are currently provisioned and able to access CareConnect.
      </p>
    </div>
  );
}

function BlockedRow({ item }: { item: BlockedProviderLogItem }) {
  return (
    <tr className="hover:bg-gray-50 transition-colors">
      <td className="px-4 py-3">
        {item.userEmail ? (
          <div>
            <div className="text-sm font-medium text-gray-900">{item.userEmail}</div>
            {item.userId && (
              <div className="text-xs text-gray-400 font-mono mt-0.5">{item.userId}</div>
            )}
          </div>
        ) : (
          <span className="text-xs text-gray-400 italic">Unknown user</span>
        )}
      </td>
      <td className="px-4 py-3">{reasonBadge(item.failureReason)}</td>
      <td className="px-4 py-3">
        <span className="text-sm font-semibold text-gray-900">{item.attemptCount}</span>
      </td>
      <td className="px-4 py-3">
        <span className="text-sm text-gray-700">{formatTs(item.lastAttemptUtc)}</span>
      </td>
      <td className="px-4 py-3 text-right">
        {item.remediationPath ? (
          <Link
            href={item.remediationPath}
            className="inline-flex items-center gap-1 text-sm text-primary hover:underline font-medium"
          >
            Remediate →
          </Link>
        ) : (
          <span className="text-xs text-gray-400">—</span>
        )}
      </td>
    </tr>
  );
}

export default async function BlockedProviderQueuePage() {
  await requireAdmin();

  let data: BlockedProviderLogPage | null = null;
  let errorMessage: string | null = null;

  try {
    data = await careConnectServerApi.adminDashboard.getBlockedProviders({ page: 1, pageSize: 50 });
  } catch (err) {
    if (err instanceof ServerApiError) {
      errorMessage = `Failed to load blocked-provider queue (HTTP ${err.status}).`;
    } else {
      errorMessage = 'Failed to load blocked-provider queue. Please try again.';
    }
  }

  const items = data?.items ?? [];

  return (
    <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
      {/* Header */}
      <div className="mb-6 flex items-center justify-between">
        <div>
          <h1 className="text-xl font-semibold text-gray-900">Blocked Provider Queue</h1>
          <p className="text-sm text-gray-500 mt-0.5">
            Providers whose access-readiness check failed in the last 7 days
          </p>
        </div>
        <div className="flex items-center gap-3">
          {data && data.total > 0 && (
            <span className="text-sm font-medium text-amber-700 bg-amber-50 border border-amber-200 rounded-full px-3 py-0.5">
              {data.total} blocked
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

      {/* Error */}
      {errorMessage && (
        <div className="bg-red-50 border border-red-200 rounded-lg px-4 py-3 text-sm text-red-700 mb-6">
          {errorMessage}
        </div>
      )}

      {/* Banner when providers are blocked */}
      {!errorMessage && items.length > 0 && (
        <div className="bg-amber-50 border border-amber-200 rounded-lg px-4 py-3 text-sm text-amber-800 mb-6 flex items-start gap-2">
          <svg className="w-4 h-4 mt-0.5 flex-shrink-0" fill="currentColor" viewBox="0 0 20 20">
            <path fillRule="evenodd" d="M8.257 3.099c.765-1.36 2.722-1.36 3.486 0l5.58 9.92c.75 1.334-.213 2.98-1.742 2.98H4.42c-1.53 0-2.493-1.646-1.743-2.98l5.58-9.92zM11 13a1 1 0 11-2 0 1 1 0 012 0zm-1-8a1 1 0 00-1 1v3a1 1 0 002 0V6a1 1 0 00-1-1z" clipRule="evenodd" />
          </svg>
          <span>
            {items.length} provider{items.length !== 1 ? 's are' : ' is'} blocked from CareConnect.
            Click <strong>Remediate</strong> to open the provisioning page for that user.
          </span>
        </div>
      )}

      {/* Empty state */}
      {!errorMessage && items.length === 0 && <EmptyState />}

      {/* Queue table */}
      {items.length > 0 && (
        <div className="bg-white rounded-xl border border-gray-200 overflow-hidden">
          <div className="overflow-x-auto">
            <table className="w-full text-left">
              <thead>
                <tr className="border-b border-gray-200 bg-gray-50">
                  <th className="px-4 py-3 text-xs font-semibold text-gray-500 uppercase tracking-wide">User</th>
                  <th className="px-4 py-3 text-xs font-semibold text-gray-500 uppercase tracking-wide">Reason</th>
                  <th className="px-4 py-3 text-xs font-semibold text-gray-500 uppercase tracking-wide">Attempts (7d)</th>
                  <th className="px-4 py-3 text-xs font-semibold text-gray-500 uppercase tracking-wide">Last attempt</th>
                  <th className="px-4 py-3" />
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {items.map((item, idx) => (
                  <BlockedRow key={`${item.userId ?? 'anon'}-${idx}`} item={item} />
                ))}
              </tbody>
            </table>
          </div>
          {data && data.total > items.length && (
            <div className="border-t border-gray-100 px-4 py-3 text-sm text-gray-500 text-center">
              Showing {items.length} of {data.total} blocked entries
            </div>
          )}
        </div>
      )}
    </div>
  );
}
