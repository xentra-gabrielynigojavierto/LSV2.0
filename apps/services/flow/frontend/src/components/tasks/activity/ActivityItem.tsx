"use client";

import type { ActivityEvent, ActivityEventType } from "@/types/activity";

interface ActivityItemProps {
  event: ActivityEvent;
}

function formatTime(dateStr: string): string {
  return new Date(dateStr).toLocaleString("en-US", {
    month: "short",
    day: "numeric",
    hour: "numeric",
    minute: "2-digit",
  });
}

const ICONS: Record<ActivityEventType, { path: string; color: string }> = {
  CREATED: {
    path: "M12 4v16m8-8H4",
    color: "text-emerald-500 bg-emerald-50",
  },
  STATUS_CHANGED: {
    path: "M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15",
    color: "text-blue-500 bg-blue-50",
  },
  ASSIGNED: {
    path: "M16 7a4 4 0 11-8 0 4 4 0 018 0zM12 14a7 7 0 00-7 7h14a7 7 0 00-7-7z",
    color: "text-purple-500 bg-purple-50",
  },
  UPDATED: {
    path: "M11 5H6a2 2 0 00-2 2v11a2 2 0 002 2h11a2 2 0 002-2v-5m-1.414-9.414a2 2 0 112.828 2.828L11.828 15H9v-2.828l8.586-8.586z",
    color: "text-amber-500 bg-amber-50",
  },
};

export function ActivityItem({ event }: ActivityItemProps) {
  const icon = ICONS[event.type];

  return (
    <div className="flex gap-3 py-2.5">
      <div className={`mt-0.5 flex h-7 w-7 flex-shrink-0 items-center justify-center rounded-full ${icon.color}`}>
        <svg className="h-3.5 w-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24" strokeWidth={2}>
          <path strokeLinecap="round" strokeLinejoin="round" d={icon.path} />
        </svg>
      </div>
      <div className="flex-1 min-w-0">
        <p className="text-sm text-gray-700">{event.message}</p>
        <div className="mt-0.5 flex items-center gap-2">
          <span className="text-xs text-gray-400">{formatTime(event.createdAt)}</span>
          {event.createdBy && (
            <span className="text-xs text-gray-400">by {event.createdBy}</span>
          )}
        </div>
      </div>
    </div>
  );
}
