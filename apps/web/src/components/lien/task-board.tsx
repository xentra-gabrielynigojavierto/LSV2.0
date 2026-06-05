'use client';

import type { TaskDto } from '@/lib/liens/lien-tasks.types';
import type { TenantUser } from '@/types/tenant';
import { TaskCard } from './task-card';

export interface BoardColumn {
  status: string;
  label: string;
  borderColor: string;
  items: TaskDto[];
}

interface TaskBoardProps {
  columns: BoardColumn[];
  usersById: Map<string, TenantUser>;
  onTaskClick: (task: TaskDto) => void;
  onNewTask?: () => void;
}

export function TaskBoard({ columns, usersById, onTaskClick, onNewTask }: TaskBoardProps) {
  return (
    <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-3">
      {columns.map((col) => (
        <div
          key={col.status}
          className={`bg-gray-50 rounded-lg border border-gray-200 border-t-4 ${col.borderColor} flex flex-col min-h-[120px]`}
        >
          <div className="px-3 py-2 flex items-center justify-between border-b border-gray-100 shrink-0">
            <span className="text-xs font-semibold text-gray-700">{col.label}</span>
            <span className="text-[10px] font-medium text-gray-400 bg-white border border-gray-200 rounded-full px-1.5 py-0.5 leading-none tabular-nums">
              {col.items.length}
            </span>
          </div>
          <div className="p-2 space-y-1.5 flex-1">
            {col.items.map((task) => (
              <TaskCard
                key={task.id}
                task={task}
                onClick={onTaskClick}
                compact
                assigneeUser={task.assignedUserId ? (usersById.get(task.assignedUserId) ?? null) : null}
              />
            ))}
            {col.items.length === 0 && (
              <div className="flex flex-col items-center justify-center py-4 gap-1">
                <span className="text-[11px] text-gray-300">No tasks</span>
                {onNewTask && (
                  <button
                    onClick={onNewTask}
                    className="text-[10px] text-primary/60 hover:text-primary flex items-center gap-0.5 transition-colors"
                  >
                    <i className="ri-add-line" /> Add task
                  </button>
                )}
              </div>
            )}
          </div>
        </div>
      ))}
    </div>
  );
}
