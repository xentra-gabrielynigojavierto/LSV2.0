"use client";

import { useState, useEffect, useCallback } from "react";
import type {
  WorkflowTransition,
  WorkflowStage,
  AutomationHook,
  AutomationAction,
  ActionType,
} from "@/types/workflow";
import { ACTION_TYPE_LABELS } from "@/types/workflow";
import {
  listAutomationHooks,
  addAutomationHook,
  updateAutomationHook,
  deleteAutomationHook,
} from "@/lib/api/workflows";
import { ConfirmDialog } from "@/components/ui/ConfirmDialog";
import AutomationActionEditor, {
  type ActionFormState,
  type ActionFieldErrors,
} from "./AutomationActionEditor";
import {
  parseConditionJson,
  serializeCondition,
  validateCondition,
  DEFAULT_CONDITION,
} from "./AutomationConditionEditor";

interface Props {
  workflowId: string;
  transitions: WorkflowTransition[];
  stages: WorkflowStage[];
}

function getTransitionLabel(t: WorkflowTransition, stages: WorkflowStage[]): string {
  const from = stages.find((s) => s.id === t.fromStageId);
  const to = stages.find((s) => s.id === t.toStageId);
  return `${from?.name ?? "?"} → ${to?.name ?? "?"} (${t.name})`;
}

function newActionFormState(actionType: ActionType = "ADD_ACTIVITY_EVENT"): ActionFormState {
  return {
    key: `new-${Math.random().toString(36).slice(2, 10)}`,
    id: null,
    actionType,
    config: {},
    condition: { ...DEFAULT_CONDITION },
    retryCount: 0,
    retryDelaySeconds: null,
    stopOnFailure: false,
  };
}

function parseConfig(json: string | null | undefined): Record<string, unknown> {
  if (!json) return {};
  try {
    const v = JSON.parse(json);
    return v && typeof v === "object" ? (v as Record<string, unknown>) : {};
  } catch {
    return {};
  }
}

function hookToFormStates(hook: AutomationHook): ActionFormState[] {
  // Backend always returns Actions[] (canonical, ordered). Fall back to legacy
  // single-action shape if Actions is unexpectedly empty.
  const sourceActions: AutomationAction[] =
    hook.actions && hook.actions.length > 0
      ? [...hook.actions].sort((a, b) => (a.order ?? 0) - (b.order ?? 0))
      : [
          {
            id: null,
            actionType: hook.actionType,
            configJson: hook.configJson ?? null,
            order: 0,
            conditionJson: null,
            retryCount: 0,
            retryDelaySeconds: null,
            stopOnFailure: false,
          },
        ];

  return sourceActions.map((a, idx) => ({
    key: a.id ?? `existing-${idx}`,
    id: a.id ?? null,
    actionType: a.actionType as ActionType,
    config: parseConfig(a.configJson),
    condition: parseConditionJson(a.conditionJson),
    retryCount: a.retryCount ?? 0,
    retryDelaySeconds: a.retryDelaySeconds ?? null,
    stopOnFailure: a.stopOnFailure ?? false,
  }));
}

function buildConfigJson(actionType: ActionType, config: Record<string, unknown>): string | null {
  switch (actionType) {
    case "ADD_ACTIVITY_EVENT": {
      const m = (config.messageTemplate as string)?.trim();
      return m ? JSON.stringify({ messageTemplate: m }) : null;
    }
    case "SET_DUE_DATE_OFFSET_DAYS": {
      const days = Number(config.days);
      return JSON.stringify({ days: Number.isFinite(days) && days > 0 ? days : 1 });
    }
    case "ASSIGN_ROLE":
      return JSON.stringify({ roleKey: ((config.roleKey as string) ?? "").trim() });
    case "ASSIGN_USER":
      return JSON.stringify({ userId: ((config.userId as string) ?? "").trim() });
    case "ASSIGN_ORG":
      return JSON.stringify({ orgId: ((config.orgId as string) ?? "").trim() });
    default:
      return null;
  }
}

