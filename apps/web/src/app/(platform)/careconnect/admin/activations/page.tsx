/**
 * LSCC-009: Admin Activation Queue.
 * Route: /careconnect/admin/activations
 *
 * Shows all pending provider activation requests submitted via the LSCC-008
 * activation funnel. Admin-only (TenantAdmin or PlatformAdmin).
 *
 * Default view: Pending requests only, newest first.
 */

import Link from 'next/link';
import { requireAdmin } from '@/lib/auth-guards';
import { careConnectServerApi } from '@/lib/careconnect-server-api';
import { ServerApiError } from '@/lib/server-api-client';
import type { ActivationRequestSummary } from '@/types/careconnect';

export const dynamic = 'force-dynamic';


function formatDate(iso: string) {
  return new Date(iso).toLocaleDateString('en-US', {
    year: 'numeric', month: 'short', day: 'numeric',
  });
}

function EmptyState() {
  return (
    <div className="bg-white rounded-xl border border-gray-200 p-12 text-center">
      <div className="w-14 h-14 bg-green-100 rounded-full flex items-center justify-center mx-auto mb-4">
        <svg className="w-7 h-7 text-green-600" fill="none" viewBox="0 0 24 24" stroke="currentColor">
          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 13l4 4L19 7" />
        </svg>
      </div>
      <h3 className="text-base font-semibold text-gray-900 mb-1">No pending activation requests</h3>
      <p className="text-sm text-gray-500">
        All activation requests have been processed. New requests will appear here when providers
        submit the activation form.
      </p>
    </div>
  );
}

function QueueRow({ item }: { item: ActivationRequestSummary }) {
  return (
    <tr className="hover:bg-gray-50 transition-colors">
      <td className="px-4 py-3">
        <div className="text-sm font-medium text-gray-900">{item.providerName}</div>
        <div className="text-xs text-gray-500">{item.providerEmail}</div>
      </td>
      <td className="px-4 py-3">
        {item.requesterName || item.requesterEmail ? (
          <div>
            {item.requesterName && <div className="text-sm text-gray-900">{item.requesterName}</div>}
            {item.requesterEmail && <div className="text-xs text-gray-500">{item.requesterEmail}</div>}
          </div>
        ) : (
          <span className="text-xs text-gray-400">—</span>
        )}
      </td>
      <td className="px-4 py-3">
        {item.clientName ? (
          <div>
            <div className="text-sm text-gray-900">{item.clientName}</div>
            {item.referringFirmName && (
              <div className="text-xs text-gray-500">{item.referringFirmName}</div>
            )}
          </div>
        ) : (
          <span className="text-xs text-gray-400">—</span>
        )}
      </td>
      <td className="px-4 py-3">
        {item.requestedService ? (
          <span className="text-sm text-gray-700">{item.requestedService}</span>
        ) : (
          <span className="text-xs text-gray-400">—</span>
        )}
      </td>
      <td className="px-4 py-3">
        <span className="text-sm text-gray-700">{formatDate(item.createdAtUtc)}</span>
      </td>
      <td className="px-4 py-3">
        <span className="inline-flex items-center px-2 py-0.5 rounded text-xs font-medium bg-yellow-100 text-yellow-800">
          Pending
        </span>
      </td>
      <td className="px-4 py-3 text-right">
        <Link
          href={`/careconnect/admin/activations/${item.id}`}
          className="text-sm text-primary hover:underline font-medium"
        >
          Review →
        </Link>
      </td>
    </tr>
  );
}

export default async function ActivationQueuePage() {
  await requireAdmin();

  let items: ActivationRequestSummary[] = [];
  let errorMessage: string | null = null;

  try {
    const result = await careConnectServerApi.adminActivations.getPending();
    items = result.items;
  } catch (err) {
    if (err instanceof ServerApiError) {
      errorMessage = `Failed to load activation queue (${err.status}).`;
    } else {
      errorMessage = 'Failed to load activation queue. Please try again.';
    }
  }

  return (
    <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
      {/* Header */}
      <div className="mb-6 flex items-center justify-between">
        <div>
          <h1 className="text-xl font-semibold text-gray-900">Provider Activation Queue</h1>
          <p className="text-sm text-gray-500 mt-0.5">
            Pending provider activation requests — review and approve to activate provider accounts
          </p>
        </div>
        <div className="text-sm text-gray-500">
          {items.length > 0 && `${items.length} pending`}
        </div>
      </div>

      {/* Error */}
      {errorMessage && (
        <div className="bg-red-50 border border-red-200 rounded-lg px-4 py-3 text-sm text-red-700 mb-6">
          {errorMessage}
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
                  <th className="px-4 py-3 text-xs font-semibold text-gray-500 uppercase tracking-wide">Provider</th>
                  <th className="px-4 py-3 text-xs font-semibold text-gray-500 uppercase tracking-wide">Requester</th>
                  <th className="px-4 py-3 text-xs font-semibold text-gray-500 uppercase tracking-wide">Referral context</th>
                  <th className="px-4 py-3 text-xs font-semibold text-gray-500 uppercase tracking-wide">Service</th>
                  <th className="px-4 py-3 text-xs font-semibold text-gray-500 uppercase tracking-wide">Requested</th>
                  <th className="px-4 py-3 text-xs font-semibold text-gray-500 uppercase tracking-wide">Status</th>
                  <th className="px-4 py-3" />
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {items.map((item) => (
                  <QueueRow key={item.id} item={item} />
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}
    </div>
  );
}
