import Link from 'next/link';
import { requireOrg } from '@/lib/auth-guards';
import {

  notificationsServerApi,
  parseRecipient,
  type NotifStats,
  type NotifSummary,
} from '@/lib/notifications-server-api';

export const dynamic = 'force-dynamic';


// ── Stat card ─────────────────────────────────────────────────────────────────

function StatCard({
  label,
  value,
  note,
  accent,
}: {
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
  email:  'bg-sky-50     text-sky-700     border border-sky-200',
  sms:    'bg-violet-50  text-violet-700  border border-violet-200',
  push:   'bg-orange-50  text-orange-700  border border-orange-200',
  'in-app': 'bg-teal-50  text-teal-700   border border-teal-200',
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
      month: 'short',
      day:   'numeric',
      year:  'numeric',
      hour:  'numeric',
      minute: '2-digit',
    });
  } catch {
    return iso;
  }
}

// ── Overview overview ─────────────────────────────────────────────────────────

export default async function NotificationsPage() {
  const session = await requireOrg();
  const { tenantId } = session;

  // Fetch stats + last 10 notifications in parallel.
  let stats: NotifStats | null = null;
  let recent: NotifSummary[]   = [];
  let statsError: string | null = null;
  let listError:  string | null = null;

  const [statsResult, listResult] = await Promise.allSettled([
    notificationsServerApi.stats(tenantId),
    notificationsServerApi.list(tenantId, { pageSize: 10 }),
  ]);

  if (statsResult.status === 'fulfilled') {
    stats = statsResult.value ?? null;
  } else {
    statsError = statsResult.reason instanceof Error
      ? statsResult.reason.message
      : 'Unable to load stats.';
  }

  if (listResult.status === 'fulfilled') {
    recent = listResult.value?.items ?? [];
  } else {
    listError = listResult.reason instanceof Error
      ? listResult.reason.message
      : 'Unable to load recent notifications.';
  }

  // Compute period stats from the daily trend array (chronological order).
  const trend = stats?.recentTrend ?? [];
  const last24h = trend.length > 0
    ? trend[trend.length - 1]
    : { total: 0, sent: 0, failed: 0, blocked: 0 };
  const last7dSlice = trend.slice(-7);
  const last7d = last7dSlice.reduce(
    (acc, d) => ({ total: acc.total + d.total, sent: acc.sent + d.sent, failed: acc.failed + d.failed, blocked: acc.blocked + d.blocked }),
    { total: 0, sent: 0, failed: 0, blocked: 0 },
  );

  const deliveryRate =
    stats && stats.totalCount > 0
      ? Math.round((stats.sentCount / stats.totalCount) * 100)
      : null;

  return (
    <div className="max-w-5xl mx-auto space-y-8">

      {/* ── Header ──────────────────────────────────────────────────────────── */}
      <div className="flex items-start justify-between">
        <div>
          <h1 className="text-2xl font-bold text-gray-900">Notifications</h1>
          <p className="mt-1 text-sm text-gray-500">
            Email delivery overview for your organisation.
          </p>
        </div>
        <div className="flex items-center gap-3">
          <Link
            href="/notifications/activity"
            className="inline-flex items-center gap-1.5 rounded-md bg-indigo-600 px-4 py-2 text-sm font-semibold text-white shadow-sm hover:bg-indigo-500 transition-colors"
          >
            <i className="ri-pulse-line text-base" />
            Activity
          </Link>
          <Link
            href="/notifications/templates"
            className="inline-flex items-center gap-1.5 rounded-md bg-white border border-gray-300 px-4 py-2 text-sm font-semibold text-gray-700 shadow-sm hover:bg-gray-50 transition-colors"
          >
            <i className="ri-file-text-line text-base" />
            Templates
          </Link>
          <Link
            href="/notifications/branding"
            className="inline-flex items-center gap-1.5 rounded-md bg-white border border-gray-300 px-4 py-2 text-sm font-semibold text-gray-700 shadow-sm hover:bg-gray-50 transition-colors"
          >
            <i className="ri-palette-line text-base" />
            Branding
          </Link>
          <Link
            href="/notifications/log"
            className="inline-flex items-center gap-1.5 rounded-md bg-white border border-gray-300 px-4 py-2 text-sm font-semibold text-gray-700 shadow-sm hover:bg-gray-50 transition-colors"
          >
            <i className="ri-list-check-line text-base" />
            Delivery Log
          </Link>
        </div>
      </div>

      {/* ── Stats ───────────────────────────────────────────────────────────── */}
      {statsError ? (
        <div className="rounded-lg bg-red-50 border border-red-200 px-4 py-3 text-sm text-red-700">
          <i className="ri-error-warning-line mr-1.5" />
          Could not load statistics: {statsError}
        </div>
      ) : (
        <div className="grid grid-cols-2 sm:grid-cols-4 gap-4">
          <StatCard
            label="Total sent"
            value={stats?.totalCount ?? '—'}
            note="All time"
          />
          <StatCard
            label="Delivery rate"
            value={deliveryRate !== null ? `${deliveryRate}%` : '—'}
            note={stats ? `${stats.sentCount} of ${stats.totalCount} delivered` : undefined}
            accent={deliveryRate !== null ? (deliveryRate >= 90 ? 'text-emerald-600' : deliveryRate >= 70 ? 'text-amber-600' : 'text-red-600') : undefined}
          />
          <StatCard
            label="Last 24 h"
            value={stats ? last24h.total : '—'}
            note={stats ? `${last24h.sent} sent · ${last24h.failed} failed` : undefined}
          />
          <StatCard
            label="Last 7 days"
            value={stats ? last7d.total : '—'}
            note={stats ? `${last7d.sent} sent · ${last7d.failed} failed` : undefined}
          />
        </div>
      )}

      {/* ── By-status breakdown ──────────────────────────────────────────────── */}
      {stats && stats.totalCount > 0 && (
        <div className="bg-white rounded-lg border border-gray-200 p-5">
          <h2 className="text-sm font-semibold text-gray-700 mb-4">Delivery Status Breakdown</h2>
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

      {/* ── Recent notifications ─────────────────────────────────────────────── */}
      <div className="bg-white rounded-lg border border-gray-200 overflow-hidden">
        <div className="flex items-center justify-between px-5 py-4 border-b border-gray-100">
          <h2 className="text-sm font-semibold text-gray-700">Recent Notifications</h2>
          <Link
            href="/notifications/log"
            className="text-xs text-indigo-600 hover:text-indigo-500 font-medium"
          >
            View all
          </Link>
        </div>

        {listError ? (
          <div className="px-5 py-4 text-sm text-red-600">
            <i className="ri-error-warning-line mr-1.5" />
            {listError}
          </div>
        ) : recent.length === 0 ? (
          <div className="px-5 py-12 text-center">
            <i className="ri-mail-line text-3xl text-gray-300" />
            <p className="mt-2 text-sm text-gray-400">No notifications sent yet.</p>
          </div>
        ) : (
          <table className="min-w-full divide-y divide-gray-100">
            <thead className="bg-gray-50">
              <tr>
                <th className="px-5 py-2.5 text-left text-[11px] font-semibold uppercase tracking-wide text-gray-400">Recipient</th>
                <th className="px-5 py-2.5 text-left text-[11px] font-semibold uppercase tracking-wide text-gray-400">Channel</th>
                <th className="px-5 py-2.5 text-left text-[11px] font-semibold uppercase tracking-wide text-gray-400">Status</th>
                <th className="px-5 py-2.5 text-left text-[11px] font-semibold uppercase tracking-wide text-gray-400">Sent at</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-50">
              {recent.map(n => (
                <tr key={n.id} className="hover:bg-gray-50 transition-colors">
                  <td className="px-5 py-3 text-sm text-gray-700 font-mono">
                    {parseRecipient(n.recipientJson)}
                  </td>
                  <td className="px-5 py-3">
                    <ChannelBadge channel={n.channel} />
                  </td>
                  <td className="px-5 py-3">
                    <StatusBadge status={n.status} />
                    {n.lastErrorMessage && (
                      <p className="mt-0.5 text-[11px] text-red-500 max-w-[220px] truncate">
                        {n.lastErrorMessage}
                      </p>
                    )}
                  </td>
                  <td className="px-5 py-3 text-xs text-gray-400 whitespace-nowrap">
                    {fmtDate(n.createdAt)}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
    </div>
  );
}
