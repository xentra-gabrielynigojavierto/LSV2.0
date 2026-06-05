import { requirePlatformAdmin }           from '@/lib/auth-guards';
import { CCShell }                        from '@/components/shell/cc-shell';
import { NotificationStatusBadge }       from '@/components/notifications/status-badge';
import { ChannelBadge }                   from '@/components/notifications/channel-badge';
import { notifClient, NOTIF_CACHE_TAGS } from '@/lib/notifications-api';
import type { NotifListResponse }        from '@/lib/notifications-api';

export const dynamic = 'force-dynamic';

const PAGE_SIZE = 25;

interface Props {
  searchParams: Promise<{ page?: string }>;
}

/**
 * /notifications/delivery-issues
 *
 * Shows all notifications in a failed or blocked state.
 * The backend exposes per-notification issues via /v1/notifications/:id/issues;
 * there is no cross-notification aggregate endpoint. This page queries the
 * notification log filtered by status=failed and status=blocked, then unions
 * them client-side to present a single delivery-issue view.
 */
export default async function DeliveryIssuesPage({ searchParams }: Props) {
  const searchParamsData = await searchParams;
  const session = await requirePlatformAdmin();

  const page = Math.max(1, parseInt(searchParamsData.page ?? '1', 10));

  let failedData:  NotifListResponse | null = null;
  let blockedData: NotifListResponse | null = null;
  let fetchError:  string | null            = null;

  try {
    [failedData, blockedData] = await Promise.all([
      notifClient.get<NotifListResponse>(
        `/notifications?status=failed&page=${page}&pageSize=${Math.ceil(PAGE_SIZE / 2)}`,
        10,
        [NOTIF_CACHE_TAGS.notifications],
      ),
      notifClient.get<NotifListResponse>(
        `/notifications?status=blocked&page=${page}&pageSize=${Math.ceil(PAGE_SIZE / 2)}`,
        10,
        [NOTIF_CACHE_TAGS.notifications],
      ),
    ]);
  } catch (err) {
    fetchError = err instanceof Error ? err.message : 'Failed to load delivery issues.';
  }

  const allItems = [
    ...(failedData?.data  ?? []),
    ...(blockedData?.data ?? []),
  ].sort((a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime());

  const failedCount  = failedData?.meta?.total  ?? 0;
  const blockedCount = blockedData?.meta?.total ?? 0;

  function buildPageUrl(p: number) {
    return `/notifications/delivery-issues?page=${p}`;
  }

  const totalCount = failedCount + blockedCount;

  return (
    <CCShell userEmail={session.email}>
      <div className="space-y-4">

        {/* Header */}
        <div className="flex items-start justify-between gap-4">
          <div>
            <h1 className="text-xl font-semibold text-gray-900">Delivery Issues</h1>
            <p className="text-sm text-gray-500 mt-0.5">
              Platform-wide failed and blocked notifications.
            </p>
          </div>
          <a
            href="/notifications/log"
            className="inline-flex items-center gap-1.5 h-9 px-3 text-sm font-medium text-gray-600 hover:text-gray-900 bg-white border border-gray-300 hover:border-gray-400 rounded-md transition-colors whitespace-nowrap"
          >
            <i className="ri-list-check-2 text-sm" />
            Full Log
          </a>
        </div>

        {/* Stat pills */}
        <div className="flex gap-3 flex-wrap">
          <div className="inline-flex items-center gap-2 px-3 py-1.5 rounded-lg border border-red-200 bg-red-50">
            <span className="text-xs font-semibold text-red-700">Failed</span>
            <span className="text-sm font-bold text-red-800">{failedCount.toLocaleString()}</span>
          </div>
          <div className="inline-flex items-center gap-2 px-3 py-1.5 rounded-lg border border-amber-200 bg-amber-50">
            <span className="text-xs font-semibold text-amber-700">Blocked</span>
            <span className="text-sm font-bold text-amber-800">{blockedCount.toLocaleString()}</span>
          </div>
          <div className="inline-flex items-center gap-2 px-3 py-1.5 rounded-lg border border-gray-200 bg-gray-50">
            <span className="text-xs font-semibold text-gray-600">Total</span>
            <span className="text-sm font-bold text-gray-800">{totalCount.toLocaleString()}</span>
          </div>
        </div>

        {/* Note about per-notification issues */}
        <div className="rounded-lg border border-blue-200 bg-blue-50 px-4 py-2.5 text-xs text-blue-700">
          <i className="ri-information-line mr-1.5" />
          Per-notification issue detail (events, error codes) is available on each notification&apos;s detail page.
          Click any ID to inspect.
        </div>

        {fetchError && (
          <div className="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">{fetchError}</div>
        )}

        {!fetchError && (
          <div className="rounded-lg border border-gray-200 bg-white overflow-hidden">
            <table className="min-w-full divide-y divide-gray-100 text-sm">
              <thead className="bg-gray-50 text-xs text-gray-500 uppercase tracking-wide">
                <tr>
                  <th className="px-4 py-2.5 text-left font-medium">ID</th>
                  <th className="px-4 py-2.5 text-left font-medium">Channel</th>
                  <th className="px-4 py-2.5 text-left font-medium">Status</th>
                  <th className="px-4 py-2.5 text-left font-medium">Provider</th>
                  <th className="px-4 py-2.5 text-left font-medium">Failure</th>
                  <th className="px-4 py-2.5 text-left font-medium">Error</th>
                  <th className="px-4 py-2.5 text-left font-medium">Blocked Reason</th>
                  <th className="px-4 py-2.5 text-left font-medium">Created (UTC)</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {allItems.map(n => (
                  <tr key={n.id} className="hover:bg-gray-50">
                    <td className="px-4 py-2.5 font-mono text-[11px] text-gray-500 whitespace-nowrap">
                      <a href={`/notifications/log/${n.id}`} className="hover:text-indigo-600 hover:underline">
                        {n.id.slice(0, 8)}…
                      </a>
                    </td>
                    <td className="px-4 py-2.5"><ChannelBadge channel={n.channel} /></td>
                    <td className="px-4 py-2.5"><NotificationStatusBadge status={n.status} /></td>
                    <td className="px-4 py-2.5 text-xs text-gray-600">
                      {n.providerUsed ?? <span className="text-gray-400 italic">—</span>}
                    </td>
                    <td className="px-4 py-2.5 text-xs text-red-600 max-w-[120px] truncate">
                      {n.failureCategory?.replace(/_/g, ' ') ?? <span className="text-gray-400 italic">—</span>}
                    </td>
                    <td className="px-4 py-2.5 text-xs text-red-700 max-w-[160px] truncate">
                      {n.lastErrorMessage ?? <span className="text-gray-400 italic">—</span>}
                    </td>
                    <td className="px-4 py-2.5 text-xs text-amber-700 max-w-[120px] truncate">
                      {n.blockedReasonCode ?? <span className="text-gray-400 italic">—</span>}
                    </td>
                    <td className="px-4 py-2.5 font-mono text-[11px] text-gray-500 whitespace-nowrap">
                      {new Date(n.createdAt).toLocaleString('en-US', { timeZone: 'UTC', hour12: false })}
                    </td>
                  </tr>
                ))}
                {allItems.length === 0 && (
                  <tr>
                    <td colSpan={8} className="px-4 py-10 text-center text-sm text-gray-400">
                      No delivery issues — no failed or blocked notifications found.
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
        )}

        {/* Pagination */}
        {totalCount > PAGE_SIZE && (
          <div className="flex items-center justify-between text-sm">
            <span className="text-gray-500">Page {page}</span>
            <div className="flex gap-2">
              {page > 1 && (
                <a href={buildPageUrl(page - 1)} className="px-3 py-1.5 rounded-md border border-gray-300 bg-white text-gray-600 hover:bg-gray-50 text-xs font-medium">
                  ← Previous
                </a>
              )}
              <a href={buildPageUrl(page + 1)} className="px-3 py-1.5 rounded-md border border-gray-300 bg-white text-gray-600 hover:bg-gray-50 text-xs font-medium">
                Next →
              </a>
            </div>
          </div>
        )}

      </div>
    </CCShell>
  );
}
