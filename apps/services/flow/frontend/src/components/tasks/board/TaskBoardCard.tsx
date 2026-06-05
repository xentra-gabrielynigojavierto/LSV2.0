"use client";

import type { TaskResponse, TaskItemStatus } from "@/types/task";
import { TASK_STATUSES, STATUS_LABELS } from "@/types/task";
import { getTransitionLabel } from "@/lib/transitionLabels";
import { ProductKeyBadge } from "@/components/ui/ProductKeyBadge";

interface TaskBoardCardProps {
  task: TaskResponse;
  isMoving?: boolean;
  onCardClick: (task: TaskResponse) => void;
  onMoveTask: (taskId: string, newStatus: TaskItemStatus) => void;
}

function formatDate(dateStr?: string): string {
  if (!dateStr) return "";
  return new Date(dateStr).toLocaleDateString("en-US", {
    month: "short",
    day: "numeric",
  });
}

export function TaskBoardCard({ task, isMoving, onCardClick, onMoveTask }: TaskBoardCardProps) {
  const handleDragStart = (e: React.DragEvent) => {
    e.dataTransfer.setData("text/plain", task.id);
    e.dataTransfer.effectAllowed = "move";
  };

  const otherStatuses = task.allowedNextStatuses ?? TASK_STATUSES.filter((s) => s !== task.status);

  return (
    <div
      draggable
      onDragStart={handleDragStart}
      onClick={() => onCardClick(task)}
      className={`rounded-lg border border-gray-200 bg-white p-3 shadow-sm cursor-pointer transition-all hover:shadow-md hover:border-gray-300 ${
        isMoving ? "opacity-50 pointer-events-none" : ""
      }`}
    >
      <div className="flex items-start justify-between gap-2 mb-2">
        <h4 className="text-sm font-medium text-gray-900 line-clamp-2 flex-1">
          {task.title}
        </h4>
        <ProductKeyBadge productKey={task.productKey} />
      </div>

      {task.description && (
        <p className="text-xs text-gray-500 line-clamp-2 mb-2">{task.description}</p>
      )}

      <div className="flex flex-wrap gap-1.5 mb-2">
        {task.assignedToUserId && (
          <span className="inline-flex items-center rounded bg-gray-100 px-1.5 py-0.5 text-xs text-gray-600">
            {task.assignedToUserId}
          </span>
        )}
        {task.assignedToRoleKey && (
          <span className="inline-flex items-center rounded bg-indigo-50 px-1.5 py-0.5 text-xs text-indigo-600">
            {task.assignedToRoleKey}
          </span>
        )}
        {task.assignedToOrgId && (
          <span className="inline-flex items-center rounded bg-purple-50 px-1.5 py-0.5 text-xs text-purple-600">
            {task.assignedToOrgId}
          </span>
        )}
      </div>

      <div className="flex items-center justify-between">
        <div className="flex items-center gap-2">
          {task.context && (
            <span className="text-xs text-gray-400">
              {task.context.contextType}:{task.context.contextId}
            </span>
          )}
          {task.dueDate && (
            <span className={`text-xs ${
              new Date(task.dueDate) < new Date() ? "text-red-500 font-medium" : "text-gray-400"
            }`}>
              {formatDate(task.dueDate)}
            </span>
          )}
        </div>
        <div className="relative group">
          <button
            onClick={(e) => {
              e.stopPropagation();
            }}
            className="rounded p-0.5 text-gray-400 hover:text-gray-600 hover:bg-gray-100"
          >
            <svg className="h-4 w-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 5v.01M12 12v.01M12 19v.01" />
            </svg>
          </button>
          <div className="absolute right-0 top-full z-10 mt-1 hidden w-36 rounded-lg border border-gray-200 bg-white py-1 shadow-lg group-hover:block">
            {otherStatuses.map((s) => (
              <button
                key={s}
                onClick={(e) => {
                  e.stopPropagation();
                  onMoveTask(task.id, s);
                }}
                className="block w-full px-3 py-1.5 text-left text-xs text-gray-700 hover:bg-gray-50"
              >
                {getTransitionLabel(task.status, s)}
              </button>
            ))}
          </div>
        </div>
      </div>
    </div>
  );
}
