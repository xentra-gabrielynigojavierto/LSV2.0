"use client";

import { STATUS_LABELS, STATUS_COLORS, type TaskItemStatus } from "@/types/task";

interface StatusBadgeProps {
  status: TaskItemStatus;
  className?: string;
}

export function StatusBadge({ status, className = "" }: StatusBadgeProps) {
  return (
    <span
      className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium ${STATUS_COLORS[status]} ${className}`}
    >
      {STATUS_LABELS[status]}
    </span>
  );
}
