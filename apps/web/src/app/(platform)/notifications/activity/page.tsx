import Link from 'next/link';
import { requireOrg } from '@/lib/auth-guards';
import {

  notificationsServerApi,
  parseRecipient,
  NOTIF_STATUS_OPTIONS,
  NOTIF_CHANNEL_OPTIONS,
  type NotifStats,
  type NotifSummary,
  type NotifFanOutSummary,
} from '@/lib/notifications-server-api';

export const dynamic = 'force-dynamic';


const PAGE_SIZE = 25;

interface SearchParams {
  status?:  string;
  channel?: string;
  page?:    string;
}

const STATUS_CLS: Record<string, string> = {
  sent:       'bg-emerald-50 text-emerald-700 border border-emerald-200',
  delivered:  'bg-emerald-50 text-emerald-700 border border-emerald-200',
  accepted:   'bg-blue-50    text-blue-700    border border-blue-200',
  processing: 'bg-indigo-50  text-indigo-700  border border-indigo-200',
  queued:     'bg-indigo-50  text-indigo-700  border border-indigo-200',
  failed:     'bg-red-50     text-red-700     border border-red-200',
  blocked:    'bg-amber-50   text-amber-700   border border-amber-200',
};

function StatusBadge({ status }: { status: string }) {
  const cls = STATUS_CLS[status.toLowerCase()] ?? 'bg-gray-100 text-gray-500 border border-gray-200';
  return (
    <span className={`inline-flex items-center px-2 py-0.5 rounded-full text-[10px] font-semibold uppercase tracking-wide ${cls}`}>
      {status}
    </span>
  );
}

const CHANNEL_CLS: Record<string, string> = {
  email:    'bg-sky-50    text-sky-700    border border-sky-200',
  sms:      'bg-violet-50 text-violet-700 border border-violet-200',
  push:     'bg-orange-50 text-orange-700 border border-orange-200',
  'in-app': 'bg-teal-50   text-teal-700   border border-teal-200',
};

function ChannelBadge({ channel }: { channel: string }) {
  const cls = CHANNEL_CLS[channel.toLowerCase()] ?? 'bg-gray-100 text-gray-500 border border-gray-200';
  return (
    <span className={`inline-flex items-center px-2 py-0.5 rounded-full text-[10px] font-medium capitalize ${cls}`}>
      {channel}
    </span>
  );
}

function fmtDate(iso: string): string {
  try {
    return new Date(iso).toLocaleString('en-US', {
      month:  'short',
      day:    'numeric',
      year:   'numeric',
      hour:   'numeric',
      minute: '2-digit',
    });
  } catch {
    return iso;
  }
}

function StatCard({ label, value, note, accent }: {
  label: string;
  value: string | number;
  note?: string;
  accent?: string;
}) {
  return (
    <div className="bg-white rounded-lg border border-gray-200 p-5 flex flex-col gap-1">
      <p className="text-xs font-semibold uppercase tracking-wide text-gray-400">{label}</p>
      <p className={`text-3xl font-bold ${accent ?? 'text-gray-900'}`}>{value}</p>
      {note && <p className="text-xs text-gray-400 mt-1">{note}</p>}
    </div>
  );
}

function parseFanOutSummary(json: string | null): NotifFanOutSummary | null {
  if (!json) return null;
  try {
    const meta = JSON.parse(json) as Record<string, unknown>;
    const raw = meta['fanout'];
    if (!raw || typeof raw !== 'object') return null;
    const obj = raw as Record<string, unknown>;
    if (typeof obj.totalResolved !== 'number') return null;
    const num = (k: string) => (typeof obj[k] === 'number' ? (obj[k] as number) : 0);
    return {
      mode:               (obj.mode    as string) ?? null,
      roleKey:            (obj.roleKey as string) ?? null,
      orgId:              (obj.orgId   as string) ?? null,
      channel:            (obj.channel as string) ?? '',
      totalResolved:      obj.totalResolved,
      sentCount:          num('sentCount'),
      failedCount:        num('failedCount'),
      blockedCount:       num('blockedCount'),
      skippedCount:       num('skippedCount'),
      deliveredByChannel: (obj.deliveredByChannel as Record<string, number>) ?? undefined,
      skippedByReason:    (obj.skippedByReason    as Record<string, number>) ?? undefined,
      blockedByReason:    (obj.blockedByReason    as Record<string, number>) ?? undefined,
    };
  } catch {
    return null;
  }
}

