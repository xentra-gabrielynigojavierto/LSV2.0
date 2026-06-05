'use client';

import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { ApiError } from '@/lib/api-client';
import {
  STATUS_FILTER_OPTIONS,
  tasksApi,
  type MyTask,
  type StatusFilter,
  type WorkflowTaskStatus,
} from '@/lib/tasks';
import { TaskRow } from './task-row';

/**
 * LS-FLOW-E11.6 — root client component for the My Work page.
 *
 * Owns:
 *   - the status filter (single-select; "all" by default),
 *   - the list fetch (via tasksApi.listMine, scoped to the calling user
 *     by Flow.Api itself — no userId in the request),
 *   - the loading / empty / error states,
 *   - and the page-level "refetch after action" handler that the rows
 *     call when an action succeeds or surfaces a stale-state error.
 *
 * State strategy is intentionally simple: the backend is the source of
 * truth, every action is followed by a refetch, and there is no
 * optimistic mutation. This matches the spec's "mutation → refetch"
 * recommendation and keeps the surface honest about the workflow
 * progression that may happen on Complete (E11.7).
 *
 * Concurrency guard: a per-fetch sequence number discards the response
 * of any superseded request, so rapid filter changes / refetches can't
 * race each other into a stale render.
 *
 * Ordering: the backend already orders Open / InProgress before
 * Completed / Cancelled, then by recency (see MyTasksService); we keep
 * the list in server order so the queue feels consistent.
 */
/**
 * LS-FLOW-E15 — opt-in callback so the WorkAreaClient drawer can
 * open from a row click. When omitted, MyWorkClient continues to
 * render exactly as it did pre-E15 (no row-click affordance,
 * inline action buttons only).
 */
export interface MyWorkClientProps {
  onOpenTask?: (taskId: string) => void;
}

export function MyWorkClient({ onOpenTask }: MyWorkClientProps = {}) {
  const [filter, setFilter] = useState<StatusFilter>('all');
  const [tasks, setTasks]   = useState<MyTask[]>([]);
  const [total, setTotal]   = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError]   = useState<string | null>(null);
  const seq = useRef(0);

  const fetchTasks = useCallback(async () => {
    const my = ++seq.current;
    setLoading(true);
    setError(null);
    try {
      const status: WorkflowTaskStatus[] | undefined =
        filter === 'all' ? undefined : [filter];
      const { data } = await tasksApi.listMine({ status, page: 1, pageSize: 50 });
      if (my !== seq.current) return; // superseded
      setTasks(data.items ?? []);
      setTotal(data.totalCount ?? 0);
    } catch (err) {
      if (my !== seq.current) return;
      if (err instanceof ApiError) {
        if (err.isUnauthorized) {
          setError('Your session expired. Please sign in again.');
        } else {
          setError(err.message || 'Could not load your tasks.');
        }
      } else {
        setError('Could not load your tasks.');
      }
      setTasks([]);
      setTotal(0);
    } finally {
      if (my === seq.current) setLoading(false);
    }
  }, [filter]);

  useEffect(() => { void fetchTasks(); }, [fetchTasks]);

  const counts = useMemo(() => {
    // Page-local counts only — used as a soft cue beside the filter
    // chips. A precise per-status total would require N requests; we
    // intentionally avoid that for now (out of spec for E11.6).
    return tasks.reduce<Record<WorkflowTaskStatus, number>>(
      (acc, t) => {
        acc[t.status] = (acc[t.status] ?? 0) + 1;
        return acc;
      },
      { Open: 0, InProgress: 0, Completed: 0, Cancelled: 0 },
    );
  }, [tasks]);

  return (
    <div className="space-y-4">
      {/* Header: title + refresh + filter chips */}
      <div className="flex items-center justify-between gap-3 flex-wrap">
        <div>
          <h1 className="text-xl font-semibold text-gray-900">My Work</h1>
          <p className="text-sm text-gray-500">
            Tasks assigned to you across every workflow.
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

      <nav className="flex items-center gap-1 flex-wrap" aria-label="Filter tasks by status">
        {STATUS_FILTER_OPTIONS.map(opt => {
          const active = filter === opt.value;
          const showCount = opt.value !== 'all' && filter === 'all';
          const count = showCount ? counts[opt.value as WorkflowTaskStatus] : null;
          return (
            <button
              key={opt.value}
              type="button"
              onClick={() => setFilter(opt.value)}
              className={
                'px-3 py-1.5 text-xs font-medium rounded-full border transition-colors ' +
                (active
                  ? 'bg-gray-900 text-white border-gray-900'
                  : 'bg-white text-gray-600 border-gray-300 hover:bg-gray-50')
              }
              aria-pressed={active}
            >
              {opt.label}
              {count != null && count > 0 && (
                <span className="ml-1 text-[10px] opacity-75">({count})</span>
              )}
            </button>
          );
        })}
      </nav>

      {/* Body */}
      {loading && tasks.length === 0 ? (
        <SkeletonList />
      ) : error ? (
        <ErrorState message={error} onRetry={() => void fetchTasks()} />
      ) : tasks.length === 0 ? (
        <EmptyState filter={filter} />
      ) : (
        <>
          <p className="text-xs text-gray-500">
            Showing {tasks.length} of {total} task{total === 1 ? '' : 's'}.
          </p>
          <ul className="space-y-2">
            {tasks.map(t => (
              <TaskRow
                key={t.taskId}
                task={t}
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

function SkeletonList() {
  return (
    <ul className="space-y-2" aria-label="Loading tasks">
      {Array.from({ length: 4 }).map((_, i) => (
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

function EmptyState({ filter }: { filter: StatusFilter }) {
  const isFiltered = filter !== 'all';
  return (
    <div className="bg-white border border-dashed border-gray-300 rounded-lg p-10 text-center">
      <i className="ri-checkbox-multiple-line text-3xl text-gray-300" aria-hidden="true" />
      <h2 className="mt-2 text-sm font-medium text-gray-900">
        {isFiltered ? 'No tasks match this filter' : "You're all caught up"}
      </h2>
      <p className="mt-1 text-xs text-gray-500">
        {isFiltered
          ? 'Try a different status or clear the filter.'
          : 'No tasks are assigned to you right now.'}
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
