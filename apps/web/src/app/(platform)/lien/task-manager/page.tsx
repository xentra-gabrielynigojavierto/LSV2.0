'use client';

import { useState, useEffect, useCallback, useMemo } from 'react';
import { lienTasksService } from '@/lib/liens/lien-tasks.service';
import { apiClient } from '@/lib/api-client';
import type { TaskDto, TaskStatus, TaskPriority, TasksQuery } from '@/lib/liens/lien-tasks.types';
import {
  TASK_STATUS_LABELS,
  TASK_STATUS_COLORS,
  TASK_PRIORITY_COLORS,
  TASK_PRIORITY_ICONS,
  BOARD_COLUMNS,
} from '@/lib/liens/lien-tasks.types';
import type { TenantUser } from '@/types/tenant';
import { useLienStore } from '@/stores/lien-store';
import { CreateEditTaskForm } from '@/components/lien/forms/create-edit-task-form';
import { TaskDetailDrawer } from '@/components/lien/task-detail-drawer';
import { TaskManagerHeader } from '@/components/lien/task-manager-header';
import { TaskManagerToolbar } from '@/components/lien/task-manager-toolbar';
import { TaskBoard } from '@/components/lien/task-board';

export const dynamic = 'force-dynamic';


type ViewMode = 'board' | 'list';
type AssignmentScope = 'all' | 'me' | 'others' | 'unassigned';

const PRIORITY_LABELS: Record<string, string> = {
  LOW: 'Low', MEDIUM: 'Medium', HIGH: 'High', URGENT: 'Urgent',
};

const AVATAR_COLORS = [
  'bg-violet-500', 'bg-blue-500', 'bg-teal-500',
  'bg-indigo-500', 'bg-pink-500', 'bg-amber-500',
];

function avatarColor(id: string): string {
  let hash = 0;
  for (let i = 0; i < id.length; i++) hash = (hash * 31 + id.charCodeAt(i)) >>> 0;
  return AVATAR_COLORS[hash % AVATAR_COLORS.length];
}

function getInitials(first: string, last: string): string {
  return `${first.charAt(0)}${last.charAt(0)}`.toUpperCase();
}

function formatDate(val?: string | null): string {
  if (!val) return '\u2014';
  try {
    const d = new Date(val);
    return isNaN(d.getTime()) ? val : d.toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' });
  } catch { return val ?? '\u2014'; }
}

function isOverdue(dueDate?: string | null, status?: string): boolean {
  if (!dueDate || status === 'COMPLETED' || status === 'CANCELLED') return false;
  return new Date(dueDate) < new Date();
}

function shortCaseId(caseId: string): string {
  return caseId.length > 8 ? caseId.slice(0, 8).toUpperCase() : caseId.toUpperCase();
}

