import Link                              from 'next/link';
import { requirePlatformAdmin }          from '@/lib/auth-guards';
import { Routes }                        from '@/lib/routes';
import { notifClient, NOTIF_CACHE_TAGS } from '@/lib/notifications-api';
import type { NotifListResponse, NotifStats } from '@/lib/notifications-api';
import { NotificationStatusBadge }       from '@/components/notifications/status-badge';
import { ChannelBadge }                  from '@/components/notifications/channel-badge';

export const dynamic = 'force-dynamic';

interface Props {
  params:       Promise<{ id: string }>;
  searchParams: Promise<{
    status?:  string;
    channel?: string;
    page?:    string;
  }>;
}

const PAGE_SIZE = 20;

const STATUS_OPTIONS  = ['', 'accepted', 'processing', 'sent', 'failed', 'blocked'];
const CHANNEL_OPTIONS = ['', 'email', 'sms', 'push', 'in-app'];

function parseRecipient(j: string): string {
  try { const r = JSON.parse(j); return r.email ?? r.phone ?? r.address ?? '—'; }
  catch { return '—'; }
}

/**
 * /tenants/[id]/notifications — Notifications tab body.
 *
 * Shows notification stats and the delivery log scoped to this tenant only.
 * User activity (audit events) lives on the dedicated User Activity tab.
 *
 * The shared header, breadcrumb, and sub-nav tabs are rendered by layout.tsx.
 * Access: PlatformAdmin only (enforced by layout + requirePlatformAdmin below).
 */
