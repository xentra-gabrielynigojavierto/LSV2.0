"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { listNotifications, markRead, markUnread } from "@/lib/api/notifications";
import { refreshUnreadCount } from "@/lib/useUnreadCount";
import { NotificationItem } from "./NotificationItem";
import type { NotificationResponse } from "@/types/notification";

interface Props {
  taskId: string;
}

const PAGE_SIZE = 10;

export function TaskNotificationsPanel({ taskId }: Props) {
  const [items, setItems] = useState<NotificationResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [actionLoading, setActionLoading] = useState<string | null>(null);
  const [showAll, setShowAll] = useState(false);
  const [totalCount, setTotalCount] = useState(0);
  // Guards against stale responses when the user opens a different task
  // before the previous fetch resolves.
  const fetchIdRef = useRef(0);
  const activeTaskIdRef = useRef(taskId);

  const fetchItems = useCallback(async () => {
    const requestId = ++fetchIdRef.current;
    activeTaskIdRef.current = taskId;
    setLoading(true);
    setError(null);
    try {
      const result = await listNotifications({
        taskId,
        page: 1,
        pageSize: 50,
      });
      // Drop stale responses: another fetch was started after this one,
      // OR the open task changed underneath us.
      if (requestId !== fetchIdRef.current || activeTaskIdRef.current !== taskId) {
        return;
      }
      setItems(result.items);
      setTotalCount(result.totalCount);
    } catch (e) {
      if (requestId !== fetchIdRef.current || activeTaskIdRef.current !== taskId) {
        return;
      }
      setError(e instanceof Error ? e.message : "Failed to load notifications");
    } finally {
      if (requestId === fetchIdRef.current) {
        setLoading(false);
      }
    }
  }, [taskId]);

  useEffect(() => {
    setShowAll(false);
    fetchItems();
  }, [fetchItems]);

  const handleMarkRead = async (id: string) => {
    try {
      setActionLoading(id);
      await markRead(id);
      setItems((prev) =>
        prev.map((n) =>
          n.id === id ? { ...n, status: "Read" as const, readAt: new Date().toISOString() } : n
        )
      );
      refreshUnreadCount();
    } catch {
      // keep panel stable; surface inline only if needed
    } finally {
      setActionLoading(null);
    }
  };

  const handleMarkUnread = async (id: string) => {
    try {
      setActionLoading(id);
      await markUnread(id);
      setItems((prev) =>
        prev.map((n) =>
          n.id === id ? { ...n, status: "Unread" as const, readAt: null } : n
        )
      );
      refreshUnreadCount();
    } catch {
      // ignore; non-critical
    } finally {
      setActionLoading(null);
    }
  };

  if (loading) {
    return (
      <div className="rounded-lg border border-gray-200 bg-white px-3 py-3 text-xs text-gray-400">
        Loading notifications…
      </div>
    );
  }

  if (error) {
    return (
      <div className="rounded-lg border border-amber-200 bg-amber-50 px-3 py-2">
        <p className="text-xs text-amber-700 font-medium">Notifications unavailable</p>
        <p className="text-[11px] text-amber-600 mt-0.5">{error}</p>
        <button
          onClick={fetchItems}
          className="mt-1 text-[11px] text-amber-700 hover:text-amber-900 underline"
        >
          Retry
        </button>
      </div>
    );
  }

  if (items.length === 0) {
    return (
      <div className="rounded-lg border border-gray-200 bg-gray-50 px-3 py-3 text-center">
        <p className="text-xs text-gray-500">No notifications for this task yet</p>
      </div>
    );
  }

  const visible = showAll ? items : items.slice(0, PAGE_SIZE);
  const hiddenCount = items.length - visible.length;
  const unreadCount = items.filter((n) => n.status === "Unread").length;

  return (
    <div className="rounded-lg border border-gray-200 bg-white overflow-hidden">
      {unreadCount > 0 && (
        <div className="px-3 py-1.5 border-b border-gray-100 bg-blue-50/40">
          <p className="text-[11px] text-blue-700 font-medium">
            {unreadCount} unread
          </p>
        </div>
      )}
      <div className="max-h-72 overflow-y-auto">
        {visible.map((n) => (
          <NotificationItem
            key={n.id}
            notification={n}
            onMarkRead={handleMarkRead}
            onMarkUnread={handleMarkUnread}
            loading={actionLoading === n.id}
            variant="compact"
            disableNavigation
          />
        ))}
      </div>
      {hiddenCount > 0 && (
        <div className="px-3 py-1.5 border-t border-gray-100 bg-gray-50">
          <button
            onClick={() => setShowAll(true)}
            className="text-[11px] text-blue-600 hover:text-blue-800"
          >
            Show {hiddenCount} more
          </button>
        </div>
      )}
      {totalCount > items.length && (
        <div className="px-3 py-1.5 border-t border-gray-100 bg-gray-50">
          <p className="text-[11px] text-gray-500">
            Showing latest {items.length} of {totalCount}.{" "}
            <a href="/notifications" className="text-blue-600 hover:text-blue-800 underline">
              View all
            </a>
          </p>
        </div>
      )}
    </div>
  );
}
