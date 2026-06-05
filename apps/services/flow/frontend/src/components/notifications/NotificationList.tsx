"use client";

import { useState, useEffect, useCallback, useMemo, useRef } from "react";
import type { NotificationResponse, NotificationPagedResponse } from "@/types/notification";
import {
  NOTIFICATION_TYPES,
  NOTIFICATION_TYPE_LABELS,
  NOTIFICATION_SOURCE_TYPES,
} from "@/types/notification";
import { listNotifications, markRead, markUnread, markAllRead } from "@/lib/api/notifications";
import { NotificationItem } from "./NotificationItem";
import { NotificationBulkActionBar } from "./NotificationBulkActionBar";
import { Pagination } from "@/components/ui/Pagination";
import { refreshUnreadCount } from "@/lib/useUnreadCount";

type FilterMode = "all" | "unread";

export function NotificationList() {
  const [data, setData] = useState<NotificationPagedResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [filter, setFilter] = useState<FilterMode>("all");
  const [typeFilter, setTypeFilter] = useState<string>("");
  const [sourceFilter, setSourceFilter] = useState<string>("");
  const [page, setPage] = useState(1);
  const [actionLoading, setActionLoading] = useState<string | null>(null);
  const [markingAll, setMarkingAll] = useState(false);
  const [mutationError, setMutationError] = useState<string | null>(null);
  const [bulkBusy, setBulkBusy] = useState(false);
  const [selected, setSelected] = useState<Set<string>>(new Set());
  // Tracks the last result-set identity so we know when to clear selection.
  const lastResultKeyRef = useRef<string>("");
  // Stale-response guard: only the most-recent in-flight fetch may commit state.
  const fetchIdRef = useRef(0);

  const pageSize = 20;

  const fetchNotifications = useCallback(async () => {
    const requestId = ++fetchIdRef.current;
    setLoading(true);
    setError(null);
    try {
      const result = await listNotifications({
        status: filter === "unread" ? "Unread" : undefined,
        type: typeFilter || undefined,
        sourceType: sourceFilter || undefined,
        page,
        pageSize,
      });
      if (requestId !== fetchIdRef.current) return;
      setData(result);
    } catch (e: unknown) {
      if (requestId !== fetchIdRef.current) return;
      setError(e instanceof Error ? e.message : "Failed to load notifications");
    } finally {
      if (requestId === fetchIdRef.current) {
        setLoading(false);
      }
    }
  }, [filter, typeFilter, sourceFilter, page]);

  const hasActiveAdvancedFilter = !!typeFilter || !!sourceFilter;
  const resetFilters = () => {
    setTypeFilter("");
    setSourceFilter("");
    setFilter("all");
    setPage(1);
  };

  useEffect(() => { fetchNotifications(); }, [fetchNotifications]);

  // Always re-sync the unread badge when the user lands on this page,
  // even if the cache is warm — they may have read items in another tab.
  useEffect(() => { refreshUnreadCount(); }, []);

  // Reset selection whenever the filter or page changes (per spec).
  useEffect(() => {
    setSelected(new Set());
  }, [filter, typeFilter, sourceFilter, page]);

  // Reset selection if the result set's identity changes (e.g. background refresh
  // returned different items). Identity is the joined sorted ID list.
  useEffect(() => {
    if (!data) return;
    const key = data.items.map((i) => i.id).sort().join("|");
    if (lastResultKeyRef.current && key !== lastResultKeyRef.current) {
      setSelected(new Set());
    }
    lastResultKeyRef.current = key;
  }, [data]);

  const handleMarkRead = async (id: string) => {
    try {
      setActionLoading(id);
      setMutationError(null);
      await markRead(id);
      setData((prev) => {
        if (!prev) return prev;
        return {
          ...prev,
          items: prev.items.map((n) =>
            n.id === id ? { ...n, status: "Read" as const, readAt: new Date().toISOString() } : n
          ),
        };
      });
      refreshUnreadCount();
    } catch {
      setMutationError("Failed to mark notification as read.");
    } finally {
      setActionLoading(null);
    }
  };

  const handleMarkUnread = async (id: string) => {
    try {
      setActionLoading(id);
      setMutationError(null);
      await markUnread(id);
      setData((prev) => {
        if (!prev) return prev;
        return {
          ...prev,
          items: prev.items.map((n) =>
            n.id === id ? { ...n, status: "Unread" as const, readAt: null } : n
          ),
        };
      });
      refreshUnreadCount();
    } catch {
      setMutationError("Failed to mark notification as unread.");
    } finally {
      setActionLoading(null);
    }
  };

  const handleMarkAllRead = async () => {
    try {
      setMarkingAll(true);
      setMutationError(null);
      await markAllRead();
      await fetchNotifications();
      refreshUnreadCount();
    } catch {
      setMutationError("Failed to mark all notifications as read.");
    } finally {
      setMarkingAll(false);
    }
  };

  const items = data?.items ?? [];
  const totalCount = data?.totalCount ?? 0;
  const unreadCount = items.filter((n) => n.status === "Unread").length;
  const totalPages = Math.ceil(totalCount / pageSize);

  const selectedItems = useMemo(
    () => items.filter((n) => selected.has(n.id)),
    [items, selected],
  );
  const selectedUnreadCount = selectedItems.filter((n) => n.status === "Unread").length;
  const selectedReadCount = selectedItems.filter((n) => n.status === "Read").length;

  const allSelected = items.length > 0 && selectedItems.length === items.length;
  const someSelected = selectedItems.length > 0 && !allSelected;

  const headerCheckboxRef = useRef<HTMLInputElement>(null);
  useEffect(() => {
    if (headerCheckboxRef.current) {
      headerCheckboxRef.current.indeterminate = someSelected;
    }
  }, [someSelected]);

  const toggleOne = useCallback((id: string, isSel: boolean) => {
    setSelected((prev) => {
      const next = new Set(prev);
      if (isSel) next.add(id);
      else next.delete(id);
      return next;
    });
  }, []);

  const toggleAll = (isSel: boolean) => {
    if (isSel) setSelected(new Set(items.map((n) => n.id)));
    else setSelected(new Set());
  };

  const clearSelection = () => setSelected(new Set());

  const runBulk = async (
    ids: string[],
    action: (id: string) => Promise<unknown>,
    targetStatus: "Read" | "Unread",
  ) => {
    if (ids.length === 0 || bulkBusy) return;
    setBulkBusy(true);
    setMutationError(null);
    try {
      const results = await Promise.allSettled(ids.map((id) => action(id)));
      const succeeded: string[] = [];
      let failedCount = 0;
      results.forEach((r, i) => {
        if (r.status === "fulfilled") succeeded.push(ids[i]);
        else failedCount++;
      });

      if (succeeded.length > 0) {
        const succeededSet = new Set(succeeded);
        setData((prev) => {
          if (!prev) return prev;
          const nowIso = new Date().toISOString();
          return {
            ...prev,
            items: prev.items.map((n) =>
              succeededSet.has(n.id)
                ? {
                    ...n,
                    status: targetStatus,
                    readAt: targetStatus === "Read" ? nowIso : null,
                  }
                : n,
            ),
          };
        });
        refreshUnreadCount();
      }

      // Clear successful selections; keep failed ones so the user can retry.
      setSelected((prev) => {
        const next = new Set(prev);
        succeeded.forEach((id) => next.delete(id));
        return next;
      });

      if (failedCount > 0) {
        const verb = targetStatus === "Read" ? "mark as read" : "mark as unread";
        setMutationError(
          succeeded.length === 0
            ? `Failed to ${verb} ${failedCount} notification${failedCount === 1 ? "" : "s"}.`
            : `${succeeded.length} updated, ${failedCount} failed to ${verb}.`,
        );
      }
    } finally {
      setBulkBusy(false);
    }
  };

  const handleBulkMarkRead = () => {
    const ids = selectedItems.filter((n) => n.status === "Unread").map((n) => n.id);
    void runBulk(ids, markRead, "Read");
  };

  const handleBulkMarkUnread = () => {
    const ids = selectedItems.filter((n) => n.status === "Read").map((n) => n.id);
    void runBulk(ids, markUnread, "Unread");
  };

  if (error) {
    const isBackendDown = error.includes("Backend unavailable") || error.includes("timed out");
    return (
      <div className="space-y-4">
        {isBackendDown ? (
          <div className="rounded-lg border border-amber-200 bg-amber-50 p-6 text-center">
            <p className="text-sm text-amber-800 font-medium mb-1">Notifications require backend connection</p>
            <p className="text-xs text-amber-600">The notification service is currently unavailable. Your tasks and workflows are unaffected.</p>
          </div>
        ) : (
          <div className="rounded-lg border border-red-200 bg-red-50 p-6 text-center">
            <p className="text-sm text-red-800 mb-3">{error}</p>
            <button
              onClick={fetchNotifications}
              className="rounded bg-red-600 px-4 py-2 text-sm text-white hover:bg-red-700"
            >
              Retry
            </button>
          </div>
        )}
      </div>
    );
  }

  return (
    <div className="space-y-4">
      {mutationError && (
        <div className="flex items-center justify-between rounded-lg border border-red-200 bg-red-50 px-4 py-2">
          <p className="text-sm text-red-700">{mutationError}</p>
          <button
            onClick={() => setMutationError(null)}
            className="text-red-400 hover:text-red-600 text-xs ml-4"
          >
            Dismiss
          </button>
        </div>
      )}
      <div className="flex items-center justify-between flex-wrap gap-3">
        <div className="flex items-center gap-2 flex-wrap">
          <button
            onClick={() => { setFilter("all"); setPage(1); }}
            className={`px-3 py-1.5 text-sm rounded-md transition-colors ${
              filter === "all" ? "bg-gray-900 text-white" : "bg-gray-100 text-gray-600 hover:bg-gray-200"
            }`}
          >
            All
          </button>
          <button
            onClick={() => { setFilter("unread"); setPage(1); }}
            className={`px-3 py-1.5 text-sm rounded-md transition-colors ${
              filter === "unread" ? "bg-gray-900 text-white" : "bg-gray-100 text-gray-600 hover:bg-gray-200"
            }`}
          >
            Unread
          </button>
          <span className="h-4 w-px bg-gray-200 mx-1" />
          <select
            aria-label="Filter by type"
            value={typeFilter}
            onChange={(e) => { setTypeFilter(e.target.value); setPage(1); }}
            className="rounded-md border border-gray-300 bg-white px-2 py-1.5 text-xs text-gray-700 focus:border-blue-500 focus:outline-none"
          >
            <option value="">All types</option>
            {NOTIFICATION_TYPES.map((t) => (
              <option key={t} value={t}>{NOTIFICATION_TYPE_LABELS[t]}</option>
            ))}
          </select>
          <select
            aria-label="Filter by source"
            value={sourceFilter}
            onChange={(e) => { setSourceFilter(e.target.value); setPage(1); }}
            className="rounded-md border border-gray-300 bg-white px-2 py-1.5 text-xs text-gray-700 focus:border-blue-500 focus:outline-none"
          >
            <option value="">All sources</option>
            {NOTIFICATION_SOURCE_TYPES.map((s) => (
              <option key={s} value={s}>{s}</option>
            ))}
          </select>
          {(hasActiveAdvancedFilter || filter !== "all") && (
            <button
              onClick={resetFilters}
              className="text-xs text-gray-500 hover:text-gray-700 underline ml-1"
            >
              Reset
            </button>
          )}
        </div>

        {unreadCount > 0 && (
          <button
            onClick={handleMarkAllRead}
            disabled={markingAll || bulkBusy}
            className="px-3 py-1.5 text-xs text-blue-600 hover:bg-blue-50 rounded-md disabled:opacity-50 transition-colors"
          >
            {markingAll ? "Marking…" : "Mark all read"}
          </button>
        )}
      </div>

      <NotificationBulkActionBar
        selectedCount={selectedItems.length}
        selectedUnreadCount={selectedUnreadCount}
        selectedReadCount={selectedReadCount}
        busy={bulkBusy}
        onMarkRead={handleBulkMarkRead}
        onMarkUnread={handleBulkMarkUnread}
        onClear={clearSelection}
      />

      {loading ? (
        <div className="flex items-center justify-center py-12">
          <div className="h-6 w-6 animate-spin rounded-full border-2 border-gray-300 border-t-blue-600" />
        </div>
      ) : items.length === 0 ? (
        <div className="rounded-lg border border-gray-200 bg-white p-12 text-center">
          <p className="text-sm text-gray-500">
            {filter === "unread" ? "No unread notifications" : "No notifications yet"}
          </p>
          <p className="text-xs text-gray-400 mt-1">
            Notifications appear when tasks are assigned, transitioned, or automations run.
          </p>
        </div>
      ) : (
        <div className="rounded-lg border border-gray-200 bg-white overflow-hidden">
          <div className="flex items-center gap-3 px-4 py-2 border-b border-gray-200 bg-gray-50">
            <input
              ref={headerCheckboxRef}
              type="checkbox"
              checked={allSelected}
              onChange={(e) => toggleAll(e.target.checked)}
              aria-label={allSelected ? "Deselect all notifications" : "Select all notifications on this page"}
              className="h-4 w-4 rounded border-gray-300 text-blue-600 focus:ring-2 focus:ring-blue-500 cursor-pointer"
            />
            <span className="text-xs text-gray-500">
              {allSelected
                ? `All ${items.length} on this page selected`
                : someSelected
                  ? `${selectedItems.length} of ${items.length} selected`
                  : `Select all on this page`}
            </span>
          </div>
          {items.map((n) => (
            <NotificationItem
              key={n.id}
              notification={n}
              onMarkRead={handleMarkRead}
              onMarkUnread={handleMarkUnread}
              loading={actionLoading === n.id || (bulkBusy && selected.has(n.id))}
              isSelectable
              isSelected={selected.has(n.id)}
              onSelect={toggleOne}
              showIcon
            />
          ))}
        </div>
      )}

      {totalPages > 1 && (
        <Pagination
          page={page}
          pageSize={pageSize}
          totalCount={totalCount}
          onPageChange={setPage}
          onPageSizeChange={() => {}}
        />
      )}
    </div>
  );
}