function validateAction(state: ActionFormState): ActionFieldErrors {
  const errors: ActionFieldErrors = {};
  switch (state.actionType) {
    case "ADD_ACTIVITY_EVENT":
      if (!((state.config.messageTemplate as string) ?? "").trim()) {
        errors.config = "Message template is required.";
      }
      break;
    case "SET_DUE_DATE_OFFSET_DAYS": {
      const days = Number(state.config.days);
      if (!Number.isFinite(days) || days <= 0) {
        errors.config = "Days must be a positive integer.";
      }
      break;
    }
    case "ASSIGN_ROLE":
      if (!((state.config.roleKey as string) ?? "").trim()) errors.config = "Role key is required.";
      break;
    case "ASSIGN_USER":
      if (!((state.config.userId as string) ?? "").trim()) errors.config = "User ID is required.";
      break;
    case "ASSIGN_ORG":
      if (!((state.config.orgId as string) ?? "").trim()) errors.config = "Org ID is required.";
      break;
  }
  if (state.retryCount < 0) errors.retryCount = "Must be ≥ 0.";
  if (state.retryDelaySeconds !== null && state.retryDelaySeconds < 0) {
    errors.retryDelaySeconds = "Must be ≥ 0.";
  }
  const condErr = validateCondition(state.condition);
  if (condErr) errors.condition = condErr;
  return errors;
}

function actionsToPayload(states: ActionFormState[]): AutomationAction[] {
  return states.map((s, idx) => ({
    id: s.id ?? null,
    actionType: s.actionType,
    configJson: buildConfigJson(s.actionType, s.config),
    order: idx,
    conditionJson: serializeCondition(s.condition),
    retryCount: s.retryCount,
    retryDelaySeconds: s.retryDelaySeconds,
    stopOnFailure: s.stopOnFailure,
  }));
}

interface HookFormState {
  name: string;
  transitionId: string;
  isActive: boolean;
  actions: ActionFormState[];
}

function summarizeAction(state: ActionFormState): string {
  const label = ACTION_TYPE_LABELS[state.actionType] ?? state.actionType;
  const bits: string[] = [label];
  if (state.condition.enabled) bits.push("conditional");
  if (state.retryCount > 0) bits.push(`retries: ${state.retryCount}`);
  if (state.stopOnFailure) bits.push("stop-on-failure");
  return bits.join(" · ");
}

