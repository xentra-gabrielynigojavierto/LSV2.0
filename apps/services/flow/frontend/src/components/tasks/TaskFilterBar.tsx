"use client";

import { TASK_STATUSES, STATUS_LABELS, type TaskItemStatus } from "@/types/task";

interface FilterValues {
  status?: TaskItemStatus;
  assignedToUserId?: string;
  assignedToRoleKey?: string;
  assignedToOrgId?: string;
  contextType?: string;
  contextId?: string;
}

interface TaskFilterBarProps {
  filters: FilterValues;
  onChange: (filters: FilterValues) => void;
}

export function TaskFilterBar({ filters, onChange }: TaskFilterBarProps) {
  const update = (key: keyof FilterValues, value: string) => {
    onChange({ ...filters, [key]: value || undefined });
  };

  const hasFilters = Object.values(filters).some((v) => v !== undefined && v !== "");

  return (
    <div className="rounded-lg border border-gray-200 bg-white p-4">
      <div className="flex items-center justify-between mb-3">
        <h3 className="text-sm font-medium text-gray-700">Filters</h3>
        {hasFilters && (
          <button
            onClick={() => onChange({})}
            className="text-xs text-blue-600 hover:text-blue-800"
          >
            Clear all
          </button>
        )}
      </div>
      <div className="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-6">
        <div>
          <label className="mb-1 block text-xs font-medium text-gray-500">Status</label>
          <select
            value={filters.status ?? ""}
            onChange={(e) => update("status", e.target.value)}
            className="w-full rounded border border-gray-300 bg-white px-2 py-1.5 text-sm text-gray-700 focus:border-blue-500 focus:outline-none"
          >
            <option value="">All</option>
            {TASK_STATUSES.map((s) => (
              <option key={s} value={s}>
                {STATUS_LABELS[s]}
              </option>
            ))}
          </select>
        </div>
        <div>
          <label className="mb-1 block text-xs font-medium text-gray-500">User</label>
          <input
            type="text"
            value={filters.assignedToUserId ?? ""}
            onChange={(e) => update("assignedToUserId", e.target.value)}
            placeholder="User ID"
            className="w-full rounded border border-gray-300 px-2 py-1.5 text-sm text-gray-700 placeholder:text-gray-400 focus:border-blue-500 focus:outline-none"
          />
        </div>
        <div>
          <label className="mb-1 block text-xs font-medium text-gray-500">Role</label>
          <input
            type="text"
            value={filters.assignedToRoleKey ?? ""}
            onChange={(e) => update("assignedToRoleKey", e.target.value)}
            placeholder="Role key"
            className="w-full rounded border border-gray-300 px-2 py-1.5 text-sm text-gray-700 placeholder:text-gray-400 focus:border-blue-500 focus:outline-none"
          />
        </div>
        <div>
          <label className="mb-1 block text-xs font-medium text-gray-500">Org</label>
          <input
            type="text"
            value={filters.assignedToOrgId ?? ""}
            onChange={(e) => update("assignedToOrgId", e.target.value)}
            placeholder="Org ID"
            className="w-full rounded border border-gray-300 px-2 py-1.5 text-sm text-gray-700 placeholder:text-gray-400 focus:border-blue-500 focus:outline-none"
          />
        </div>
        <div>
          <label className="mb-1 block text-xs font-medium text-gray-500">
            Context Type
          </label>
          <input
            type="text"
            value={filters.contextType ?? ""}
            onChange={(e) => update("contextType", e.target.value)}
            placeholder="e.g. case"
            className="w-full rounded border border-gray-300 px-2 py-1.5 text-sm text-gray-700 placeholder:text-gray-400 focus:border-blue-500 focus:outline-none"
          />
        </div>
        <div>
          <label className="mb-1 block text-xs font-medium text-gray-500">
            Context ID
          </label>
          <input
            type="text"
            value={filters.contextId ?? ""}
            onChange={(e) => update("contextId", e.target.value)}
            placeholder="ID"
            className="w-full rounded border border-gray-300 px-2 py-1.5 text-sm text-gray-700 placeholder:text-gray-400 focus:border-blue-500 focus:outline-none"
          />
        </div>
      </div>
    </div>
  );
}
