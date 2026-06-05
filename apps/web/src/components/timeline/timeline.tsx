'use client';

/**
 * LS-FLOW-E16 — reusable, presentational timeline list.
 *
 * Consumes the {@link TimelineEvent} shape returned by the Flow
 * service (`/workflow-tasks/{id}/timeline`,
 * `/workflow-instances/{id}/timeline`). Renders an oldest-first
 * vertical rail of events with a category dot, friendly summary,
 * actor, and timestamp. Optional metadata key/value chips render
 * only when the event has metadata.
 *
 * The component is intentionally simple — no per-category filter
 * chips, no virtualisation. Lists for a single task or workflow are
 * expected to be small (≲ a few hundred rows). The Control Center
 * already ships a richer in-house TimelineSection for admin use;
 * this component is sized for the tenant-portal task drawer.
 */

import { useMemo, useState } from 'react';
import type { TimelineEvent } from '@/lib/timeline';

export interface TimelineProps {
  events: TimelineEvent[];
  loading?: boolean;
  error?: string | null;
  truncated?: boolean;
  /** Compact vertical rhythm; useful inside a drawer. */
  dense?: boolean;
}

export function Timeline({
  events,
  loading = false,
  error = null,
  truncated = false,
  dense = false,
}: TimelineProps) {
  if (loading) return <TimelineSkeleton />;
  if (error)   return <TimelineError message={error} />;
  if (events.length === 0) return <TimelineEmpty />;

  return (
    <div className={dense ? 'space-y-1.5' : 'space-y-2.5'}>
      {events.map((e) => (
        <TimelineRow key={e.eventId} event={e} dense={dense} />
      ))}
      {truncated && (
        <p className="text-[11px] italic text-amber-700 mt-2">
          History may be incomplete — additional older events exist in
          the audit service.
        </p>
      )}
    </div>
  );
}

// ── Single row ───────────────────────────────────────────────────────

function TimelineRow({ event, dense }: { event: TimelineEvent; dense: boolean }) {
  const [open, setOpen] = useState(false);
  const meta = useMemo(() => filterDisplayMetadata(event.metadata), [event.metadata]);
  const hasDetails = meta.length > 0
    || !!event.previousStatus
    || !!event.newStatus;

  const summary = event.summary?.trim() || event.action;
  const actor   = formatActor(event);
  const stamp   = formatStamp(event.occurredAtUtc);

  return (
    <div className="flex items-start gap-2.5">
      <CategoryDot category={event.category} />
      <div className="flex-1 min-w-0">
        <div className={'flex items-baseline gap-2 ' + (dense ? 'text-xs' : 'text-sm')}>
          <span className="font-medium text-gray-800 break-words">{summary}</span>
          <span className="text-[11px] text-gray-500 shrink-0" title={event.occurredAtUtc}>
            {stamp}
          </span>
        </div>
        <div className="text-[11px] text-gray-500 mt-0.5">
          {actor}
          {(event.previousStatus || event.newStatus) && (
            <span className="ml-2">
              <code className="text-gray-700">{event.previousStatus ?? '—'}</code>
              {' → '}
              <code className="text-gray-700">{event.newStatus ?? '—'}</code>
            </span>
          )}
          {hasDetails && (
            <button
              type="button"
              onClick={() => setOpen((v) => !v)}
              className="ml-2 text-indigo-600 hover:text-indigo-700"
            >
              {open ? 'Hide details' : 'Details'}
            </button>
          )}
        </div>
        {open && hasDetails && (
          <dl className="mt-1.5 grid grid-cols-[max-content_1fr] gap-x-2 gap-y-0.5 text-[11px] bg-gray-50 border border-gray-200 rounded px-2 py-1.5">
            <dt className="text-gray-500">action</dt>
            <dd className="text-gray-800 font-mono break-all">{event.action}</dd>
            {meta.map(([k, v]) => (
              <Fragment key={k}>
                <dt className="text-gray-500">{k}</dt>
                <dd className="text-gray-800 font-mono break-all">{v}</dd>
              </Fragment>
            ))}
          </dl>
        )}
      </div>
    </div>
  );
}

// React fragment without importing React namespace
function Fragment({ children }: { children: React.ReactNode }) {
  return <>{children}</>;
}

// ── Pieces ────────────────────────────────────────────────────────────

function CategoryDot({ category }: { category: string }) {
  const color = categoryColor(category);
  return (
    <div className="flex flex-col items-center pt-1.5 shrink-0">
      <span
        className={'inline-block h-2 w-2 rounded-full ' + color}
        aria-hidden="true"
      />
      <span className="block w-px flex-1 bg-gray-200 mt-1 min-h-[1rem]" />
    </div>
  );
}

function TimelineSkeleton() {
  return (
    <div className="space-y-2 animate-pulse">
      {[0, 1, 2].map((i) => (
        <div key={i} className="flex items-start gap-2">
          <div className="h-2 w-2 rounded-full bg-gray-200 mt-1.5" />
          <div className="flex-1">
            <div className="h-3 bg-gray-100 rounded w-2/3" />
            <div className="h-2.5 bg-gray-100 rounded w-1/3 mt-1.5" />
          </div>
        </div>
      ))}
    </div>
  );
}

function TimelineEmpty() {
  return (
    <p className="text-xs italic text-gray-500">
      No history recorded yet.
    </p>
  );
}

function TimelineError({ message }: { message: string }) {
  return (
    <p className="text-xs text-red-600">
      Couldn’t load history: {message}
    </p>
  );
}

// ── Helpers ───────────────────────────────────────────────────────────

function categoryColor(category: string): string {
  if (category.startsWith('workflow.admin'))      return 'bg-amber-500';
  if (category.startsWith('workflow.sla'))        return 'bg-red-500';
  if (category.startsWith('workflow.task.claim') ||
      category.startsWith('workflow.task.reassign') ||
      category === 'task.assigned')               return 'bg-blue-500';
  if (category === 'task.completed' ||
      category === 'workflow.completed')          return 'bg-emerald-500';
  if (category.startsWith('workflow'))            return 'bg-indigo-500';
  if (category === 'task' || category === 'notification') return 'bg-gray-400';
  return 'bg-gray-300';
}

function formatActor(e: TimelineEvent): string {
  const name = e.performedBy ?? e.actor?.name ?? e.actor?.id;
  if (name) return `by ${name}`;
  if (e.source === 'flow') return 'by System';
  return e.source ? `by ${e.source}` : 'by Unknown';
}

function formatStamp(iso: string): string {
  try {
    return new Date(iso).toLocaleString();
  } catch {
    return iso;
  }
}

// Drop noise + known-empty keys from the displayed metadata bag so the
// detail panel highlights useful information. Fully-empty values are
// already excluded by the backend; this filter just removes keys that
// duplicate columns already shown elsewhere on the row.
function filterDisplayMetadata(
  meta: Record<string, string | null>,
): Array<[string, string]> {
  const HIDE = new Set([
    'sourceSystem', 'sourceService',
    'previousStatus', 'newStatus',
  ]);
  const out: Array<[string, string]> = [];
  for (const [k, v] of Object.entries(meta)) {
    if (v == null || v === '') continue;
    if (HIDE.has(k)) continue;
    out.push([k, v]);
  }
  return out;
}
