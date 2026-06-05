'use client';

import { useState, useEffect, useCallback } from 'react';
import Link from 'next/link';
import {
  unifiedActivityService,
  getEntityHref,
  getNotificationHref,
  filterActivityByMode,
  type UnifiedActivityItem,
  type ActivitySource,
} from '@/lib/unified-activity';
import { useProviderMode } from '@/hooks/use-provider-mode';

export const dynamic = 'force-dynamic';


const SOURCE_LABELS: Record<string, string> = {
  audit: 'Audit',
  notification: 'Notification',
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

function getItemHref(item: UnifiedActivityItem): string | null {
  if (item.source === 'audit') {
    return getEntityHref(item.entity);
  }
  if (item.source === 'notification') {
    return getNotificationHref(item.id);
  }
  return null;
}

export default function UnifiedActivityPage() {
  const { isSellMode } = useProviderMode();
  const [items, setItems] = useState<UnifiedActivityItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(false);
  const [page, setPage] = useState(1);
  const [hasMore, setHasMore] = useState(false);
  const [sourceFilter, setSourceFilter] = useState<ActivitySource | ''>('');

  const load = useCallback(async (p: number, src: ActivitySource | '') => {
    setLoading(true);
    setError(false);
    try {
      const result = await unifiedActivityService.getUnifiedActivity({
        limit: 20,
        page: p,
        source: src || undefined,
      });
      const filtered = filterActivityByMode(result.items, isSellMode);
      setItems(p === 1 ? filtered : (prev) => [...prev, ...filtered]);
      setHasMore(result.hasMore);
    } catch {
      setError(true);
    } finally {
      setLoading(false);
    }
  }, [isSellMode]);

  useEffect(() => {
    setPage(1);
    setItems([]);
    load(1, sourceFilter);
  }, [sourceFilter, load]);

  const loadMore = () => {
    const next = page + 1;
    setPage(next);
    load(next, sourceFilter);
  };

  return (
    <div className="space-y-5">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-semibold text-gray-900">Activity Feed</h1>
          <p className="text-sm text-gray-500 mt-0.5">Combined audit and notification activity</p>
        </div>
      </div>

      <div className="flex items-center gap-2">
        {(['', 'audit', 'notification'] as const).map((val) => (
          <button
            key={val}
            onClick={() => setSourceFilter(val)}
            className={[
              'text-xs font-medium px-3 py-1.5 rounded-lg border transition-colors',
              sourceFilter === val
                ? 'bg-indigo-50 border-indigo-200 text-indigo-700'
                : 'bg-white border-gray-200 text-gray-600 hover:bg-gray-50',
            ].join(' ')}
          >
            {val === '' ? 'All Sources' : SOURCE_LABELS[val]}
          </button>
        ))}
      </div>

      <div className="bg-white border border-gray-200 rounded-xl overflow-hidden">
        {loading && items.length === 0 && (
          <div className="px-5 py-12 flex items-center justify-center gap-2 text-sm text-gray-400">
            <span className="inline-block w-4 h-4 border-2 border-gray-300 border-t-indigo-500 rounded-full animate-spin" />
            Loading activity...
          </div>
        )}

        {!loading && error && items.length === 0 && (
          <div className="px-5 py-12 text-center">
            <i className="ri-error-warning-line text-2xl text-gray-300" />
            <p className="text-sm text-gray-400 mt-2">Unable to load activity</p>
            <button onClick={() => load(1, sourceFilter)} className="text-xs text-indigo-600 mt-2 hover:underline">Retry</button>
          </div>
        )}

        {!loading && !error && items.length === 0 && (
          <div className="px-5 py-12 text-center">
            <i className="ri-time-line text-2xl text-gray-300" />
            <p className="text-sm text-gray-400 mt-2">No activity found</p>
          </div>
        )}

        {items.length > 0 && (
          <ul className="divide-y divide-gray-100">
            {items.map((item) => {
              const href = getItemHref(item);
              const Wrapper = href ? Link : 'div';
              const wrapperProps = href
                ? { href, className: 'px-5 py-3.5 flex items-start gap-3 hover:bg-gray-50 transition-colors block' }
                : { className: 'px-5 py-3.5 flex items-start gap-3' };

              return (
                <li key={item.id}>
                  <Wrapper {...(wrapperProps as any)}>
                    <div className={`w-8 h-8 rounded-lg bg-gray-50 flex items-center justify-center shrink-0 mt-0.5 ${item.iconColor}`}>
                      <i className={`${item.icon} text-base`} />
                    </div>
                    <div className="min-w-0 flex-1">
                      <div className="flex items-center gap-2">
                        <p className="text-sm text-gray-800 font-medium truncate">{item.title}</p>
                        <span className={[
                          'text-[10px] font-medium px-1.5 py-0.5 rounded-full shrink-0',
                          item.source === 'audit'
                            ? 'bg-blue-50 text-blue-600'
                            : 'bg-purple-50 text-purple-600',
                        ].join(' ')}>
                          {SOURCE_LABELS[item.source]}
                        </span>
                      </div>
                      <p className="text-xs text-gray-500 mt-0.5 truncate">{item.description}</p>
                      {item.sourceDetail.kind === 'notification' && item.sourceDetail.errorMessage && (
                        <p className="text-[10px] text-red-500 truncate mt-0.5">{item.sourceDetail.errorMessage}</p>
                      )}
                      <div className="flex items-center gap-2 mt-1">
                        {item.actor && (
                          <span className="text-[11px] text-gray-400">{item.actor.name}</span>
                        )}
                        <span className="text-[11px] text-gray-400">{timeAgo(item.timestampRaw)}</span>
                        {item.entity && (
                          <span className="text-[10px] text-gray-400 bg-gray-100 rounded px-1.5 py-0.5">
                            {item.entity.type}
                          </span>
                        )}
                      </div>
                    </div>
                  </Wrapper>
                </li>
              );
            })}
          </ul>
        )}

        {hasMore && !loading && (
          <div className="px-5 py-3 border-t border-gray-100 bg-gray-50 text-center">
            <button onClick={loadMore} className="text-xs text-indigo-600 font-medium hover:underline">
              Load more
            </button>
          </div>
        )}

        {loading && items.length > 0 && (
          <div className="px-5 py-3 border-t border-gray-100 flex items-center justify-center gap-2 text-xs text-gray-400">
            <span className="inline-block w-3 h-3 border-2 border-gray-300 border-t-indigo-500 rounded-full animate-spin" />
            Loading...
          </div>
        )}
      </div>
    </div>
  );
}