export default async function TenantNotificationsPage({ params, searchParams }: Props) {
  const searchParamsData = await searchParams;
  await requirePlatformAdmin();

  const { id } = await params;
  const status  = searchParamsData.status  ?? '';
  const channel = searchParamsData.channel ?? '';
  const page    = Math.max(1, parseInt(searchParamsData.page ?? '1', 10));

  // ── Fetch notification stats ───────────────────────────────────────────────
  let stats:    NotifStats | null = null;
  let statsErr: string | null = null;
  try {
    stats = await notifClient
      .get<{ data: NotifStats }>(`/notifications/stats?tenantId=${encodeURIComponent(id)}`, 30, [NOTIF_CACHE_TAGS.notifications])
      .then(r => r.data)
      .catch(() => null);
  } catch (e) {
    statsErr = e instanceof Error ? e.message : 'Notification stats unavailable.';
  }

  // ── Fetch notification delivery log ───────────────────────────────────────
  const notifQs = new URLSearchParams();
  notifQs.set('tenantId', id);
  notifQs.set('limit',    String(PAGE_SIZE));
  notifQs.set('offset',   String((page - 1) * PAGE_SIZE));
  if (status)  notifQs.set('status',  status);
  if (channel) notifQs.set('channel', channel);

  let notifData: NotifListResponse | null = null;
  let notifErr:  string | null = null;
  try {
    notifData = await notifClient.get<NotifListResponse>(
      `/notifications?${notifQs.toString()}`,
      0,
      [NOTIF_CACHE_TAGS.notifications],
    );
  } catch (e) {
    notifErr = e instanceof Error ? e.message : 'Failed to load notification logs.';
  }

  const notifItems = notifData?.data       ?? [];
  const notifTotal = notifData?.meta?.total ?? 0;
  const notifPages = Math.max(1, Math.ceil(notifTotal / PAGE_SIZE));

  // ── URL builders ──────────────────────────────────────────────────────────
  const base = Routes.tenantNotifications(id);

  function filterHref(overrides: Record<string, string>) {
    const q = new URLSearchParams({ status, channel, ...overrides });
    if (!q.get('status'))  q.delete('status');
    if (!q.get('channel')) q.delete('channel');
    if (q.get('page') === '1') q.delete('page');
    const s = q.toString();
    return `${base}${s ? `?${s}` : ''}`;
  }

  function pageHref(p: number) {
    return filterHref({ page: String(p) });
  }

  return (
    <div className="space-y-5">

      {/* ── Section 1: Stats ──────────────────────────────────────────────── */}
      <div>
        <p className="text-xs font-semibold text-gray-500 uppercase tracking-wide mb-3">
          Notification Activity
        </p>

        {statsErr && (
          <div className="rounded-lg border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-800 mb-3">
            Stats unavailable: {statsErr}
          </div>
        )}

        {stats ? (
          <div className="space-y-3">
            {/* All-time stats */}
            <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
              {[
                { label: 'Total (all time)', value: (stats.total ?? 0).toLocaleString(),             color: 'text-indigo-700' },
                { label: 'Sent',             value: (stats.byStatus?.sent    ?? 0).toLocaleString(), color: 'text-green-700' },
                { label: 'Failed',           value: (stats.byStatus?.failed  ?? 0).toLocaleString(), color: 'text-red-700' },
                { label: 'Blocked',          value: (stats.byStatus?.blocked ?? 0).toLocaleString(), color: 'text-amber-700' },
              ].map(c => (
                <div key={c.label} className="rounded-lg border border-gray-200 bg-white px-4 py-3">
                  <p className="text-xs text-gray-500 mb-1">{c.label}</p>
                  <p className={`text-2xl font-bold ${c.color}`}>{c.value}</p>
                </div>
              ))}
            </div>
            {/* Last 24h */}
            {stats.last24h && (
              <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
                {[
                  { label: 'Last 24h — total',   value: stats.last24h.total.toLocaleString(),   color: 'text-gray-800' },
                  { label: 'Last 24h — sent',    value: stats.last24h.sent.toLocaleString(),    color: 'text-green-700' },
                  { label: 'Last 24h — failed',  value: stats.last24h.failed.toLocaleString(),  color: 'text-red-700' },
                  { label: 'Last 24h — blocked', value: stats.last24h.blocked.toLocaleString(), color: 'text-amber-700' },
                ].map(c => (
                  <div key={c.label} className="rounded-lg border border-gray-200 bg-white px-4 py-3">
                    <p className="text-xs text-gray-500 mb-1">{c.label}</p>
                    <p className={`text-2xl font-bold ${c.color}`}>{c.value}</p>
                  </div>
                ))}
              </div>
            )}
          </div>
        ) : !statsErr ? (
          <div className="rounded-lg border border-gray-200 bg-white px-6 py-6 text-center">
            <p className="text-sm text-gray-400">No notification statistics available for this tenant.</p>
          </div>
        ) : null}
      </div>

      {/* ── Section 2: Delivery Log ───────────────────────────────────────── */}
      <div>
        <p className="text-xs font-semibold text-gray-500 uppercase tracking-wide mb-3">Delivery Log</p>

        {/* Filter bar */}
        <div className="flex flex-wrap gap-4 items-start bg-white border border-gray-200 rounded-lg px-4 py-3 mb-3">
          <div className="flex items-center gap-2 flex-wrap">
            <span className="text-xs font-medium text-gray-600 whitespace-nowrap">Status</span>
            <div className="flex gap-1 flex-wrap">
              {STATUS_OPTIONS.map(s => (
                <Link key={s || '__all'} href={filterHref({ status: s, page: '1' })}
                  className={`px-2.5 py-1 rounded-md text-xs font-medium border transition-colors ${
                    status === s
                      ? 'bg-indigo-600 text-white border-indigo-600'
                      : 'bg-white text-gray-600 border-gray-300 hover:border-gray-400'
                  }`}
                >
                  {s || 'All'}
                </Link>
              ))}
            </div>
          </div>
          <div className="flex items-center gap-2 flex-wrap">
            <span className="text-xs font-medium text-gray-600 whitespace-nowrap">Channel</span>
            <div className="flex gap-1 flex-wrap">
              {CHANNEL_OPTIONS.map(c => (
                <Link key={c || '__all'} href={filterHref({ channel: c, page: '1' })}
                  className={`px-2.5 py-1 rounded-md text-xs font-medium border transition-colors ${
                    channel === c
                      ? 'bg-indigo-600 text-white border-indigo-600'
                      : 'bg-white text-gray-600 border-gray-300 hover:border-gray-400'
                  }`}
                >
                  {c || 'All'}
                </Link>
              ))}
            </div>
          </div>
        </div>

        {notifErr && (
          <div className="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700 mb-3">
            {notifErr}
          </div>
        )}

        {!notifErr && (
          <div className="rounded-lg border border-gray-200 bg-white overflow-hidden">
            {notifTotal > 0 && (
              <div className="px-4 py-2.5 border-b border-gray-100 bg-gray-50">
                <p className="text-xs text-gray-500">
                  {notifTotal.toLocaleString()} notification{notifTotal !== 1 ? 's' : ''}
                  {(status || channel) && <span className="ml-1 text-indigo-600">(filtered)</span>}
                </p>
              </div>
            )}
            <div className="overflow-x-auto">
              <table className="min-w-full divide-y divide-gray-100 text-sm">
                <thead className="bg-gray-50 text-xs text-gray-500 uppercase tracking-wide">
                  <tr>
                    <th className="px-4 py-2.5 text-left font-medium">ID</th>
                    <th className="px-4 py-2.5 text-left font-medium">Channel</th>
                    <th className="px-4 py-2.5 text-left font-medium">Recipient</th>
                    <th className="px-4 py-2.5 text-left font-medium">Subject / Template</th>
                    <th className="px-4 py-2.5 text-left font-medium">Status</th>
                    <th className="px-4 py-2.5 text-left font-medium">Provider</th>
                    <th className="px-4 py-2.5 text-left font-medium whitespace-nowrap">Created (UTC)</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-100">
                  {notifItems.map(n => {
                    const recipient = parseRecipient(n.recipientJson);
                    const subject   = n.renderedSubject ?? n.templateKey ?? null;
                    return (
                      <tr key={n.id} className="hover:bg-gray-50">
                        <td className="px-4 py-2.5 font-mono text-[11px] text-gray-500 whitespace-nowrap">
                          <a href={`/notifications/log/${n.id}`} className="hover:text-indigo-600 hover:underline">
                            {n.id.slice(0, 8)}…
                          </a>
                        </td>
                        <td className="px-4 py-2.5"><ChannelBadge channel={n.channel} /></td>
                        <td className="px-4 py-2.5 font-mono text-[11px] text-gray-700 max-w-[160px] truncate" title={recipient}>
                          {recipient}
                        </td>
                        <td className="px-4 py-2.5 text-xs text-gray-700 max-w-[200px]">
                          {subject
                            ? <span className="truncate block" title={subject}>{subject}</span>
                            : <span className="text-gray-400 italic">—</span>
                          }
                        </td>
                        <td className="px-4 py-2.5"><NotificationStatusBadge status={n.status} /></td>
                        <td className="px-4 py-2.5 text-xs text-gray-600 whitespace-nowrap">
                          {n.providerUsed ?? <span className="text-gray-400 italic">—</span>}
                        </td>
                        <td className="px-4 py-2.5 font-mono text-[11px] text-gray-500 whitespace-nowrap">
                          {new Date(n.createdAt).toLocaleString('en-US', { timeZone: 'UTC', hour12: false })}
                        </td>
                      </tr>
                    );
                  })}
                  {notifItems.length === 0 && (
                    <tr>
                      <td colSpan={7} className="px-4 py-10 text-center text-sm text-gray-400">
                        No notifications found for this tenant.
                      </td>
                    </tr>
                  )}
                </tbody>
              </table>
            </div>
          </div>
        )}

        {/* Pagination */}
        {notifPages > 1 && (
          <div className="flex items-center justify-between text-sm mt-3">
            <span className="text-xs text-gray-500">
              Page {page} of {notifPages} · {notifTotal.toLocaleString()} total
            </span>
            <div className="flex gap-2">
              {page > 1 && (
                <Link href={pageHref(page - 1)}
                  className="px-3 py-1.5 rounded-md border border-gray-300 bg-white text-gray-600 hover:bg-gray-50 text-xs font-medium">
                  ← Previous
                </Link>
              )}
              {page < notifPages && (
                <Link href={pageHref(page + 1)}
                  className="px-3 py-1.5 rounded-md border border-gray-300 bg-white text-gray-600 hover:bg-gray-50 text-xs font-medium">
                  Next →
                </Link>
              )}
            </div>
          </div>
        )}
      </div>

    </div>
  );
}
