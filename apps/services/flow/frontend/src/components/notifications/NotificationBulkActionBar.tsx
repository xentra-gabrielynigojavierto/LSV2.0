"use client";

interface Props {
  selectedCount: number;
  selectedUnreadCount: number;
  selectedReadCount: number;
  busy: boolean;
  onMarkRead: () => void;
  onMarkUnread: () => void;
  onClear: () => void;
}

export function NotificationBulkActionBar({
  selectedCount,
  selectedUnreadCount,
  selectedReadCount,
  busy,
  onMarkRead,
  onMarkUnread,
  onClear,
}: Props) {
  if (selectedCount === 0) return null;

  const canMarkRead = selectedUnreadCount > 0;
  const canMarkUnread = selectedReadCount > 0;

  return (
    <div
      role="region"
      aria-label="Bulk notification actions"
      className="flex flex-wrap items-center justify-between gap-3 rounded-lg border border-blue-200 bg-blue-50 px-4 py-2"
    >
      <div className="flex items-center gap-2">
        <span className="inline-flex items-center justify-center h-6 min-w-6 rounded-full bg-blue-600 text-white text-xs font-semibold px-2">
          {selectedCount}
        </span>
        <span className="text-sm text-blue-900 font-medium">
          {selectedCount === 1 ? "notification selected" : "notifications selected"}
        </span>
      </div>
      <div className="flex items-center gap-2">
        <button
          type="button"
          onClick={onMarkRead}
          disabled={busy || !canMarkRead}
          className="px-3 py-1.5 text-xs font-medium rounded-md bg-white border border-blue-200 text-blue-700 hover:bg-blue-100 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
        >
          {busy ? "Working…" : "Mark as Read"}
        </button>
        <button
          type="button"
          onClick={onMarkUnread}
          disabled={busy || !canMarkUnread}
          className="px-3 py-1.5 text-xs font-medium rounded-md bg-white border border-blue-200 text-blue-700 hover:bg-blue-100 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
        >
          {busy ? "Working…" : "Mark as Unread"}
        </button>
        <button
          type="button"
          onClick={onClear}
          disabled={busy}
          className="px-2 py-1.5 text-xs text-blue-700 hover:text-blue-900 underline disabled:opacity-50"
        >
          Clear
        </button>
      </div>
    </div>
  );
}
