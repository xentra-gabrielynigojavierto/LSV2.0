'use client';

import type { TaskStat } from './task-manager-stats-strip';

type ViewMode = 'board' | 'list';

interface TaskManagerHeaderProps {
  title: string;
  stats?: TaskStat[];
  viewMode: ViewMode;
  onViewModeChange: (mode: ViewMode) => void;
  onNewTask: () => void;
}

export function TaskManagerHeader({
  title,
  stats,
  viewMode,
  onViewModeChange,
  onNewTask,
}: TaskManagerHeaderProps) {
  return (
    <div className="flex items-center justify-between gap-3">
      <div className="flex items-center gap-3 min-w-0">
        <span className="text-base font-semibold text-gray-800 shrink-0">{title}</span>

        {stats && stats.length > 0 && (
          <>
            <div className="w-px h-4 bg-gray-200 shrink-0" />
            <div className="flex items-center gap-2 flex-wrap">
              {stats.map((stat) => (
                <div key={stat.label} className="flex items-center gap-1 shrink-0">
                  <i className={`${stat.icon} text-[11px] ${stat.color}`} />
                  <span className="text-[11px] text-gray-500">{stat.label}</span>
                  <span className={`text-[11px] font-semibold tabular-nums ${stat.color}`}>{stat.value}</span>
                </div>
              ))}
            </div>
          </>
        )}
      </div>

      <div className="flex items-center gap-2 shrink-0">
        <div className="flex items-center border border-gray-200 rounded-lg overflow-hidden">
          <button
            onClick={() => onViewModeChange('board')}
            className={`px-2.5 py-1 text-xs flex items-center gap-1 transition-colors ${
              viewMode === 'board' ? 'bg-primary text-white' : 'bg-white text-gray-600 hover:bg-gray-50'
            }`}
          >
            <i className="ri-layout-column-line" /> Board
          </button>
          <button
            onClick={() => onViewModeChange('list')}
            className={`px-2.5 py-1 text-xs flex items-center gap-1 transition-colors ${
              viewMode === 'list' ? 'bg-primary text-white' : 'bg-white text-gray-600 hover:bg-gray-50'
            }`}
          >
            <i className="ri-list-unordered" /> List
          </button>
        </div>
        <button
          onClick={onNewTask}
          className="flex items-center gap-1 text-xs font-medium text-white bg-primary hover:bg-primary/90 rounded-lg px-3 py-1.5 transition-colors"
        >
          <i className="ri-add-line" /> New Task
        </button>
      </div>
    </div>
  );
}
