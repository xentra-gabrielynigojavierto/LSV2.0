'use client';

import { useEffect, useId, useMemo, useState } from 'react';
import { useRouter, useSearchParams } from 'next/navigation';
import type {
  WorkflowAdminAction,
  WorkflowInstanceDetail,
  WorkflowTimelineEvent,
  WorkflowTimelineResponse,
  WorkflowTimelineSeverity,
} from '@/types/control-center';

interface WorkflowDetailDrawerProps {
  /**
   * The currently selected workflow id (driven by the `?selected=` URL
   * param). When `null` the drawer is closed.
   */
  selectedId: string | null;

  /**
   * Server-fetched detail for `selectedId`, or `null` if the lookup
   * returned 404 / forbidden / scoped-out.
   */
  detail: WorkflowInstanceDetail | null;

  /**
   * Optional fetch error string (set by the parent page when the
   * server-side detail call threw). Drawer renders an error state
   * without breaking the underlying list.
   */
  errorMessage?: string | null;

  /**
   * E10.1 — when true, the drawer renders the Admin Actions section
   * (Retry / Force Complete / Cancel). The parent page is responsible
   * for asserting PlatformAdmin role server-side before passing this
   * flag; the BFF route re-asserts on submit.
   */
  canAdmin?: boolean;
}

const STATUS_STYLES: Record<string, string> = {
  Active:    'bg-blue-50   text-blue-700   border-blue-200',
  Pending:   'bg-amber-50  text-amber-700  border-amber-200',
  Completed: 'bg-green-50  text-green-700  border-green-200',
  Cancelled: 'bg-gray-100  text-gray-500   border-gray-200',
  Failed:    'bg-red-50    text-red-700    border-red-200',
};

const PRODUCT_LABELS: Record<string, string> = {
  FLOW_GENERIC:     'Flow',
  SYNQ_LIENS:       'SynqLien',
  SYNQ_LIEN:        'SynqLien',
  SYNQ_FUND:        'SynqFund',
  SYNQ_BILL:        'SynqBill',
  SYNQ_RX:          'SynqRx',
  SYNQ_PAYOUT:      'SynqPayout',
  SYNQ_CARECONNECT: 'CareConnect',
  CARE_CONNECT:     'CareConnect',
};

/**
 * E9.2 — Map (productKey, sourceEntityType, sourceEntityId) → tenant
 * portal route.
 *
 * Returns a relative path opened in a new tab. The tenant portal lives
 * at a different origin in production; relative navigation will resolve
 * within whichever tenant-portal subdomain the operator pastes/visits.
 * This is intentionally conservative: only product/entity combinations
 * with a verified route shape are surfaced. Everything else returns
 * null and the drawer just shows the entity id as text.
 */
function deriveSourceRecordHref(
  productKey: string | null | undefined,
  sourceEntityType: string | null | undefined,
  sourceEntityId: string | null | undefined,
): string | null {
  if (!sourceEntityId) return null;
  const type = (sourceEntityType ?? '').toLowerCase();
  const product = (productKey ?? '').toUpperCase();

  // SynqLien — case detail
  if (product === 'SYNQ_LIENS' || product === 'SYNQ_LIEN') {
    if (type === 'lien_case' || type === 'case' || type === 'lien-case') {
      return `/lien/cases/${encodeURIComponent(sourceEntityId)}`;
    }
  }

  // SynqFund — funding application detail
  if (product === 'SYNQ_FUND') {
    if (type === 'fund_application' || type === 'application' || type === 'funding_application') {
      return `/fund/applications/${encodeURIComponent(sourceEntityId)}`;
    }
  }

  // CareConnect — referral detail
  if (product === 'SYNQ_CARECONNECT' || product === 'CARE_CONNECT') {
    if (type === 'referral' || type === 'careconnect_referral') {
      return `/careconnect/referrals/${encodeURIComponent(sourceEntityId)}`;
    }
  }

  return null;
}

function formatTimestamp(iso: string | null | undefined): string {
  if (!iso) return '—';
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return '—';
  return d.toLocaleString('en-US', {
    month:  'short',
    day:    'numeric',
    year:   'numeric',
    hour:   '2-digit',
    minute: '2-digit',
  });
}

function StatusBadge({ status }: { status: string }) {
  const cls = STATUS_STYLES[status] ?? 'bg-gray-100 text-gray-600 border-gray-200';
  return (
    <span className={`inline-flex items-center px-2 py-0.5 rounded text-[11px] font-semibold border ${cls}`}>
      {status || 'Unknown'}
    </span>
  );
}

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div>
      <dt className="text-[11px] uppercase tracking-wide text-gray-400 font-semibold">{label}</dt>
      <dd className="mt-0.5 text-sm text-gray-800 break-words">{children}</dd>
    </div>
  );
}