function fanOutSubjectLabel(summary: NotifFanOutSummary): { kind: string; subject: string } {
  const mode = (summary.mode ?? '').toLowerCase();
  if (mode === 'org') {
    return { kind: 'Org', subject: summary.orgId ?? '—' };
  }
  if (mode === 'role') {
    return {
      kind: 'Role',
      subject: summary.roleKey
        ? (summary.orgId ? `${summary.roleKey} · ${summary.orgId}` : summary.roleKey)
        : '—',
    };
  }
  return { kind: summary.mode ?? 'Fan-out', subject: summary.roleKey ?? summary.orgId ?? '—' };
}

function FanOutBadge({ kind }: { kind: string }) {
  return (
    <span className="inline-flex items-center px-2 py-0.5 rounded-full text-[10px] font-semibold uppercase tracking-wide bg-purple-50 text-purple-700 border border-purple-200">
      <i className="ri-team-line mr-1" />
      {kind}
    </span>
  );
}

function FanOutInlineSummary({ summary }: { summary: NotifFanOutSummary }) {
  const reached = summary.sentCount;
  const total = summary.totalResolved;
  const notReached = summary.failedCount + summary.blockedCount + summary.skippedCount;
  const underDelivered = total > 0 && reached < total;
  const noneResolved = total === 0;

  return (
    <div className="mt-1 flex flex-wrap items-center gap-x-2 gap-y-0.5 text-[11px]">
      <span
        className={`font-semibold tabular-nums ${
          noneResolved
            ? 'text-amber-700'
            : underDelivered
              ? 'text-amber-700'
              : 'text-emerald-700'
        }`}
        title={noneResolved ? 'No members were resolved for this fan-out' : undefined}
      >
        {reached}/{total} reached
      </span>
      {summary.failedCount > 0 && (
        <span className="text-red-600 tabular-nums">{summary.failedCount} failed</span>
      )}
      {summary.blockedCount > 0 && (
        <span className="text-amber-600 tabular-nums">{summary.blockedCount} blocked</span>
      )}
      {summary.skippedCount > 0 && (
        <span className="text-gray-500 tabular-nums">{summary.skippedCount} skipped</span>
      )}
      {!underDelivered && !noneResolved && notReached === 0 && total > 0 && (
        <span className="text-gray-400">all reached</span>
      )}
    </div>
  );
}

function MetaInfo({ json }: { json: string | null }) {
  if (!json) return <span className="text-gray-300">—</span>;
  try {
    const parsed = JSON.parse(json) as Record<string, unknown>;
    const templateKey = parsed['templateKey'] as string | undefined;
    const template = parsed['template'] as string | undefined;
    const subject = parsed['subject'] as string | undefined;
    const label = templateKey ?? template ?? subject ?? null;
    if (label) {
      return (
        <span className="text-xs text-gray-500 max-w-[180px] truncate block" title={label}>
          {label}
        </span>
      );
    }
  } catch { /* ignore */ }
  return <span className="text-gray-300 text-xs">—</span>;
}

function FilterSelect({ name, value, options, baseHref, otherParams }: {
  name: string;
  value: string;
  options: Array<{ value: string; label: string }>;
  baseHref: string;
  otherParams: Record<string, string>;
}) {
  return (
    <div className="flex items-center gap-1.5 flex-wrap">
      {options.map(opt => {
        const isActive = opt.value === value;
        const params = new URLSearchParams({ ...otherParams, [name]: opt.value });
        if (!opt.value) params.delete(name);
        const q = params.toString();
        return (
          <Link
            key={opt.value}
            href={`${baseHref}${q ? `?${q}` : ''}`}
            className={`px-3 py-1 rounded-full text-xs font-medium border transition-colors ${
              isActive
                ? 'bg-indigo-600 text-white border-indigo-600'
                : 'bg-white text-gray-600 border-gray-200 hover:border-indigo-300 hover:text-indigo-600'
            }`}
          >
            {opt.label}
          </Link>
        );
      })}
    </div>
  );
}

function PageLink({ page, current, baseHref, otherParams }: {
  page: number;
  current: number;
  baseHref: string;
  otherParams: Record<string, string>;
}) {
  const params = new URLSearchParams({ ...otherParams, page: String(page) });
  if (page === 1) params.delete('page');
  const q = params.toString();
  const isActive = page === current;
  return (
    <Link
      href={`${baseHref}${q ? `?${q}` : ''}`}
      className={`w-8 h-8 flex items-center justify-center rounded text-sm font-medium transition-colors ${
        isActive ? 'bg-indigo-600 text-white' : 'text-gray-600 hover:bg-gray-100'
      }`}
    >
      {page}
    </Link>
  );
}

