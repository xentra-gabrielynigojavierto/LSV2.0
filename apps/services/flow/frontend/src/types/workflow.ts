import type { TaskItemStatus } from "./task";
import type { ProductKey } from "@/lib/productKeys";

export interface WorkflowDefinitionSummary {
  id: string;
  name: string;
  description?: string;
  version: string;
  status: string;
  stageCount: number;
  transitionCount: number;
  productKey?: ProductKey;
}

export interface WorkflowStage {
  id: string;
  key: string;
  name: string;
  mappedStatus: TaskItemStatus;
  order: number;
  isInitial: boolean;
  isTerminal: boolean;
  canvasX?: number | null;
  canvasY?: number | null;
}

export interface WorkflowTransition {
  id: string;
  fromStageId: string;
  toStageId: string;
  name: string;
  isActive: boolean;
  rulesJson?: string | null;
}

export interface WorkflowDefinition {
  id: string;
  name: string;
  description?: string;
  version: string;
  status: string;
  stages: WorkflowStage[];
  transitions: WorkflowTransition[];
  createdAt: string;
  createdBy?: string;
  updatedAt?: string;
  updatedBy?: string;
  productKey?: ProductKey;
}

export interface CreateWorkflowRequest {
  name: string;
  description?: string;
  productKey?: ProductKey;
}

export interface UpdateWorkflowRequest {
  name: string;
  description?: string;
  status?: string;
  productKey?: ProductKey;
}

export interface CreateStageRequest {
  key: string;
  name: string;
  mappedStatus: TaskItemStatus;
  order: number;
  isInitial: boolean;
  isTerminal: boolean;
  canvasX?: number | null;
  canvasY?: number | null;
}

export interface UpdateStageRequest {
  name: string;
  mappedStatus: TaskItemStatus;
  order: number;
  isInitial: boolean;
  isTerminal: boolean;
  canvasX?: number | null;
  canvasY?: number | null;
}

export interface CreateTransitionRequest {
  fromStageId: string;
  toStageId: string;
  name: string;
  rulesJson?: string | null;
}

export interface UpdateTransitionRequest {
  name: string;
  isActive: boolean;
  rulesJson?: string | null;
}

export const TRIGGER_EVENT_TYPES = ["TRANSITION_COMPLETED"] as const;
export type TriggerEventType = typeof TRIGGER_EVENT_TYPES[number];

export const ACTION_TYPES = [
  "ADD_ACTIVITY_EVENT",
  "SET_DUE_DATE_OFFSET_DAYS",
  "ASSIGN_ROLE",
  "ASSIGN_USER",
  "ASSIGN_ORG",
] as const;
export type ActionType = typeof ACTION_TYPES[number];

export const ACTION_TYPE_LABELS: Record<ActionType, string> = {
  ADD_ACTIVITY_EVENT: "Add Activity Event",
  SET_DUE_DATE_OFFSET_DAYS: "Set Due Date (offset days)",
  ASSIGN_ROLE: "Assign Role",
  ASSIGN_USER: "Assign User",
  ASSIGN_ORG: "Assign Org",
};

export const CONDITION_FIELDS = [
  "status",
  "assignedToUserId",
  "assignedToRoleKey",
  "assignedToOrgId",
  "workflowStageId",
  "flowDefinitionId",
] as const;
export type ConditionField = typeof CONDITION_FIELDS[number];

export const CONDITION_FIELD_LABELS: Record<ConditionField, string> = {
  status: "Status",
  assignedToUserId: "Assigned User ID",
  assignedToRoleKey: "Assigned Role Key",
  assignedToOrgId: "Assigned Org ID",
  workflowStageId: "Workflow Stage ID",
  flowDefinitionId: "Flow Definition ID",
};

export const CONDITION_OPERATORS = ["equals", "not_equals", "in", "not_in"] as const;
export type ConditionOperator = typeof CONDITION_OPERATORS[number];

export const CONDITION_OPERATOR_LABELS: Record<ConditionOperator, string> = {
  equals: "equals",
  not_equals: "does not equal",
  in: "is in",
  not_in: "is not in",
};

export interface AutomationAction {
  id?: string | null;
  actionType: ActionType;
  configJson?: string | null;
  order?: number | null;
  conditionJson?: string | null;
  retryCount?: number | null;
  retryDelaySeconds?: number | null;
  stopOnFailure?: boolean | null;
}

export interface AutomationHook {
  id: string;
  workflowDefinitionId: string;
  workflowTransitionId: string;
  name: string;
  triggerEventType: TriggerEventType;
  // Legacy mirrored fields — Actions[0] is the canonical source.
  actionType: ActionType;
  configJson?: string | null;
  actions: AutomationAction[];
  isActive: boolean;
  createdAt: string;
  updatedAt?: string;
}

export interface CreateAutomationHookRequest {
  workflowTransitionId: string;
  name: string;
  triggerEventType: TriggerEventType;
  actions: AutomationAction[];
}

export interface UpdateAutomationHookRequest {
  name: string;
  actions: AutomationAction[];
  isActive: boolean;
}

export interface AutomationExecutionLog {
  id: string;
  taskId: string;
  workflowAutomationHookId: string;
  hookName: string;
  actionType: string;
  status: string;
  message?: string;
  attempts?: number;
  executedAt: string;
}