/**
 * E10.1 — client-side eligibility for an admin action. Mirrors the
 * server-side state matrix on AdminWorkflowInstancesController so the
 * UI never offers a button the backend would reject. The server
 * remains the source of truth (any client/server divergence surfaces
 * as a `not_allowed_in_state` ProblemDetails banner inside the drawer
 * rather than silent corruption).
 */
function isActionEligible(
  action: WorkflowAdminAction,
  detail: WorkflowInstanceDetail | null,
): boolean {
  if (!detail) return false;
  const s = detail.status;
  const hasErr = !!(detail.lastErrorMessage && detail.lastErrorMessage.length > 0);
  switch (action) {
    case 'retry':
      return s === 'Failed' || ((s === 'Active' || s === 'Pending') && hasErr);
    case 'force-complete':
    case 'cancel':
      return s === 'Active' || s === 'Pending';
    default:
      return false;
  }
}

const ACTION_LABELS: Record<WorkflowAdminAction, string> = {
  'retry':          'Retry',
  'force-complete': 'Force complete',
  'cancel':         'Cancel',
};

const ACTION_INELIGIBLE_HINT: Record<WorkflowAdminAction, string> = {
  'retry':          'Retry is only available for Failed workflows or Active/Pending workflows with a captured error.',
  'force-complete': 'Force complete is only available for Active or Pending workflows.',
  'cancel':         'Cancel is only available for Active or Pending workflows.',
};

const REASON_MAX = 1000;

// ── Timeline rendering ─────────────────────────────────────────────────
//
// The timeline section is its own component so the parent stays
// focused on routing/admin-action state. All formatting is local.
//
// E13.3 extends the baseline timeline with two polish features:
//   • events are grouped under date headings ("Today", "Yesterday",
//     older formatted dates) so long histories scan quickly
//   • each row shows a severity indicator (info / warning / critical)
//     driven by the Flow audit `severity` field

const TIMELINE_CATEGORY_STYLES: Record<string, { icon: string; dot: string; label: string }> = {
  AdminAction:      { icon: 'ri-shield-user-line',     dot: 'bg-indigo-500',  label: 'Admin action'      },
  EngineTransition: { icon: 'ri-arrow-right-up-line',  dot: 'bg-blue-500',    label: 'Engine transition' },
  Lifecycle:        { icon: 'ri-flag-line',            dot: 'bg-emerald-500', label: 'Lifecycle'         },
  Task:             { icon: 'ri-checkbox-circle-line', dot: 'bg-amber-500',   label: 'Task'              },
  Other:            { icon: 'ri-circle-line',          dot: 'bg-gray-400',    label: 'Other'             },
};

/**
 * Map the upstream category string (action-key style, e.g.
 * `workflow.admin.retry`, `workflow.state_changed`, `task.assigned`)
 * to one of the high-level UI buckets used for color/iconography.
 */
function bucketFromCategory(category: string): string {
  if (category.startsWith('workflow.admin.') || category === 'workflow.admin') return 'AdminAction';
  if (category.startsWith('workflow.state'))                                   return 'EngineTransition';
  if (category.startsWith('workflow.'))                                        return 'Lifecycle';
  if (category === 'workflow')                                                 return 'Lifecycle';
  if (category.startsWith('task.') || category === 'task')                     return 'Task';
  return 'Other';
}

function categoryMeta(category: string) {
  return TIMELINE_CATEGORY_STYLES[bucketFromCategory(category)] ?? TIMELINE_CATEGORY_STYLES.Other;
}

function formatTimelineTimestamp(iso: string): string {
  if (!iso) return '—';
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return iso;
  return d.toLocaleString('en-US', {
    month:  'short',
    day:    'numeric',
    year:   'numeric',
    hour:   '2-digit',
    minute: '2-digit',
    second: '2-digit',
  });
}

/**
 * E13.3 — severity-driven visual overrides. Info events keep the
 * category dot tint unchanged (no visual noise on the common case).
 * Warning and critical add amber/red rings around the dot; critical
 * additionally gives the whole row a subtle tinted background so it
 * pops in long histories.
 */
function severityDotRing(severity: WorkflowTimelineSeverity): string {
  switch (severity) {
    case 'critical': return 'ring-2 ring-red-300';
    case 'warning':  return 'ring-2 ring-amber-300';
    case 'info':
    default:         return '';
  }
}

function severityRowClass(severity: WorkflowTimelineSeverity): string {
  switch (severity) {
    case 'critical': return 'bg-red-50/60 border-l-2 border-red-200 pl-2 -ml-2 rounded-sm';
    case 'warning':  return '';
    case 'info':
    default:         return '';
  }
}

const SEVERITY_BADGE: Record<WorkflowTimelineSeverity, { label: string; cls: string } | null> = {
  info:     null,
  warning:  { label: 'Warning',  cls: 'bg-amber-100 text-amber-800 border-amber-200' },
  critical: { label: 'Critical', cls: 'bg-red-100   text-red-700   border-red-200'   },
};

