"use client";

import { useState, useRef, useEffect } from "react";
import type { TaskItemStatus } from "@/types/task";
import { TASK_STATUSES, STATUS_LABELS } from "@/types/task";

interface Props {
  x: number;
  y: number;
  canvasX: number;
  canvasY: number;
  onAddStage: (stage: {
    key: string;
    name: string;
    mappedStatus: TaskItemStatus;
    order: number;
    isInitial: boolean;
    isTerminal: boolean;
    canvasX: number;
    canvasY: number;
  }) => Promise<void>;
  onClose: () => void;
  nextOrder: number;
  hasInitial: boolean;
}

export function CanvasContextMenu({ x, y, canvasX, canvasY, onAddStage, onClose, nextOrder, hasInitial }: Props) {
  const [showForm, setShowForm] = useState(false);
  const [name, setName] = useState("");
  const [key, setKey] = useState("");
  const [mappedStatus, setMappedStatus] = useState<TaskItemStatus>("Open");
  const [isInitial, setIsInitial] = useState(false);
  const [isTerminal, setIsTerminal] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [keyEdited, setKeyEdited] = useState(false);
  const menuRef = useRef<HTMLDivElement>(null);
  const nameRef = useRef<HTMLInputElement>(null);
  const keyRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    const handleClickOutside = (e: MouseEvent) => {
      if (menuRef.current && !menuRef.current.contains(e.target as Node)) {
        onClose();
      }
    };
    const handleEsc = (e: KeyboardEvent) => {
      if (e.key === "Escape") onClose();
    };
    document.addEventListener("mousedown", handleClickOutside);
    document.addEventListener("keydown", handleEsc);
    return () => {
      document.removeEventListener("mousedown", handleClickOutside);
      document.removeEventListener("keydown", handleEsc);
    };
  }, [onClose]);

  useEffect(() => {
    if (showForm && nameRef.current) {
      nameRef.current.focus();
    }
  }, [showForm]);

  const handleNameChange = (val: string) => {
    setName(val);
    if (!keyEdited) {
      setKey(val.toLowerCase().replace(/[^a-z0-9]+/g, "_").replace(/^_|_$/g, ""));
    }
  };

  const handleSubmit = async () => {
    const finalName = name || nameRef.current?.value || "";
    const finalKey = key || keyRef.current?.value || "";
    if (!finalName.trim() || !finalKey.trim()) {
      setError("Name and key are required");
      return;
    }
    setSaving(true);
    setError(null);
    try {
      await onAddStage({
        name: finalName.trim(),
        key: finalKey.trim(),
        mappedStatus,
        order: nextOrder,
        isInitial,
        isTerminal,
        canvasX,
        canvasY,
      });
      onClose();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to add stage");
    } finally {
      setSaving(false);
    }
  };

  if (!showForm) {
    return (
      <div
        ref={menuRef}
        className="fixed bg-white rounded-lg shadow-lg border border-gray-200 py-1 z-[100]"
        style={{ left: x, top: y }}
      >
        <button
          onClick={() => setShowForm(true)}
          className="w-full text-left px-4 py-2 text-sm text-gray-700 hover:bg-gray-50 flex items-center gap-2"
        >
          <svg className="w-4 h-4 text-gray-400" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
            <path strokeLinecap="round" strokeLinejoin="round" d="M12 4v16m8-8H4" />
          </svg>
          Add Stage Here
        </button>
      </div>
    );
  }

  return (
    <div
      ref={menuRef}
      className="fixed bg-white rounded-lg shadow-xl border border-gray-200 p-4 z-[100] w-[280px]"
      style={{ left: x, top: y }}
    >
      <h3 className="text-sm font-semibold text-gray-900 mb-3">New Stage</h3>

      {error && (
        <div className="mb-3 rounded bg-red-50 border border-red-200 px-3 py-1.5">
          <p className="text-xs text-red-700">{error}</p>
        </div>
      )}

      <div className="space-y-2.5">
        <div>
          <label className="block text-xs text-gray-500 mb-0.5">Name</label>
          <input
            ref={nameRef}
            type="text"
            value={name}
            onChange={(e) => handleNameChange(e.target.value)}
            placeholder="e.g. Review"
            className="w-full rounded border border-gray-300 px-2.5 py-1.5 text-sm focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
          />
        </div>
        <div>
          <label className="block text-xs text-gray-500 mb-0.5">Key</label>
          <input
            ref={keyRef}
            type="text"
            value={key}
            onChange={(e) => { setKey(e.target.value); setKeyEdited(true); }}
            placeholder="e.g. review"
            className="w-full rounded border border-gray-300 px-2.5 py-1.5 text-sm font-mono focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
          />
        </div>
        <div>
          <label className="block text-xs text-gray-500 mb-0.5">Mapped Status</label>
          <select
            value={mappedStatus}
            onChange={(e) => setMappedStatus(e.target.value as TaskItemStatus)}
            className="w-full rounded border border-gray-300 px-2.5 py-1.5 text-sm focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
          >
            {TASK_STATUSES.map((s) => (
              <option key={s} value={s}>{STATUS_LABELS[s]}</option>
            ))}
          </select>
        </div>
        <div className="flex items-center gap-4">
          {!hasInitial && (
            <label className="flex items-center gap-1.5 text-xs text-gray-600">
              <input
                type="checkbox"
                checked={isInitial}
                onChange={(e) => { setIsInitial(e.target.checked); if (e.target.checked) setIsTerminal(false); }}
                className="rounded border-gray-300"
              />
              Initial
            </label>
          )}
          <label className="flex items-center gap-1.5 text-xs text-gray-600">
            <input
              type="checkbox"
              checked={isTerminal}
              onChange={(e) => { setIsTerminal(e.target.checked); if (e.target.checked) setIsInitial(false); }}
              className="rounded border-gray-300"
            />
            Terminal
          </label>
        </div>
      </div>

      <div className="flex items-center justify-end gap-2 mt-4">
        <button
          onClick={onClose}
          className="px-3 py-1.5 text-xs text-gray-600 hover:text-gray-800"
        >
          Cancel
        </button>
        <button
          onClick={handleSubmit}
          disabled={saving}
          className="inline-flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium text-white bg-blue-600 rounded hover:bg-blue-700 disabled:opacity-50"
        >
          {saving && <div className="h-3 w-3 animate-spin rounded-full border-2 border-white/30 border-t-white" />}
          Add Stage
        </button>
      </div>
    </div>
  );
}
