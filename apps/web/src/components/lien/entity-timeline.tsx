'use client';

import { useState, useEffect, useCallback } from 'react';
import { auditService, type TimelineItem, type TimelinePagination, type AuditEntityType } from '@/lib/audit';
import { ApiError } from '@/lib/api-client';

interface EntityTimelineProps {
  entityType: AuditEntityType;
  entityId: string;
  title?: string;
  pageSize?: number;
}

export function EntityTimeline({ entityType, entityId, title = 'Activity History', pageSize = 20 }: EntityTimelineProps) {
  const [items, setItems] = useState<TimelineItem[]>([]);
  const [pagination, setPagination] = useState<TimelinePagination | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async (page = 1) => {
    setLoading(true);
    setError(null);
    try {
      const result = await auditService.getEntityTimeline(entityType, entityId, { page, pageSize });
      setItems(result.items);
      setPagination(result.pagination);
    } catch (err) {
      if (err instanceof ApiError && err.status === 404) {
        setItems([]);
        setPagination(null);
      } else {
        setError('Failed to load activity history.');
      }
    } finally {
      setLoading(false);
    }
  }, [entityType, entityId, pageSize]);

  useEffect(() => { load(); }, [load]);

  return (
    <div className="bg-white border border-gray-200 rounded-xl p-5">
      <h3 className="text-sm font-semibold text-gray-800 mb-4">{title}</h3>

      {loading && (
        <div className="flex items-center gap-2 text-sm text-gray-400 py-4">
          <span className="inline-block w-4 h-4 border-2 border-gray-300 border-t-primary rounded-full animate-spin" />
          Loading activity...
        </div>
      )}

      {!loading && error && (
        <div className="text-sm text-red-500 py-2">{error}</div>
      )}

      {!loading && !error && items.length === 0 && (
        <p className="text-sm text-gray-400">No activity yet.</p>
      )}

      {!loading && !error && items.length > 0 && (
        <>
          <div className="relative">
            <div className="absolute left-[7px] top-2 bottom-2 w-px bg-gray-200" />
            <ul className="space-y-4">
              {items.map((item) => (
                <li key={item.id} className="relative pl-6">
                  <div className="absolute left-0 top-1.5 w-[15px] h-[15px] rounded-full bg-white border-2 border-gray-300 z-10" />
                  <div>
                    <p className="text-sm text-gray-700 font-medium">{item.action}</p>
                    <p className="text-xs text-gray-500 mt-0.5">{item.description}</p>
                    <p className="text-xs text-gray-400 mt-0.5">
                      {item.actor} &middot; {item.timestamp}
                    </p>
                  </div>
                </li>
              ))}
            </ul>
          </div>

          {pagination && pagination.totalPages > 1 && (
            <div className="flex items-center justify-between mt-4 pt-3 border-t border-gray-100">
              <span className="text-xs text-gray-400">
                Page {pagination.page} of {pagination.totalPages} ({pagination.totalCount} events)
              </span>
              <div className="flex gap-2">
                <button disabled={pagination.page <= 1} onClick={() => load(pagination.page - 1)}
                  className="text-xs px-2 py-1 rounded border border-gray-200 text-gray-600 hover:bg-gray-50 disabled:opacity-40 disabled:cursor-not-allowed">
                  Prev
                </button>
                <button disabled={!pagination.hasNext} onClick={() => load(pagination.page + 1)}
                  className="text-xs px-2 py-1 rounded border border-gray-200 text-gray-600 hover:bg-gray-50 disabled:opacity-40 disabled:cursor-not-allowed">
                  Next
                </button>
              </div>
            </div>
          )}
        </>
      )}
    </div>
  );
}
