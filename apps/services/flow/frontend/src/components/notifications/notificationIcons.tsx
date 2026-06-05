import type * as React from "react";
import type { NotificationResponse } from "@/types/notification";

interface IconProps {
  className?: string;
}

const baseIconProps = {
  width: 16,
  height: 16,
  viewBox: "0 0 24 24",
  fill: "none",
  stroke: "currentColor",
  strokeWidth: 2,
  strokeLinecap: "round" as const,
  strokeLinejoin: "round" as const,
};

function UserIcon({ className }: IconProps) {
  return (
    <svg {...baseIconProps} className={className} aria-hidden="true">
      <path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2" />
      <circle cx="12" cy="7" r="4" />
    </svg>
  );
}

function ArrowsIcon({ className }: IconProps) {
  return (
    <svg {...baseIconProps} className={className} aria-hidden="true">
      <polyline points="17 1 21 5 17 9" />
      <path d="M3 11V9a4 4 0 0 1 4-4h14" />
      <polyline points="7 23 3 19 7 15" />
      <path d="M21 13v2a4 4 0 0 1-4 4H3" />
    </svg>
  );
}

function GearIcon({ className }: IconProps) {
  return (
    <svg {...baseIconProps} className={className} aria-hidden="true">
      <circle cx="12" cy="12" r="3" />
      <path d="M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 1 1-2.83 2.83l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 1 1-4 0v-.09A1.65 1.65 0 0 0 9 19.4a1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 1 1-2.83-2.83l.06-.06a1.65 1.65 0 0 0 .33-1.82 1.65 1.65 0 0 0-1.51-1H3a2 2 0 1 1 0-4h.09A1.65 1.65 0 0 0 4.6 9a1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 1 1 2.83-2.83l.06.06a1.65 1.65 0 0 0 1.82.33H9a1.65 1.65 0 0 0 1-1.51V3a2 2 0 1 1 4 0v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 1 1 2.83 2.83l-.06.06a1.65 1.65 0 0 0-.33 1.82V9a1.65 1.65 0 0 0 1.51 1H21a2 2 0 1 1 0 4h-.09a1.65 1.65 0 0 0-1.51 1z" />
    </svg>
  );
}

function AlertIcon({ className }: IconProps) {
  return (
    <svg {...baseIconProps} className={className} aria-hidden="true">
      <path d="M10.29 3.86 1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z" />
      <line x1="12" y1="9" x2="12" y2="13" />
      <line x1="12" y1="17" x2="12.01" y2="17" />
    </svg>
  );
}

function BellIcon({ className }: IconProps) {
  return (
    <svg {...baseIconProps} className={className} aria-hidden="true">
      <path d="M18 8a6 6 0 0 0-12 0c0 7-3 9-3 9h18s-3-2-3-9" />
      <path d="M13.73 21a2 2 0 0 1-3.46 0" />
    </svg>
  );
}

function CheckCircleIcon({ className }: IconProps) {
  return (
    <svg {...baseIconProps} className={className} aria-hidden="true">
      <path d="M22 11.08V12a10 10 0 1 1-5.93-9.14" />
      <polyline points="22 4 12 14.01 9 11.01" />
    </svg>
  );
}

type IconComponent = (p: IconProps) => React.ReactElement;

function getIconAccent(notification: NotificationResponse): {
  Icon: IconComponent;
  bg: string;
  fg: string;
} {
  // Source type takes precedence; fall back to notification type.
  switch (notification.sourceType) {
    case "Assignment":
      return { Icon: UserIcon, bg: "bg-blue-100", fg: "text-blue-700" };
    case "WorkflowTransition":
      return { Icon: ArrowsIcon, bg: "bg-green-100", fg: "text-green-700" };
    case "AutomationHook": {
      if (notification.type === "AUTOMATION_FAILED") {
        return { Icon: AlertIcon, bg: "bg-red-100", fg: "text-red-700" };
      }
      if (notification.type === "AUTOMATION_SUCCEEDED") {
        return { Icon: CheckCircleIcon, bg: "bg-emerald-100", fg: "text-emerald-700" };
      }
      return { Icon: GearIcon, bg: "bg-amber-100", fg: "text-amber-700" };
    }
    case "System":
      return { Icon: BellIcon, bg: "bg-gray-100", fg: "text-gray-600" };
  }

  // Fallback by type
  switch (notification.type) {
    case "TASK_ASSIGNED":
    case "TASK_REASSIGNED":
      return { Icon: UserIcon, bg: "bg-blue-100", fg: "text-blue-700" };
    case "TASK_TRANSITIONED":
      return { Icon: ArrowsIcon, bg: "bg-green-100", fg: "text-green-700" };
    case "AUTOMATION_FAILED":
      return { Icon: AlertIcon, bg: "bg-red-100", fg: "text-red-700" };
    case "AUTOMATION_SUCCEEDED":
      return { Icon: CheckCircleIcon, bg: "bg-emerald-100", fg: "text-emerald-700" };
    case "WORKFLOW_ASSIGNED":
      return { Icon: GearIcon, bg: "bg-purple-100", fg: "text-purple-700" };
    default:
      return { Icon: BellIcon, bg: "bg-gray-100", fg: "text-gray-600" };
  }
}

export function NotificationIcon({
  notification,
  size = "md",
}: {
  notification: NotificationResponse;
  size?: "sm" | "md";
}) {
  const { Icon, bg, fg } = getIconAccent(notification);
  const dim = size === "sm" ? "h-7 w-7" : "h-8 w-8";
  const inner = size === "sm" ? "h-3.5 w-3.5" : "h-4 w-4";
  return (
    <div
      className={`${dim} ${bg} ${fg} flex-shrink-0 rounded-full flex items-center justify-center`}
      aria-hidden="true"
    >
      <Icon className={inner} />
    </div>
  );
}
