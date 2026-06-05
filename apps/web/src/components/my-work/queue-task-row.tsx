'use client';

import { useState } from 'react';
import { ApiError } from '@/lib/api-client';
import { tasksApi, type MyTask } from '@/lib/tasks';
import { useToast } from '@/lib/toast-context';
import { TaskStatusBadge } from './status-badge';
import { TaskPriorityBadge } from './priority-badge';
import { SlaBadge } from './sla-badge';

/**
 * LS-FLOW-E15 — a single row in a queue list (Role Queue or Org Queue).
 *
 * Visually mirrors {@link TaskRow} but trades the lifecycle action set
 * for a single **Claim** action and a row-click affordance that opens
 * the parent's task-detail drawer.
 *
 * <p>Backend authority: claim goes through the E14.2 governed endpoint;
 * the UI only sends the optional reason (left blank — the backend
 * stamps a deterministic default so audit rows are never blank).</p>
 *
 * <p>State strategy: same "mutation → refetch" pattern as MyWorkClient.
 * On success, the row asks the parent to refetch via {@link
 * QueueTaskRowProps.onChanged}; the row never owns the list state.</p>
 */
export interface QueueTaskRowProps {
  task: MyTask;
  /** Called after a successful claim so the parent list can refetch. */
  onChanged: () => void;
  /** Called when the user clicks the row body to inspect the task. */
  onOpen: (taskId: string) => void;
  /** What kind of queue this row belongs to — used only for display. */
  queueKind: 'role' | 'org';
}

function fmtDate(iso: string | null | undefined): string {
  if (!iso) return '—';
  try {
    return new Date(iso).toLocaleString();
  } catch {
    return iso;
  }
}

function friendlyClaimError(err: ApiError): string {
  // 422 covers backend AssignmentRuleException family. The message
  // text is human-readable and stable enough to surface verbatim,
  // but we override the most common cases for clarity.
  if (err.status === 422) {
    const msg = (err.message ?? '').toLowerCase();
    if (msg.includes('already')) return 'This task was already claimed.';
    if (msg.includes('not_claimable') || msg.includes('not claimable'))
      return "This task isn't claimable. Ask an admin to reassign it.";
    return err.message || 'This task cannot be claimed in its current state.';
  }
  if (err.status === 403) return "You aren't eligible to claim this task.";
  if (err.isConflict) return 'Someone else just claimed this task. Refreshing.';
  if (err.isNotFound) return 'This task no longer exists.';
  if (err.isUnauthorized) return 'Your session expired. Please sign in again.';
  return err.message || 'Could not claim this task. Please try again.';
}

export function QueueTaskRow({ task, onChanged, onOpen, queueKind }: QueueTaskRowProps) {
  const toast = useToast();
  const [busy, setBusy] = useState(false);

  async function handleClaim(e: React.MouseEvent) {
    // Don't trigger the row-click drawer-open from a button click.
    e.stopPropagation();
    if (busy) return;
    setBusy(true);
    try {
      await tasksApi.claim(task.taskId, {});
      toast.show('Task claimed.', 'success');
      onChanged();
    } catch (err) {
      if (err instanceof ApiError) {
        toast.show(friendlyClaimError(err), 'error');
        // 404 / 409 / 422 all mean the local row is stale — the most
        // useful thing for the user is an immediate refresh.
        if (err.isNotFound || err.isConflict || err.status === 422) {
          onChanged();
        }
      } else {
        toast.show('Something went wrong. Please try again.', 'error');
      }
    } finally {
      setBusy(false);
    }
  }

  // The whole card is clickable as a "view detail" affordance. The
  // Claim button uses stopPropagation so it doesn't double-fire.
  return (
    <li
      className="bg-white border border-gray-200 rounded-lg px-4 py-3 hover:border-gray-300 transition-colors cursor-pointer"
      onClick={() => onOpen(task.taskId)}
      role="button"
      tabIndex={0}
      onKeyDown={(e) => {
        if (e.key === 'Enter' || e.key === ' ') {
          e.preventDefault();
          onOpen(task.taskId);
        }
      }}
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
            <span className="px-2 py-0.5 text-[10px] font-medium uppercase tracking-wide rounded-full bg-indigo-50 text-indigo-700 border border-indigo-200">
              {queueKind === 'role' ? 'Role queue' : 'Org queue'}
            </span>
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
            {queueKind === 'role' && task.assignedRole && (
              <div className="truncate">
                <dt className="inline font-medium text-gray-600">Role:</dt>{' '}
                <dd className="inline">{task.assignedRole}</dd>
              </div>
            )}
            {queueKind === 'org' && task.assignedOrgId && (
              <div className="truncate">
                <dt className="inline font-medium text-gray-600">Org:</dt>{' '}
                <dd className="inline font-mono">{task.assignedOrgId}</dd>
              </div>
            )}
            <div className="truncate">
              <dt className="inline font-medium text-gray-600">Created:</dt>{' '}
              <dd className="inline">{fmtDate(task.createdAt)}</dd>
            </div>
            {task.assignedAt && (
              <div className="truncate">
                <dt className="inline font-medium text-gray-600">Queued:</dt>{' '}
                <dd className="inline">{fmtDate(task.assignedAt)}</dd>
              </div>
            )}
            {task.dueAt && (
              <div className="truncate">
                <dt className="inline font-medium text-gray-600">Due:</dt>{' '}
                <dd className="inline">{fmtDate(task.dueAt)}</dd>
              </div>
            )}
          </dl>
        </div>

        <div className="flex flex-col sm:flex-row gap-2 shrink-0">
          <button
            type="button"
            onClick={handleClaim}
            disabled={busy}
            className="px-3 py-1.5 text-xs font-medium rounded-md bg-indigo-600 text-white hover:bg-indigo-500 disabled:opacity-60 disabled:cursor-not-allowed"
          >
            {busy ? 'Claiming…' : 'Claim'}
          </button>
        </div>
      </div>
    </li>
  );
}
