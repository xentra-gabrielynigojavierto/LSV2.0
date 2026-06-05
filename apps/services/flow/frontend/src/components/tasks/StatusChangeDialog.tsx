"use client";

import { useState } from "react";
import {
  TASK_STATUSES,
  STATUS_LABELS,
  type TaskItemStatus,
  type TaskResponse,
} from "@/types/task";
import { StatusBadge } from "@/components/ui/StatusBadge";

interface StatusChangeDialogProps {
  task: TaskResponse;
  onConfirm: (taskId: string, newStatus: TaskItemStatus) => void;
  onClose: () => void;
}

export function StatusChangeDialog({
  task,
  onConfirm,
  onClose,
}: StatusChangeDialogProps) {
  const [selected, setSelected] = useState<TaskItemStatus>(task.status);
  const [error, setError] = useState<string | null>(null);

  const handleConfirm = () => {
    if (selected === task.status) {
      onClose();
      return;
    }
    setError(null);
    onConfirm(task.id, selected);
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="w-full max-w-sm rounded-lg bg-white p-6 shadow-xl">
        <h3 className="text-lg font-semibold text-gray-900 mb-1">Change Status</h3>
        <p className="text-sm text-gray-500 mb-4 truncate">
          {task.title}
        </p>
        <div className="mb-2 text-sm text-gray-600">
          Current: <StatusBadge status={task.status} />
        </div>
        <div className="mb-4">
          <label className="block text-sm font-medium text-gray-700 mb-1">
            New Status
          </label>
          <select
            value={selected}
            onChange={(e) => setSelected(e.target.value as TaskItemStatus)}
            className="w-full rounded border border-gray-300 bg-white px-3 py-2 text-sm text-gray-700 focus:border-blue-500 focus:outline-none"
          >
            {TASK_STATUSES.map((s) => (
              <option key={s} value={s}>
                {STATUS_LABELS[s]}
              </option>
            ))}
          </select>
        </div>
        {error && (
          <p className="mb-3 text-sm text-red-600">{error}</p>
        )}
        <div className="flex justify-end gap-2">
          <button
            onClick={onClose}
            className="rounded border border-gray-300 bg-white px-4 py-2 text-sm text-gray-700 hover:bg-gray-50"
          >
            Cancel
          </button>
          <button
            onClick={handleConfirm}
            disabled={selected === task.status}
            className="rounded bg-blue-600 px-4 py-2 text-sm text-white hover:bg-blue-700 disabled:opacity-40 disabled:cursor-not-allowed"
          >
            Update
          </button>
        </div>
      </div>
    </div>
  );
}
