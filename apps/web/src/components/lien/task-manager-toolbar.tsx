'use client';

import type { ReactNode } from 'react';
import type { TaskStatus, TaskPriority } from '@/lib/liens/lien-tasks.types';
import { TASK_STATUS_LABELS, ALL_TASK_STATUSES } from '@/lib/liens/lien-tasks.types';

const PRIORITY_LABELS: Record<string, string> = {
  LOW: 'Low', MEDIUM: 'Medium', HIGH: 'High', URGENT: 'Urgent',
};

interface TaskManagerToolbarProps {
  search: string;
  onSearch: (v: string) => void;
  statusFilter: TaskStatus | '';
  onStatusFilter: (v: TaskStatus | '') => void;
  priorityFilter: TaskPriority | '';
  onPriorityFilter: (v: TaskPriority | '') => void;
  assigneeSlot?: ReactNode;
  activeFilterCount?: number;
  onClearFilters?: () => void;
}

export function TaskManagerToolbar({
  search,
  onSearch,
  statusFilter,
  onStatusFilter,
  priorityFilter,
  onPriorityFilter,
  assigneeSlot,
  activeFilterCount = 0,
  onClearFilters,
}: TaskManagerToolbarProps) {
  return (
    <div className="flex items-center gap-2 flex-wrap">
      <div className="relative flex-1 min-w-[160px]">
        <i className="ri-search-line absolute left-2.5 top-1/2 -translate-y-1/2 text-gray-400 text-[11px]" />
        <input
          type="text"
          value={search}
          onChange={(e) => onSearch(e.target.value)}
          placeholder="Search tasks..."
          className="w-full pl-7 pr-3 py-1.5 text-xs border border-gray-200 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary/30 bg-white"
        />
      </div>

      {assigneeSlot}

      <select
        value={statusFilter}
        onChange={(e) => onStatusFilter(e.target.value as TaskStatus | '')}
        className="text-xs border border-gray-200 rounded-lg px-2.5 py-1.5 focus:outline-none focus:ring-2 focus:ring-primary/30 bg-white"
      >
        <option value="">All Statuses</option>
        {ALL_TASK_STATUSES.map((s) => (
          <option key={s} value={s}>{TASK_STATUS_LABELS[s]}</option>
        ))}
      </select>

      <select
        value={priorityFilter}
        onChange={(e) => onPriorityFilter(e.target.value as TaskPriority | '')}
        className="text-xs border border-gray-200 rounded-lg px-2.5 py-1.5 focus:outline-none focus:ring-2 focus:ring-primary/30 bg-white"
      >
        <option value="">All Priorities</option>
        {(['LOW', 'MEDIUM', 'HIGH', 'URGENT'] as TaskPriority[]).map((p) => (
          <option key={p} value={p}>{PRIORITY_LABELS[p]}</option>
        ))}
      </select>

      {activeFilterCount > 0 && onClearFilters && (
        <button
          onClick={onClearFilters}
          className="flex items-center gap-1 text-xs text-gray-500 hover:text-gray-700 border border-gray-200 rounded-lg px-2.5 py-1.5 bg-white transition-colors"
        >
          <i className="ri-close-line" /> Clear ({activeFilterCount})
        </button>
      )}
    </div>
  );
}
