'use client';

import type { TaskDto } from '@/lib/liens/lien-tasks.types';
import {
  TASK_STATUS_LABELS,
  TASK_PRIORITY_COLORS,
  TASK_PRIORITY_ICONS,
} from '@/lib/liens/lien-tasks.types';

interface AssigneeInfo {
  firstName: string;
  lastName: string;
  email?: string;
}

interface TaskCardProps {
  task:         TaskDto;
  onClick?:     (task: TaskDto) => void;
  compact?:     boolean;
  assigneeUser?: AssigneeInfo | null;
}

const PRIORITY_LABELS: Record<string, string> = {
  LOW:    'Low',
  MEDIUM: 'Medium',
  HIGH:   'High',
  URGENT: 'Urgent',
};

const PRIORITY_BG: Record<string, string> = {
  LOW:    'bg-gray-100 text-gray-600',
  MEDIUM: 'bg-blue-50 text-blue-600',
  HIGH:   'bg-orange-50 text-orange-600',
  URGENT: 'bg-red-50 text-red-600',
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
  if (!val) return '—';
  try {
    const d = new Date(val);
    if (isNaN(d.getTime())) return val;
    return d.toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' });
  } catch {
    return val;
  }
}

function isOverdue(dueDate?: string | null, status?: string): boolean {
  if (!dueDate || status === 'COMPLETED' || status === 'CANCELLED') return false;
  return new Date(dueDate) < new Date();
}

function shortCaseId(caseId: string): string {
  return caseId.length > 8 ? caseId.slice(0, 8).toUpperCase() : caseId.toUpperCase();
}

export function TaskCard({ task, onClick, compact = false, assigneeUser }: TaskCardProps) {
  const overdue = isOverdue(task.dueDate, task.status);

  return (
    <div
      className={`bg-white border border-gray-200 rounded-lg p-2 shadow-sm hover:shadow-md transition-shadow ${onClick ? 'cursor-pointer' : ''}`}
      onClick={() => onClick?.(task)}
    >
      {/* Priority + Title */}
      <div className="flex items-start gap-2 mb-1.5">
        <div className="flex-1 min-w-0">
          {/* Priority pill */}
          <span className={`inline-flex items-center gap-1 text-[10px] font-semibold px-1.5 py-0.5 rounded mb-1 ${PRIORITY_BG[task.priority]}`}>
            <i className={`${TASK_PRIORITY_ICONS[task.priority]} text-[10px] ${TASK_PRIORITY_COLORS[task.priority]}`} />
            {PRIORITY_LABELS[task.priority] ?? task.priority}
          </span>
          <p className={`font-medium text-gray-800 leading-tight ${compact ? 'text-xs' : 'text-sm'} line-clamp-2`}>
            {task.title}
          </p>
          {!compact && task.description && (
            <p className="text-xs text-gray-500 mt-1 line-clamp-2">{task.description}</p>
          )}
        </div>
      </div>

      {/* Meta row: case ID, liens, due date */}
      <div className="flex items-center gap-1.5 flex-wrap mb-1.5">
        {task.caseId && (
          <span className="inline-flex items-center gap-0.5 text-[10px] font-mono font-medium bg-slate-100 text-slate-600 border border-slate-200 rounded px-1.5 py-0.5">
            <i className="ri-briefcase-line text-[10px]" />
            {shortCaseId(task.caseId)}
          </span>
        )}
        {task.linkedLiens.length > 0 && (
          <span className="text-[10px] bg-purple-50 text-purple-700 rounded px-1.5 py-0.5">
            <i className="ri-stack-line mr-0.5" />{task.linkedLiens.length} lien{task.linkedLiens.length !== 1 ? 's' : ''}
          </span>
        )}
        {task.isSystemGenerated && (
          <span className="text-[10px] bg-violet-50 text-violet-700 border border-violet-200 rounded px-1.5 py-0.5 flex items-center gap-0.5">
            <i className="ri-robot-line" />Auto
          </span>
        )}
        {task.dueDate && (
          <span className={`text-[10px] flex items-center gap-0.5 ${overdue ? 'text-red-600 font-medium' : 'text-gray-400'}`}>
            <i className="ri-calendar-line" />{formatDate(task.dueDate)}
            {overdue && <i className="ri-error-warning-line" />}
          </span>
        )}
      </div>

      {/* Footer: assignee avatar + name on left, status badge on right */}
      <div className="flex items-center justify-between gap-2 pt-1 border-t border-gray-100">
        {/* Assignee */}
        {assigneeUser ? (
          <div className="flex items-center gap-1.5 min-w-0">
            <div className={`w-5 h-5 rounded-full flex items-center justify-center text-white text-[9px] font-bold shrink-0 ${avatarColor(task.assignedUserId ?? assigneeUser.email ?? 'u')}`}>
              {getInitials(assigneeUser.firstName, assigneeUser.lastName)}
            </div>
            <span className="text-[11px] text-gray-600 truncate font-medium">
              {assigneeUser.firstName} {assigneeUser.lastName}
            </span>
          </div>
        ) : task.assignedUserId ? (
          <div className="flex items-center gap-1.5">
            <div className={`w-5 h-5 rounded-full flex items-center justify-center text-white text-[9px] font-bold shrink-0 ${avatarColor(task.assignedUserId)}`}>
              <i className="ri-user-line text-[9px]" />
            </div>
            <span className="text-[11px] text-gray-400">Assigned</span>
          </div>
        ) : (
          <span className="text-[11px] text-gray-300 flex items-center gap-0.5">
            <i className="ri-user-line text-[10px]" />Unassigned
          </span>
        )}

        {/* Status badge */}
        <span className={`inline-flex items-center text-[10px] font-semibold px-1.5 py-0.5 rounded-full shrink-0
          ${task.status === 'COMPLETED'       ? 'bg-green-100 text-green-700' :
            task.status === 'CANCELLED'       ? 'bg-red-100 text-red-700' :
            task.status === 'IN_PROGRESS'     ? 'bg-blue-100 text-blue-700' :
            task.status === 'WAITING_BLOCKED' ? 'bg-amber-100 text-amber-700' :
                                                'bg-gray-100 text-gray-600'}`}>
          {TASK_STATUS_LABELS[task.status]}
        </span>
      </div>
    </div>
  );
}
