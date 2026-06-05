"use client";

import { useState } from "react";
import type { WorkflowStage, WorkflowTransition } from "@/types/workflow";

interface TransitionRulesForm {
  requireTitle: boolean;
  requireDescription: boolean;
  requireAssignment: boolean;
  requireDueDate: boolean;
}

const EMPTY_RULES: TransitionRulesForm = {
  requireTitle: false,
  requireDescription: false,
  requireAssignment: false,
  requireDueDate: false,
};

function parseRulesJson(rulesJson?: string | null): TransitionRulesForm {
  if (!rulesJson) return { ...EMPTY_RULES };
  try {
    const parsed = JSON.parse(rulesJson);
    return {
      requireTitle: !!parsed.requireTitle,
      requireDescription: !!parsed.requireDescription,
      requireAssignment: !!parsed.requireAssignment,
      requireDueDate: !!parsed.requireDueDate,
    };
  } catch {
    return { ...EMPTY_RULES };
  }
}

function serializeRules(rules: TransitionRulesForm): string | null {
  const hasAny = rules.requireTitle || rules.requireDescription || rules.requireAssignment || rules.requireDueDate;
  if (!hasAny) return null;
  return JSON.stringify(rules);
}

function RuleCheckboxes({
  rules,
  onChange,
}: {
  rules: TransitionRulesForm;
  onChange: (rules: TransitionRulesForm) => void;
}) {
  const items: { key: keyof TransitionRulesForm; label: string }[] = [
    { key: "requireTitle", label: "Require title" },
    { key: "requireDescription", label: "Require description" },
    { key: "requireAssignment", label: "Require assignment" },
    { key: "requireDueDate", label: "Require due date" },
  ];

  return (
    <div>
      <label className="block text-xs text-gray-500 mb-1.5">Transition Rules</label>
      <div className="grid grid-cols-2 gap-x-4 gap-y-1.5">
        {items.map(({ key, label }) => (
          <label key={key} className="flex items-center gap-1.5 text-xs text-gray-600 cursor-pointer">
            <input
              type="checkbox"
              checked={rules[key]}
              onChange={(e) => onChange({ ...rules, [key]: e.target.checked })}
              className="rounded border-gray-300"
            />
            {label}
          </label>
        ))}
      </div>
    </div>
  );
}

function RuleBadges({ rulesJson }: { rulesJson?: string | null }) {
  const rules = parseRulesJson(rulesJson);
  const labels: string[] = [];
  if (rules.requireTitle) labels.push("Title");
  if (rules.requireDescription) labels.push("Description");
  if (rules.requireAssignment) labels.push("Assignment");
  if (rules.requireDueDate) labels.push("Due date");
  if (labels.length === 0) return null;

  return (
    <p className="text-[10px] text-amber-600 mt-0.5">
      Requires: {labels.join(", ")}
    </p>
  );
}

interface Props {
  transitions: WorkflowTransition[];
  stages: WorkflowStage[];
  onAdd: (transition: { fromStageId: string; toStageId: string; name: string; rulesJson?: string | null }) => Promise<void>;
  onUpdate: (transitionId: string, data: { name: string; isActive: boolean; rulesJson?: string | null }) => Promise<void>;
  onDelete: (transitionId: string) => Promise<void>;
}

