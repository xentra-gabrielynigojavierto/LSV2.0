/**
 * E8.1 — Tenant-portal types for the Flow product-workflow surface.
 *
 * These mirror the wire shapes returned by the SynqLien BFF
 * (`/api/lien/api/liens/cases/{id}/workflows` and
 * `/api/lien/api/liens/workflow-definitions`) which in turn passthrough to
 * Flow. Field names match the C# DTOs camel-cased by System.Text.Json.
 *
 * Kept product-agnostic so CareConnect / SynqFund can reuse the module.
 */

export type WorkflowStatus =
  | 'Active'
  | 'Completed'
  | 'Cancelled'
  | 'Failed'
  | 'Pending'
  | string;

/** Row returned by `GET .../cases/{id}/workflows` (and from the start call). */
export interface ProductWorkflowRow {
  id: string;
  productKey: string;
  sourceEntityType: string;
  sourceEntityId: string;
  workflowDefinitionId: string;
  workflowInstanceId?: string | null;
  workflowInstanceTaskId?: string | null;
  correlationKey?: string | null;
  status: WorkflowStatus;
  createdAt: string;
  updatedAt?: string | null;
}

/** Optional richer detail returned by the atomic ownership-aware endpoint. */
export interface WorkflowInstanceDetail {
  id: string;
  workflowDefinitionId: string;
  productKey: string;
  correlationKey?: string | null;
  initialTaskId?: string | null;
  status: WorkflowStatus;
  currentStageId?: string | null;
  currentStepKey?: string | null;
  startedAt?: string | null;
  completedAt?: string | null;
  assignedToUserId?: string | null;
  lastErrorMessage?: string | null;
  createdAt: string;
  updatedAt?: string | null;
}

/** Workflow definition row used by the start modal selector. */
export interface WorkflowDefinitionRow {
  id: string;
  name: string;
  description?: string | null;
  version: string;
  status: string;
  productKey: string;
}

/** Body accepted by `POST .../cases/{id}/workflows`. */
export interface StartWorkflowRequest {
  workflowDefinitionId: string;
  title: string;
  description?: string;
  correlationKey?: string;
  assignedToUserId?: string;
  assignedToRoleKey?: string;
  assignedToOrgId?: string;
  dueDate?: string;
}

/**
 * E8.4 — body accepted by
 * `POST .../cases/{id}/workflows/{workflowInstanceId}/advance`.
 *
 * `expectedCurrentStepKey` is required for optimistic concurrency: Flow
 * compares it against the engine's current step before transitioning and
 * returns `409 Conflict` (`expected_step_mismatch`) on drift.
 *
 * `toStepKey` is optional — when omitted Flow advances to the next step
 * defined by the workflow definition's transitions.
 */
export interface AdvanceWorkflowRequest {
  expectedCurrentStepKey: string;
  toStepKey?: string;
  payload?: Record<string, string>;
}
