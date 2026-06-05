"use client";

import { useState, useCallback, useMemo } from "react";
import {
  DndContext,
  closestCenter,
  KeyboardSensor,
  PointerSensor,
  useSensor,
  useSensors,
  type DragEndEvent,
} from "@dnd-kit/core";
import {
  SortableContext,
  sortableKeyboardCoordinates,
  verticalListSortingStrategy,
} from "@dnd-kit/sortable";
import type { WorkflowStage } from "@/types/workflow";
import type { TaskItemStatus } from "@/types/task";
import { TASK_STATUSES, STATUS_LABELS } from "@/types/task";
import { DraggableStageItem } from "./DraggableStageItem";

interface ReorderUpdate {
  stageId: string;
  data: { name: string; mappedStatus: TaskItemStatus; order: number; isInitial: boolean; isTerminal: boolean };
}

interface Props {
  stages: WorkflowStage[];
  onAdd: (stage: { key: string; name: string; mappedStatus: TaskItemStatus; order: number; isInitial: boolean; isTerminal: boolean }) => Promise<void>;
  onUpdate: (stageId: string, stage: { name: string; mappedStatus: TaskItemStatus; order: number; isInitial: boolean; isTerminal: boolean }) => Promise<void>;
  onDelete: (stageId: string) => Promise<void>;
  onReorder?: (updates: ReorderUpdate[]) => Promise<void>;
  disabled?: boolean;
}

