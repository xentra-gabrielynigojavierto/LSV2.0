import Link from 'next/link';
import { requireOrg } from '@/lib/auth-guards';
import {

  notificationsServerApi,
  parseRecipient,
  NOTIF_STATUS_OPTIONS,
  NOTIF_CHANNEL_OPTIONS,
  type NotifSummary,
} from '@/lib/notifications-server-api';

export const dynamic = 'force-dynamic';


const PAGE_SIZE = 25;

// ── Search-param types ────────────────────────────────────────────────────────

interface SearchParams {
  status?:  string;
  channel?: string;
  page?:    string;
}

interface PageProps {
  searchParams: Promise<SearchParams>;
}

// ── Status badge ──────────────────────────────────────────────────────────────

const STATUS_CLS: Record<string, string> = {
  sent:       'bg-emerald-50 text-emerald-700 border border-emerald-200',
  accepted:   'bg-blue-50    text-blue-700    border border-blue-200',
  processing: 'bg-indigo-50  text-indigo-700  border border-indigo-200',
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

// ── Channel badge ─────────────────────────────────────────────────────────────

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

// ── Date formatter ────────────────────────────────────────────────────────────

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

// ── Subject / Template label ──────────────────────────────────────────────────

function SubjectLabel({ subject, templateKey }: { subject: string | null; templateKey: string | null }) {
  const label = subject ?? templateKey ?? null;
  if (!label) return <span className="text-gray-300 text-xs">—</span>;
  return (
    <span
      className={`text-xs max-w-[220px] truncate block ${subject ? 'text-gray-700' : 'font-mono text-gray-400'}`}
      title={label}
    >
      {label}
    </span>
  );
}

// ── Failure label ─────────────────────────────────────────────────────────────

const FAILURE_LABELS: Record<string, string> = {
  retryable_provider_failure: 'Provider error',
  non_retryable_failure:      'Non-retryable',
  provider_unavailable:       'Provider down',
  invalid_recipient:          'Bad recipient',
  auth_config_failure:        'Auth error',
};

function FailureCell({ category, message }: { category: string | null; message: string | null }) {
  if (category) {
    return (
      <span className="text-xs text-red-600 whitespace-nowrap">
        {FAILURE_LABELS[category] ?? category}
      </span>
    );
  }
  if (message) {
    return (
      <span className="text-[11px] text-red-500 max-w-[160px] truncate block" title={message}>
        {message}
      </span>
    );
  }
  return <span className="text-gray-300 text-xs">—</span>;
}

// ── Filter select (server-rendered, uses <a> tags to navigate) ────────────────

function FilterSelect({
  name,
  value,
  options,
  baseHref,
  otherParams,
}: {
  name: string;
  value: string;
  options: Array<{ value: string; label: string }>;
  baseHref: string;
  otherParams: Record<string, string>;
}) {
  return (
    <div className="flex items-center gap-1.5">
      {options.map(opt => {
        const isActive = opt.value === value;
        const params = new URLSearchParams({
          ...otherParams,
          [name]: opt.value,
        });
        // Remove empty params
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

// ── Pagination link ───────────────────────────────────────────────────────────

function PageLink({
  page,
  current,
  baseHref,
  otherParams,
}: {
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
        isActive
          ? 'bg-indigo-600 text-white'
          : 'text-gray-600 hover:bg-gray-100'
      }`}
    >
      {page}
    </Link>
  );
}

// ── Page ──────────────────────────────────────────────────────────────────────

export default async function NotificationLogPage({ searchParams }: PageProps) {
  const searchParamsData = await searchParams;
  const session = await requireOrg();
  const { tenantId } = session;

  const status  = searchParamsData.status  || '';
  const channel = searchParamsData.channel || '';
  const page    = Math.max(1, parseInt(searchParamsData.page ?? '1', 10));

  const baseHref = '/notifications/log';
  const filterParams: Record<string, string> = {};
  if (status)  filterParams['status']  = status;
  if (channel) filterParams['channel'] = channel;

  let notifications: NotifSummary[] = [];
  let total    = 0;
  let fetchErr: string | null = null;

  try {
    const res = await notificationsServerApi.list(tenantId, {
      status:   status   || undefined,
      channel:  channel  || undefined,
      page,
      pageSize: PAGE_SIZE,
    });
    notifications = res.items;
    total         = res.totalCount;
  } catch (err: unknown) {
    fetchErr = err instanceof Error ? err.message : 'Unable to load notifications.';
  }

  const totalPages = Math.max(1, Math.ceil(total / PAGE_SIZE));
  const offset     = (page - 1) * PAGE_SIZE;
  const startItem  = total === 0 ? 0 : offset + 1;
  const endItem    = Math.min(offset + PAGE_SIZE, total);
  const hasFilters = !!(status || channel);

  // Build prev/next param sets
  const prevParams = new URLSearchParams({ ...filterParams });
  if (page > 2) prevParams.set('page', String(page - 1)); // page 2 → remove so page 1 is clean
  const nextParams = new URLSearchParams({ ...filterParams, page: String(page + 1) });

  return (
    <div className="max-w-6xl mx-auto space-y-6">

      {/* ── Header ──────────────────────────────────────────────────────────── */}
      <div className="flex items-start justify-between">
        <div>
          <div className="flex items-center gap-2 mb-1">
            <Link
              href="/notifications"
              className="text-sm text-gray-400 hover:text-gray-600 transition-colors"
            >
              Notifications
            </Link>
            <i className="ri-arrow-right-s-line text-gray-300" />
            <span className="text-sm text-gray-700 font-medium">Delivery Log</span>
          </div>
          <h1 className="text-2xl font-bold text-gray-900">Delivery Log</h1>
          <p className="mt-1 text-sm text-gray-500">
            All notifications sent by your organisation.
          </p>
        </div>
      </div>

      {/* ── Filters ─────────────────────────────────────────────────────────── */}
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

      {/* ── Table ───────────────────────────────────────────────────────────── */}
      <div className="bg-white rounded-lg border border-gray-200 overflow-hidden">

        {fetchErr ? (
          <div className="px-5 py-4 text-sm text-red-600">
            <i className="ri-error-warning-line mr-1.5" />
            {fetchErr}
          </div>
        ) : notifications.length === 0 ? (
          <div className="px-5 py-16 text-center">
            <i className="ri-mail-line text-3xl text-gray-300" />
            <p className="mt-2 text-sm text-gray-400">
              {hasFilters ? 'No notifications match the current filters.' : 'No notifications sent yet.'}
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
                  <th className="px-5 py-2.5 text-left text-[11px] font-semibold uppercase tracking-wide text-gray-400 hidden lg:table-cell">Subject / Template</th>
                  <th className="px-5 py-2.5 text-left text-[11px] font-semibold uppercase tracking-wide text-gray-400 hidden xl:table-cell">Provider</th>
                  <th className="px-5 py-2.5 text-left text-[11px] font-semibold uppercase tracking-wide text-gray-400 hidden xl:table-cell">Failure</th>
                  <th className="px-5 py-2.5 text-left text-[11px] font-semibold uppercase tracking-wide text-gray-400">Sent at</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-50">
                {notifications.map(n => (
                  <tr key={n.id} className="hover:bg-gray-50 transition-colors">
                    <td className="px-5 py-3 text-sm text-gray-700 font-mono">
                      {parseRecipient(n.recipientJson)}
                    </td>
                    <td className="px-5 py-3">
                      <ChannelBadge channel={n.channel} />
                    </td>
                    <td className="px-5 py-3">
                      <StatusBadge status={n.status} />
                    </td>
                    <td className="px-5 py-3 hidden lg:table-cell">
                      <SubjectLabel subject={n.renderedSubject ?? null} templateKey={n.templateKey ?? null} />
                    </td>
                    <td className="px-5 py-3 text-xs text-gray-600 whitespace-nowrap hidden xl:table-cell">
                      {n.providerUsed ?? <span className="text-gray-300">—</span>}
                    </td>
                    <td className="px-5 py-3 hidden xl:table-cell">
                      <FailureCell category={n.failureCategory ?? null} message={n.lastErrorMessage ?? null} />
                    </td>
                    <td className="px-5 py-3 text-xs text-gray-400 whitespace-nowrap">
                      {fmtDate(n.createdAt)}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>

            {/* ── Pagination ─────────────────────────────────────────────────── */}
            <div className="flex items-center justify-between px-5 py-3 border-t border-gray-100">
              <p className="text-xs text-gray-400">
                {total > 0
                  ? `Showing ${startItem}–${endItem} of ${total} notifications`
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
                    // Show pages around the current page.
                    const mid = Math.max(4, Math.min(page, totalPages - 3));
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
                        <PageLink
                          page={p!}
                          current={page}
                          baseHref={baseHref}
                          otherParams={filterParams}
                        />
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