/**
 * E13.3 — group events under date headings ("Today", "Yesterday",
 * formatted older dates). Operates on the event list as returned by
 * the server; we emit groups from most-recent date to oldest and
 * reverse within each group so the newest event of the day sits at
 * the top.
 *
 * The "today / yesterday" calculation uses the operator's local time
 * zone since the drawer is operator-facing and operators reason about
 * events in their own day boundary.
 */
interface TimelineGroup {
  key:     string;
  label:   string;
  events:  WorkflowTimelineEvent[];
}

function localDateKey(d: Date): string {
  const y = d.getFullYear();
  const m = String(d.getMonth() + 1).padStart(2, '0');
  const day = String(d.getDate()).padStart(2, '0');
  return `${y}-${m}-${day}`;
}

function groupEventsByDate(events: readonly WorkflowTimelineEvent[]): TimelineGroup[] {
  const today        = new Date();
  const todayKey     = localDateKey(today);
  const yesterday    = new Date(today);
  yesterday.setDate(today.getDate() - 1);
  const yesterdayKey = localDateKey(yesterday);

  const buckets = new Map<string, { label: string; events: WorkflowTimelineEvent[] }>();
  for (const ev of events) {
    const d = new Date(ev.occurredAtUtc);
    const key = Number.isNaN(d.getTime()) ? '0000-00-00' : localDateKey(d);
    let label: string;
    if (key === todayKey)          label = 'Today';
    else if (key === yesterdayKey) label = 'Yesterday';
    else if (key === '0000-00-00') label = 'Unknown date';
    else                           label = d.toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' });

    let bucket = buckets.get(key);
    if (!bucket) {
      bucket = { label, events: [] };
      buckets.set(key, bucket);
    }
    bucket.events.push(ev);
  }

  const sortedKeys = Array.from(buckets.keys()).sort((a, b) => (a < b ? 1 : a > b ? -1 : 0));
  return sortedKeys.map(key => {
    const b = buckets.get(key)!;
    return { key, label: b.label, events: [...b.events].reverse() };
  });
}

function formatTimeOfDay(iso: string): string {
  if (!iso) return '—';
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return iso;
  return d.toLocaleTimeString('en-US', { hour: '2-digit', minute: '2-digit' });
}

function TimelineRow({ event }: { event: WorkflowTimelineEvent }) {
  const meta            = categoryMeta(event.category);
  const showTransition  = !!(event.previousStatus && event.newStatus);
  const summary         = event.summary && event.summary.length > 0
    ? event.summary
    : (showTransition ? `Status ${event.previousStatus} → ${event.newStatus}` : event.action);
  const rowCls          = severityRowClass(event.severity);
  const dotRing         = severityDotRing(event.severity);
  const badge           = SEVERITY_BADGE[event.severity];
  // Deep-link the action chip to the central Audit Logs page,
  // pre-filtered by the canonical AuditId so the operator lands on
  // the matching row (which exposes before/after JSON, request id,
  // IP, etc. via its detail panel). Falls back to a plain label
  // when the upstream did not surface an AuditId.
  const auditHref = event.auditId
    ? `/audit-logs?search=${encodeURIComponent(event.auditId)}`
    : null;

  return (
    <li
      className={`relative pl-5 py-1 ${rowCls}`}
      data-severity={event.severity}
      data-category={event.category}
    >
      <span
        className={`absolute left-0 top-2 h-2 w-2 rounded-full ${meta.dot} ${dotRing}`}
        aria-hidden="true"
      />
      <div className="flex items-baseline justify-between gap-2">
        <p className="text-sm text-gray-800 break-words">{summary}</p>
        <time
          className="shrink-0 text-[11px] text-gray-400 font-mono whitespace-nowrap"
          dateTime={event.occurredAtUtc}
          title={event.occurredAtUtc}
        >
          {formatTimeOfDay(event.occurredAtUtc)}
        </time>
      </div>
      <div className="mt-0.5 flex flex-wrap items-center gap-x-2 gap-y-1 text-[11px] text-gray-500">
        <span className="inline-flex items-center gap-1">
          <i className={`${meta.icon} text-[11px]`} aria-hidden="true" />
          {meta.label}
        </span>
        {auditHref ? (
          <a
            href={auditHref}
            target="_blank"
            rel="noopener noreferrer"
            title="Open this event in the Audit Logs"
            className="inline-flex items-center gap-1 font-mono text-gray-500 hover:text-indigo-600 hover:underline"
          >
            {event.action}
            <i className="ri-external-link-line text-[10px]" aria-hidden="true" />
          </a>
        ) : (
          <span className="font-mono text-gray-400">{event.action}</span>
        )}
        {showTransition && (
          <span className="inline-flex items-center gap-1 rounded bg-gray-100 px-1.5 py-0.5 font-mono text-[10px] text-gray-600">
            {event.previousStatus} → {event.newStatus}
          </span>
        )}
        {event.performedBy && (
          <span className="inline-flex items-center gap-1 text-gray-500">
            <i className="ri-user-line text-[11px]" aria-hidden="true" />
            <span className="font-mono">{event.performedBy}</span>
          </span>
        )}
        {badge && (
          <span className={`inline-flex items-center rounded border px-1.5 py-0.5 text-[10px] font-semibold ${badge.cls}`}>
            {badge.label}
          </span>
        )}
      </div>
    </li>
  );
}

