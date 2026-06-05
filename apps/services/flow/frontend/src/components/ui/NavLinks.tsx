"use client";

import Link from "next/link";
import { useUnreadCount } from "@/lib/useUnreadCount";

interface NavLinksProps {
  current: "tasks" | "workflows" | "notifications" | null;
}

interface BadgeProps {
  count: number;
  ready: boolean;
}

function UnreadBadge({ count, ready }: BadgeProps) {
  if (!ready || count <= 0) return null;
  const label = count > 99 ? "99+" : String(count);
  return (
    <span
      className="ml-1 inline-flex items-center justify-center rounded-full bg-red-500 px-1.5 py-0 text-[10px] font-semibold leading-4 text-white min-w-[18px] h-[18px]"
      aria-label={`${count} unread notifications`}
      title={`${count} unread notification${count === 1 ? "" : "s"}`}
    >
      {label}
    </span>
  );
}

export function NavLinks({ current }: NavLinksProps) {
  const { count, ready } = useUnreadCount();

  const linkClass = (active: boolean) =>
    `inline-flex items-center text-sm transition-colors ${
      active ? "text-gray-900 font-medium" : "text-gray-500 hover:text-gray-700"
    }`;

  return (
    <>
      {current !== "tasks" && (
        <Link href="/tasks" className={linkClass(false)}>
          Tasks
        </Link>
      )}
      {current !== "workflows" && (
        <Link href="/workflows" className={linkClass(false)}>
          Workflows
        </Link>
      )}
      {current !== "notifications" && (
        <Link href="/notifications" className={linkClass(false)}>
          Notifications
          <UnreadBadge count={count} ready={ready} />
        </Link>
      )}
      {current === "notifications" && (
        <span className={linkClass(true)}>
          {/* still show badge even on the page itself for consistency */}
          <span className="text-gray-400">Notifications</span>
          <UnreadBadge count={count} ready={ready} />
        </span>
      )}
    </>
  );
}
