/**
 * LS-FLOW-E16 — typed shape of timeline events returned by the Flow
 * service. Mirrors the C# `TimelineEvent` record produced by
 * `AuditTimelineNormalizer` so the same UI consumes both the
 * tenant-portal and Control Center timeline endpoints.
 *
 * Events are returned **oldest-first** (ascending `occurredAtUtc`,
 * tie-broken by `eventId`).
 */

export interface TimelineActor {
  id?:   string | null;
  name?: string | null;
  type?: string | null;
}

export interface TimelineEvent {
  /** Stable id from the upstream audit service. */
  eventId: string;
  /** Internal audit row id (UUID); optional. */
  auditId?: string | null;
  /** ISO-8601 UTC timestamp. */
  occurredAtUtc: string;
  /**
   * Classified bucket — one of:
   * `workflow.created` | `workflow.state_changed` | `workflow.completed` |
   * `workflow.admin.cancel` | `workflow.admin.retry` | `workflow.admin.force_complete` |
   * `workflow.admin` | `workflow.sla` | `workflow.task.claim` |
   * `workflow.task.reassign` | `task.assigned` | `task.completed` |
   * `task` | `workflow` | `notification` | `other`
   */
  category: string;
  /** Raw audit action verb, preserved verbatim. */
  action: string;
  /** Source system (e.g. "flow"). */
  source: string;
  actor?: TimelineActor | null;
  /** Friendly form: name fallback to id. */
  performedBy?: string | null;
  /** Human-readable summary; may be null for unknown actions. */
  summary?: string | null;
  previousStatus?: string | null;
  newStatus?: string | null;
  /** Flat string→string map of enriched + JSON metadata keys. */
  metadata: Record<string, string | null>;
}

/** Response envelope for `GET /api/v1/workflow-tasks/{id}/timeline`. */
export interface TaskTimelineResponse {
  taskId: string;
  workflowInstanceId: string;
  totalCount: number;
  truncated: boolean;
  events: TimelineEvent[];
}

/** Response envelope for `GET /api/v1/workflow-instances/{id}/timeline`. */
export interface WorkflowInstanceTimelineResponse {
  workflowInstanceId: string;
  totalCount: number;
  truncated: boolean;
  events: TimelineEvent[];
}
