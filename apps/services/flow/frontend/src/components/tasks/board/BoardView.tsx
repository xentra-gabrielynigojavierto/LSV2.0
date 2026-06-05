"use client";

import { useEffect, useMemo, useRef, useState } from "react";
import { BoardColumn } from "./BoardColumn";
import { TASK_STATUSES, type TaskItemStatus, type TaskResponse } from "@/types/task";

interface BoardViewProps {
  tasks: TaskResponse[];
  onCardClick: (task: TaskResponse) => void;
  onMoveTask: (taskId: string, newStatus: TaskItemStatus) => Promise<void>;
}

export function BoardView({ tasks, onCardClick, onMoveTask }: BoardViewProps) {
  const [moveError, setMoveError] = useState<string | null>(null);
  const [movingTaskId, setMovingTaskId] = useState<string | null>(null);
  const errorTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  useEffect(() => {
    return () => {
      if (errorTimerRef.current) clearTimeout(errorTimerRef.current);
    };
  }, []);

  const columns = useMemo(() => {
    const grouped: Record<TaskItemStatus, TaskResponse[]> = {
      Open: [],
      InProgress: [],
      Blocked: [],
      Done: [],
      Cancelled: [],
    };
    for (const task of tasks) {
      if (grouped[task.status]) {
        grouped[task.status].push(task);
      }
    }
    return grouped;
  }, [tasks]);

  const handleMoveTask = async (taskId: string, newStatus: TaskItemStatus) => {
    const task = tasks.find((t) => t.id === taskId);
    if (!task || task.status === newStatus || movingTaskId) return;

    if (task.allowedNextStatuses && !task.allowedNextStatuses.includes(newStatus)) {
      setMoveError(`Cannot move to "${newStatus}" — not an allowed transition from "${task.status}"`);
      errorTimerRef.current = setTimeout(() => setMoveError(null), 5000);
      return;
    }

    if (errorTimerRef.current) {
      clearTimeout(errorTimerRef.current);
      errorTimerRef.current = null;
    }
    setMoveError(null);
    setMovingTaskId(taskId);

    try {
      await onMoveTask(taskId, newStatus);
    } catch (err) {
      setMoveError(
        err instanceof Error
          ? err.message
          : "Failed to move task"
      );
      errorTimerRef.current = setTimeout(() => setMoveError(null), 5000);
    } finally {
      setMovingTaskId(null);
    }
  };

  return (
    <div>
      {moveError && (
        <div className="mb-3 flex items-center justify-between rounded-lg border border-red-200 bg-red-50 px-4 py-2">
          <p className="text-sm text-red-700">{moveError}</p>
          <button
            onClick={() => setMoveError(null)}
            className="text-red-400 hover:text-red-600"
          >
            <svg className="h-4 w-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
            </svg>
          </button>
        </div>
      )}

      <div className="flex gap-3 overflow-x-auto pb-4">
        {TASK_STATUSES.map((status) => (
          <BoardColumn
            key={status}
            status={status}
            tasks={columns[status]}
            movingTaskId={movingTaskId}
            onCardClick={onCardClick}
            onMoveTask={handleMoveTask}
          />
        ))}
      </div>
    </div>
  );
}
