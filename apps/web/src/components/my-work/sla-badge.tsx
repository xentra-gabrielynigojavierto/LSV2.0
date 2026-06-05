'use client';

import type { WorkflowTaskSlaStatus } from '@/lib/tasks';

/**
 * LS-FLOW-E10.3 (task slice) — visual chip for the task SLA classification.
 *
 * The wire enum (`OnTrack` / `DueSoon` / `Overdue` / `Escalated`) maps to
 * the spec's UI vocabulary:
 *   OnTrack   → "On Track"   (neutral)
 *   DueSoon   → "At Risk"    (amber)
 *   Overdue   → "Overdue"    (red)
 *   Escalated → "Escalated"  (red, bold) — tasks never carry this in
 *               this phase but we keep the visual mapping for symmetry
 *               with the workflow-level SLA badge.
 *
 * Rendered as `null` when there is no `dueAt` / no SLA classification.
 * That keeps the badge invisible for legacy or SLA-disabled tasks
 * instead of mis-claiming "On Track" for rows that never had a clock.
 */
export interface SlaBadgeProps {
  status?: WorkflowTaskSlaStatus | null;
  /** When provided, the badge prefers a remaining/overdue countdown over the static label. */
  dueAt?: string | null;
  /** Hide the dot marker (used in compact layouts). */
  compact?: boolean;
}

const STYLES: Record<WorkflowTaskSlaStatus, { label: string; cls: string }> = {
  OnTrack:   { label: 'On Track',  cls: 'bg-gray-100 text-gray-700 border-gray-200' },
  DueSoon:   { label: 'At Risk',   cls: 'bg-amber-50 text-amber-800 border-amber-200' },
  Overdue:   { label: 'Overdue',   cls: 'bg-red-50 text-red-700 border-red-200' },
  Escalated: { label: 'Escalated', cls: 'bg-red-100 text-red-800 border-red-300 font-semibold' },
};

/**
 * Coarse human countdown to/since a deadline. Resolution is tuned for
 * task-grain SLAs — minutes under an hour, hours under a day, days
 * thereafter. Prefix is "in" for future, "ago" for past.
 */
function fmtRelative(now: Date, target: Date): string {
  const diffMs  = target.getTime() - now.getTime();
  const past    = diffMs < 0;
  const absMins = Math.round(Math.abs(diffMs) / 60_000);

  let body: string;
  if (absMins < 60)            body = `${absMins}m`;
  else if (absMins < 60 * 24)  body = `${Math.round(absMins / 60)}h`;
  else                         body = `${Math.round(absMins / (60 * 24))}d`;

  return past ? `${body} ago` : `in ${body}`;
}

export function SlaBadge({ status, dueAt, compact }: SlaBadgeProps) {
  if (!status || !dueAt) return null;

  const style = STYLES[status] ?? STYLES.OnTrack;

  // Render the deadline relative to now ONLY for non-OnTrack statuses;
  // an "in 4h" countdown next to a green chip adds noise but next to
  // an amber/red chip is genuinely useful.
  let suffix: string | null = null;
  if (status !== 'OnTrack') {
    try {
      suffix = fmtRelative(new Date(), new Date(dueAt));
    } catch {
      suffix = null;
    }
  }

  return (
    <span
      className={
        'inline-flex items-center gap-1 px-2 py-0.5 text-[10px] font-medium uppercase tracking-wide rounded-full border ' +
        style.cls
      }
      title={dueAt ? `Due ${new Date(dueAt).toLocaleString()}` : undefined}
    >
      {!compact && (
        <span
          className={
            'inline-block w-1.5 h-1.5 rounded-full ' +
            (status === 'OnTrack'
              ? 'bg-gray-400'
              : status === 'DueSoon'
              ? 'bg-amber-500'
              : 'bg-red-500')
          }
          aria-hidden="true"
        />
      )}
      <span>{style.label}</span>
      {suffix && <span className="font-normal normal-case opacity-80">· {suffix}</span>}
    </span>
  );
}