export default function AutomationEditor({ workflowId, transitions, stages }: Props) {
  const [hooks, setHooks] = useState<AutomationHook[]>([]);
  const [loading, setLoading] = useState(true);
  const [degraded, setDegraded] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  const [showCreate, setShowCreate] = useState(false);
  const [createForm, setCreateForm] = useState<HookFormState>({
    name: "",
    transitionId: "",
    isActive: true,
    actions: [newActionFormState()],
  });

  const [editingHookId, setEditingHookId] = useState<string | null>(null);
  const [editForm, setEditForm] = useState<HookFormState | null>(null);

  const [deleteTarget, setDeleteTarget] = useState<AutomationHook | null>(null);
  const [deleting, setDeleting] = useState(false);

  const fetchHooks = useCallback(async () => {
    try {
      setLoading(true);
      setError(null);
      const data = await listAutomationHooks(workflowId);
      setHooks(data);
      setDegraded(false);
    } catch (e: unknown) {
      setDegraded(true);
      setError(e instanceof Error ? e.message : "Failed to load automation hooks");
    } finally {
      setLoading(false);
    }
  }, [workflowId]);

  useEffect(() => {
    fetchHooks();
  }, [fetchHooks]);

  const resetCreate = () => {
    setShowCreate(false);
    setCreateForm({
      name: "",
      transitionId: "",
      isActive: true,
      actions: [newActionFormState()],
    });
  };

  const startEdit = (hook: AutomationHook) => {
    setEditingHookId(hook.id);
    setEditForm({
      name: hook.name,
      transitionId: hook.workflowTransitionId,
      isActive: hook.isActive,
      actions: hookToFormStates(hook),
    });
  };

  const cancelEdit = () => {
    setEditingHookId(null);
    setEditForm(null);
  };

  const validateForm = (form: HookFormState): { ok: boolean; firstError?: string; perAction: ActionFieldErrors[] } => {
    const perAction = form.actions.map(validateAction);
    if (!form.name.trim()) return { ok: false, firstError: "Name is required.", perAction };
    if (!form.transitionId) return { ok: false, firstError: "Transition is required.", perAction };
    if (form.actions.length === 0) return { ok: false, firstError: "At least one action is required.", perAction };
    const hasErr = perAction.some((e) => Object.keys(e).length > 0);
    if (hasErr) return { ok: false, firstError: "Please fix the highlighted action errors.", perAction };
    return { ok: true, perAction };
  };

  const [createErrors, setCreateErrors] = useState<ActionFieldErrors[]>([]);
  const [editErrors, setEditErrors] = useState<ActionFieldErrors[]>([]);

  const handleCreate = async () => {
    const v = validateForm(createForm);
    setCreateErrors(v.perAction);
    if (!v.ok) {
      setError(v.firstError ?? "Validation failed");
      return;
    }
    try {
      setSaving(true);
      setError(null);
      await addAutomationHook(workflowId, {
        workflowTransitionId: createForm.transitionId,
        name: createForm.name.trim(),
        triggerEventType: "TRANSITION_COMPLETED",
        actions: actionsToPayload(createForm.actions),
      });
      resetCreate();
      setCreateErrors([]);
      await fetchHooks();
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : "Failed to create hook");
    } finally {
      setSaving(false);
    }
  };

  const handleUpdate = async () => {
    if (!editingHookId || !editForm) return;
    const v = validateForm(editForm);
    setEditErrors(v.perAction);
    if (!v.ok) {
      setError(v.firstError ?? "Validation failed");
      return;
    }
    try {
      setSaving(true);
      setError(null);
      await updateAutomationHook(workflowId, editingHookId, {
        name: editForm.name.trim(),
        actions: actionsToPayload(editForm.actions),
        isActive: editForm.isActive,
      });
      cancelEdit();
      setEditErrors([]);
      await fetchHooks();
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : "Failed to update hook");
    } finally {
      setSaving(false);
    }
  };

  const handleDeleteConfirmed = async () => {
    if (!deleteTarget) return;
    try {
      setDeleting(true);
      setError(null);
      await deleteAutomationHook(workflowId, deleteTarget.id);
      setDeleteTarget(null);
      await fetchHooks();
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : "Failed to delete hook");
    } finally {
      setDeleting(false);
    }
  };

  // ---- Action list mutation helpers (work for any HookFormState setter) ----
  const updateActions = (
    form: HookFormState,
    setForm: (next: HookFormState) => void,
    fn: (actions: ActionFormState[]) => ActionFormState[],
  ) => {
    setForm({ ...form, actions: fn(form.actions) });
  };

  const renderActionsSection = (
    form: HookFormState,
    setForm: (next: HookFormState) => void,
    errs: ActionFieldErrors[],
  ) => (
    <div className="space-y-3">
      <div className="flex items-center justify-between">
        <span className="text-sm font-medium text-gray-700">Actions</span>
        <button
          type="button"
          onClick={() =>
            updateActions(form, setForm, (a) => [...a, newActionFormState()])
          }
          className="px-2 py-1 text-xs bg-gray-100 text-gray-700 rounded hover:bg-gray-200"
        >
          + Add Action
        </button>
      </div>
      {form.actions.length === 0 && (
        <p className="text-xs text-red-600">At least one action is required.</p>
      )}
      {form.actions.map((a, idx) => (
        <AutomationActionEditor
          key={a.key}
          index={idx}
          total={form.actions.length}
          state={a}
          errors={errs[idx]}
          onChange={(next) =>
            updateActions(form, setForm, (arr) =>
              arr.map((x, i) => (i === idx ? next : x)),
            )
          }
          onRemove={() =>
            updateActions(form, setForm, (arr) => arr.filter((_, i) => i !== idx))
          }
          onMoveUp={() =>
            updateActions(form, setForm, (arr) => {
              if (idx === 0) return arr;
              const next = [...arr];
              [next[idx - 1], next[idx]] = [next[idx], next[idx - 1]];
              return next;
            })
          }
          onMoveDown={() =>
            updateActions(form, setForm, (arr) => {
              if (idx === arr.length - 1) return arr;
              const next = [...arr];
              [next[idx], next[idx + 1]] = [next[idx + 1], next[idx]];
              return next;
            })
          }
        />
      ))}
    </div>
  );

  if (loading) {
    return <div className="p-4 text-gray-500 text-sm">Loading automation hooks…</div>;
  }

  if (degraded) {
    return (
      <div className="space-y-3">
        <h3 className="text-lg font-semibold text-gray-900">Automation Hooks</h3>
        <div className="border border-amber-200 bg-amber-50 text-amber-800 rounded-md px-3 py-2 text-sm">
          Automation configuration is unavailable. {error ?? "Backend connection required."}
        </div>
        <button
          onClick={fetchHooks}
          className="px-3 py-1.5 text-sm bg-gray-200 text-gray-700 rounded-md hover:bg-gray-300"
        >
          Retry
        </button>
      </div>
    );
  }

  return (
    <div className="space-y-4">
      <ConfirmDialog
        open={!!deleteTarget}
        title="Delete Automation Hook"
        message={`Are you sure you want to delete "${deleteTarget?.name}"? This action cannot be undone.`}
        confirmLabel="Delete"
        variant="danger"
        loading={deleting}
        onConfirm={handleDeleteConfirmed}
        onCancel={() => setDeleteTarget(null)}
      />

      <div className="flex items-center justify-between">
        <h3 className="text-lg font-semibold text-gray-900">Automation Hooks</h3>
        {!showCreate && (
          <button
            onClick={() => {
              setShowCreate(true);
              if (transitions.length > 0) {
                setCreateForm((f) => ({
                  ...f,
                  transitionId: f.transitionId || transitions[0].id,
                }));
              }
            }}
            className="px-3 py-1.5 text-sm bg-indigo-600 text-white rounded-md hover:bg-indigo-700"
          >
            + Add Hook
          </button>
        )}
      </div>

      {error && (
        <div className="bg-red-50 text-red-700 px-3 py-2 rounded text-sm">{error}</div>
      )}

      {showCreate && (
        <div className="border border-indigo-200 bg-indigo-50 rounded-lg p-4 space-y-3">
          <h4 className="text-sm font-semibold text-indigo-900">New Automation Hook</h4>

          <label className="block text-sm">
            <span className="text-gray-600">Name</span>
            <input
              type="text"
              className="mt-1 block w-full border border-gray-300 rounded-md px-3 py-2 text-sm"
              placeholder="e.g. Auto-assign reviewer"
              value={createForm.name}
              onChange={(e) => setCreateForm({ ...createForm, name: e.target.value })}
            />
          </label>

          <label className="block text-sm">
            <span className="text-gray-600">Transition</span>
            <select
              className="mt-1 block w-full border border-gray-300 rounded-md px-3 py-2 text-sm"
              value={createForm.transitionId}
              onChange={(e) => setCreateForm({ ...createForm, transitionId: e.target.value })}
            >
              <option value="">Select a transition…</option>
              {transitions.map((t) => (
                <option key={t.id} value={t.id}>
                  {getTransitionLabel(t, stages)}
                </option>
              ))}
            </select>
          </label>

          <div className="block text-sm">
            <span className="text-gray-600">Trigger</span>
            <p className="mt-1 text-xs text-gray-500 bg-gray-100 rounded px-3 py-2">
              Transition Completed
            </p>
          </div>

          {renderActionsSection(createForm, setCreateForm, createErrors)}

          <div className="flex gap-2 pt-2">
            <button
              onClick={handleCreate}
              disabled={saving}
              className="px-3 py-1.5 text-sm bg-indigo-600 text-white rounded-md hover:bg-indigo-700 disabled:opacity-50"
            >
              {saving ? "Saving…" : "Create"}
            </button>
            <button
              onClick={resetCreate}
              className="px-3 py-1.5 text-sm bg-gray-200 text-gray-700 rounded-md hover:bg-gray-300"
            >
              Cancel
            </button>
          </div>
        </div>
      )}

      {hooks.length === 0 && !showCreate && (
        <p className="text-sm text-gray-500">No automation hooks configured yet.</p>
      )}

      <div className="space-y-2">
        {hooks.map((hook) => {
          const transition = transitions.find((t) => t.id === hook.workflowTransitionId);
          const isEditing = editingHookId === hook.id;

          return (
            <div
              key={hook.id}
              className={`border rounded-lg p-4 ${
                hook.isActive ? "border-gray-200 bg-white" : "border-gray-200 bg-gray-50 opacity-60"
              }`}
            >
              {isEditing && editForm ? (
                <div className="space-y-3">
                  <label className="block text-sm">
                    <span className="text-gray-600">Name</span>
                    <input
                      type="text"
                      className="mt-1 block w-full border border-gray-300 rounded-md px-3 py-2 text-sm"
                      value={editForm.name}
                      onChange={(e) => setEditForm({ ...editForm, name: e.target.value })}
                    />
                  </label>

                  {renderActionsSection(editForm, setEditForm, editErrors)}

                  <label className="flex items-center gap-2 text-sm">
                    <input
                      type="checkbox"
                      checked={editForm.isActive}
                      onChange={(e) => setEditForm({ ...editForm, isActive: e.target.checked })}
                    />
                    <span>Active</span>
                  </label>

                  <div className="flex gap-2">
                    <button
                      onClick={handleUpdate}
                      disabled={saving}
                      className="px-3 py-1.5 text-sm bg-indigo-600 text-white rounded-md hover:bg-indigo-700 disabled:opacity-50"
                    >
                      {saving ? "Saving…" : "Save"}
                    </button>
                    <button
                      onClick={cancelEdit}
                      className="px-3 py-1.5 text-sm bg-gray-200 text-gray-700 rounded-md hover:bg-gray-300"
                    >
                      Cancel
                    </button>
                  </div>
                </div>
              ) : (
                <div className="flex items-start justify-between">
                  <div className="space-y-1">
                    <div className="flex items-center gap-2">
                      <span className="font-medium text-gray-900">{hook.name}</span>
                      {!hook.isActive && (
                        <span className="text-xs bg-gray-200 text-gray-600 px-1.5 py-0.5 rounded">
                          Inactive
                        </span>
                      )}
                    </div>
                    <p className="text-xs text-gray-500">
                      On transition completed
                      {transition ? ` of "${getTransitionLabel(transition, stages)}"` : ""}
                    </p>
                    <ol className="text-xs text-gray-700 list-decimal pl-5 space-y-0.5">
                      {hookToFormStates(hook).map((s) => (
                        <li key={s.key}>{summarizeAction(s)}</li>
                      ))}
                    </ol>
                  </div>
                  <div className="flex gap-1">
                    <button
                      onClick={() => startEdit(hook)}
                      className="px-2 py-1 text-xs text-indigo-600 hover:bg-indigo-50 rounded"
                    >
                      Edit
                    </button>
                    <button
                      onClick={() => setDeleteTarget(hook)}
                      disabled={saving}
                      className="px-2 py-1 text-xs text-red-600 hover:bg-red-50 rounded disabled:opacity-50"
                    >
                      Delete
                    </button>
                  </div>
                </div>
              )}
            </div>
          );
        })}
      </div>
    </div>
  );
}
