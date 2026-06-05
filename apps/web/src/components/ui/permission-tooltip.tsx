'use client';

/**
 * LS-ID-TNT-015-004: CSS tooltip wrapper for disabled UI elements.
 *
 * Wraps any action (button, link, etc.) and shows an explanation tooltip
 * when the action is unavailable. The outer `<span>` remains interactive on
 * hover even when the inner button is `disabled`, so the explanation is
 * always reachable by pointing device or keyboard.
 *
 * Keyboard access: the wrapper is focusable (`tabIndex={0}`) when the tooltip
 * is active, and the tooltip appears on `focus-within` — so Tab users see the
 * explanation without hovering.
 *
 * Hydration safety: the wrapper span is always rendered so the DOM structure
 * is consistent between server and client renders regardless of whether
 * `show` changes after session load. The tooltip bubble and interactive
 * attributes are conditionally applied via CSS + prop values only (attribute
 * changes are safe for hydration; structural changes are not).
 *
 * Usage:
 *   import { PermissionTooltip } from '@/components/ui/permission-tooltip';
 *
 *   <PermissionTooltip
 *     show={!canApprove}
 *     message="You do not have permission to approve applications."
 *   >
 *     <button disabled={!canApprove} ...>Approve</button>
 *   </PermissionTooltip>
 */

import type { ReactNode } from 'react';

interface PermissionTooltipProps {
  /** When true the tooltip is active and interactive; the wrapper is always rendered. */
  show: boolean;
  /** Human-readable explanation shown in the tooltip. Keep it concise (< 60 chars). */
  message: string;
  children: ReactNode;
  className?: string;
}

export function PermissionTooltip({
  show,
  message,
  children,
  className,
}: PermissionTooltipProps) {
  return (
    <span
      // 'group' is applied only when show=true so the CSS hover/focus triggers
      // activate. The wrapper span itself is always present in the DOM.
      className={`relative inline-flex${show ? ' group' : ''} ${className ?? ''}`.trim()}
      tabIndex={show ? 0 : undefined}
      aria-label={show ? message : undefined}
    >
      {children}

      {/*
        Tooltip bubble — always in the DOM alongside the wrapper so there is no
        structural change between server/client renders. When show=false, the
        'group' class is absent so group-hover / group-focus-within never fire,
        keeping the bubble permanently hidden.
      */}
      <span
        role="tooltip"
        aria-hidden={!show}
        className={[
          'pointer-events-none',
          'absolute bottom-full left-1/2 -translate-x-1/2 mb-2',
          'w-max max-w-[220px] px-2.5 py-1.5 rounded-md shadow-sm',
          'text-xs text-white bg-gray-800 text-center whitespace-normal leading-snug',
          'opacity-0 group-hover:opacity-100 group-focus-within:opacity-100',
          'transition-opacity duration-150 z-50',
        ].join(' ')}
      >
        {message}
        {/* Downward arrow */}
        <span
          aria-hidden="true"
          className="absolute top-full left-1/2 -translate-x-1/2 border-4 border-transparent border-t-gray-800"
        />
      </span>
    </span>
  );
}
