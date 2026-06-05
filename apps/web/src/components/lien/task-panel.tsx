'use client';

import { useState, useEffect, useCallback } from 'react';
import { lienTasksService } from '@/lib/liens/lien-tasks.service';
import { apiClient } from '@/lib/api-client';
import type { TaskDto, TasksQuery } from '@/lib/liens/lien-tasks.types';
import { ACTIVE_TASK_STATUSES } from '@/lib/liens/lien-tasks.types';
import type { TenantUser } from '@/types/tenant';
import { TaskCard } from './task-card';
import { TaskDetailDrawer } from './task-detail-drawer';
import { CreateEditTaskForm } from './forms/create-edit-task-form';

interface TaskPanelProps {
  caseId?: string;
  lienId?: string;
  workflowStageId?: string | null;
  title?: string;
}

export function TaskPanel({ caseId, lienId, workflowStageId, title = 'Tasks' }: TaskPanelProps) {
  const [tasks, setTasks] = useState<TaskDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [showCreate, setShowCreate] = useState(false);
  const [editTask, setEditTask] = useState<TaskDto | undefined>(undefined);
  const [selectedTask, setSelectedTask] = useState<TaskDto | null>(null);
  const [statusFilter, setStatusFilter] = useState<string>('active');
  const [usersById, setUsersById] = useState<Map<string, TenantUser>>(new Map());

  useEffect(() => {
    apiClient.get<TenantUser[]>('/identity/api/users')
      .then(({ data }) => {
        const map = new Map<string, TenantUser>();
        (data ?? []).forEach((u) => map.set(u.id, u));
        setUsersById(map);
      })
      .catch(() => { /* best-effort — cards fall back to icon */ });
  }, []);

  const fetchTasks = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const query: TasksQuery = {
        caseId: caseId ?? undefined,
        lienId: lienId ?? undefined,
        pageSize: 100,
      };
      const result = await lienTasksService.getTasks(query);
      setTasks(result.items);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load tasks');
    } finally {
      setLoading(false);
    }
  }, [caseId, lienId]);

  useEffect(() => { fetchTasks(); }, [fetchTasks]);

  const filteredTasks = statusFilter === 'active'
    ? tasks.filter((t) => ACTIVE_TASK_STATUSES.includes(t.status as never))
    : tasks;

  const counts = {
    active: tasks.filter((t) => ACTIVE_TASK_STATUSES.includes(t.status as never)).length,
    all: tasks.length,
  };

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-2">
          <h3 className="text-sm font-semibold text-gray-700">{title}</h3>
          <span className="text-xs bg-gray-100 text-gray-600 rounded-full px-2 py-0.5">
            {counts.active} active
          </span>
        </div>
        <div className="flex items-center gap-2">
          <div className="flex rounded-lg border border-gray-200 overflow-hidden text-xs">
            <button
              onClick={() => setStatusFilter('active')}
              className={`px-3 py-1.5 ${statusFilter === 'active' ? 'bg-primary text-white' : 'text-gray-600 hover:bg-gray-50'}`}
            >
              Active
            </button>
            <button
              onClick={() => setStatusFilter('all')}
              className={`px-3 py-1.5 ${statusFilter === 'all' ? 'bg-primary text-white' : 'text-gray-600 hover:bg-gray-50'}`}
            >
              All ({counts.all})
            </button>
          </div>
          <button
            onClick={() => setShowCreate(true)}
            className="flex items-center gap-1.5 bg-primary text-white text-xs px-3 py-1.5 rounded-lg hover:bg-primary/90"
          >
            <i className="ri-add-line" /> New Task
          </button>
        </div>
      </div>

      {loading && (
        <div className="text-center py-8 text-sm text-gray-400">
          <i className="ri-loader-4-line animate-spin text-xl mb-2 block" />
          Loading tasks...
        </div>
      )}

      {error && (
        <div className="bg-red-50 border border-red-200 rounded-lg px-4 py-3 text-sm text-red-700">
          {error}
        </div>
      )}

      {!loading && !error && filteredTasks.length === 0 && (
        <div className="text-center py-10 text-sm text-gray-400">
          <i className="ri-task-line text-3xl mb-2 block text-gray-300" />
          No tasks{statusFilter === 'active' ? ' in progress' : ''}.{' '}
          <button onClick={() => setShowCreate(true)} className="text-primary hover:underline">
            Create one
          </button>
        </div>
      )}

      {!loading && filteredTasks.length > 0 && (
        <div className="space-y-2">
          {filteredTasks.map((task) => (
            <TaskCard
              key={task.id}
              task={task}
              onClick={(t) => setSelectedTask(t)}
              compact
              assigneeUser={task.assignedUserId ? (usersById.get(task.assignedUserId) ?? null) : null}
            />
          ))}
        </div>
      )}

      <TaskDetailDrawer
        task={selectedTask}
        onClose={() => setSelectedTask(null)}
        onEdit={(t) => { setSelectedTask(null); setEditTask(t); }}
        onStatusChange={(updated) => {
          setTasks((prev) => prev.map((t) => (t.id === updated.id ? updated : t)));
          setSelectedTask(updated);
        }}
      />

      <CreateEditTaskForm
        open={showCreate}
        onClose={() => setShowCreate(false)}
        onSaved={() => { fetchTasks(); setShowCreate(false); }}
        prefillCaseId={caseId}
        prefillLienId={lienId}
        prefillWorkflowStageId={workflowStageId ?? undefined}
      />

      {editTask && (
        <CreateEditTaskForm
          open
          onClose={() => setEditTask(undefined)}
          onSaved={() => { fetchTasks(); setEditTask(undefined); }}
          editTask={editTask}
        />
      )}
    </div>
  );
}
