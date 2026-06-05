"use client";

import {
  CONDITION_FIELDS,
  CONDITION_FIELD_LABELS,
  CONDITION_OPERATORS,
  CONDITION_OPERATOR_LABELS,
  type ConditionField,
  type ConditionOperator,
} from "@/types/workflow";

export interface ConditionState {
  enabled: boolean;
  field: ConditionField;
  operator: ConditionOperator;
  value: string;
}

export const DEFAULT_CONDITION: ConditionState = {
  enabled: false,
  field: "status",
  operator: "equals",
  value: "",
};

export function parseConditionJson(json: string | null | undefined): ConditionState {
  if (!json) return { ...DEFAULT_CONDITION };
  try {
    const parsed = JSON.parse(json);
    const field = (CONDITION_FIELDS as readonly string[]).includes(parsed.field)
      ? (parsed.field as ConditionField)
      : "status";
    const operator = (CONDITION_OPERATORS as readonly string[]).includes(parsed.operator)
      ? (parsed.operator as ConditionOperator)
      : "equals";
    let value = "";
    if (Array.isArray(parsed.value)) {
      value = parsed.value.join(", ");
    } else if (parsed.value != null) {
      value = String(parsed.value);
    }
    return { enabled: true, field, operator, value };
  } catch {
    return { ...DEFAULT_CONDITION };
  }
}

export function serializeCondition(state: ConditionState): string | null {
  if (!state.enabled) return null;
  const trimmed = state.value.trim();
  if (!trimmed) return null;
  if (state.operator === "in" || state.operator === "not_in") {
    const values = trimmed
      .split(",")
      .map((v) => v.trim())
      .filter((v) => v.length > 0);
    return JSON.stringify({ field: state.field, operator: state.operator, value: values });
  }
  return JSON.stringify({ field: state.field, operator: state.operator, value: trimmed });
}

export function validateCondition(state: ConditionState): string | null {
  if (!state.enabled) return null;
  const trimmed = state.value.trim();
  if (!trimmed) return "Condition value is required when condition is enabled.";
  if (state.operator === "in" || state.operator === "not_in") {
    const values = trimmed.split(",").map((v) => v.trim()).filter((v) => v.length > 0);
    if (values.length === 0) return "Provide at least one comma-separated value.";
  }
  return null;
}

interface Props {
  state: ConditionState;
  onChange: (next: ConditionState) => void;
  error?: string | null;
}

export default function AutomationConditionEditor({ state, onChange, error }: Props) {
  const isList = state.operator === "in" || state.operator === "not_in";
  return (
    <div className="border border-gray-200 rounded-md p-3 bg-gray-50 space-y-2">
      <label className="flex items-center gap-2 text-sm font-medium text-gray-800">
        <input
          type="checkbox"
          checked={state.enabled}
          onChange={(e) => onChange({ ...state, enabled: e.target.checked })}
        />
        <span>Run only when condition matches</span>
      </label>

      {state.enabled && (
        <div className="space-y-2 pl-6">
          <div className="grid grid-cols-2 gap-2">
            <label className="block text-xs">
              <span className="text-gray-600">Field</span>
              <select
                className="mt-1 block w-full border border-gray-300 rounded-md px-2 py-1.5 text-sm bg-white"
                value={state.field}
                onChange={(e) =>
                  onChange({ ...state, field: e.target.value as ConditionField })
                }
              >
                {CONDITION_FIELDS.map((f) => (
                  <option key={f} value={f}>
                    {CONDITION_FIELD_LABELS[f]}
                  </option>
                ))}
              </select>
            </label>
            <label className="block text-xs">
              <span className="text-gray-600">Operator</span>
              <select
                className="mt-1 block w-full border border-gray-300 rounded-md px-2 py-1.5 text-sm bg-white"
                value={state.operator}
                onChange={(e) =>
                  onChange({ ...state, operator: e.target.value as ConditionOperator })
                }
              >
                {CONDITION_OPERATORS.map((op) => (
                  <option key={op} value={op}>
                    {CONDITION_OPERATOR_LABELS[op]}
                  </option>
                ))}
              </select>
            </label>
          </div>
          <label className="block text-xs">
            <span className="text-gray-600">
              {isList ? "Values (comma-separated)" : "Value"}
            </span>
            <input
              type="text"
              className="mt-1 block w-full border border-gray-300 rounded-md px-2 py-1.5 text-sm bg-white"
              placeholder={isList ? "Done, Cancelled" : "Done"}
              value={state.value}
              onChange={(e) => onChange({ ...state, value: e.target.value })}
            />
          </label>
          {error && (
            <p className="text-xs text-red-600">{error}</p>
          )}
        </div>
      )}
    </div>
  );
}