export default function TaskManagerPage() {
  const addToast = useLienStore((s) => s.addToast);

  const [tasks, setTasks]           = useState<TaskDto[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [loading, setLoading]       = useState(true);
  const [error, setError]           = useState<string | null>(null);
  const [viewMode, setViewMode]     = useState<ViewMode>('board');
  const [usersById, setUsersById]   = useState<Map<string, TenantUser>>(new Map());

  const [search, setSearch]                   = useState('');
  const [statusFilter, setStatusFilter]       = useState<TaskStatus | ''>('');
  const [priorityFilter, setPriorityFilter]   = useState<TaskPriority | ''>('');
  const [assignmentScope, setAssignmentScope] = useState<AssignmentScope>('all');

  const [showCreate, setShowCreate] = useState(false);
  const [editTask, setEditTask]     = useState<TaskDto | undefined>();
  const [detailTask, setDetailTask] = useState<TaskDto | null>(null);

  useEffect(() => {
    apiClient.get<TenantUser[]>('/identity/api/users')
      .then(({ data }) => {
        const map = new Map<string, TenantUser>();
        (data ?? []).forEach((u) => map.set(u.id, u));
        setUsersById(map);
      })
      .catch(() => {});
  }, []);

  const fetchTasks = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const query: TasksQuery = {
        search: search || undefined,
        status: statusFilter || undefined,
        priority: priorityFilter || undefined,
        assignmentScope: assignmentScope === 'all' ? undefined : assignmentScope,
        pageSize: 200,
        page: 1,
      };
      const result = await lienTasksService.getTasks(query);
      setTasks(result.items);
      setTotalCount(result.totalCount);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load tasks');
    } finally {
      setLoading(false);
    }
  }, [search, statusFilter, priorityFilter, assignmentScope]);

  useEffect(() => { fetchTasks(); }, [fetchTasks]);

  const kpis = useMemo(() => ({
    total:      tasks.length,
    new_:       tasks.filter((t) => t.status === 'NEW').length,
    inProgress: tasks.filter((t) => t.status === 'IN_PROGRESS').length,
    blocked:    tasks.filter((t) => t.status === 'WAITING_BLOCKED').length,
    overdue:    tasks.filter((t) => isOverdue(t.dueDate, t.status)).length,
  }), [tasks]);

  const boardColumns = BOARD_COLUMNS.map((status) => ({
    status,
    label: TASK_STATUS_LABELS[status],
    borderColor: TASK_STATUS_COLORS[status].border,
    items: tasks.filter((t) => t.status === status),
  }));

  const activeFilterCount = [search, statusFilter, priorityFilter, assignmentScope !== 'all' ? assignmentScope : ''].filter(Boolean).length;

  function clearFilters() {
    setSearch('');
    setStatusFilter('');
    setPriorityFilter('');
    setAssignmentScope('all');
  }

  return (
    <div className="space-y-3">

      {/* Row 1 — Header with inline stats */}
      <TaskManagerHeader
        title="Task Manager"
        stats={[
          { label: 'Total',       value: kpis.total,      icon: 'ri-task-line',          color: 'text-gray-500'  },
          { label: 'New',         value: kpis.new_,        icon: 'ri-time-line',          color: 'text-gray-500'  },
          { label: 'In Progress', value: kpis.inProgress,  icon: 'ri-loader-4-line',      color: 'text-blue-600'  },
          { label: 'Blocked',     value: kpis.blocked,     icon: 'ri-pause-circle-line',  color: 'text-amber-600' },
          { label: 'Overdue',     value: kpis.overdue,     icon: 'ri-alarm-warning-line', color: 'text-red-600'   },
        ]}
        viewMode={viewMode}
        onViewModeChange={setViewMode}
        onNewTask={() => setShowCreate(true)}
      />

      {/* Row 3 — Toolbar */}
      <TaskManagerToolbar
        search={search}
        onSearch={setSearch}
        statusFilter={statusFilter}
        onStatusFilter={setStatusFilter}
        priorityFilter={priorityFilter}
        onPriorityFilter={setPriorityFilter}
        activeFilterCount={activeFilterCount}
        onClearFilters={clearFilters}
        assigneeSlot={
          <select
            value={assignmentScope}
            onChange={(e) => setAssignmentScope(e.target.value as AssignmentScope)}
            className="text-xs border border-gray-200 rounded-lg px-2.5 py-1.5 focus:outline-none focus:ring-2 focus:ring-primary/30 bg-white"
          >
            <option value="all">All Assignees</option>
            <option value="me">My Tasks</option>
            <option value="others">Others&apos; Tasks</option>
            <option value="unassigned">Unassigned</option>
          </select>
        }
      />

      {/* Error */}
      {error && (
        <div className="bg-red-50 border border-red-200 rounded-lg px-4 py-2 flex items-center justify-between">
          <div className="flex items-center gap-2">
            <i className="ri-error-warning-line text-red-500 text-sm" />
            <span className="text-xs text-red-700">{error}</span>
          </div>
          <button onClick={fetchTasks} className="text-xs text-red-600 hover:text-red-800 font-medium">Retry</button>
        </div>
      )}

      {/* Row 4 — Board / List */}
      {loading ? (
        <div className="flex items-center justify-center py-8 gap-2 text-gray-400">
          <i className="ri-loader-4-line animate-spin text-lg" />
          <span className="text-xs">Loading tasks...</span>
        </div>
      ) : viewMode === 'board' ? (
        <TaskBoard
          columns={boardColumns}
          usersById={usersById}
          onTaskClick={setDetailTask}
          onNewTask={() => setShowCreate(true)}
        />
      ) : (
        /* ── List view ───────────────────────────────────────────── */
        <div className="bg-white border border-gray-200 rounded-xl overflow-hidden">
          <div className="overflow-x-auto">
            <table className="min-w-full divide-y divide-gray-100">
              <thead className="bg-gray-50">
                <tr>
                  <th className="px-4 py-2.5 text-left text-[10px] font-medium text-gray-500 uppercase tracking-wide">Title</th>
                  <th className="px-4 py-2.5 text-left text-[10px] font-medium text-gray-500 uppercase tracking-wide">Status</th>
                  <th className="px-4 py-2.5 text-left text-[10px] font-medium text-gray-500 uppercase tracking-wide">Priority</th>
                  <th className="px-4 py-2.5 text-left text-[10px] font-medium text-gray-500 uppercase tracking-wide">Assignee</th>
                  <th className="px-4 py-2.5 text-left text-[10px] font-medium text-gray-500 uppercase tracking-wide">Case</th>
                  <th className="px-4 py-2.5 text-left text-[10px] font-medium text-gray-500 uppercase tracking-wide">Liens</th>
                  <th className="px-4 py-2.5 text-left text-[10px] font-medium text-gray-500 uppercase tracking-wide">Due</th>
                  <th className="px-4 py-2.5 text-left text-[10px] font-medium text-gray-500 uppercase tracking-wide">Updated</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {tasks.map((task) => {
                  const assignee = task.assignedUserId ? usersById.get(task.assignedUserId) : undefined;
                  return (
                    <tr
                      key={task.id}
                      className="hover:bg-gray-50 cursor-pointer"
                      onClick={() => setDetailTask(task)}
                    >
                      <td className="px-4 py-2">
                        <div className="flex items-center gap-2">
                          <i className={`${TASK_PRIORITY_ICONS[task.priority]} text-xs ${TASK_PRIORITY_COLORS[task.priority]}`} />
                          <span className="text-xs font-medium text-gray-800 line-clamp-1">{task.title}</span>
                        </div>
                      </td>
                      <td className="px-4 py-2">
                        <span className={`inline-flex text-[10px] font-medium px-1.5 py-0.5 rounded-full
                          ${task.status === 'COMPLETED'       ? 'bg-green-100 text-green-700' :
                            task.status === 'CANCELLED'       ? 'bg-red-100 text-red-700' :
                            task.status === 'IN_PROGRESS'     ? 'bg-blue-100 text-blue-700' :
                            task.status === 'WAITING_BLOCKED' ? 'bg-amber-100 text-amber-700' :
                                                                'bg-gray-100 text-gray-600'}`}>
                          {TASK_STATUS_LABELS[task.status]}
                        </span>
                      </td>
                      <td className="px-4 py-2">
                        <span className={`text-[10px] font-medium ${TASK_PRIORITY_COLORS[task.priority]}`}>
                          {PRIORITY_LABELS[task.priority] ?? task.priority}
                        </span>
                      </td>
                      <td className="px-4 py-2">
                        {assignee ? (
                          <div className="flex items-center gap-1.5">
                            <div className={`w-5 h-5 rounded-full flex items-center justify-center text-white text-[9px] font-bold shrink-0 ${avatarColor(task.assignedUserId!)}`}>
                              {getInitials(assignee.firstName, assignee.lastName)}
                            </div>
                            <span className="text-xs text-gray-700 whitespace-nowrap">
                              {assignee.firstName} {assignee.lastName}
                            </span>
                          </div>
                        ) : task.assignedUserId ? (
                          <span className="flex items-center gap-1 text-[10px] text-gray-400">
                            <i className="ri-user-line" />Assigned
                          </span>
                        ) : (
                          <span className="text-gray-300 text-[10px]">&mdash;</span>
                        )}
                      </td>
                      <td className="px-4 py-2">
                        {task.caseId ? (
                          <span className="inline-flex items-center gap-0.5 text-[10px] font-mono font-medium bg-slate-100 text-slate-600 border border-slate-200 rounded px-1.5 py-0.5">
                            <i className="ri-briefcase-line text-[10px]" />
                            {shortCaseId(task.caseId)}
                          </span>
                        ) : (
                          <span className="text-gray-300 text-[10px]">&mdash;</span>
                        )}
                      </td>
                      <td className="px-4 py-2">
                        {task.linkedLiens.length > 0 ? (
                          <span className="bg-purple-50 text-purple-700 text-[10px] rounded px-1.5 py-0.5">
                            {task.linkedLiens.length}
                          </span>
                        ) : <span className="text-gray-300 text-[10px]">&mdash;</span>}
                      </td>
                      <td className="px-4 py-2">
                        <span className={`text-[10px] ${isOverdue(task.dueDate, task.status) ? 'text-red-600 font-medium' : 'text-gray-400'}`}>
                          {formatDate(task.dueDate)}
                          {isOverdue(task.dueDate, task.status) && <i className="ri-error-warning-line ml-1" />}
                        </span>
                      </td>
                      <td className="px-4 py-2 text-[10px] text-gray-400">{formatDate(task.updatedAtUtc)}</td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
          {tasks.length === 0 && !loading && (
            <div className="py-8 text-center text-xs text-gray-400">No tasks match your filters.</div>
          )}
        </div>
      )}

      <CreateEditTaskForm
        open={showCreate}
        onClose={() => setShowCreate(false)}
        onSaved={() => { fetchTasks(); setShowCreate(false); }}
      />

      {editTask && (
        <CreateEditTaskForm
          open
          onClose={() => setEditTask(undefined)}
          onSaved={() => { fetchTasks(); setEditTask(undefined); }}
          editTask={editTask}
        />
      )}

      <TaskDetailDrawer
        task={detailTask}
        onClose={() => setDetailTask(null)}
        onEdit={(t) => { setDetailTask(null); setEditTask(t); }}
        onStatusChange={(updated) => {
          setTasks((prev) => prev.map((t) => (t.id === updated.id ? updated : t)));
          setDetailTask(updated);
        }}
      />
    </div>
  );
}
