"use client";

import { useSortable } from "@dnd-kit/sortable";
import { CSS } from "@dnd-kit/utilities";
import type { WorkflowStage } from "@/types/workflow";
import type { TaskItemStatus } from "@/types/task";
import { TASK_STATUSES, STATUS_LABELS } from "@/types/task";

interface Props {
  stage: WorkflowStage;
  isEditing: boolean;
  form: { key: string; name: string; mappedStatus: TaskItemStatus; isInitial: boolean; isTerminal: boolean };
  hasInitial: boolean;
  busy: boolean;
  onStartEdit: (stage: WorkflowStage) => void;
  onDelete: (stageId: string) => void;
  onSaveEdit: () => void;
  onCancelEdit: () => void;
  onFormChange: (form: { key: string; name: string; mappedStatus: TaskItemStatus; isInitial: boolean; isTerminal: boolean }) => void;
  disabled?: boolean;
}

export function DraggableStageItem({
  stage,
  isEditing,
  form,
  hasInitial,
  busy,
  onStartEdit,
  onDelete,
  onSaveEdit,
  onCancelEdit,
  onFormChange,
  disabled,
}: Props) {
  const {
    attributes,
    listeners,
    setNodeRef,
    transform,
    transition,
    isDragging,
  } = useSortable({ id: stage.id, disabled: disabled || isEditing });

  const style = {
    transform: CSS.Transform.toString(transform),
    transition,
    zIndex: isDragging ? 50 : undefined,
    opacity: isDragging ? 0.5 : 1,
  };

  return (
    <div
      ref={setNodeRef}
      style={style}
      className={`rounded-lg border bg-white px-3 py-2.5 ${
        isDragging ? "border-blue-300 shadow-lg" : "border-gray-200"
      }`}
    >
      {isEditing ? (
        <div className="space-y-3">
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block text-xs text-gray-500 mb-1">Name</label>
              <input
                type="text"
                value={form.name}
                onChange={(e) => onFormChange({ ...form, name: e.target.value })}
                className="w-full rounded border border-gray-300 px-2 py-1.5 text-sm focus:border-blue-500 focus:outline-none"
              />
            </div>
            <div>
              <label className="block text-xs text-gray-500 mb-1">Mapped Status</label>
              <select
                value={form.mappedStatus}
                onChange={(e) => onFormChange({ ...form, mappedStatus: e.target.value as TaskItemStatus })}
                className="w-full rounded border border-gray-300 px-2 py-1.5 text-sm focus:border-blue-500 focus:outline-none"
              >
                {TASK_STATUSES.map((s) => (
                  <option key={s} value={s}>{STATUS_LABELS[s]}</option>
                ))}
              </select>
            </div>
          </div>
          <div className="flex items-center gap-4">
            <label className="flex items-center gap-1.5 text-xs text-gray-600">
              <input
                type="checkbox"
                checked={form.isInitial}
                onChange={(e) => onFormChange({ ...form, isInitial: e.target.checked })}
                disabled={hasInitial && !stage.isInitial}
                className="rounded border-gray-300"
              />
              Initial stage
            </label>
            <label className="flex items-center gap-1.5 text-xs text-gray-600">
              <input
                type="checkbox"
                checked={form.isTerminal}
                onChange={(e) => onFormChange({ ...form, isTerminal: e.target.checked })}
                className="rounded border-gray-300"
              />
              Terminal stage
            </label>
          </div>
          <div className="flex justify-end gap-2">
            <button onClick={onCancelEdit} className="text-xs text-gray-500 hover:text-gray-700 px-2 py-1">Cancel</button>
            <button onClick={onSaveEdit} disabled={busy} className="text-xs text-blue-600 hover:text-blue-800 font-medium px-2 py-1 disabled:opacity-50">Save</button>
          </div>
        </div>
      ) : (
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-3">
            <button
              {...attributes}
              {...listeners}
              className={`cursor-grab active:cursor-grabbing text-gray-300 hover:text-gray-500 focus-visible:outline-2 focus-visible:outline-blue-500 focus-visible:text-gray-500 rounded touch-none ${disabled ? "cursor-not-allowed opacity-30" : ""}`}
              aria-label="Drag to reorder"
            >
              <svg width="16" height="16" viewBox="0 0 16 16" fill="currentColor">
                <circle cx="5" cy="3" r="1.5" />
                <circle cx="11" cy="3" r="1.5" />
                <circle cx="5" cy="8" r="1.5" />
                <circle cx="11" cy="8" r="1.5" />
                <circle cx="5" cy="13" r="1.5" />
                <circle cx="11" cy="13" r="1.5" />
              </svg>
            </button>
            <div>
              <div className="flex items-center gap-2">
                <span className="text-sm font-medium text-gray-900">{stage.name}</span>
                <span className="text-xs text-gray-400 font-mono">{stage.key}</span>
                {stage.isInitial && (
                  <span className="inline-flex items-center rounded-full bg-green-100 px-1.5 py-0.5 text-[10px] font-medium text-green-700">
                    Initial
                  </span>
                )}
                {stage.isTerminal && (
                  <span className="inline-flex items-center rounded-full bg-gray-100 px-1.5 py-0.5 text-[10px] font-medium text-gray-600">
                    Terminal
                  </span>
                )}
              </div>
              <p className="text-xs text-gray-500">→ {STATUS_LABELS[stage.mappedStatus]}</p>
            </div>
          </div>
          <div className="flex items-center gap-2">
            <button onClick={() => onStartEdit(stage)} disabled={disabled} className="text-xs text-gray-500 hover:text-blue-600 disabled:opacity-50">Edit</button>
            <button onClick={() => onDelete(stage.id)} disabled={busy || disabled} className="text-xs text-gray-500 hover:text-red-600 disabled:opacity-50">Delete</button>
          </div>
        </div>
      )}
    </div>
  );
}
