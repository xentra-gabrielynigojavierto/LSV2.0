export type StartStageMode = 'FIRST_ACTIVE_STAGE' | 'EXPLICIT_STAGE';
export type GovernanceUpdateSource = 'TENANT_PRODUCT_SETTINGS' | 'CONTROL_CENTER';

export interface TaskGovernanceSettings {
  id: string;
  tenantId: string;
  productCode: string;
  requireAssigneeOnCreate: boolean;
  requireCaseLinkOnCreate: boolean;
  allowMultipleAssignees: boolean;
  requireWorkflowStageOnCreate: boolean;
  defaultStartStageMode: StartStageMode;
  explicitStartStageId?: string;
  version: number;
  lastUpdatedAt: string;
  lastUpdatedByUserId?: string;
  lastUpdatedByName?: string;
  lastUpdatedSource: GovernanceUpdateSource;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface UpdateTaskGovernanceRequest {
  requireAssigneeOnCreate: boolean;
  requireCaseLinkOnCreate: boolean;
  allowMultipleAssignees: boolean;
  requireWorkflowStageOnCreate: boolean;
  defaultStartStageMode: StartStageMode;
  explicitStartStageId?: string;
  updateSource: GovernanceUpdateSource;
  version: number;
  updatedByName?: string;
}

export type TaskStatus = 'NEW' | 'IN_PROGRESS' | 'WAITING_BLOCKED' | 'COMPLETED' | 'CANCELLED';
export type TaskPriority = 'LOW' | 'MEDIUM' | 'HIGH' | 'URGENT';

export interface TaskLienLinkDto {
  taskId: string;
  lienId: string;
  createdAtUtc: string;
}

export type TaskSourceType = 'MANUAL' | 'AUTOMATED';

export interface TaskDto {
  id: string;
  tenantId: string;
  title: string;
  description?: string;
  status: TaskStatus;
  priority: TaskPriority;
  assignedUserId?: string;
  caseId?: string;
  workflowStageId?: string;
  dueDate?: string;
  completedAt?: string;
  closedByUserId?: string;
  linkedLiens: TaskLienLinkDto[];
  createdByUserId?: string;
  sourceType?: TaskSourceType;
  generationRuleId?: string;
  generatingTemplateId?: string;
  isSystemGenerated?: boolean;
  createdAtUtc: string;
  updatedAtUtc: string;
  workflowInstanceId?: string;
  workflowStepKey?: string;
}

export interface PaginatedTasksDto {
  items: TaskDto[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface CreateTaskRequest {
  title: string;
  description?: string;
  priority?: TaskPriority;
  assignedUserId?: string;
  caseId?: string;
  lienIds?: string[];
  workflowStageId?: string;
  dueDate?: string;
  templateId?: string;
}

export interface UpdateTaskRequest {
  title: string;
  description?: string;
  priority?: TaskPriority;
  caseId?: string;
  lienIds?: string[];
  workflowStageId?: string;
  dueDate?: string;
}

export interface AssignTaskRequest {
  assignedUserId?: string;
}

export interface UpdateTaskStatusRequest {
  status: TaskStatus;
}

export interface TasksQuery {
  search?: string;
  status?: TaskStatus;
  priority?: TaskPriority;
  assignedUserId?: string;
  caseId?: string;
  lienId?: string;
  workflowStageId?: string;
  assignmentScope?: 'me' | 'others' | 'unassigned' | 'all';
  page?: number;
  pageSize?: number;
}

export const TASK_STATUS_LABELS: Record<TaskStatus, string> = {
  NEW: 'New',
  IN_PROGRESS: 'In Progress',
  WAITING_BLOCKED: 'Waiting / Blocked',
  COMPLETED: 'Completed',
  CANCELLED: 'Cancelled',
};

export const TASK_STATUS_COLORS: Record<TaskStatus, { bg: string; text: string; border: string }> = {
  NEW:             { bg: 'bg-gray-100',   text: 'text-gray-700',  border: 'border-t-gray-400'  },
  IN_PROGRESS:     { bg: 'bg-blue-50',    text: 'text-blue-700',  border: 'border-t-blue-500'  },
  WAITING_BLOCKED: { bg: 'bg-amber-50',   text: 'text-amber-700', border: 'border-t-amber-500' },
  COMPLETED:       { bg: 'bg-green-50',   text: 'text-green-700', border: 'border-t-green-500' },
  CANCELLED:       { bg: 'bg-red-50',     text: 'text-red-700',   border: 'border-t-red-500'   },
};

export const TASK_PRIORITY_COLORS: Record<TaskPriority, string> = {
  LOW:    'text-gray-500',
  MEDIUM: 'text-blue-500',
  HIGH:   'text-orange-500',
  URGENT: 'text-red-600',
};

export const TASK_PRIORITY_ICONS: Record<TaskPriority, string> = {
  LOW:    'ri-arrow-down-line',
  MEDIUM: 'ri-subtract-line',
  HIGH:   'ri-arrow-up-line',
  URGENT: 'ri-alarm-warning-line',
};

export const ALL_TASK_STATUSES: TaskStatus[] = ['NEW', 'IN_PROGRESS', 'WAITING_BLOCKED', 'COMPLETED', 'CANCELLED'];
export const ACTIVE_TASK_STATUSES: TaskStatus[] = ['NEW', 'IN_PROGRESS', 'WAITING_BLOCKED'];
export const BOARD_COLUMNS: TaskStatus[] = ['NEW', 'IN_PROGRESS', 'WAITING_BLOCKED', 'COMPLETED'];
