/**
 * LS-FLOW-E11.6 / LS-FLOW-E15 — type contracts for the Work area UI.
 *
 * Mirrors the Flow.Api DTOs:
 *   - MyTaskDto                       (Application/DTOs/MyTaskDtos.cs) — widened in E15
 *   - PagedResponse<T>                (Application/DTOs/TaskDtos.cs)
 *   - WorkflowTaskTransitionResult    (Application/Interfaces/IWorkflowTaskLifecycleService.cs)
 *   - WorkflowTaskCompletionResult    (Application/Interfaces/IWorkflowTaskCompletionService.cs)
 *   - WorkflowTaskAssignmentResult    (Application/Interfaces/IWorkflowTaskAssignmentService.cs)  — E14.2
 *
 * Field shapes are kept narrow on purpose so the UI does not couple to
 * engine internals. Anything missing here is intentionally not surfaced.
 */

export type WorkflowTaskStatus = 'Open' | 'InProgress' | 'Completed' | 'Cancelled';

export type WorkflowTaskPriority = 'Low' | 'Normal' | 'High' | 'Urgent';

/**
 * LS-FLOW-E14.1 — assignment-mode discriminator. Mirrors
 * Flow.Domain.Common.WorkflowTaskAssignmentMode. Stable strings, not
 * enum ordinals, so the wire format is the same as the persisted form.
 */
export type WorkflowTaskAssignmentMode =
  | 'DirectUser'
  | 'RoleQueue'
  | 'OrgQueue'
  | 'Unassigned';

export const ASSIGNMENT_MODES: WorkflowTaskAssignmentMode[] = [
  'DirectUser',
  'RoleQueue',
  'OrgQueue',
  'Unassigned',
];

export interface MyTask {
  taskId: string;
  title: string;
  description?: string | null;
  status: WorkflowTaskStatus;
  priority: WorkflowTaskPriority;
  stepKey: string;

  // Assignment context (LS-FLOW-E15)
  assignmentMode: WorkflowTaskAssignmentMode;
  assignedUserId?: string | null;
  assignedRole?: string | null;
  assignedOrgId?: string | null;
  assignedAt?: string | null;
  assignedBy?: string | null;
  assignmentReason?: string | null;

  createdAt: string;
  updatedAt?: string | null;
  startedAt?: string | null;
  completedAt?: string | null;
  cancelledAt?: string | null;

  workflowInstanceId: string;
  workflowName?: string | null;
  productKey?: string | null;

  // SLA / Timer (LS-FLOW-E10.3 task slice)
  /** UTC deadline for this task. Null when no SLA applied at creation. */
  dueAt?: string | null;
  /**
   * SLA classification. The wire string `DueSoon` is rendered as
   * "At Risk" in the UI to match the spec vocabulary.
   */
  slaStatus?: WorkflowTaskSlaStatus;
  /** First-observation breach timestamp; null until the task is observed Overdue. */
  slaBreachedAt?: string | null;
}

/**
 * LS-FLOW-E10.3 (task slice) — wire-format SLA status. Mirrors
 * Flow.Domain.Common.WorkflowSlaStatus. Tasks never carry `Escalated`
 * in this phase.
 */
export type WorkflowTaskSlaStatus = 'OnTrack' | 'DueSoon' | 'Overdue' | 'Escalated';

export interface PagedTasks {
  items: MyTask[];
  totalCount: number;
  page: number;
  pageSize: number;
}

/** Response from start / cancel. */
export interface TaskTransitionResult {
  taskId: string;
  previousStatus: WorkflowTaskStatus;
  newStatus: WorkflowTaskStatus;
  transitionedAtUtc: string;
}

/**
 * Response from complete (E11.7). Strictly additive over
 * TaskTransitionResult — it carries every legacy field plus the
 * resulting workflow snapshot so the UI can refresh the row in one
 * round-trip.
 */
export interface TaskCompletionResult extends TaskTransitionResult {
  workflowInstanceId: string;
  fromStepKey: string;
  toStepKey: string;
  workflowStatus: string;
  workflowAdvanced: boolean;
}

/**
 * LS-FLOW-E14.2 — response shape from /claim and /reassign. The
 * server returns the post-write task state so callers can refresh
 * local UI without a follow-up GET.
 */
export interface WorkflowTaskAssignmentResult {
  taskId: string;
  workflowInstanceId: string;
  status: WorkflowTaskStatus;
  assignmentMode: WorkflowTaskAssignmentMode;
  assignedUserId?: string | null;
  assignedRole?: string | null;
  assignedOrgId?: string | null;
  assignedAt?: string | null;
  assignedBy?: string | null;
  assignmentReason?: string | null;
  occurredAtUtc: string;
}

/** Body for POST /claim. All fields optional; the body itself may be omitted. */
export interface ClaimTaskRequest {
  reason?: string;
}

/** Body for POST /reassign. */
export interface ReassignTaskRequest {
  targetMode: WorkflowTaskAssignmentMode;
  assignedUserId?: string | null;
  assignedRole?: string | null;
  assignedOrgId?: string | null;
  reason: string;
}

export type StatusFilter = 'all' | WorkflowTaskStatus;

/** Status values the user can pick in the My Tasks filter chip. */
export const STATUS_FILTER_OPTIONS: { value: StatusFilter; label: string }[] = [
  { value: 'all',        label: 'All' },
  { value: 'Open',       label: 'Open' },
  { value: 'InProgress', label: 'In Progress' },
  { value: 'Completed',  label: 'Completed' },
  { value: 'Cancelled',  label: 'Cancelled' },
];

/** Reason text limit, matched to the backend MaxReasonLength (E14.2). */
export const REASSIGN_REASON_MAX = 500;
