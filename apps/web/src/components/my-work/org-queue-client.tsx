'use client';

import { useCallback, useEffect, useRef, useState } from 'react';
import { ApiError } from '@/lib/api-client';
import { tasksApi, type MyTask } from '@/lib/tasks';
import { QueueTaskRow } from './queue-task-row';

/**
 * LS-FLOW-E15 — Org Queue tab. Lists open OrgQueue tasks that
 * belong to the caller's organisation. Eligibility is enforced
 * server-side from IFlowUserContext.OrgId.
 */
export interface OrgQueueClientProps {
  onOpenTask: (taskId: string) => void;
  refreshKey: number;
}

export function OrgQueueClient({ onOpenTask, refreshKey }: OrgQueueClientProps) {
  const [tasks, setTasks] = useState<MyTask[]>([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const seq = useRef(0);

  const fetchTasks = useCallback(async () => {
    const my = ++seq.current;
    setLoading(true);
    setError(null);
    try {
      const { data } = await tasksApi.listOrgQueue({ page: 1, pageSize: 50 });
      if (my !== seq.current) return;
      setTasks(data.items ?? []);
      setTotal(data.totalCount ?? 0);
    } catch (err) {
      if (my !== seq.current) return;
      if (err instanceof ApiError && err.isUnauthorized) {
        setError('Your session expired. Please sign in again.');
      } else {
        setError(err instanceof Error ? err.message : 'Could not load org queue.');
      }
      setTasks([]);
      setTotal(0);
    } finally {
      if (my === seq.current) setLoading(false);
    }
  }, []);

  useEffect(() => {
    void fetchTasks();
  }, [fetchTasks, refreshKey]);

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between gap-3 flex-wrap">
        <div>
          <h2 className="text-base font-semibold text-gray-900">Org queue</h2>
          <p className="text-sm text-gray-500">
            Open tasks waiting for any member of your organisation.
          </p>
        </div>
        <button
          type="button"
          onClick={() => void fetchTasks()}
          disabled={loading}
          className="inline-flex items-center gap-1 px-3 py-1.5 text-xs font-medium rounded-md border border-gray-300 bg-white text-gray-700 hover:bg-gray-50 disabled:opacity-60"
        >
          <i className={`ri-refresh-line ${loading ? 'animate-spin' : ''}`} aria-hidden="true" />
          Refresh
        </button>
      </div>

      {loading && tasks.length === 0 ? (
        <SkeletonList />
      ) : error ? (
        <ErrorState message={error} onRetry={() => void fetchTasks()} />
      ) : tasks.length === 0 ? (
        <EmptyState />
      ) : (
        <>
          <div className="flex items-center gap-3 flex-wrap">
            <p className="text-xs text-gray-500">
              Showing {tasks.length} of {total} task{total === 1 ? '' : 's'}.
            </p>
            <QueueUrgencySummary tasks={tasks} />
          </div>
          <ul className="space-y-2">
            {tasks.map((t) => (
              <QueueTaskRow
                key={t.taskId}
                task={t}
                queueKind="org"
                onChanged={() => void fetchTasks()}
                onOpen={onOpenTask}
              />
            ))}
          </ul>
        </>
      )}
    </div>
  );
}

/**
 * LS-FLOW-E18 — Urgency hint strip displayed above the task list.
 * Shows counts of escalated/overdue and at-risk (DueSoon) tasks so
 * the user can triage at a glance before scrolling. Renders nothing
 * when the queue is fully on-track.
 */
function QueueUrgencySummary({ tasks }: { tasks: MyTask[] }) {
  const overdue = tasks.filter(
    (t) => t.slaStatus === 'Escalated' || t.slaStatus === 'Overdue',
  ).length;
  const atRisk = tasks.filter((t) => t.slaStatus === 'DueSoon').length;

  if (overdue === 0 && atRisk === 0) return null;

  return (
    <div className="flex items-center gap-2 flex-wrap" aria-label="Queue urgency summary">
      {overdue > 0 && (
        <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-[10px] font-semibold bg-red-100 text-red-700 border border-red-200">
          <i className="ri-alarm-warning-line" aria-hidden="true" />
          {overdue} overdue
        </span>
      )}
      {atRisk > 0 && (
        <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-[10px] font-semibold bg-amber-50 text-amber-700 border border-amber-200">
          <i className="ri-time-line" aria-hidden="true" />
          {atRisk} at risk
        </span>
      )}
    </div>
  );
}

function SkeletonList() {
  return (
    <ul className="space-y-2" aria-label="Loading org-queue tasks">
      {Array.from({ length: 3 }).map((_, i) => (
        <li
          key={i}
          className="bg-white border border-gray-200 rounded-lg p-4 animate-pulse"
        >
          <div className="h-4 bg-gray-200 rounded w-1/3 mb-2" />
          <div className="h-3 bg-gray-100 rounded w-2/3 mb-1" />
          <div className="h-3 bg-gray-100 rounded w-1/2" />
        </li>
      ))}
    </ul>
  );
}

function EmptyState() {
  return (
    <div className="bg-white border border-dashed border-gray-300 rounded-lg p-10 text-center">
      <i className="ri-building-line text-3xl text-gray-300" aria-hidden="true" />
      <h3 className="mt-2 text-sm font-medium text-gray-900">No org-queue work</h3>
      <p className="mt-1 text-xs text-gray-500">
        No open tasks are currently waiting for your organisation.
      </p>
    </div>
  );
}

function ErrorState({ message, onRetry }: { message: string; onRetry: () => void }) {
  return (
    <div className="bg-red-50 border border-red-200 rounded-lg p-6 text-center">
      <i className="ri-error-warning-line text-2xl text-red-500" aria-hidden="true" />
      <p className="mt-2 text-sm text-red-700">{message}</p>
      <button
        type="button"
        onClick={onRetry}
        className="mt-3 px-3 py-1.5 text-xs font-medium rounded-md bg-white border border-red-300 text-red-700 hover:bg-red-100"
      >
        Try again
      </button>
    </div>
  );
}
