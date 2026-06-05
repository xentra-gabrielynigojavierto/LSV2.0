"use client";

import {
  ACTION_TYPES,
  ACTION_TYPE_LABELS,
  type ActionType,
} from "@/types/workflow";
import AutomationConditionEditor, {
  type ConditionState,
} from "./AutomationConditionEditor";

export interface ActionFormState {
  key: string;
  id?: string | null;
  actionType: ActionType;
  config: Record<string, unknown>;
  condition: ConditionState;
  retryCount: number;
  retryDelaySeconds: number | null;
  stopOnFailure: boolean;
}

export interface ActionFieldErrors {
  config?: string;
  retryCount?: string;
  retryDelaySeconds?: string;
  condition?: string;
}

function ConfigFields({
  actionType,
  config,
  onChange,
  error,
}: {
  actionType: ActionType;
  config: Record<string, unknown>;
  onChange: (next: Record<string, unknown>) => void;
  error?: string;
}) {
  const set = (patch: Record<string, unknown>) => onChange({ ...config, ...patch });

  let body: React.ReactNode = null;
  switch (actionType) {
    case "ADD_ACTIVITY_EVENT":
      body = (
        <label className="block text-xs">
          <span className="text-gray-600">Message template</span>
          <input
            type="text"
            className="mt-1 block w-full border border-gray-300 rounded-md px-2 py-1.5 text-sm"
            placeholder="Automation executed"
            value={(config.messageTemplate as string) ?? ""}
            onChange={(e) => set({ messageTemplate: e.target.value })}
          />
        </label>
      );
      break;
    case "SET_DUE_DATE_OFFSET_DAYS":
      body = (
        <label className="block text-xs">
          <span className="text-gray-600">Days from now</span>
          <input
            type="number"
            min={1}
            className="mt-1 block w-full border border-gray-300 rounded-md px-2 py-1.5 text-sm"
            value={(config.days as number) ?? 7}
            onChange={(e) => set({ days: parseInt(e.target.value) || 0 })}
          />
        </label>
      );
      break;
    case "ASSIGN_ROLE":
      body = (
        <label className="block text-xs">
          <span className="text-gray-600">Role key</span>
          <input
            type="text"
            className="mt-1 block w-full border border-gray-300 rounded-md px-2 py-1.5 text-sm"
            placeholder="e.g. reviewer"
            value={(config.roleKey as string) ?? ""}
            onChange={(e) => set({ roleKey: e.target.value })}
          />
        </label>
      );
      break;
    case "ASSIGN_USER":
      body = (
        <label className="block text-xs">
          <span className="text-gray-600">User ID</span>
          <input
            type="text"
            className="mt-1 block w-full border border-gray-300 rounded-md px-2 py-1.5 text-sm"
            placeholder="user-id"
            value={(config.userId as string) ?? ""}
            onChange={(e) => set({ userId: e.target.value })}
          />
        </label>
      );
      break;
    case "ASSIGN_ORG":
      body = (
        <label className="block text-xs">
          <span className="text-gray-600">Org ID</span>
          <input
            type="text"
            className="mt-1 block w-full border border-gray-300 rounded-md px-2 py-1.5 text-sm"
            placeholder="org-id"
            value={(config.orgId as string) ?? ""}
            onChange={(e) => set({ orgId: e.target.value })}
          />
        </label>
      );
      break;
  }
  return (
    <div className="space-y-1">
      {body}
      {error && <p className="text-xs text-red-600">{error}</p>}
    </div>
  );
}

interface Props {
  index: number;
  total: number;
  state: ActionFormState;
  errors?: ActionFieldErrors;
  onChange: (next: ActionFormState) => void;
  onRemove: () => void;
  onMoveUp: () => void;
  onMoveDown: () => void;
}

export default function AutomationActionEditor({
  index,
  total,
  state,
  errors,
  onChange,
  onRemove,
  onMoveUp,
  onMoveDown,
}: Props) {
  return (
    <div className="border border-gray-300 rounded-lg p-3 bg-white space-y-3">
      <div className="flex items-center justify-between gap-2">
        <div className="flex items-center gap-2">
          <span className="text-xs font-semibold text-gray-500">#{index + 1}</span>
          <span className="text-sm font-medium text-gray-800">
            {ACTION_TYPE_LABELS[state.actionType] ?? state.actionType}
          </span>
        </div>
        <div className="flex items-center gap-1">
          <button
            type="button"
            onClick={onMoveUp}
            disabled={index === 0}
            className="px-2 py-1 text-xs text-gray-600 hover:bg-gray-100 rounded disabled:opacity-30"
            aria-label="Move up"
          >
            ↑
          </button>
          <button
            type="button"
            onClick={onMoveDown}
            disabled={index === total - 1}
            className="px-2 py-1 text-xs text-gray-600 hover:bg-gray-100 rounded disabled:opacity-30"
            aria-label="Move down"
          >
            ↓
          </button>
          <button
            type="button"
            onClick={onRemove}
            className="px-2 py-1 text-xs text-red-600 hover:bg-red-50 rounded"
          >
            Remove
          </button>
        </div>
      </div>

      <label className="block text-xs">
        <span className="text-gray-600">Action type</span>
        <select
          className="mt-1 block w-full border border-gray-300 rounded-md px-2 py-1.5 text-sm"
          value={state.actionType}
          onChange={(e) =>
            onChange({
              ...state,
              actionType: e.target.value as ActionType,
              config: {},
            })
          }
        >
          {ACTION_TYPES.map((a) => (
            <option key={a} value={a}>
              {ACTION_TYPE_LABELS[a]}
            </option>
          ))}
        </select>
      </label>

      <ConfigFields
        actionType={state.actionType}
        config={state.config}
        onChange={(c) => onChange({ ...state, config: c })}
        error={errors?.config}
      />

      <AutomationConditionEditor
        state={state.condition}
        onChange={(c) => onChange({ ...state, condition: c })}
        error={errors?.condition}
      />

      <div className="grid grid-cols-3 gap-2">
        <label className="block text-xs">
          <span className="text-gray-600">Retry count</span>
          <input
            type="number"
            min={0}
            className="mt-1 block w-full border border-gray-300 rounded-md px-2 py-1.5 text-sm"
            value={state.retryCount}
            onChange={(e) =>
              onChange({ ...state, retryCount: Math.max(0, parseInt(e.target.value) || 0) })
            }
          />
          {errors?.retryCount && <p className="text-xs text-red-600 mt-0.5">{errors.retryCount}</p>}
        </label>
        <label className="block text-xs">
          <span className="text-gray-600">Retry delay (sec)</span>
          <input
            type="number"
            min={0}
            placeholder="0"
            className="mt-1 block w-full border border-gray-300 rounded-md px-2 py-1.5 text-sm"
            value={state.retryDelaySeconds ?? ""}
            onChange={(e) => {
              const v = e.target.value;
              onChange({
                ...state,
                retryDelaySeconds: v === "" ? null : Math.max(0, parseInt(v) || 0),
              });
            }}
          />
          {errors?.retryDelaySeconds && (
            <p className="text-xs text-red-600 mt-0.5">{errors.retryDelaySeconds}</p>
          )}
        </label>
        <label className="flex items-end gap-2 text-xs pb-1.5">
          <input
            type="checkbox"
            checked={state.stopOnFailure}
            onChange={(e) => onChange({ ...state, stopOnFailure: e.target.checked })}
          />
          <span className="text-gray-700">Stop on failure</span>
        </label>
      </div>
    </div>
  );
}
