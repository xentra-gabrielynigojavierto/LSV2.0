import type { ProductKey } from "@/lib/productKeys";

export type TaskItemStatus = "Open" | "InProgress" | "Blocked" | "Done" | "Cancelled";

export interface ContextReference {
  contextType: string;
  contextId: string;
  label?: string;
}

export interface TaskResponse {
  id: string;
  title: string;
  description?: string;
  status: TaskItemStatus;
  flowDefinitionId?: string;
  workflowStageId?: string;
  workflowName?: string;
  workflowStageName?: string;
  allowedNextStatuses?: TaskItemStatus[];
  allowedTransitionRules?: Record<string, TransitionRuleHints>;
  assignedToUserId?: string;
  assignedToRoleKey?: string;
  assignedToOrgId?: string;
  dueDate?: string;
  context?: ContextReference;
  productKey?: ProductKey;
  createdAt: string;
  createdBy?: string;
  updatedAt?: string;
  updatedBy?: string;
}

export interface PagedResponse<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface TaskListQuery {
  status?: TaskItemStatus;
  assignedToUserId?: string;
  assignedToRoleKey?: string;
  assignedToOrgId?: string;
  contextType?: string;
  contextId?: string;
  productKey?: ProductKey;
  page?: number;
  pageSize?: number;
  sortBy?: string;
  sortDirection?: "asc" | "desc";
}

export interface UpdateTaskRequest {
  title: string;
  description?: string;
  flowDefinitionId?: string;
  assignedToUserId?: string;
  assignedToRoleKey?: string;
  assignedToOrgId?: string;
  dueDate?: string;
  context?: ContextReference;
  productKey?: ProductKey;
}

export interface UpdateTaskStatusRequest {
  status: TaskItemStatus;
}

export interface AssignTaskRequest {
  assignedToUserId?: string;
  assignedToRoleKey?: string;
  assignedToOrgId?: string;
}

export interface CreateTaskRequest {
  title: string;
  description?: string;
  status?: TaskItemStatus;
  flowDefinitionId?: string;
  assignedToUserId?: string;
  assignedToRoleKey?: string;
  assignedToOrgId?: string;
  dueDate?: string;
  context?: ContextReference;
  productKey?: ProductKey;
}

export interface TransitionRuleHints {
  requireTitle: boolean;
  requireDescription: boolean;
  requireAssignment: boolean;
  requireDueDate: boolean;
}

export const TASK_STATUSES: TaskItemStatus[] = [
  "Open",
  "InProgress",
  "Blocked",
  "Done",
  "Cancelled",
];

export const STATUS_LABELS: Record<TaskItemStatus, string> = {
  Open: "Open",
  InProgress: "In Progress",
  Blocked: "Blocked",
  Done: "Done",
  Cancelled: "Cancelled",
};

export const STATUS_COLORS: Record<TaskItemStatus, string> = {
  Open: "bg-blue-100 text-blue-800",
  InProgress: "bg-amber-100 text-amber-800",
  Blocked: "bg-red-100 text-red-800",
  Done: "bg-emerald-100 text-emerald-800",
  Cancelled: "bg-gray-100 text-gray-500",
};
