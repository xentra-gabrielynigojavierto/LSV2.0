'use client';

import { useState } from 'react';
import { ApiError } from '@/lib/api-client';
import { tasksApi, type MyTask } from '@/lib/tasks';
import { useToast } from '@/lib/toast-context';
import { TaskStatusBadge } from './status-badge';
import { TaskPriorityBadge } from './priority-badge';
import { SlaBadge } from './sla-badge';

/**
 * LS-FLOW-E11.6 — a single row in the My Work inbox.
 *
 * Each row is a self-contained card: it owns the per-task action button
 * loading/disabled state, but it does NOT own the list state — once an
 * action succeeds (or fails with a stale-state error), it asks the
 * parent to refetch via `onChanged`. This keeps the list as the single
 * source of truth and matches the "mutation → refetch" recommendation
 * in the spec (no optimistic UI).
 *
 * Action visibility is gated by status:
 *   Open        → Start, Cancel
 *   InProgress  → Complete, Cancel
 *   Completed   → (none)
 *   Cancelled   → (none)
 *
 * Hiding instead of disabling means the UI can never offer an invalid
 * lifecycle transition; the backend enforces the same rules so the two
 * stay in sync.
 */
export interface TaskRowProps {
  task: MyTask;
  /** Called after any successful or stale-state action so the list can refetch. */
  onChanged: () => void;
  /**
   * LS-FLOW-E15 — optional row-click handler that opens the
   * task-detail drawer. When provided, the row body becomes
   * clickable; the inline action buttons use stopPropagation so
   * they don't double-fire.
   */
  onOpen?: (taskId: string) => void;
}

type ActionKind = 'start' | 'complete' | 'cancel';

function fmtDate(iso: string | null | undefined): string {
  if (!iso) return '—';
  try {
    return new Date(iso).toLocaleString();
  } catch {
    return iso;
  }
}

function friendlyError(err: ApiError, kind: ActionKind): string {
  // 422: server-side InvalidStateTransition — the task can't be moved from
  //      its current state. Usually a stale UI; advise refresh.
  if (err.status === 422) {
    return 'This task cannot be ' + (
      kind === 'start'    ? 'started' :
      kind === 'complete' ? 'completed' :
                            'cancelled'
    ) + ' from its current state. Please refresh.';
  }
  if (err.isConflict) {
    // 409 covers both:
    //   - WorkflowTaskConcurrencyException (CAS lost)
    //   - InvalidWorkflowTransitionException (stale workflow step)
    if (kind === 'complete') {
      return 'The workflow has already moved forward, or another tab finished this task. Please refresh.';
    }
    return 'This task was already updated. Please refresh.';
  }
  if (err.isNotFound) {
    return 'This task no longer exists. Please refresh.';
  }
  if (err.isUnauthorized) {
    return 'Your session expired. Please sign in again.';
  }
  return err.message || 'Something went wrong. Please try again.';
}