// All possible category buckets surfaced as filter chips. We only
// render chips for buckets actually present in the fetched payload so
// the filter row stays compact for short timelines.
const TIMELINE_BUCKET_ORDER: ReadonlyArray<keyof typeof TIMELINE_CATEGORY_STYLES> = [
  'AdminAction',
  'EngineTransition',
  'Lifecycle',
  'Task',
  'Other',
];

function FilterChip({
  active,
  onClick,
  children,
  count,
  testId,
}: {
  active:   boolean;
  onClick:  () => void;
  children: React.ReactNode;
  count?:   number;
  testId?:  string;
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      aria-pressed={active}
      data-testid={testId}
      className={
        'inline-flex items-center gap-1 rounded-full border px-2 py-0.5 text-[11px] transition-colors ' +
        (active
          ? 'border-indigo-500 bg-indigo-50 text-indigo-700'
          : 'border-gray-200 bg-white text-gray-600 hover:border-gray-300 hover:bg-gray-50')
      }
    >
      <span>{children}</span>
      {typeof count === 'number' && (
        <span className={active ? 'text-indigo-500' : 'text-gray-400'}>{count}</span>
      )}
    </button>
  );
}

function TimelineSection({
  loading,
  error,
  timeline,
  onRetry,
}: {
  loading:  boolean;
  error:    string | null;
  timeline: WorkflowTimelineResponse | null;
  onRetry:  () => void;
}) {
  // Client-side filters. Reset whenever the underlying payload changes
  // (e.g. after a Refresh) so a stale "Cancel actor X" filter doesn't
  // wipe out a freshly refetched timeline that no longer contains X.
  const [bucketFilter, setBucketFilter] = useState<string | null>(null);
  const [actorFilter,  setActorFilter]  = useState<string | null>(null);

  useEffect(() => {
    setBucketFilter(null);
    setActorFilter(null);
  }, [timeline]);

  const events = timeline?.events ?? [];

  // Tally bucket + actor occurrences in one pass so chips can show
  // counts and we never render a chip that filters everything out.
  const { bucketCounts, actorCounts } = useMemo(() => {
    const buckets = new Map<string, number>();
    const actors  = new Map<string, number>();
    for (const ev of events) {
      const b = bucketFromCategory(ev.category);
      buckets.set(b, (buckets.get(b) ?? 0) + 1);
      if (ev.performedBy) {
        actors.set(ev.performedBy, (actors.get(ev.performedBy) ?? 0) + 1);
      }
    }
    return { bucketCounts: buckets, actorCounts: actors };
  }, [events]);

  const visibleEvents = useMemo(() => {
    if (!bucketFilter && !actorFilter) return events;
    return events.filter((ev) => {
      if (bucketFilter && bucketFromCategory(ev.category) !== bucketFilter) return false;
      if (actorFilter  && ev.performedBy !== actorFilter)                   return false;
      return true;
    });
  }, [events, bucketFilter, actorFilter]);

  const filtersActive  = !!(bucketFilter || actorFilter);
  const totalEvents    = events.length;
  const filteredOut    = totalEvents - visibleEvents.length;

  // Sort actors by frequency (desc) then alpha so the chip row is
  // stable and most-common operators appear first.
  const actorList = useMemo(() => {
    return Array.from(actorCounts.entries()).sort((a, b) => {
      if (b[1] !== a[1]) return b[1] - a[1];
      return a[0].localeCompare(b[0]);
    });
  }, [actorCounts]);

  // Date grouping runs over the *filtered* event set so date headers
  // never appear empty when the operator narrows by category/actor.
  const groups = useMemo(() => groupEventsByDate(visibleEvents), [visibleEvents]);

  return (
    <section className="space-y-3" data-testid="workflow-timeline">
      <div className="flex items-center justify-between">
        <h3 className="text-[11px] uppercase tracking-wide text-gray-500 font-semibold">Timeline</h3>
        <div className="flex items-center gap-3">
          {timeline && timeline.events.length > 0 && (
            <span className="text-[11px] text-gray-400">
              {timeline.events.length}{timeline.truncated ? '+' : ''} event{timeline.events.length === 1 ? '' : 's'}
            </span>
          )}
          {!loading && (
            <button
              type="button"
              onClick={onRetry}
              className="text-[11px] text-gray-400 hover:text-gray-700 hover:underline"
            >
              Refresh
            </button>
          )}
        </div>
      </div>

      {loading && (
        <div className="rounded-md border border-gray-200 bg-gray-50 px-3 py-3 text-xs text-gray-500">
          Loading timeline…
        </div>
      )}

      {!loading && error && (
        <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-xs text-red-700">
          {error}
        </div>
      )}

      {!loading && !error && timeline && totalEvents === 0 && (
        <div className="rounded-md border border-gray-200 bg-gray-50 px-3 py-2 text-xs text-gray-500">
          No audit events have been recorded for this workflow yet.
        </div>
      )}

      {!loading && !error && timeline && totalEvents > 0 && (
        <div className="space-y-4">
          {timeline.truncated && (
            <div className="rounded-md border border-amber-200 bg-amber-50 px-3 py-2 text-[11px] text-amber-800">
              Showing the most recent events only — open the audit service for the full record.
            </div>
          )}

          {/* Filter chips. Rendered only when there's something to
              filter on — a single-category, single-actor timeline
              has nothing meaningful to narrow. */}
          {(bucketCounts.size > 1 || actorCounts.size > 1) && (
            <div className="space-y-2" data-testid="workflow-timeline-filters">
              {bucketCounts.size > 1 && (
                <div className="flex flex-wrap items-center gap-1.5">
                  <span className="text-[10px] uppercase tracking-wide text-gray-400 font-semibold mr-1">
                    Category
                  </span>
                  <FilterChip
                    active={bucketFilter === null}
                    onClick={() => setBucketFilter(null)}
                    count={totalEvents}
                    testId="timeline-filter-category-all"
                  >
                    All
                  </FilterChip>
                  {TIMELINE_BUCKET_ORDER.filter((b) => bucketCounts.has(b)).map((b) => (
                    <FilterChip
                      key={b}
                      active={bucketFilter === b}
                      onClick={() => setBucketFilter(bucketFilter === b ? null : b)}
                      count={bucketCounts.get(b) ?? 0}
                      testId={`timeline-filter-category-${b}`}
                    >
                      {TIMELINE_CATEGORY_STYLES[b].label}
                    </FilterChip>
                  ))}
                </div>
              )}
              {actorCounts.size > 1 && (
                <div className="flex flex-wrap items-center gap-1.5">
                  <span className="text-[10px] uppercase tracking-wide text-gray-400 font-semibold mr-1">
                    Actor
                  </span>
                  <FilterChip
                    active={actorFilter === null}
                    onClick={() => setActorFilter(null)}
                    testId="timeline-filter-actor-all"
                  >
                    All
                  </FilterChip>
                  {actorList.map(([actor, count]) => (
                    <FilterChip
                      key={actor}
                      active={actorFilter === actor}
                      onClick={() => setActorFilter(actorFilter === actor ? null : actor)}
                      count={count}
                      testId={`timeline-filter-actor-${actor}`}
                    >
                      <span className="font-mono">{actor}</span>
                    </FilterChip>
                  ))}
                </div>
              )}
            </div>
          )}

          {visibleEvents.length === 0 ? (
            <div
              className="rounded-md border border-gray-200 bg-gray-50 px-3 py-2 text-xs text-gray-500 flex items-center justify-between gap-3"
              data-testid="workflow-timeline-empty-filtered"
            >
              <span>
                No events match the active filters
                {filteredOut > 0 ? ` (${filteredOut} hidden)` : ''}.
              </span>
              <button
                type="button"
                onClick={() => { setBucketFilter(null); setActorFilter(null); }}
                className="text-[11px] text-indigo-600 hover:underline"
              >
                Clear filters
              </button>
            </div>
          ) : (
            <>
              {filtersActive && filteredOut > 0 && (
                <p className="text-[11px] text-gray-400">
                  Showing {visibleEvents.length} of {totalEvents} events.
                </p>
              )}
              {groups.map(group => (
                <div key={group.key} className="space-y-2">
                  <div className="sticky top-0 bg-white pt-1 pb-1 z-[1]">
                    <h4 className="flex items-center gap-2 text-[10px] font-semibold uppercase tracking-wider text-gray-400">
                      {group.label}
                      <span className="flex-1 border-t border-gray-100" />
                      <span className="text-[10px] font-normal normal-case text-gray-300">
                        {group.events.length} event{group.events.length === 1 ? '' : 's'}
                      </span>
                    </h4>
                  </div>
                  <ol className="relative space-y-1 border-l border-gray-200 pl-3 ml-1">
                    {group.events.map((ev, idx) => (
                      <TimelineRow key={ev.eventId || `${ev.occurredAtUtc}-${ev.action}-${idx}`} event={ev} />
                    ))}
                  </ol>
                </div>
              ))}
            </>
          )}
        </div>
      )}
    </section>
  );
}

