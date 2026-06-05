'use client';

import { useState, useEffect, useRef, useCallback } from 'react';
import Link from 'next/link';
import { notificationsService, type NotificationItem, type NotificationStats } from '@/lib/notifications';

const STATUS_DOT: Record<string, string> = {
  sent: 'bg-emerald-500',
  accepted: 'bg-blue-500',
  processing: 'bg-indigo-500',
  failed: 'bg-red-500',
  blocked: 'bg-amber-500',
};

const CHANNEL_ICON: Record<string, string> = {
  email: 'ri-mail-line',
  sms: 'ri-chat-1-line',
  push: 'ri-notification-3-line',
  'in-app': 'ri-apps-line',
};

function timeAgo(iso: string): string {
  const diff = Date.now() - new Date(iso).getTime();
  const mins = Math.floor(diff / 60000);
  if (mins < 1) return 'just now';
  if (mins < 60) return `${mins}m ago`;
  const hrs = Math.floor(mins / 60);
  if (hrs < 24) return `${hrs}h ago`;
  const days = Math.floor(hrs / 24);
  return `${days}d ago`;
}

export function NotificationBell() {
  const [open, setOpen] = useState(false);
  const [items, setItems] = useState<NotificationItem[]>([]);
  const [stats, setStats] = useState<NotificationStats | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(false);
  const ref = useRef<HTMLDivElement>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(false);
    try {
      const [recentItems, statsData] = await Promise.all([
        notificationsService.getRecentNotifications(8),
        notificationsService.getStats(),
      ]);
      setItems(recentItems);
      setStats(statsData);
    } catch {
      setError(true);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { load(); }, [load]);

  useEffect(() => {
    if (!open) return;
    function handler(e: MouseEvent) {
      if (ref.current && !ref.current.contains(e.target as Node)) setOpen(false);
    }
    document.addEventListener('mousedown', handler);
    return () => document.removeEventListener('mousedown', handler);
  }, [open]);

  useEffect(() => {
    if (!open) return;
    function handler(e: KeyboardEvent) { if (e.key === 'Escape') setOpen(false); }
    document.addEventListener('keydown', handler);
    return () => document.removeEventListener('keydown', handler);
  }, [open]);

  const failedCount = stats?.failed ?? 0;

  return (
    <div ref={ref} className="relative flex items-center shrink-0">
      <button
        onClick={() => { setOpen((p) => !p); if (!open) load(); }}
        title="Notifications"
        aria-label={failedCount > 0 ? `Notifications, ${failedCount} failed` : 'Notifications'}
        aria-haspopup="true"
        aria-expanded={open}
        className={[
          'w-8 h-8 flex items-center justify-center rounded-lg transition-colors relative',
          open
            ? 'bg-white/15 text-white'
            : 'text-slate-400 hover:bg-white/10 hover:text-white',
        ].join(' ')}
      >
        <i className="ri-notification-3-line text-[18px] leading-none" />
        {failedCount > 0 && (
          <span className="absolute -top-0.5 -right-0.5 w-4 h-4 rounded-full bg-red-500 text-[9px] font-bold text-white flex items-center justify-center leading-none">
            {failedCount > 9 ? '9+' : failedCount}
          </span>
        )}
      </button>

      {open && (
        <div className="absolute right-0 top-[calc(100%+10px)] w-80 rounded-xl bg-white shadow-2xl border border-gray-200 overflow-hidden z-50">
          <div className="flex items-center justify-between px-4 py-3 border-b border-gray-100">
            <p className="text-sm font-semibold text-gray-800">Notifications</p>
            {stats && (
              <span className="text-[10px] text-gray-400">
                {stats.last24hTotal} in last 24h
              </span>
            )}
          </div>

          {stats && (
            <div className="flex items-center gap-3 px-4 py-2 bg-gray-50 border-b border-gray-100">
              <span className="text-[10px] text-gray-500">
                <span className="font-medium text-emerald-600">{stats.sent}</span> sent
              </span>
              {stats.failed > 0 && (
                <span className="text-[10px] text-gray-500">
                  <span className="font-medium text-red-600">{stats.failed}</span> failed
                </span>
              )}
              {stats.blocked > 0 && (
                <span className="text-[10px] text-gray-500">
                  <span className="font-medium text-amber-600">{stats.blocked}</span> blocked
                </span>
              )}
              {stats.deliveryRate !== null && (
                <span className="text-[10px] text-gray-400 ml-auto">
                  {stats.deliveryRate}% delivery
                </span>
              )}
            </div>
          )}

          <div className="max-h-[340px] overflow-y-auto">
            {loading && (
              <div className="flex items-center gap-2 text-sm text-gray-400 px-4 py-6 justify-center">
                <span className="inline-block w-4 h-4 border-2 border-gray-300 border-t-indigo-500 rounded-full animate-spin" />
                Loading...
              </div>
            )}

            {!loading && error && (
              <div className="px-4 py-6 text-center">
                <p className="text-xs text-gray-400">Unable to load notifications</p>
                <button onClick={load} className="text-xs text-indigo-600 mt-1 hover:underline">
                  Retry
                </button>
              </div>
            )}

            {!loading && !error && items.length === 0 && (
              <div className="px-4 py-8 text-center">
                <i className="ri-mail-check-line text-2xl text-gray-300" />
                <p className="text-xs text-gray-400 mt-2">No notifications yet</p>
              </div>
            )}

            {!loading && !error && items.length > 0 && (
              <ul className="divide-y divide-gray-50">
                {items.map((item) => (
                  <li key={item.id}>
                    <Link
                      href={`/notifications/activity/${item.id}`}
                      onClick={() => setOpen(false)}
                      className="flex items-start gap-3 px-4 py-3 hover:bg-gray-50 transition-colors"
                    >
                      <div className="w-7 h-7 rounded-lg bg-gray-100 flex items-center justify-center shrink-0 mt-0.5">
                        <i className={`${CHANNEL_ICON[item.channel] ?? 'ri-mail-line'} text-sm text-gray-500`} />
                      </div>
                      <div className="min-w-0 flex-1">
                        <div className="flex items-center gap-1.5">
                          <span className={`w-1.5 h-1.5 rounded-full shrink-0 ${STATUS_DOT[item.status.toLowerCase()] ?? 'bg-gray-400'}`} />
                          <span className="text-xs font-medium text-gray-700 truncate">
                            {item.subject ?? item.templateKey ?? item.channel}
                          </span>
                        </div>
                        <p className="text-[11px] text-gray-500 truncate mt-0.5">
                          To: {item.recipient}
                        </p>
                        {item.errorMessage && (
                          <p className="text-[10px] text-red-500 truncate mt-0.5">
                            {item.errorMessage}
                          </p>
                        )}
                        <p className="text-[10px] text-gray-400 mt-0.5">
                          {timeAgo(item.timestampRaw)}
                        </p>
                      </div>
                    </Link>
                  </li>
                ))}
              </ul>
            )}
          </div>

          <div className="px-4 py-2.5 border-t border-gray-100 bg-gray-50">
            <Link
              href="/notifications/activity"
              onClick={() => setOpen(false)}
              className="text-xs text-indigo-600 hover:text-indigo-500 font-medium"
            >
              View all notifications <i className="ri-arrow-right-s-line" />
            </Link>
          </div>
        </div>
      )}
    </div>
  );
}