export function TaskRow({ task, onChanged, onOpen }: TaskRowProps) {
  const toast = useToast();
  const [busy, setBusy] = useState<ActionKind | null>(null);

  async function run(kind: ActionKind, e?: React.MouseEvent) {
    // E15: stop click bubbling so the row-click drawer-open doesn't
    // fire when an action button is pressed.
    e?.stopPropagation();
    if (busy) return;
    setBusy(kind);
    try {
      if (kind === 'start') {
        await tasksApi.start(task.taskId);
        toast.show('Task started.', 'success');
      } else if (kind === 'complete') {
        const { data } = await tasksApi.complete(task.taskId);
        // Surface the workflow progression result — completion may also
        // advance / complete the owning workflow (E11.7 binding). We
        // intentionally do NOT navigate here; the user stays in the
        // inbox. New tasks (if the next step assigns back) appear on
        // refetch.
        if (data.workflowAdvanced) {
          if (data.workflowStatus === 'Completed') {
            toast.show('Task completed — workflow finished.', 'success');
          } else {
            toast.show(
              `Task completed — workflow moved to "${data.toStepKey}".`,
              'success',
            );
          }
        } else {
          toast.show('Task completed.', 'success');
        }
      } else {
        await tasksApi.cancel(task.taskId);
        toast.show('Task cancelled.', 'success');
      }
      onChanged();
    } catch (err) {
      if (err instanceof ApiError) {
        toast.show(friendlyError(err, kind), 'error');
        // For 404 / 409 / 422 the local row is by definition stale —
        // the most useful thing for the user is an immediate refresh.
        if (err.isNotFound || err.isConflict || err.status === 422) {
          onChanged();
        }
      } else {
        toast.show('Something went wrong. Please try again.', 'error');
      }
    } finally {
      setBusy(null);
    }
  }

  const canStart    = task.status === 'Open';
  const canComplete = task.status === 'InProgress';
  const canCancel   = task.status === 'Open' || task.status === 'InProgress';

  // Row body is clickable as a "view detail" affordance only when
  // the parent provided onOpen (E15). Pre-E15 callers get the
  // original non-interactive list item.
  const rowClickable = !!onOpen;
  const rowProps = rowClickable
    ? {
        onClick: () => onOpen!(task.taskId),
        role: 'button' as const,
        tabIndex: 0,
        onKeyDown: (ev: React.KeyboardEvent) => {
          if (ev.key === 'Enter' || ev.key === ' ') {
            ev.preventDefault();
            onOpen!(task.taskId);
          }
        },
      }
    : {};

  return (
    <li
      className={
        'bg-white border border-gray-200 rounded-lg px-4 py-3 hover:border-gray-300 transition-colors ' +
        (rowClickable ? 'cursor-pointer' : '')
      }
      {...rowProps}
    >
      <div className="flex items-start gap-4">
        <div className="flex-1 min-w-0">
          <div className="flex items-center gap-2 flex-wrap">
            <h3 className="text-sm font-semibold text-gray-900 truncate">
              {task.title || '(untitled task)'}
            </h3>
            <TaskStatusBadge status={task.status} />
            <TaskPriorityBadge priority={task.priority} />
            <SlaBadge status={task.slaStatus} dueAt={task.dueAt} />
          </div>

          {task.description && (
            <p className="mt-1 text-sm text-gray-600 line-clamp-2">{task.description}</p>
          )}

          <dl className="mt-2 grid grid-cols-1 sm:grid-cols-2 gap-x-6 gap-y-1 text-xs text-gray-500">
            {task.workflowName && (
              <div className="truncate">
                <dt className="inline font-medium text-gray-600">Workflow:</dt>{' '}
                <dd className="inline">{task.workflowName}</dd>
              </div>
            )}
            {task.productKey && (
              <div className="truncate">
                <dt className="inline font-medium text-gray-600">Product:</dt>{' '}
                <dd className="inline">{task.productKey}</dd>
              </div>
            )}
            <div className="truncate">
              <dt className="inline font-medium text-gray-600">Step:</dt>{' '}
              <dd className="inline">{task.stepKey || '—'}</dd>
            </div>
            <div className="truncate">
              <dt className="inline font-medium text-gray-600">Created:</dt>{' '}
              <dd className="inline">{fmtDate(task.createdAt)}</dd>
            </div>
            {task.dueAt && (
              <div className="truncate">
                <dt className="inline font-medium text-gray-600">Due:</dt>{' '}
                <dd className="inline">{fmtDate(task.dueAt)}</dd>
              </div>
            )}
            {task.startedAt && (
              <div className="truncate">
                <dt className="inline font-medium text-gray-600">Started:</dt>{' '}
                <dd className="inline">{fmtDate(task.startedAt)}</dd>
              </div>
            )}
            {task.completedAt && (
              <div className="truncate">
                <dt className="inline font-medium text-gray-600">Completed:</dt>{' '}
                <dd className="inline">{fmtDate(task.completedAt)}</dd>
              </div>
            )}
          </dl>
        </div>

        {(canStart || canComplete || canCancel) && (
          <div className="flex flex-col sm:flex-row gap-2 shrink-0">
            {canStart && (
              <button
                type="button"
                onClick={(e) => run('start', e)}
                disabled={busy !== null}
                className="px-3 py-1.5 text-xs font-medium rounded-md bg-indigo-600 text-white hover:bg-indigo-500 disabled:opacity-60 disabled:cursor-not-allowed"
              >
                {busy === 'start' ? 'Starting…' : 'Start'}
              </button>
            )}
            {canComplete && (
              <button
                type="button"
                onClick={(e) => run('complete', e)}
                disabled={busy !== null}
                className="px-3 py-1.5 text-xs font-medium rounded-md bg-emerald-600 text-white hover:bg-emerald-500 disabled:opacity-60 disabled:cursor-not-allowed"
              >
                {busy === 'complete' ? 'Completing…' : 'Complete'}
              </button>
            )}
            {canCancel && (
              <button
                type="button"
                onClick={(e) => run('cancel', e)}
                disabled={busy !== null}
                className="px-3 py-1.5 text-xs font-medium rounded-md bg-white border border-gray-300 text-gray-700 hover:bg-gray-50 disabled:opacity-60 disabled:cursor-not-allowed"
              >
                {busy === 'cancel' ? 'Cancelling…' : 'Cancel'}
              </button>
            )}
          </div>
        )}
      </div>
    </li>
  );
}
