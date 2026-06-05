'use client';

import { useCallback, useEffect, useRef, useState } from 'react';
import { ApiError } from '@/lib/api-client';
import { tasksApi, type MyTask } from '@/lib/tasks';
import { timelineApi, type TimelineEvent } from '@/lib/timeline';
import { Timeline } from '@/components/timeline/timeline';
import { useToast } from '@/lib/toast-context';
import { TaskPriorityBadge } from './priority-badge';
import { ReassignModal } from './reassign-modal';
import { SlaBadge } from './sla-badge';
import { TaskStatusBadge } from './status-badge';

/**
 * LS-FLOW-E15 — slide-over drawer for inspecting a single task and
 * acting on it (Claim / Reassign).
 *
 * <p>The drawer owns its own data load via GET /workflow-tasks/{id};
 * the parent only passes the task id and the actor capabilities.
 * This keeps list state in the background and the drawer a
 * single-source-of-truth for the row it is showing.</p>
 *
 * <p>Lifecycle actions (Start/Complete/Cancel) are intentionally NOT
 * surfaced here — the My Tasks rows already carry them and bringing
 * lifecycle into queue rows is out of scope for E15.</p>
 */
export interface TaskDetailDrawerProps {
  taskId: string | null;
  /** True when the calling user is platform-admin or tenant-admin. */
  canReassign: boolean;
  onClose: () => void;
  /**
   * Called after a successful claim/reassign so the parent can
   * refetch the relevant list (My Tasks + the queue this task came
   * from).
   */
  onTaskMutated: () => void;
}

function fmtDate(iso: string | null | undefined): string {
  if (!iso) return '—';
  try {
    return new Date(iso).toLocaleString();
  } catch {
    return iso;
  }
}

const MODE_LABEL: Record<string, string> = {
  DirectUser: 'Direct user',
  RoleQueue:  'Role queue',
  OrgQueue:   'Org queue',
  Unassigned: 'Unassigned',
};

function friendlyClaimError(err: ApiError): string {
  if (err.status === 422) {
    const msg = (err.message ?? '').toLowerCase();
    if (msg.includes('already')) return 'This task was already claimed.';
    if (msg.includes('not_claimable') || msg.includes('not claimable'))
      return "This task isn't claimable. Ask an admin to reassign it.";
    return err.message || 'This task cannot be claimed in its current state.';
  }
  if (err.status === 403) return "You aren't eligible to claim this task.";
  if (err.isConflict) return 'Someone else just claimed this task.';
  if (err.isNotFound) return 'This task no longer exists.';
  return err.message || 'Could not claim this task. Please try again.';
}