export function TransitionEditor({ transitions, stages, onAdd, onUpdate, onDelete }: Props) {
  const [adding, setAdding] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [form, setForm] = useState({ fromStageId: "", toStageId: "", name: "" });
  const [formRules, setFormRules] = useState<TransitionRulesForm>({ ...EMPTY_RULES });
  const [editForm, setEditForm] = useState({ name: "", isActive: true });
  const [editRules, setEditRules] = useState<TransitionRulesForm>({ ...EMPTY_RULES });
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const stageMap = new Map(stages.map((s) => [s.id, s]));

  const handleStartAdd = () => {
    setForm({ fromStageId: stages[0]?.id || "", toStageId: stages.length > 1 ? stages[1].id : stages[0]?.id || "", name: "" });
    setFormRules({ ...EMPTY_RULES });
    setEditingId(null);
    setAdding(true);
    setError(null);
  };

  const handleStartEdit = (t: WorkflowTransition) => {
    setEditForm({ name: t.name, isActive: t.isActive });
    setEditRules(parseRulesJson(t.rulesJson));
    setEditingId(t.id);
    setAdding(false);
    setError(null);
  };

  const handleSaveNew = async () => {
    if (!form.fromStageId || !form.toStageId) {
      setError("Select both From and To stages");
      return;
    }
    setBusy(true);
    setError(null);
    try {
      await onAdd({
        fromStageId: form.fromStageId,
        toStageId: form.toStageId,
        name: form.name.trim() || `${stageMap.get(form.fromStageId)?.name} → ${stageMap.get(form.toStageId)?.name}`,
        rulesJson: serializeRules(formRules),
      });
      setAdding(false);
      setForm({ fromStageId: "", toStageId: "", name: "" });
      setFormRules({ ...EMPTY_RULES });
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to add transition");
    } finally {
      setBusy(false);
    }
  };

  const handleSaveEdit = async () => {
    if (!editingId) return;
    setBusy(true);
    setError(null);
    try {
      await onUpdate(editingId, {
        ...editForm,
        rulesJson: serializeRules(editRules),
      });
      setEditingId(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to update transition");
    } finally {
      setBusy(false);
    }
  };

  const handleDelete = async (transitionId: string) => {
    setBusy(true);
    setError(null);
    try {
      await onDelete(transitionId);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to delete transition");
    } finally {
      setBusy(false);
    }
  };

  return (
    <div>
      <div className="flex items-center justify-between mb-3">
        <h3 className="text-sm font-semibold text-gray-900">Transitions ({transitions.length})</h3>
        <button
          onClick={handleStartAdd}
          disabled={adding || busy || stages.length < 2}
          className="text-xs text-blue-600 hover:text-blue-800 font-medium disabled:opacity-50"
        >
          + Add Transition
        </button>
      </div>

      {stages.length < 2 && (
        <p className="text-xs text-gray-500 mb-3">Add at least two stages before creating transitions.</p>
      )}

      {error && (
        <div className="mb-3 rounded-lg border border-red-200 bg-red-50 px-3 py-2">
          <p className="text-xs text-red-700">{error}</p>
        </div>
      )}

      {transitions.length === 0 && !adding && stages.length >= 2 && (
        <p className="text-sm text-gray-500 py-4 text-center">No transitions defined yet.</p>
      )}

      <div className="space-y-2">
        {transitions.map((t) => (
          <div key={t.id} className="rounded-lg border border-gray-200 bg-white px-3 py-2.5">
            {editingId === t.id ? (
              <div className="space-y-3">
                <div>
                  <label className="block text-xs text-gray-500 mb-1">Name</label>
                  <input
                    type="text"
                    value={editForm.name}
                    onChange={(e) => setEditForm({ ...editForm, name: e.target.value })}
                    className="w-full rounded border border-gray-300 px-2 py-1.5 text-sm focus:border-blue-500 focus:outline-none"
                  />
                </div>
                <label className="flex items-center gap-1.5 text-xs text-gray-600">
                  <input
                    type="checkbox"
                    checked={editForm.isActive}
                    onChange={(e) => setEditForm({ ...editForm, isActive: e.target.checked })}
                    className="rounded border-gray-300"
                  />
                  Active
                </label>
                <RuleCheckboxes rules={editRules} onChange={setEditRules} />
                <div className="flex justify-end gap-2">
                  <button onClick={() => setEditingId(null)} className="text-xs text-gray-500 hover:text-gray-700 px-2 py-1">Cancel</button>
                  <button onClick={handleSaveEdit} disabled={busy} className="text-xs text-blue-600 hover:text-blue-800 font-medium px-2 py-1 disabled:opacity-50">Save</button>
                </div>
              </div>
            ) : (
              <div className="flex items-center justify-between">
                <div>
                  <div className="flex items-center gap-2">
                    <span className="text-sm font-medium text-gray-900">{t.name}</span>
                    {!t.isActive && (
                      <span className="inline-flex items-center rounded-full bg-gray-100 px-1.5 py-0.5 text-[10px] font-medium text-gray-500">
                        Inactive
                      </span>
                    )}
                  </div>
                  <p className="text-xs text-gray-500">
                    {stageMap.get(t.fromStageId)?.name || "Unknown"} → {stageMap.get(t.toStageId)?.name || "Unknown"}
                  </p>
                  <RuleBadges rulesJson={t.rulesJson} />
                </div>
                <div className="flex items-center gap-2">
                  <button onClick={() => handleStartEdit(t)} className="text-xs text-gray-500 hover:text-blue-600">Edit</button>
                  <button onClick={() => handleDelete(t.id)} disabled={busy} className="text-xs text-gray-500 hover:text-red-600 disabled:opacity-50">Delete</button>
                </div>
              </div>
            )}
          </div>
        ))}

        {adding && (
          <div className="rounded-lg border border-blue-200 bg-blue-50/50 px-3 py-3 space-y-3">
            <div className="grid grid-cols-2 gap-3">
              <div>
                <label className="block text-xs text-gray-500 mb-1">From Stage</label>
                <select
                  value={form.fromStageId}
                  onChange={(e) => setForm({ ...form, fromStageId: e.target.value })}
                  className="w-full rounded border border-gray-300 px-2 py-1.5 text-sm focus:border-blue-500 focus:outline-none"
                >
                  {stages.map((s) => (
                    <option key={s.id} value={s.id}>{s.name}</option>
                  ))}
                </select>
              </div>
              <div>
                <label className="block text-xs text-gray-500 mb-1">To Stage</label>
                <select
                  value={form.toStageId}
                  onChange={(e) => setForm({ ...form, toStageId: e.target.value })}
                  className="w-full rounded border border-gray-300 px-2 py-1.5 text-sm focus:border-blue-500 focus:outline-none"
                >
                  {stages.map((s) => (
                    <option key={s.id} value={s.id}>{s.name}</option>
                  ))}
                </select>
              </div>
            </div>
            <div>
              <label className="block text-xs text-gray-500 mb-1">
                Name <span className="text-gray-400">(optional — auto-generated if blank)</span>
              </label>
              <input
                type="text"
                value={form.name}
                onChange={(e) => setForm({ ...form, name: e.target.value })}
                className="w-full rounded border border-gray-300 px-2 py-1.5 text-sm focus:border-blue-500 focus:outline-none"
                placeholder="e.g. Start Work"
              />
            </div>
            <RuleCheckboxes rules={formRules} onChange={setFormRules} />
            <div className="flex justify-end gap-2">
              <button onClick={() => { setAdding(false); setError(null); }} className="text-xs text-gray-500 hover:text-gray-700 px-2 py-1">Cancel</button>
              <button onClick={handleSaveNew} disabled={busy} className="text-xs text-blue-600 hover:text-blue-800 font-medium px-2 py-1 disabled:opacity-50">Add Transition</button>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}
