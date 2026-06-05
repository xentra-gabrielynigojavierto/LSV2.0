import { requirePlatformAdmin }              from '@/lib/auth-guards';
import { CCShell }                           from '@/components/shell/cc-shell';
import { NotificationStatusBadge }          from '@/components/notifications/status-badge';
import { ChannelBadge }                      from '@/components/notifications/channel-badge';
import { notifClient, NOTIF_CACHE_TAGS, formatFailureCategory } from '@/lib/notifications-api';
import type { AdminNotifListResponse }     from '@/lib/notifications-api';
import { getCachedTenantById }             from '@/lib/tenant-fetch';

export const dynamic = 'force-dynamic';

interface Props {
  searchParams: Promise<{
    status?:  string;
    channel?: string;
    page?:    string;
    q?:       string;
  }>;
}

const PAGE_SIZE = 25;

function parseRecipient(recipientJson: string): string {
  try {
    const r = JSON.parse(recipientJson);
    return r.email ?? r.phone ?? r.address ?? '—';
  } catch {
    return '—';
  }
}

function isTestSend(metadataJson: string | null): boolean {
  try {
    if (!metadataJson) return false;
    const m = JSON.parse(metadataJson);
    return m?.testSend === true;
  } catch {
    return false;
  }
}

export default async function NotificationsLogPage({ searchParams }: Props) {
  const session = await requirePlatformAdmin();

  const sp      = await searchParams;
  const status  = sp.status  ?? '';
  const channel = sp.channel ?? '';
  const page    = Math.max(1, parseInt(sp.page ?? '1', 10));

  const qs = new URLSearchParams();
  qs.set('page',     String(page));
  qs.set('pageSize', String(PAGE_SIZE));
  if (status)  qs.set('status',  status);
  if (channel) qs.set('channel', channel);

  let data:       AdminNotifListResponse | null = null;
  let fetchError: string | null                 = null;

  try {
    data = await notifClient.get<AdminNotifListResponse>(
      `/admin/notifications?${qs.toString()}`,
      0,
      [NOTIF_CACHE_TAGS.notifications],
    );
  } catch (err) {
    fetchError = err instanceof Error ? err.message : 'Failed to load notifications.';
  }

  const items      = data?.items      ?? [];
  const totalCount = data?.totalCount ?? 0;
  const totalPages = Math.max(1, Math.ceil(totalCount / PAGE_SIZE));

  // Resolve tenant codes for every unique tenantId on this page in parallel.
  // getCachedTenantById is memoised per render so duplicates cost nothing.
  const uniqueTenantIds = [...new Set(items.map(n => n.tenantId).filter(Boolean))];
  const tenantCodeMap = new Map<string, string>();
  await Promise.all(
    uniqueTenantIds.map(async (id) => {
      try {
        const t = await getCachedTenantById(id);
        tenantCodeMap.set(id, t?.code ?? id.slice(0, 8));
      } catch {
        tenantCodeMap.set(id, id.slice(0, 8));
      }
    }),
  );

  const statusOptions  = ['', 'accepted', 'processing', 'sent', 'failed', 'blocked'];
  const channelOptions = ['', 'email', 'sms', 'push', 'in-app'];

  function buildPageUrl(p: number) {
    const q = new URLSearchParams();
    q.set('page', String(p));
    if (status)  q.set('status',  status);
    if (channel) q.set('channel', channel);
    return `/notifications/log?${q.toString()}`;
  }

  function buildFilterUrl(key: string, val: string) {
    const q = new URLSearchParams();
    q.set('page', '1');
    if (key === 'status')  { if (val) q.set('status',  val); if (channel) q.set('channel', channel); }
    if (key === 'channel') { if (val) q.set('channel', val); if (status)  q.set('status',  status);  }
    return `/notifications/log?${q.toString()}`;
  }

  return (
    <CCShell userEmail={session.email}>
      <div className="space-y-4">

        {/* Header */}
        <div className="flex items-start justify-between gap-4">
          <div>
            <h1 className="text-xl font-semibold text-gray-900">Delivery Log</h1>
            <p className="text-sm text-gray-500 mt-0.5">
              {totalCount.toLocaleString()} record{totalCount !== 1 ? 's' : ''} platform-wide
              {(status || channel) && <span className="ml-1 text-indigo-600">(filtered)</span>}
            </p>
          </div>
        </div>

        {/* Filter bar */}
        <div className="flex flex-wrap gap-4 items-start bg-white border border-gray-200 rounded-lg px-4 py-3">
          <div className="flex items-center gap-2 flex-wrap">
            <label className="text-xs font-medium text-gray-600 whitespace-nowrap">Status</label>
            <div className="flex gap-1 flex-wrap">
              {statusOptions.map(s => (
                <a
                  key={s || '__all'}
                  href={buildFilterUrl('status', s)}
                  className={`px-2.5 py-1 rounded-md text-xs font-medium border transition-colors ${
                    status === s
                      ? 'bg-indigo-600 text-white border-indigo-600'
                      : 'bg-white text-gray-600 border-gray-300 hover:border-gray-400'
                  }`}
                >
                  {s || 'All'}
                </a>
              ))}
            </div>
          </div>
          <div className="flex items-center gap-2 flex-wrap">
            <label className="text-xs font-medium text-gray-600 whitespace-nowrap">Channel</label>
            <div className="flex gap-1 flex-wrap">
              {channelOptions.map(c => (
                <a
                  key={c || '__all'}
                  href={buildFilterUrl('channel', c)}
                  className={`px-2.5 py-1 rounded-md text-xs font-medium border transition-colors ${
                    channel === c
                      ? 'bg-indigo-600 text-white border-indigo-600'
                      : 'bg-white text-gray-600 border-gray-300 hover:border-gray-400'
                  }`}
                >
                  {c || 'All'}
                </a>
              ))}
            </div>
          </div>
        </div>

        {/* Error */}
        {fetchError && (
          <div className="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
            {fetchError}
          </div>
        )}

        {/* Table */}
        {!fetchError && (
          <div className="rounded-lg border border-gray-200 bg-white overflow-hidden">
            <div className="overflow-x-auto">
              <table className="min-w-full divide-y divide-gray-100 text-sm">
                <thead className="bg-gray-50 text-xs text-gray-500 uppercase tracking-wide">
                  <tr>
                    <th className="px-4 py-2.5 text-left font-medium">ID</th>
                    <th className="px-4 py-2.5 text-left font-medium">Tenant</th>
                    <th className="px-4 py-2.5 text-left font-medium">Channel</th>
                    <th className="px-4 py-2.5 text-left font-medium">Recipient</th>
                    <th className="px-4 py-2.5 text-left font-medium">Subject / Template</th>
                    <th className="px-4 py-2.5 text-left font-medium">Status</th>
                    <th className="px-4 py-2.5 text-left font-medium">Provider</th>
                    <th className="px-4 py-2.5 text-left font-medium">Failure</th>
                    <th className="px-4 py-2.5 text-left font-medium whitespace-nowrap">Created (UTC)</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-100">
                  {items.map(n => {
                    const recipient = parseRecipient(n.recipientJson);
                    const subject   = n.renderedSubject ?? n.templateKey ?? null;
                    const testSend  = isTestSend(n.metadataJson);
                    const testTitle = testSend && n.status === 'accepted'
                      ? 'Test send: submitted to provider. Actual delivery outcome (blocks, bounces) arrives asynchronously — check your provider\'s activity dashboard for the final status.'
                      : undefined;
                    return (
                      <tr key={n.id} className="hover:bg-gray-50">
                        <td className="px-4 py-2.5 font-mono text-[11px] text-gray-500 whitespace-nowrap">
                          <a href={`/notifications/log/${n.id}`} className="hover:text-indigo-600 hover:underline">
                            {n.id.slice(0, 8)}…
                          </a>
                        </td>
                        <td className="px-4 py-2.5 font-mono text-[11px] whitespace-nowrap">
                          <a href={`/tenants/${n.tenantId}`} className="text-indigo-600 hover:underline" title={n.tenantId}>
                            {tenantCodeMap.get(n.tenantId) ?? n.tenantId.slice(0, 8)}
                          </a>
                        </td>
                        <td className="px-4 py-2.5"><ChannelBadge channel={n.channel} /></td>
                        <td className="px-4 py-2.5 font-mono text-[11px] text-gray-700 max-w-[180px] truncate" title={recipient}>
                          {recipient}
                        </td>
                        <td className="px-4 py-2.5 text-xs text-gray-700 max-w-[200px]">
                          {subject ? (
                            <span className="truncate block" title={subject}>
                              {n.renderedSubject
                                ? subject
                                : <span className="font-mono text-gray-500">{subject}</span>
                              }
                            </span>
                          ) : (
                            <span className="text-gray-400 italic">—</span>
                          )}
                        </td>
                        <td className="px-4 py-2.5 whitespace-nowrap">
                          <div className="flex items-center gap-1.5" title={testTitle}>
                            <NotificationStatusBadge status={n.status} />
                            {testSend && (
                              <span className="inline-flex items-center gap-0.5 px-1.5 py-0.5 rounded text-[10px] font-medium bg-gray-100 text-gray-500 border border-gray-200">
                                Test
                              </span>
                            )}
                          </div>
                        </td>
                        <td className="px-4 py-2.5 text-xs text-gray-600 whitespace-nowrap">
                          {n.providerUsed ?? <span className="text-gray-400 italic">—</span>}
                        </td>
                        <td className="px-4 py-2.5 text-xs text-red-600 max-w-[180px]">
                          {n.failureCategory
                            ? formatFailureCategory(n.failureCategory)
                            : (n.lastErrorMessage
                              ? <span className="truncate block" title={n.lastErrorMessage}>{n.lastErrorMessage}</span>
                              : <span className="text-gray-400 italic">—</span>
                            )}
                        </td>
                        <td className="px-4 py-2.5 font-mono text-[11px] text-gray-500 whitespace-nowrap">
                          {new Date(n.createdAt).toLocaleString('en-US', { timeZone: 'UTC', hour12: false })}
                        </td>
                      </tr>
                    );
                  })}
                  {items.length === 0 && (
                    <tr>
                      <td colSpan={9} className="px-4 py-10 text-center text-sm text-gray-400">
                        No notifications match the current filters.
                      </td>
                    </tr>
                  )}
                </tbody>
              </table>
            </div>
          </div>
        )}

        {/* Pagination */}
        {totalPages > 1 && (
          <div className="flex items-center justify-between text-sm">
            <span className="text-gray-500">
              Page {page} of {totalPages} · {totalCount.toLocaleString()} total
            </span>
            <div className="flex gap-2">
              {page > 1 && (
                <a href={buildPageUrl(page - 1)} className="px-3 py-1.5 rounded-md border border-gray-300 bg-white text-gray-600 hover:bg-gray-50 text-xs font-medium">
                  ← Previous
                </a>
              )}
              {page < totalPages && (
                <a href={buildPageUrl(page + 1)} className="px-3 py-1.5 rounded-md border border-gray-300 bg-white text-gray-600 hover:bg-gray-50 text-xs font-medium">
                  Next →
                </a>
              )}
            </div>
          </div>
        )}

      </div>
    </CCShell>
  );
}