export default async function ActivityPage({
  searchParams,
}: {
  searchParams: Promise<SearchParams>;
}) {
  const sp = await searchParams;
  const session = await requireOrg();
  const { tenantId } = session;

  const status  = sp.status  || '';
  const channel = sp.channel || '';
  const page    = Math.max(1, parseInt(sp.page ?? '1', 10));
  const offset  = (page - 1) * PAGE_SIZE;

  const baseHref = '/notifications/activity';
  const filterParams: Record<string, string> = {};
  if (status)  filterParams['status']  = status;
  if (channel) filterParams['channel'] = channel;

  let notifications: NotifSummary[] = [];
  let total = 0;
  let fetchErr: string | null = null;
  let stats: NotifStats | null = null;
  let statsError: string | null = null;

  const [listResult, statsResult] = await Promise.allSettled([
    notificationsServerApi.list(tenantId, {
      status:   status   || undefined,
      channel:  channel  || undefined,
      page,
      pageSize: PAGE_SIZE,
    }),
    notificationsServerApi.stats(tenantId),
  ]);

  if (listResult.status === 'fulfilled') {
    notifications = listResult.value.items;
    total = listResult.value.totalCount;
  } else {
    fetchErr = listResult.reason instanceof Error ? listResult.reason.message : 'Unable to load notifications.';
  }

  if (statsResult.status === 'fulfilled') {
    stats = statsResult.value ?? null;
  } else {
    statsError = statsResult.reason instanceof Error ? statsResult.reason.message : 'Unable to load stats.';
  }

  const totalPages = Math.max(1, Math.ceil(total / PAGE_SIZE));
  const clampedPage = Math.min(page, totalPages);
  const isPageOutOfRange = page > totalPages && total > 0;
  const startItem  = total === 0 ? 0 : offset + 1;
  const endItem    = Math.min(offset + PAGE_SIZE, total);
  const hasFilters = !!(status || channel);

  // Compute last-24h from the most-recent trend point.
  const trend = stats?.recentTrend ?? [];
  const last24h = trend.length > 0
    ? trend[trend.length - 1]
    : { total: 0, sent: 0, failed: 0, blocked: 0 };

  const prevParams = new URLSearchParams({ ...filterParams });
  if (page > 2) prevParams.set('page', String(page - 1));
  const nextParams = new URLSearchParams({ ...filterParams, page: String(page + 1) });

  return (
    <div className="max-w-6xl mx-auto space-y-6">
      <div className="flex items-start justify-between">
        <div>
          <div className="flex items-center gap-2 mb-1">
            <Link href="/notifications" className="text-sm text-gray-400 hover:text-gray-600 transition-colors">
              Notifications
            </Link>
            <i className="ri-arrow-right-s-line text-gray-300" />
            <span className="text-sm text-gray-700 font-medium">Activity</span>
          </div>
          <h1 className="text-2xl font-bold text-gray-900">Notification Activity</h1>
          <p className="mt-1 text-sm text-gray-500">
            Track notification delivery outcomes for your organisation.
          </p>
        </div>
      </div>

      {statsError ? (
        <div className="rounded-lg bg-red-50 border border-red-200 px-4 py-3 text-sm text-red-700">
          <i className="ri-error-warning-line mr-1.5" />
          Could not load summary: {statsError}
        </div>
      ) : stats ? (
        <div className="grid grid-cols-2 sm:grid-cols-5 gap-4">
          <StatCard
            label="Total"
            value={stats.totalCount}
            note="All time"
          />
          <StatCard
            label="Sent"
            value={stats.statusDistribution['sent'] ?? 0}
            accent="text-emerald-600"
          />
          <StatCard
            label="Failed"
            value={stats.statusDistribution['failed'] ?? 0}
            accent={(stats.statusDistribution['failed'] ?? 0) > 0 ? 'text-red-600' : undefined}
          />
          <StatCard
            label="Blocked"
            value={stats.statusDistribution['blocked'] ?? 0}
            accent={(stats.statusDistribution['blocked'] ?? 0) > 0 ? 'text-amber-600' : undefined}
          />
          <StatCard
            label="Last 24h"
            value={last24h.total}
            note={`${last24h.sent} sent · ${last24h.failed} failed`}
          />
        </div>
      ) : null}

      {stats && stats.totalCount > 0 && (
        <div className="bg-white rounded-lg border border-gray-200 p-5">
          <h2 className="text-sm font-semibold text-gray-700 mb-3">Delivery Breakdown</h2>
          <div className="grid grid-cols-2 sm:grid-cols-5 gap-4">
            {(['sent', 'accepted', 'processing', 'failed', 'blocked'] as const).map(s => (
              <div key={s} className="flex flex-col items-center gap-1">
                <StatusBadge status={s} />
                <span className="text-xl font-bold text-gray-900 mt-1">
                  {stats!.statusDistribution[s] ?? 0}
                </span>
                <span className="text-[10px] text-gray-400">
                  {stats!.totalCount > 0
                    ? `${Math.round(((stats!.statusDistribution[s] ?? 0) / stats!.totalCount) * 100)}%`
                    : '0%'}
                </span>
              </div>
            ))}
          </div>
        </div>
      )}

      {stats && Object.keys(stats.channelBreakdown).length > 0 && (
        <div className="bg-white rounded-lg border border-gray-200 p-5">
          <h2 className="text-sm font-semibold text-gray-700 mb-3">By Channel</h2>
          <div className="flex items-center gap-6">
            {Object.entries(stats.channelBreakdown).map(([ch, count]) => (
              <div key={ch} className="flex items-center gap-2">
                <ChannelBadge channel={ch} />
                <span className="text-sm font-semibold text-gray-700">{count}</span>
              </div>
            ))}
          </div>
        </div>
      )}

      <div className="bg-white rounded-lg border border-gray-200 px-5 py-4 space-y-3">
        <div className="flex flex-wrap items-center gap-6">
          <div className="flex flex-col gap-1.5">
            <span className="text-[11px] font-semibold uppercase tracking-wide text-gray-400">Status</span>
            <FilterSelect
              name="status"
              value={status}
              options={NOTIF_STATUS_OPTIONS}
              baseHref={baseHref}
              otherParams={channel ? { channel } : {}}
            />
          </div>
          <div className="flex flex-col gap-1.5">
            <span className="text-[11px] font-semibold uppercase tracking-wide text-gray-400">Channel</span>
            <FilterSelect
              name="channel"
              value={channel}
              options={NOTIF_CHANNEL_OPTIONS}
              baseHref={baseHref}
              otherParams={status ? { status } : {}}
            />
          </div>
        </div>
        {hasFilters && (
          <div className="pt-1">
            <Link
              href={baseHref}
              className="text-xs text-indigo-600 hover:text-indigo-500 font-medium"
            >
              <i className="ri-close-line mr-0.5" />
              Clear filters
            </Link>
          </div>
        )}
      </div>

      <div className="bg-white rounded-lg border border-gray-200 overflow-hidden">
        {fetchErr ? (
          <div className="px-5 py-4 text-sm text-red-600">
            <i className="ri-error-warning-line mr-1.5" />
            {fetchErr}
          </div>
        ) : isPageOutOfRange ? (
          <div className="px-5 py-16 text-center">
            <i className="ri-file-search-line text-3xl text-gray-300" />
            <p className="mt-2 text-sm text-gray-400">
              Page {page} is beyond the available results.
            </p>
            <Link
              href={`${baseHref}${totalPages > 1 ? `?${new URLSearchParams({ ...filterParams, page: String(totalPages) })}` : ''}`}
              className="mt-3 inline-block text-xs text-indigo-600 hover:text-indigo-500 font-medium"
            >
              Go to last page ({totalPages})
            </Link>
          </div>
        ) : notifications.length === 0 ? (
          <div className="px-5 py-16 text-center">
            <i className="ri-mail-line text-3xl text-gray-300" />
            <p className="mt-2 text-sm text-gray-400">
              {hasFilters ? 'No notifications match the current filters.' : 'No notification activity yet.'}
            </p>
            {hasFilters && (
              <Link href={baseHref} className="mt-3 inline-block text-xs text-indigo-600 hover:text-indigo-500 font-medium">
                Clear filters
              </Link>
            )}
          </div>
        ) : (
          <>
            <table className="min-w-full divide-y divide-gray-100">
              <thead className="bg-gray-50">
                <tr>
                  <th className="px-5 py-2.5 text-left text-[11px] font-semibold uppercase tracking-wide text-gray-400">Recipient</th>
                  <th className="px-5 py-2.5 text-left text-[11px] font-semibold uppercase tracking-wide text-gray-400">Channel</th>
                  <th className="px-5 py-2.5 text-left text-[11px] font-semibold uppercase tracking-wide text-gray-400">Status</th>
                  <th className="px-5 py-2.5 text-left text-[11px] font-semibold uppercase tracking-wide text-gray-400 hidden lg:table-cell">Template</th>
                  <th className="px-5 py-2.5 text-left text-[11px] font-semibold uppercase tracking-wide text-gray-400 hidden md:table-cell">Provider</th>
                  <th className="px-5 py-2.5 text-left text-[11px] font-semibold uppercase tracking-wide text-gray-400">Sent at</th>
                  <th className="px-5 py-2.5 w-10" />
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-50">
                {notifications.map(n => {
                  const fanOut = parseFanOutSummary(n.metadataJson);
                  const subj = fanOut ? fanOutSubjectLabel(fanOut) : null;
                  return (
                  <tr
                    key={n.id}
                    className={`hover:bg-gray-50 transition-colors group ${
                      fanOut ? 'bg-purple-50/30' : ''
                    }`}
                  >
                    <td className="px-5 py-3 text-sm text-gray-700">
                      {fanOut && subj ? (
                        <div className="flex flex-col gap-1">
                          <div className="flex items-center gap-2">
                            <FanOutBadge kind={subj.kind} />
                            <span className="font-mono text-xs text-gray-700 truncate max-w-[220px]" title={subj.subject}>
                              {subj.subject}
                            </span>
                          </div>
                        </div>
                      ) : (
                        <span className="font-mono">{parseRecipient(n.recipientJson)}</span>
                      )}
                    </td>
                    <td className="px-5 py-3">
                      <ChannelBadge channel={n.channel} />
                    </td>
                    <td className="px-5 py-3">
                      <StatusBadge status={n.status} />
                      {fanOut && <FanOutInlineSummary summary={fanOut} />}
                      {n.lastErrorMessage && (
                        <p className="mt-0.5 text-[11px] text-red-500 max-w-[200px] truncate" title={n.lastErrorMessage}>
                          {n.lastErrorMessage}
                        </p>
                      )}
                    </td>
                    <td className="px-5 py-3 hidden lg:table-cell">
                      <MetaInfo json={n.metadataJson} />
                    </td>
                    <td className="px-5 py-3 hidden md:table-cell">
                      {n.providerUsed ? (
                        <span className="text-xs text-gray-500">{n.providerUsed}</span>
                      ) : (
                        <span className="text-gray-300">—</span>
                      )}
                    </td>
                    <td className="px-5 py-3 text-xs text-gray-400 whitespace-nowrap">
                      {fmtDate(n.createdAt)}
                    </td>
                    <td className="px-5 py-3 text-right">
                      {n.status.toLowerCase() === 'failed' && (
                        <span className="text-[10px] text-red-500 font-medium mr-2 hidden sm:inline">Review</span>
                      )}
                      {n.status.toLowerCase() === 'blocked' && (
                        <span className="text-[10px] text-amber-500 font-medium mr-2 hidden sm:inline">Blocked</span>
                      )}
                      <Link
                        href={`/notifications/activity/${n.id}`}
                        className="text-indigo-600 hover:text-indigo-500 text-xs font-medium opacity-0 group-hover:opacity-100 transition-opacity"
                      >
                        Details <i className="ri-arrow-right-s-line" />
                      </Link>
                    </td>
                  </tr>
                  );
                })}
              </tbody>
            </table>

            <div className="flex items-center justify-between px-5 py-3 border-t border-gray-100">
              <p className="text-xs text-gray-400">
                {total > 0
                  ? `Showing ${startItem}–${endItem} of ${total}`
                  : 'No notifications'}
              </p>
              {totalPages > 1 && (
                <div className="flex items-center gap-1">
                  {page > 1 && (
                    <Link
                      href={`${baseHref}${prevParams.toString() ? `?${prevParams}` : ''}`}
                      className="w-8 h-8 flex items-center justify-center rounded text-sm text-gray-500 hover:bg-gray-100"
                    >
                      <i className="ri-arrow-left-s-line" />
                    </Link>
                  )}
                  {Array.from({ length: Math.min(totalPages, 7) }, (_, i) => {
                    const pages = new Set([1, 2, totalPages - 1, totalPages, page - 1, page, page + 1]);
                    const sorted = [...pages].filter(p => p >= 1 && p <= totalPages).sort((a, b) => a - b);
                    return sorted[i];
                  }).filter(Boolean).map((p, idx, arr) => {
                    const prev = arr[idx - 1];
                    return (
                      <div key={p} className="flex items-center">
                        {prev && p! - prev! > 1 && (
                          <span className="px-1 text-gray-300 text-xs">…</span>
                        )}
                        <PageLink page={p!} current={page} baseHref={baseHref} otherParams={filterParams} />
                      </div>
                    );
                  })}
                  {page < totalPages && (
                    <Link
                      href={`${baseHref}?${nextParams}`}
                      className="w-8 h-8 flex items-center justify-center rounded text-sm text-gray-500 hover:bg-gray-100"
                    >
                      <i className="ri-arrow-right-s-line" />
                    </Link>
                  )}
                </div>
              )}
            </div>
          </>
        )}
      </div>
    </div>
  );
}
