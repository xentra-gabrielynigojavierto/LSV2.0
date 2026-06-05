"use client";

import { useRef, useState } from "react";
import { TaskBoardCard } from "./TaskBoardCard";
import { STATUS_LABELS, STATUS_COLORS, type TaskItemStatus, type TaskResponse } from "@/types/task";

interface BoardColumnProps {
  status: TaskItemStatus;
  tasks: TaskResponse[];
  movingTaskId: string | null;
  onCardClick: (task: TaskResponse) => void;
  onMoveTask: (taskId: string, newStatus: TaskItemStatus) => void;
}

const COLUMN_HEADER_COLORS: Record<TaskItemStatus, string> = {
  Open: "border-blue-400",
  InProgress: "border-amber-400",
  Blocked: "border-red-400",
  Done: "border-emerald-400",
  Cancelled: "border-gray-300",
};

export function BoardColumn({ status, tasks, movingTaskId, onCardClick, onMoveTask }: BoardColumnProps) {
  const [dragOver, setDragOver] = useState(false);
  const dragCounterRef = useRef(0);

  const handleDragEnter = (e: React.DragEvent) => {
    e.preventDefault();
    dragCounterRef.current += 1;
    setDragOver(true);
  };

  const handleDragOver = (e: React.DragEvent) => {
    e.preventDefault();
    e.dataTransfer.dropEffect = "move";
  };

  const handleDragLeave = () => {
    dragCounterRef.current -= 1;
    if (dragCounterRef.current === 0) {
      setDragOver(false);
    }
  };

  const handleDrop = (e: React.DragEvent) => {
    e.preventDefault();
    dragCounterRef.current = 0;
    setDragOver(false);
    const taskId = e.dataTransfer.getData("text/plain");
    if (taskId) {
      onMoveTask(taskId, status);
    }
  };

  return (
    <div
      className={`flex w-72 flex-shrink-0 flex-col rounded-lg bg-gray-100 transition-colors ${
        dragOver ? "bg-blue-50 ring-2 ring-blue-300" : ""
      }`}
      onDragEnter={handleDragEnter}
      onDragOver={handleDragOver}
      onDragLeave={handleDragLeave}
      onDrop={handleDrop}
    >
      <div className={`border-t-4 ${COLUMN_HEADER_COLORS[status]} rounded-t-lg px-3 py-2.5`}>
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2">
            <h3 className="text-sm font-semibold text-gray-700">
              {STATUS_LABELS[status]}
            </h3>
            <span className={`inline-flex items-center justify-center rounded-full px-2 py-0.5 text-xs font-medium ${STATUS_COLORS[status]}`}>
              {tasks.length}
            </span>
          </div>
        </div>
      </div>

      <div className="flex-1 overflow-y-auto px-2 py-2 space-y-2" style={{ maxHeight: "calc(100vh - 260px)" }}>
        {tasks.length === 0 && (
          <div className={`rounded-lg border-2 border-dashed px-3 py-6 text-center ${
            dragOver ? "border-blue-300 bg-blue-50" : "border-gray-300"
          }`}>
            <p className="text-xs text-gray-400">No tasks</p>
          </div>
        )}
        {tasks.map((task) => (
          <TaskBoardCard
            key={task.id}
            task={task}
            isMoving={movingTaskId === task.id}
            onCardClick={onCardClick}
            onMoveTask={onMoveTask}
          />
        ))}
      </div>
    </div>
  );
}
