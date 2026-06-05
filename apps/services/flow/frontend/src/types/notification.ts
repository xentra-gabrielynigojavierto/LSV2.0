export const NOTIFICATION_TYPES = [
  "TASK_ASSIGNED",
  "TASK_REASSIGNED",
  "TASK_TRANSITIONED",
  "AUTOMATION_SUCCEEDED",
  "AUTOMATION_FAILED",
  "WORKFLOW_ASSIGNED",
] as const;
export type NotificationType = typeof NOTIFICATION_TYPES[number];

export const NOTIFICATION_TYPE_LABELS: Record<NotificationType, string> = {
  TASK_ASSIGNED: "Assigned",
  TASK_REASSIGNED: "Reassigned",
  TASK_TRANSITIONED: "Transitioned",
  AUTOMATION_SUCCEEDED: "Automation OK",
  AUTOMATION_FAILED: "Automation Failed",
  WORKFLOW_ASSIGNED: "Workflow Assigned",
};

export const NOTIFICATION_TYPE_COLORS: Record<NotificationType, string> = {
  TASK_ASSIGNED: "bg-blue-100 text-blue-700",
  TASK_REASSIGNED: "bg-indigo-100 text-indigo-700",
  TASK_TRANSITIONED: "bg-green-100 text-green-700",
  AUTOMATION_SUCCEEDED: "bg-emerald-100 text-emerald-700",
  AUTOMATION_FAILED: "bg-red-100 text-red-700",
  WORKFLOW_ASSIGNED: "bg-purple-100 text-purple-700",
};

export interface NotificationResponse {
  id: string;
  taskId?: string | null;
  workflowDefinitionId?: string | null;
  type: NotificationType;
  title: string;
  message: string;
  targetUserId?: string | null;
  targetRoleKey?: string | null;
  targetOrgId?: string | null;
  status: "Unread" | "Read";
  sourceType: string;
  createdAt: string;
  readAt?: string | null;
}

export interface NotificationPagedResponse {
  items: NotificationResponse[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface NotificationSummary {
  unreadCount: number;
}

export const NOTIFICATION_SOURCE_TYPES = [
  "Assignment",
  "WorkflowTransition",
  "AutomationHook",
  "System",
] as const;
export type NotificationSourceType = typeof NOTIFICATION_SOURCE_TYPES[number];