export function StageEditor({ stages, onAdd, onUpdate, onDelete, onReorder, disabled }: Props) {
  const [adding, setAdding] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [form, setForm] = useState({ key: "", name: "", mappedStatus: "Open" as TaskItemStatus, isInitial: false, isTerminal: false });
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [reordering, setReordering] = useState(false);
  const [localOrder, setLocalOrder] = useState<string[] | null>(null);

  const hasInitial = stages.some((s) => s.isInitial);

  const sensors = useSensors(
    useSensor(PointerSensor, { activationConstraint: { distance: 5 } }),
    useSensor(KeyboardSensor, { coordinateGetter: sortableKeyboardCoordinates })
  );

  const sortedStages = useMemo(() => {
    const sorted = [...stages].sort((a, b) => a.order - b.order);
    if (localOrder) {
      const stageMap = new Map(sorted.map((s) => [s.id, s]));
      const reordered = localOrder
        .map((id) => stageMap.get(id))
        .filter((s): s is WorkflowStage => !!s);
      const missing = sorted.filter((s) => !localOrder.includes(s.id));
      return [...reordered, ...missing];
    }
    return sorted;
  }, [stages, localOrder]);

  const resetForm = () => {
    setForm({ key: "", name: "", mappedStatus: "Open", isInitial: false, isTerminal: false });
    setError(null);
  };

  const handleStartAdd = () => {
    resetForm();
    setEditingId(null);
    setAdding(true);
  };

  const handleStartEdit = (stage: WorkflowStage) => {
    setForm({ key: stage.key, name: stage.name, mappedStatus: stage.mappedStatus, isInitial: stage.isInitial, isTerminal: stage.isTerminal });
    setEditingId(stage.id);
    setAdding(false);
    setError(null);
  };

  const handleSaveNew = async () => {
    if (!form.key.trim() || !form.name.trim()) {
      setError("Key and Name are required");
      return;
    }
    setBusy(true);
    setError(null);
    try {
      await onAdd({
        key: form.key.trim(),
        name: form.name.trim(),
        mappedStatus: form.mappedStatus,
        order: stages.length,
        isInitial: form.isInitial,
        isTerminal: form.isTerminal,
      });
      setAdding(false);
      resetForm();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to add stage");
    } finally {
      setBusy(false);
    }
  };

  const handleSaveEdit = async () => {
    if (!editingId || !form.name.trim()) {
      setError("Name is required");
      return;
    }
    setBusy(true);
    setError(null);
    try {
      const stage = stages.find((s) => s.id === editingId)!;
      await onUpdate(editingId, {
        name: form.name.trim(),
        mappedStatus: form.mappedStatus,
        order: stage.order,
        isInitial: form.isInitial,
        isTerminal: form.isTerminal,
      });
      setEditingId(null);
      resetForm();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to update stage");
    } finally {
      setBusy(false);
    }
  };

  const handleDelete = async (stageId: string) => {
    setBusy(true);
    setError(null);
    try {
      await onDelete(stageId);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to delete stage");
    } finally {
      setBusy(false);
    }
  };

  const handleDragEnd = useCallback(async (event: DragEndEvent) => {
    const { active, over } = event;
    if (!over || active.id === over.id) return;

    const oldIndex = sortedStages.findIndex((s) => s.id === active.id);
    const newIndex = sortedStages.findIndex((s) => s.id === over.id);
    if (oldIndex === -1 || newIndex === -1) return;

    const reordered = [...sortedStages];
    const [moved] = reordered.splice(oldIndex, 1);
    reordered.splice(newIndex, 0, moved);

    setLocalOrder(reordered.map((s) => s.id));

    const updates: ReorderUpdate[] = reordered
      .map((stage, idx) => ({
        stageId: stage.id,
        data: {
          name: stage.name,
          mappedStatus: stage.mappedStatus,
          order: idx,
          isInitial: stage.isInitial,
          isTerminal: stage.isTerminal,
        },
      }))
      .filter(({ stageId, data }) => {
        const original = stages.find((s) => s.id === stageId);
        return original && original.order !== data.order;
      });

    if (updates.length === 0) {
      setLocalOrder(null);
      return;
    }

    setReordering(true);
    setError(null);

    try {
      if (onReorder) {
        await onReorder(updates);
      } else {
        for (const { stageId, data } of updates) {
          await onUpdate(stageId, data);
        }
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to reorder stages");
    } finally {
      setReordering(false);
      setLocalOrder(null);
    }
  }, [sortedStages, stages, onUpdate, onReorder]);

  return (
    <div>
      <div className="flex items-center justify-between mb-3">
        <div className="flex items-center gap-2">
          <h3 className="text-sm font-semibold text-gray-900">Stages ({stages.length})</h3>
          {reordering && (
            <div className="flex items-center gap-1.5">
              <div className="h-3 w-3 animate-spin rounded-full border-2 border-gray-300 border-t-blue-600" />
              <span className="text-xs text-gray-500">Saving order…</span>
            </div>
          )}
        </div>
        <button
          onClick={handleStartAdd}
          disabled={adding || busy || disabled}
          className="text-xs text-blue-600 hover:text-blue-800 font-medium disabled:opacity-50"
        >
          + Add Stage
        </button>
      </div>

      {error && (
        <div className="mb-3 rounded-lg border border-red-200 bg-red-50 px-3 py-2">
          <p className="text-xs text-red-700">{error}</p>
        </div>
      )}

      {stages.length === 0 && !adding && (
        <p className="text-sm text-gray-500 py-4 text-center">No stages defined yet. Add your first stage to get started.</p>
      )}

      <DndContext
        sensors={sensors}
        collisionDetection={closestCenter}
        onDragEnd={handleDragEnd}
      >
        <SortableContext
          items={sortedStages.map((s) => s.id)}
          strategy={verticalListSortingStrategy}
        >
          <div className="space-y-2">
            {sortedStages.map((stage) => (
              <DraggableStageItem
                key={stage.id}
                stage={stage}
                isEditing={editingId === stage.id}
                form={form}
                hasInitial={hasInitial}
                busy={busy || reordering}
                onStartEdit={handleStartEdit}
                onDelete={handleDelete}
                onSaveEdit={handleSaveEdit}
                onCancelEdit={() => { setEditingId(null); resetForm(); }}
                onFormChange={setForm}
                disabled={disabled || reordering}
              />
            ))}
          </div>
        </SortableContext>
      </DndContext>

      {adding && (
        <div className="mt-2 rounded-lg border border-blue-200 bg-blue-50/50 px-3 py-3 space-y-3">
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block text-xs text-gray-500 mb-1">Key</label>
              <input
                type="text"
                value={form.key}
                onChange={(e) => setForm({ ...form, key: e.target.value })}
                className="w-full rounded border border-gray-300 px-2 py-1.5 text-sm focus:border-blue-500 focus:outline-none"
                placeholder="e.g. review"
                autoFocus
              />
            </div>
            <div>
              <label className="block text-xs text-gray-500 mb-1">Name</label>
              <input
                type="text"
                value={form.name}
                onChange={(e) => setForm({ ...form, name: e.target.value })}
                className="w-full rounded border border-gray-300 px-2 py-1.5 text-sm focus:border-blue-500 focus:outline-none"
                placeholder="e.g. Under Review"
              />
            </div>
          </div>
          <div>
            <label className="block text-xs text-gray-500 mb-1">Mapped Status</label>
            <select
              value={form.mappedStatus}
              onChange={(e) => setForm({ ...form, mappedStatus: e.target.value as TaskItemStatus })}
              className="w-full rounded border border-gray-300 px-2 py-1.5 text-sm focus:border-blue-500 focus:outline-none"
            >
              {TASK_STATUSES.map((s) => (
                <option key={s} value={s}>{STATUS_LABELS[s]}</option>
              ))}
            </select>
          </div>
          <div className="flex items-center gap-4">
            <label className="flex items-center gap-1.5 text-xs text-gray-600">
              <input
                type="checkbox"
                checked={form.isInitial}
                onChange={(e) => setForm({ ...form, isInitial: e.target.checked })}
                disabled={hasInitial}
                className="rounded border-gray-300"
              />
              Initial stage {hasInitial && <span className="text-gray-400">(already set)</span>}
            </label>
            <label className="flex items-center gap-1.5 text-xs text-gray-600">
              <input
                type="checkbox"
                checked={form.isTerminal}
                onChange={(e) => setForm({ ...form, isTerminal: e.target.checked })}
                className="rounded border-gray-300"
              />
              Terminal stage
            </label>
          </div>
          <div className="flex justify-end gap-2">
            <button onClick={() => { setAdding(false); resetForm(); }} className="text-xs text-gray-500 hover:text-gray-700 px-2 py-1">Cancel</button>
            <button onClick={handleSaveNew} disabled={busy} className="text-xs text-blue-600 hover:text-blue-800 font-medium px-2 py-1 disabled:opacity-50">Add Stage</button>
          </div>
        </div>
      )}
    </div>
  );
}