export function TaskDetailDrawer({
  taskId,
  canReassign,
  onClose,
  onTaskMutated,
}: TaskDetailDrawerProps) {
  const toast = useToast();
  const [task, setTask] = useState<MyTask | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [claiming, setClaiming] = useState(false);
  const [showReassign, setShowReassign] = useState(false);

  const fetchDetail = useCallback(async () => {
    if (!taskId) return;
    setLoading(true);
    setError(null);
    try {
      const { data } = await tasksApi.getTaskDetail(taskId);
      setTask(data);
    } catch (e) {
      if (e instanceof ApiError && e.isNotFound) {
        setError('This task no longer exists.');
      } else if (e instanceof ApiError && e.isUnauthorized) {
        setError('Your session expired. Please sign in again.');
      } else {
        setError(e instanceof Error ? e.message : 'Could not load task.');
      }
      setTask(null);
    } finally {
      setLoading(false);
    }
  }, [taskId]);

  useEffect(() => {
    if (taskId) {
      void fetchDetail();
    } else {
      // Drawer closing — clear local state so it doesn't flash old
      // content when reopened with a new id.
      setTask(null);
      setError(null);
      setShowReassign(false);
    }
  }, [taskId, fetchDetail]);

  // Esc to close (only when no nested modal is open).
  useEffect(() => {
    function onKey(e: KeyboardEvent) {
      if (e.key === 'Escape' && taskId && !showReassign && !claiming) onClose();
    }
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [taskId, showReassign, claiming, onClose]);

  async function handleClaim() {
    if (!task || claiming) return;
    setClaiming(true);
    try {
      await tasksApi.claim(task.taskId, {});
      toast.show('Task claimed.', 'success');
      onTaskMutated();
      // Refetch detail to show new DirectUser state.
      await fetchDetail();
    } catch (e) {
      if (e instanceof ApiError) {
        toast.show(friendlyClaimError(e), 'error');
        // 404/409/422 = stale: refetch detail (which may show 404)
        // and signal parent so list refetches too.
        if (e.isNotFound || e.isConflict || e.status === 422) {
          onTaskMutated();
          await fetchDetail();
        }
      } else {
        toast.show('Something went wrong. Please try again.', 'error');
      }
    } finally {
      setClaiming(false);
    }
  }

  if (!taskId) return null;

  const isClaimable =
    task?.status === 'Open' &&
    (task.assignmentMode === 'RoleQueue' || task.assignmentMode === 'OrgQueue');

  return (
    <>
      <div
        className="fixed inset-0 z-40 bg-gray-900/40"
        onClick={onClose}
        aria-hidden="true"
      />
      <aside
        className="fixed inset-y-0 right-0 z-40 w-full max-w-md bg-white shadow-xl flex flex-col"
        role="dialog"
        aria-modal="true"
        aria-labelledby="task-drawer-title"
      >
        <header className="px-5 py-3 border-b border-gray-200 flex items-center justify-between gap-3 shrink-0">
          <h2 id="task-drawer-title" className="text-sm font-semibold text-gray-900 truncate">
            Task details
          </h2>
          <button
            type="button"
            onClick={onClose}
            aria-label="Close"
            className="p-1 rounded hover:bg-gray-100 text-gray-500"
          >
            <i className="ri-close-line text-lg" aria-hidden="true" />
          </button>
        </header>

        <div className="flex-1 overflow-y-auto px-5 py-4 space-y-4">
          {loading && !task ? (
            <DrawerSkeleton />
          ) : error ? (
            <div className="p-4 text-sm text-red-700 bg-red-50 border border-red-200 rounded">
              {error}
            </div>
          ) : task ? (
            <>
              <div>
                <h3 className="text-base font-semibold text-gray-900">
                  {task.title || '(untitled task)'}
                </h3>
                <div className="mt-2 flex items-center gap-2 flex-wrap">
                  <TaskStatusBadge status={task.status} />
                  <TaskPriorityBadge priority={task.priority} />
                  <SlaBadge status={task.slaStatus} dueAt={task.dueAt} />
                  <span className="px-2 py-0.5 text-[10px] font-medium uppercase tracking-wide rounded-full bg-gray-100 text-gray-700 border border-gray-200">
                    {MODE_LABEL[task.assignmentMode] ?? task.assignmentMode}
                  </span>
                </div>
                {task.description && (
                  <p className="mt-3 text-sm text-gray-700 whitespace-pre-line">
                    {task.description}
                  </p>
                )}
              </div>

              <Section title="Assignment">
                <Field label="Mode" value={MODE_LABEL[task.assignmentMode] ?? task.assignmentMode} />
                {task.assignmentMode === 'DirectUser' && (
                  <Field label="User" value={task.assignedUserId ?? '—'} mono />
                )}
                {task.assignmentMode === 'RoleQueue' && (
                  <Field label="Role" value={task.assignedRole ?? '—'} />
                )}
                {task.assignmentMode === 'OrgQueue' && (
                  <Field label="Org" value={task.assignedOrgId ?? '—'} mono />
                )}
                <Field label="Assigned at" value={fmtDate(task.assignedAt)} />
                {task.assignedBy && <Field label="Assigned by" value={task.assignedBy} mono />}
                {task.assignmentReason && (
                  <Field label="Reason" value={task.assignmentReason} />
                )}
              </Section>

              <Section title="Workflow context">
                {task.workflowName && <Field label="Workflow" value={task.workflowName} />}
                {task.productKey && <Field label="Product" value={task.productKey} />}
                <Field label="Step" value={task.stepKey || '—'} />
                <Field label="Workflow ID" value={task.workflowInstanceId} mono />
              </Section>

              <Section title="Timeline">
                <Field label="Created" value={fmtDate(task.createdAt)} />
                {task.startedAt && <Field label="Started" value={fmtDate(task.startedAt)} />}
                {task.completedAt && <Field label="Completed" value={fmtDate(task.completedAt)} />}
                {task.cancelledAt && <Field label="Cancelled" value={fmtDate(task.cancelledAt)} />}
              </Section>

              {/* LS-FLOW-E16 — unified history sourced from the audit
                  service via /workflow-tasks/{id}/timeline. Collapsible
                  to keep the drawer scannable; lifecycle timestamps
                  above remain the at-a-glance summary. */}
              <TaskHistorySection taskId={task.taskId} />


              {/* LS-FLOW-E10.3 (task slice) — SLA / Timer panel. Rendered
                  only when the row carries a deadline; legacy / SLA-disabled
                  tasks stay clean. */}
              {task.dueAt && (
                <Section title="SLA">
                  <Field label="Status" value={task.slaStatus ?? 'OnTrack'} />
                  <Field label="Due"    value={fmtDate(task.dueAt)} />
                  {task.slaBreachedAt && (
                    <Field label="Breached at" value={fmtDate(task.slaBreachedAt)} />
                  )}
                </Section>
              )}
            </>
          ) : null}
        </div>

        {task && (
          <footer className="px-5 py-3 border-t border-gray-200 flex items-center justify-end gap-2 bg-gray-50 shrink-0">
            {isClaimable && (
              <button
                type="button"
                onClick={handleClaim}
                disabled={claiming}
                className="px-3 py-1.5 text-xs font-medium rounded-md bg-indigo-600 text-white hover:bg-indigo-500 disabled:opacity-60"
              >
                {claiming ? 'Claiming…' : 'Claim'}
              </button>
            )}
            {canReassign && (
              <button
                type="button"
                onClick={() => setShowReassign(true)}
                disabled={claiming}
                className="px-3 py-1.5 text-xs font-medium rounded-md border border-gray-300 bg-white text-gray-700 hover:bg-gray-50 disabled:opacity-60"
              >
                Reassign
              </button>
            )}
          </footer>
        )}
      </aside>

      {task && showReassign && (
        <ReassignModal
          task={task}
          onClose={() => setShowReassign(false)}
          onReassigned={() => {
            onTaskMutated();
            void fetchDetail();
          }}
        />
      )}
    </>
  );
}

function Section({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <section>
      <h4 className="text-[11px] uppercase tracking-wide font-semibold text-gray-500 mb-2">
        {title}
      </h4>
      <dl className="space-y-1.5">{children}</dl>
    </section>
  );
}

function Field({ label, value, mono }: { label: string; value: string; mono?: boolean }) {
  return (
    <div className="grid grid-cols-3 gap-2 text-xs">
      <dt className="text-gray-500">{label}</dt>
      <dd className={'col-span-2 text-gray-800 break-all ' + (mono ? 'font-mono' : '')}>
        {value}
      </dd>
    </div>
  );
}

/**
 * LS-FLOW-E16 — collapsible history section that lazy-loads the
 * unified timeline only when expanded. Kept inside this file because
 * the only consumer today is this drawer; promote to a shared
 * component if/when reused.
 */
function TaskHistorySection({ taskId }: { taskId: string }) {
  const [open, setOpen]           = useState(false);
  const [events, setEvents]       = useState<TimelineEvent[]>([]);
  const [truncated, setTruncated] = useState(false);
  const [loading, setLoading]     = useState(false);
  const [error, setError]         = useState<string | null>(null);
  const [loaded, setLoaded]       = useState(false);

  // Sequence guard: when the user switches between tasks while the
  // drawer is open (or closes/reopens before an in-flight fetch
  // resolves), only the most recent fetch may commit state. Without
  // this, a slow response for a previous taskId could overwrite the
  // history of the currently displayed task — a confusing data leak
  // between rows of the same tenant.
  const fetchSeq = useRef(0);

  // Reset history whenever the underlying task changes. The component
  // is rendered inside a non-keyed drawer and is reused across rows,
  // so without this hook a freshly opened task would briefly show the
  // previous task's events.
  useEffect(() => {
    fetchSeq.current += 1;
    setOpen(false);
    setEvents([]);
    setTruncated(false);
    setLoading(false);
    setError(null);
    setLoaded(false);
  }, [taskId]);

  const handleToggle = useCallback(async () => {
    const next = !open;
    setOpen(next);
    if (next && !loaded && !loading) {
      const mySeq = ++fetchSeq.current;
      setLoading(true);
      setError(null);
      try {
        const res = await timelineApi.getTaskTimeline(taskId);
        if (mySeq !== fetchSeq.current) return; // stale — newer fetch in flight
        setEvents(res.data.events);
        setTruncated(res.data.truncated);
        setLoaded(true);
      } catch (err) {
        if (mySeq !== fetchSeq.current) return;
        // ApiError extends Error, so .message is always present.
        setError(err instanceof Error ? err.message : 'Could not load history.');
      } finally {
        if (mySeq === fetchSeq.current) setLoading(false);
      }
    }
  }, [open, loaded, loading, taskId]);

  return (
    <section>
      <button
        type="button"
        onClick={handleToggle}
        className="w-full flex items-center justify-between text-[11px] uppercase tracking-wide font-semibold text-gray-500 mb-2 hover:text-gray-700"
        aria-expanded={open}
      >
        <span>History</span>
        <span className="text-gray-400" aria-hidden="true">{open ? '▾' : '▸'}</span>
      </button>
      {open && (
        <div className="pl-1">
          <Timeline
            events={events}
            loading={loading}
            error={error}
            truncated={truncated}
            dense
          />
        </div>
      )}
    </section>
  );
}

function DrawerSkeleton() {
  return (
    <div className="space-y-4 animate-pulse">
      <div className="h-5 bg-gray-200 rounded w-2/3" />
      <div className="h-3 bg-gray-100 rounded w-1/2" />
      <div className="h-3 bg-gray-100 rounded w-1/3" />
      <div className="h-20 bg-gray-100 rounded" />
      <div className="h-20 bg-gray-100 rounded" />
    </div>
  );
}
