"use client";

import { StatusBadge } from "@/components/ui/StatusBadge";
import { ProductKeyBadge } from "@/components/ui/ProductKeyBadge";
import type { TaskResponse } from "@/types/task";

interface SortState {
  sortBy: string;
  sortDirection: "asc" | "desc";
}

interface TaskTableProps {
  tasks: TaskResponse[];
  sort: SortState;
  onSortChange: (sort: SortState) => void;
  onRowClick: (task: TaskResponse) => void;
  onStatusClick: (task: TaskResponse) => void;
}

function formatDate(dateStr?: string): string {
  if (!dateStr) return "\u2014";
  const d = new Date(dateStr);
  return d.toLocaleDateString("en-US", {
    month: "short",
    day: "numeric",
    year: "numeric",
  });
}

function formatDateTime(dateStr?: string): string {
  if (!dateStr) return "\u2014";
  const d = new Date(dateStr);
  return d.toLocaleDateString("en-US", {
    month: "short",
    day: "numeric",
    hour: "numeric",
    minute: "2-digit",
  });
}

interface ColumnDef {
  key: string;
  label: string;
  sortable: boolean;
}

const COLUMNS: ColumnDef[] = [
  { key: "title", label: "Title", sortable: true },
  { key: "productKey", label: "Product", sortable: false },
  { key: "status", label: "Status", sortable: true },
  { key: "assignedToUserId", label: "User", sortable: false },
  { key: "assignedToRoleKey", label: "Role", sortable: false },
  { key: "assignedToOrgId", label: "Org", sortable: false },
  { key: "context", label: "Context", sortable: false },
  { key: "dueDate", label: "Due Date", sortable: true },
  { key: "updatedAt", label: "Updated", sortable: true },
];

export function TaskTable({
  tasks,
  sort,
  onSortChange,
  onRowClick,
  onStatusClick,
}: TaskTableProps) {
  const handleSort = (key: string) => {
    if (sort.sortBy === key) {
      onSortChange({
        sortBy: key,
        sortDirection: sort.sortDirection === "asc" ? "desc" : "asc",
      });
    } else {
      onSortChange({ sortBy: key, sortDirection: "asc" });
    }
  };

  const renderSortIcon = (key: string) => {
    if (sort.sortBy !== key) return null;
    return (
      <span className="ml-1 text-blue-600">
        {sort.sortDirection === "asc" ? "\u2191" : "\u2193"}
      </span>
    );
  };

  return (
    <div className="overflow-x-auto rounded-lg border border-gray-200 bg-white">
      <table className="min-w-full divide-y divide-gray-200">
        <thead className="bg-gray-50">
          <tr>
            {COLUMNS.map((col) => (
              <th
                key={col.key}
                scope="col"
                className={`px-4 py-3 text-left text-xs font-medium uppercase tracking-wider text-gray-500 ${
                  col.sortable ? "cursor-pointer select-none hover:text-gray-700" : ""
                }`}
                onClick={() => col.sortable && handleSort(col.key)}
              >
                {col.label}
                {col.sortable && renderSortIcon(col.key)}
              </th>
            ))}
            <th scope="col" className="px-4 py-3 text-left text-xs font-medium uppercase tracking-wider text-gray-500">
              Actions
            </th>
          </tr>
        </thead>
        <tbody className="divide-y divide-gray-100">
          {tasks.map((task) => (
            <tr
              key={task.id}
              onClick={() => onRowClick(task)}
              className="cursor-pointer hover:bg-gray-50 transition-colors"
            >
              <td className="whitespace-nowrap px-4 py-3 text-sm font-medium text-gray-900 max-w-[280px] truncate">
                {task.title}
              </td>
              <td className="whitespace-nowrap px-4 py-3 text-sm">
                <ProductKeyBadge productKey={task.productKey} />
              </td>
              <td className="whitespace-nowrap px-4 py-3 text-sm">
                <StatusBadge status={task.status} />
              </td>
              <td className="whitespace-nowrap px-4 py-3 text-sm text-gray-600">
                {task.assignedToUserId ?? "\u2014"}
              </td>
              <td className="whitespace-nowrap px-4 py-3 text-sm text-gray-600">
                {task.assignedToRoleKey ?? "\u2014"}
              </td>
              <td className="whitespace-nowrap px-4 py-3 text-sm text-gray-600">
                {task.assignedToOrgId ?? "\u2014"}
              </td>
              <td className="whitespace-nowrap px-4 py-3 text-sm text-gray-600">
                {task.context
                  ? `${task.context.contextType}:${task.context.contextId}`
                  : "\u2014"}
              </td>
              <td className="whitespace-nowrap px-4 py-3 text-sm text-gray-600">
                {formatDate(task.dueDate)}
              </td>
              <td className="whitespace-nowrap px-4 py-3 text-sm text-gray-600">
                {formatDateTime(task.updatedAt)}
              </td>
              <td className="whitespace-nowrap px-4 py-3 text-sm">
                <button
                  onClick={(e) => {
                    e.stopPropagation();
                    onStatusClick(task);
                  }}
                  className="rounded border border-gray-300 bg-white px-2 py-1 text-xs text-gray-600 hover:bg-gray-50 hover:text-gray-800"
                >
                  Change Status
                </button>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
