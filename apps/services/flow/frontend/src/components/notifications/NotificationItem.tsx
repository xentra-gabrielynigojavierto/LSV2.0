"use client";

import { useRouter } from "next/navigation";
import type { NotificationResponse } from "@/types/notification";
import {
  NOTIFICATION_TYPE_LABELS,
  NOTIFICATION_TYPE_COLORS,
} from "@/types/notification";
import { NotificationIcon } from "./notificationIcons";

export type NotificationItemVariant = "default" | "compact";

interface Props {
  notification: NotificationResponse;
  onMarkRead: (id: string) => void;
  onMarkUnread: (id: string) => void;
  loading?: boolean;
  variant?: NotificationItemVariant;
  /** When true, clicking the row never navigates. Used when the item is rendered inside a context that already represents the linked task (e.g. Task Detail Drawer). */
  disableNavigation?: boolean;
  /** Show a leading icon styled by source/type. Defaults to true for "default" variant, false for "compact". */
  showIcon?: boolean;
  /** Render a leading checkbox for bulk selection. */
  isSelectable?: boolean;
  /** Whether the row is currently selected. */
  isSelected?: boolean;
  /** Called when the row's selection toggle changes. */
  onSelect?: (id: string, selected: boolean) => void;
}

function formatRelativeTime(dateStr: string): string {
  const date = new Date(dateStr);
  const now = new Date();
  const diffMs = now.getTime() - date.getTime();
  const diffMin = Math.floor(diffMs / 60000);
  if (diffMin < 1) return "just now";
  if (diffMin < 60) return `${diffMin}m ago`;
  const diffHr = Math.floor(diffMin / 60);
  if (diffHr < 24) return `${diffHr}h ago`;
  const diffDays = Math.floor(diffHr / 24);
  if (diffDays < 7) return `${diffDays}d ago`;
  return date.toLocaleDateString();
}

export function NotificationItem({
  notification,
  onMarkRead,
  onMarkUnread,
  loading,
  variant = "default",
  disableNavigation = false,
  showIcon,
  isSelectable = false,
  isSelected = false,
  onSelect,
}: Props) {
  const router = useRouter();
  const isUnread = notification.status === "Unread";
  const typeLabel = NOTIFICATION_TYPE_LABELS[notification.type] ?? notification.type;
  const typeColor = NOTIFICATION_TYPE_COLORS[notification.type] ?? "bg-gray-100 text-gray-700";
  const isCompact = variant === "compact";
  const isClickable = !disableNavigation && !!notification.taskId;
  const renderIcon = showIcon ?? !isCompact;

  const openTask = () => {
    if (!notification.taskId) return;
    if (isUnread) onMarkRead(notification.id);
    router.push(`/tasks?taskId=${encodeURIComponent(notification.taskId)}`);
  };

  const handleRowClick = () => {
    if (isClickable) openTask();
  };

  const handleRowKeyDown = (e: React.KeyboardEvent) => {
    if (!isClickable) return;
    if (e.target !== e.currentTarget) return;
    if (e.key === "Enter" || e.key === " ") {
      e.preventDefault();
      openTask();
    }
  };

  const padding = isCompact ? "p-2.5" : "p-4";
  const titleSize = isCompact ? "text-xs" : "text-sm";
  const buttonSize = isCompact ? "px-1.5 py-0.5 text-[10px]" : "px-2 py-1 text-xs";

  // Background: selected > unread > default. Hover variant must preserve
  // the selected tint so selection state always wins visually.
  const rowBg = isSelected
    ? "bg-blue-100/70"
    : isUnread
      ? "bg-blue-50/60"
      : "bg-white";
  const hoverBg = isClickable
    ? isSelected
      ? "hover:bg-blue-100"
      : isUnread
        ? "hover:bg-blue-50"
        : "hover:bg-gray-50/80"
    : "";

  return (
    <div
      onClick={handleRowClick}
      onKeyDown={handleRowKeyDown}
      role={isClickable ? "button" : undefined}
      tabIndex={isClickable ? 0 : undefined}
      aria-label={isClickable ? `Open related task: ${notification.title}` : undefined}
      data-task-id={notification.taskId ?? undefined}
      data-variant={variant}
      data-selected={isSelected || undefined}
      data-status={notification.status}
      className={`flex items-start gap-3 ${padding} border-b border-gray-100 last:border-b-0 transition-colors ${rowBg} ${hoverBg} ${
        isClickable ? "cursor-pointer focus:outline-none focus:ring-2 focus:ring-inset focus:ring-blue-300" : ""
      }`}
    >
      {isSelectable && (
        <div
          onClick={(e) => e.stopPropagation()}
          onKeyDown={(e) => e.stopPropagation()}
          className="flex-shrink-0 pt-1"
        >
          <input
            type="checkbox"
            checked={isSelected}
            onChange={(e) => onSelect?.(notification.id, e.target.checked)}
            aria-label={`Select notification: ${notification.title}`}
            className="h-4 w-4 rounded border-gray-300 text-blue-600 focus:ring-2 focus:ring-blue-500 cursor-pointer"
          />
        </div>
      )}

      {/* Unread accent bar */}
      <div
        className={`flex-shrink-0 self-stretch w-1 rounded-full ${
          isUnread ? "bg-blue-500" : "bg-transparent"
        }`}
        aria-hidden="true"
      />

      {renderIcon && <NotificationIcon notification={notification} size={isCompact ? "sm" : "md"} />}

      <div className="flex-1 min-w-0">
        <div className={`flex items-center gap-2 ${isCompact ? "mb-0" : "mb-1"}`}>
          <span className={`text-[10px] font-semibold uppercase tracking-wide px-1.5 py-0.5 rounded ${typeColor}`}>
            {typeLabel}
          </span>
          <span className="text-[10px] text-gray-400">·</span>
          <span className="text-[10px] text-gray-500">{formatRelativeTime(notification.createdAt)}</span>
          {isClickable && !isCompact && (
            <span className="text-[10px] text-blue-600 font-medium ml-auto">Open task →</span>
          )}
        </div>
        <p
          className={`${titleSize} ${
            isUnread ? "font-semibold text-gray-900" : "font-normal text-gray-700"
          } ${isCompact ? "truncate" : ""}`}
        >
          {notification.title}
        </p>
        {notification.message && (
          <p
            className={`${isCompact ? "text-[11px] mt-0" : "text-xs mt-0.5"} text-gray-500 truncate`}
            title={isCompact ? notification.message : undefined}
          >
            {notification.message}
          </p>
        )}
      </div>

      <button
        onClick={(e) => {
          e.stopPropagation();
          if (isUnread) onMarkRead(notification.id);
          else onMarkUnread(notification.id);
        }}
        onKeyDown={(e) => e.stopPropagation()}
        disabled={loading}
        className={`flex-shrink-0 ${buttonSize} rounded border border-transparent hover:border-gray-200 hover:bg-white text-gray-500 hover:text-gray-700 disabled:opacity-50 transition-colors`}
        title={isUnread ? "Mark as read" : "Mark as unread"}
      >
        {isUnread ? "Read" : "Unread"}
      </button>
    </div>
  );
}