export function WorkflowDetailDrawer({
  selectedId,
  detail,
  errorMessage,
  canAdmin = false,
}: WorkflowDetailDrawerProps) {
  const titleId = useId();
  const router  = useRouter();
  const params  = useSearchParams();

  const isOpen = !!selectedId;

  // E10.1 — admin action panel state. `pendingAction` controls which
  // confirmation panel is open; only one can be active at a time so
  // operators cannot accidentally fire two state transitions in
  // rapid succession.
  const [pendingAction, setPendingAction] = useState<WorkflowAdminAction | null>(null);
  const [reason,        setReason]        = useState('');
  const [submitting,    setSubmitting]    = useState(false);
  const [actionError,   setActionError]   = useState<string | null>(null);

  // Audit timeline — fetched lazily via the BFF when a drawer target is
  // selected. Kept separate from the server-hydrated `detail` prop
  // because (a) the audit service is a different upstream and (b) we
  // want admin-action feedback to refresh just the timeline without
  // a full page reload.
  const [timeline,      setTimeline]      = useState<WorkflowTimelineResponse | null>(null);
  const [timelineLoading, setTimelineLoading] = useState(false);
  const [timelineError, setTimelineError] = useState<string | null>(null);
  const [timelineNonce, setTimelineNonce] = useState(0);

  // Reset action panel whenever the drawer changes target.
  useEffect(() => {
    setPendingAction(null);
    setReason('');
    setSubmitting(false);
    setActionError(null);
    setTimeline(null);
    setTimelineError(null);
  }, [selectedId]);

  // Lazy-load the audit timeline whenever the drawer changes target
  // (or an admin action bumps `timelineNonce` to ask for a refetch).
  // Cancellation guard prevents a stale response from a previously
  // selected workflow overwriting a newer one if the operator clicks
  // through the list quickly.
  useEffect(() => {
    if (!selectedId) return;
    let cancelled = false;
    setTimelineLoading(true);
    setTimelineError(null);
    fetch(`/api/admin/workflow-instances/${encodeURIComponent(selectedId)}/timeline`, {
      headers: { Accept: 'application/json' },
      cache:   'no-store',
    })
      .then(async (res) => {
        if (cancelled) return;
        if (!res.ok) {
          let msg = `Timeline failed (HTTP ${res.status}).`;
          try {
            const body = (await res.json()) as { error?: string };
            if (body?.error) msg = body.error;
          } catch { /* keep default */ }
          setTimeline(null);
          setTimelineError(msg);
          setTimelineLoading(false);
          return;
        }
        const body = (await res.json()) as WorkflowTimelineResponse;
        setTimeline(body);
        setTimelineLoading(false);
      })
      .catch((err) => {
        if (cancelled) return;
        setTimeline(null);
        setTimelineError(err instanceof Error ? err.message : 'Network error.');
        setTimelineLoading(false);
      });
    return () => { cancelled = true; };
  }, [selectedId, timelineNonce]);

  // Build the close target: same URL minus `?selected=`. Preserves all
  // other filter params so the operator returns to the same list view.
  const closeHref = useMemo(() => {
    const next = new URLSearchParams(params?.toString() ?? '');
    next.delete('selected');
    const qs = next.toString();
    return qs ? `?${qs}` : '?';
  }, [params]);

  function handleClose() {
    router.replace(closeHref, { scroll: false });
  }

  // ESC-to-close (parity with the Tenants modal pattern).
  useEffect(() => {
    if (!isOpen) return;
    function onKey(e: KeyboardEvent) {
      if (e.key === 'Escape') handleClose();
    }
    document.addEventListener('keydown', onKey);
    return () => document.removeEventListener('keydown', onKey);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isOpen, closeHref]);

  if (!isOpen) return null;

  const productLabel  = detail ? (PRODUCT_LABELS[detail.productKey] ?? detail.productKey) : '—';
  const sourceHref    = detail
    ? deriveSourceRecordHref(detail.productKey, detail.sourceEntityType, detail.sourceEntityId)
    : null;

  return (
    <div
      className="fixed inset-0 z-50 flex"
      role="dialog"
      aria-modal="true"
      aria-labelledby={titleId}
    >
      {/* Backdrop */}
      <button
        type="button"
        aria-label="Close workflow detail"
        onClick={handleClose}
        className="flex-1 bg-gray-900/30 backdrop-blur-[1px] cursor-default"
      />

      {/* Panel */}
      <aside className="w-full sm:w-[28rem] md:w-[32rem] bg-white border-l border-gray-200 shadow-xl flex flex-col">
        {/* Header */}
        <header className="px-5 py-4 border-b border-gray-100 flex items-start justify-between gap-3">
          <div className="min-w-0">
            <p className="text-[11px] uppercase tracking-wide text-gray-400 font-semibold">Workflow detail</p>
            <h2 id={titleId} className="mt-0.5 text-base font-semibold text-gray-900 truncate">
              {detail?.workflowName ?? (selectedId ? 'Loading…' : 'Workflow')}
            </h2>
            <p className="mt-0.5 text-[11px] text-gray-400 font-mono truncate">{selectedId}</p>
          </div>
          <button
            type="button"
            onClick={handleClose}
            className="rounded-md p-1.5 text-gray-400 hover:text-gray-700 hover:bg-gray-100 transition-colors"
            aria-label="Close"
          >
            <i className="ri-close-line text-lg leading-none" aria-hidden="true" />
          </button>
        </header>

        {/* Body */}
        <div className="flex-1 overflow-y-auto px-5 py-5 space-y-6">
          {errorMessage && (
            <div className="bg-red-50 border border-red-200 rounded-md px-3 py-2 text-sm text-red-700">
              {errorMessage}
            </div>
          )}

          {!errorMessage && !detail && (
            <div className="bg-amber-50 border border-amber-200 rounded-md px-3 py-2 text-sm text-amber-800">
              This workflow is no longer available, or you do not have visibility into it.
            </div>
          )}

          {detail && (
            <>
              {/* Summary */}
              <section className="space-y-3">
                <h3 className="text-[11px] uppercase tracking-wide text-gray-500 font-semibold">Summary</h3>
                <dl className="grid grid-cols-2 gap-x-4 gap-y-3">
                  <Field label="Status"><StatusBadge status={detail.status} /></Field>
                  <Field label="Product">{productLabel}</Field>
                  <Field label="Current step">
                    {detail.currentStepName ?? detail.currentStepKey ?? '—'}
                    {detail.currentStepName && detail.currentStepKey && (
                      <span className="ml-1 text-[11px] text-gray-400 font-mono">({detail.currentStepKey})</span>
                    )}
                  </Field>
                  <Field label="Tenant">
                    <span className="font-mono text-[12px]">{detail.tenantId}</span>
                  </Field>
                </dl>
              </section>

              {/* Source Context */}
              <section className="space-y-3">
                <h3 className="text-[11px] uppercase tracking-wide text-gray-500 font-semibold">Source Context</h3>
                <dl className="space-y-3">
                  <Field label="Source entity">
                    {detail.sourceEntityType
                      ? (
                        <div className="space-y-1">
                          <p>{detail.sourceEntityType}</p>
                          <p className="font-mono text-[12px] text-gray-500 break-all">{detail.sourceEntityId ?? '—'}</p>
                          {sourceHref && (
                            <a
                              href={sourceHref}
                              target="_blank"
                              rel="noopener noreferrer"
                              className="inline-flex items-center gap-1 text-xs text-indigo-600 hover:underline"
                            >
                              Open source record
                              <i className="ri-external-link-line text-[12px]" aria-hidden="true" />
                            </a>
                          )}
                        </div>
                      )
                      : <span className="text-gray-400">—</span>}
                  </Field>
                  <Field label="Correlation key">
                    {detail.correlationKey ?? <span className="text-gray-400">—</span>}
                  </Field>
                </dl>
              </section>

              {/* Lifecycle */}
              <section className="space-y-3">
                <h3 className="text-[11px] uppercase tracking-wide text-gray-500 font-semibold">Lifecycle</h3>
                <dl className="grid grid-cols-2 gap-x-4 gap-y-3">
                  <Field label="Started">{formatTimestamp(detail.startedAt)}</Field>
                  <Field label="Updated">{formatTimestamp(detail.updatedAt ?? detail.createdAt)}</Field>
                  <Field label="Completed">{formatTimestamp(detail.completedAt)}</Field>
                  <Field label="Assigned">
                    {detail.assignedToUserId
                      ? <span className="font-mono text-[12px]">{detail.assignedToUserId}</span>
                      : <span className="text-gray-400">—</span>}
                  </Field>
                </dl>
              </section>

              {/* E13.1 / E13.2 / E13.3 — Workflow Timeline */}
              <TimelineSection
                loading={timelineLoading}
                error={timelineError}
                timeline={timeline}
                onRetry={() => setTimelineNonce((n) => n + 1)}
              />

              {/* E10.1 — Admin Actions */}
              {canAdmin && (
                <section className="space-y-3" data-testid="workflow-admin-actions">
                  <h3 className="text-[11px] uppercase tracking-wide text-gray-500 font-semibold">Admin actions</h3>
                  <p className="text-[11px] text-gray-500">
                    Override the workflow engine. Every action is recorded with your reason in the audit log.
                  </p>

                  <div className="flex flex-wrap gap-2">
                    {(['retry', 'force-complete', 'cancel'] as WorkflowAdminAction[]).map((a) => {
                      const eligible = isActionEligible(a, detail);
                      const isOpenForThis = pendingAction === a;
                      const tone =
                        a === 'cancel'
                          ? 'border-red-300 text-red-700 hover:bg-red-50'
                          : a === 'force-complete'
                            ? 'border-amber-300 text-amber-800 hover:bg-amber-50'
                            : 'border-indigo-300 text-indigo-700 hover:bg-indigo-50';
                      return (
                        <button
                          key={a}
                          type="button"
                          disabled={!eligible || submitting}
                          aria-pressed={isOpenForThis}
                          title={eligible ? '' : ACTION_INELIGIBLE_HINT[a]}
                          onClick={() => {
                            if (!eligible) return;
                            setActionError(null);
                            setReason('');
                            setPendingAction(isOpenForThis ? null : a);
                          }}
                          className={`text-xs font-medium px-2.5 py-1.5 rounded-md border bg-white transition-colors disabled:opacity-40 disabled:cursor-not-allowed ${tone} ${isOpenForThis ? 'ring-2 ring-offset-1 ring-indigo-300' : ''}`}
                        >
                          {ACTION_LABELS[a]}
                        </button>
                      );
                    })}
                  </div>

                  {pendingAction && (
                    <div className="rounded-md border border-gray-200 bg-gray-50 p-3 space-y-2">
                      <label className="block text-[11px] uppercase tracking-wide text-gray-500 font-semibold" htmlFor={`${titleId}-reason`}>
                        Reason for {ACTION_LABELS[pendingAction].toLowerCase()}
                      </label>
                      <textarea
                        id={`${titleId}-reason`}
                        value={reason}
                        onChange={(e) => setReason(e.target.value.slice(0, REASON_MAX))}
                        rows={3}
                        maxLength={REASON_MAX}
                        placeholder="Required. Recorded in the audit log."
                        className="w-full rounded-md border border-gray-300 bg-white px-2 py-1.5 text-sm text-gray-800 focus:outline-none focus:ring-2 focus:ring-indigo-200 focus:border-indigo-300"
                      />
                      <div className="flex items-center justify-between text-[11px] text-gray-400">
                        <span>{reason.length} / {REASON_MAX}</span>
                        <span>This action cannot be undone.</span>
                      </div>

                      {actionError && (
                        <div className="bg-red-50 border border-red-200 rounded px-2 py-1.5 text-xs text-red-700">
                          {actionError}
                        </div>
                      )}

                      <div className="flex items-center justify-end gap-2 pt-1">
                        <button
                          type="button"
                          onClick={() => { setPendingAction(null); setReason(''); setActionError(null); }}
                          disabled={submitting}
                          className="text-xs px-2.5 py-1.5 rounded-md border border-gray-200 bg-white text-gray-700 hover:bg-gray-50 disabled:opacity-40"
                        >
                          Dismiss
                        </button>
                        <button
                          type="button"
                          disabled={submitting || reason.trim().length === 0 || !detail}
                          onClick={async () => {
                            if (!detail) return;
                            const trimmed = reason.trim();
                            if (trimmed.length === 0) {
                              setActionError('A reason is required.');
                              return;
                            }
                            setSubmitting(true);
                            setActionError(null);
                            try {
                              const res = await fetch(
                                `/api/admin/workflow-instances/${encodeURIComponent(detail.id)}/${pendingAction}`,
                                {
                                  method:  'POST',
                                  headers: { 'Content-Type': 'application/json' },
                                  body:    JSON.stringify({ reason: trimmed }),
                                },
                              );
                              if (!res.ok) {
                                let msg = `Action failed (HTTP ${res.status}).`;
                                try {
                                  const body = (await res.json()) as { error?: string; detail?: string; title?: string };
                                  msg = body.error ?? body.detail ?? body.title ?? msg;
                                } catch { /* keep default */ }
                                setActionError(msg);
                                setSubmitting(false);
                                return;
                              }
                              setSubmitting(false);
                              setPendingAction(null);
                              setReason('');
                              setTimelineNonce((n) => n + 1);
                              router.refresh();
                            } catch (err) {
                              setActionError(err instanceof Error ? err.message : 'Network error.');
                              setSubmitting(false);
                            }
                          }}
                          className="text-xs px-2.5 py-1.5 rounded-md bg-indigo-600 text-white hover:bg-indigo-700 disabled:opacity-50 disabled:cursor-not-allowed"
                        >
                          {submitting ? 'Applying…' : `Confirm ${ACTION_LABELS[pendingAction].toLowerCase()}`}
                        </button>
                      </div>
                    </div>
                  )}
                </section>
              )}

              {/* Diagnostics */}
              {(detail.lastErrorMessage || detail.status === 'Failed' || detail.status === 'Cancelled') && (
                <section className="space-y-3">
                  <h3 className="text-[11px] uppercase tracking-wide text-gray-500 font-semibold">Diagnostics</h3>
                  {detail.lastErrorMessage ? (
                    <pre className="bg-red-50 border border-red-200 rounded-md px-3 py-2 text-xs text-red-700 whitespace-pre-wrap break-words">
                      {detail.lastErrorMessage}
                    </pre>
                  ) : (
                    <p className="text-xs text-gray-500">
                      No engine error captured. Workflow ended in <strong>{detail.status}</strong> state.
                    </p>
                  )}
                </section>
              )}
            </>
          )}
        </div>

        {/* Footer */}
        <footer className="px-5 py-3 border-t border-gray-100 text-[11px] text-gray-400">
          {canAdmin
            ? 'Admin overrides bypass the workflow engine. Use sparingly.'
            : 'Read-only inspection. Execution actions remain on the product surface.'}
        </footer>
      </aside>
    </div>
  );
}
