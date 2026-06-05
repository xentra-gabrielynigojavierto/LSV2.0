/**
 * LS-FLOW-E11.6 / LS-FLOW-E15 — thin API adapter for the Work area UI.
 *
 * Talks to the Flow service via the BFF proxy at /api/flow/* which
 * forwards to GATEWAY/flow/* (see app/api/flow/[...path]/route.ts and
 * Gateway.Api `flow-protected` route). The browser never talks to Flow
 * directly; the BFF rewrites the platform_session cookie into a Bearer
 * header.
 *
 * Endpoints covered:
 *   GET    /api/v1/tasks/me
 *   GET    /api/v1/tasks/role-queue        (E15)
 *   GET    /api/v1/tasks/org-queue         (E15)
 *   GET    /api/v1/workflow-tasks/{id}     (E15)
 *   POST   /api/v1/workflow-tasks/{id}/start
 *   POST   /api/v1/workflow-tasks/{id}/complete
 *   POST   /api/v1/workflow-tasks/{id}/cancel
 *   POST   /api/v1/workflow-tasks/{id}/claim    (E14.2)
 *   POST   /api/v1/workflow-tasks/{id}/reassign (E14.2)
 *
 * Errors bubble as ApiError (see lib/api-client.ts) so callers can
 * branch on .isNotFound / .isConflict / status === 422 / status === 403
 * without parsing raw response bodies.
 */
import { apiClient } from '@/lib/api-client';
import type { ApiResponse } from '@/types';
import type {
  ClaimTaskRequest,
  MyTask,
  PagedTasks,
  ReassignTaskRequest,
  TaskCompletionResult,
  TaskTransitionResult,
  WorkflowTaskAssignmentResult,
  WorkflowTaskStatus,
} from './tasks.types';

export interface ListMyTasksParams {
  /** Repeat-able status filter. Empty / undefined returns all. */
  status?: WorkflowTaskStatus[];
  page?: number;
  pageSize?: number;
}

/** Pagination-only params for queue surfaces (E15). Eligibility is derived server-side. */
export interface ListQueueParams {
  page?: number;
  pageSize?: number;
}

const FLOW_PREFIX = '/flow/api/v1';

function buildMyTasksQuery(params: ListMyTasksParams): string {
  const qs = new URLSearchParams();
  if (params.status && params.status.length > 0) {
    for (const s of params.status) qs.append('status', s);
  }
  if (params.page)     qs.set('page', String(params.page));
  if (params.pageSize) qs.set('pageSize', String(params.pageSize));
  const s = qs.toString();
  return s ? `?${s}` : '';
}

function buildQueueQuery(params: ListQueueParams): string {
  const qs = new URLSearchParams();
  if (params.page)     qs.set('page', String(params.page));
  if (params.pageSize) qs.set('pageSize', String(params.pageSize));
  const s = qs.toString();
  return s ? `?${s}` : '';
}

export const tasksApi = {
  // ------- read surfaces -------
  listMine(params: ListMyTasksParams = {}): Promise<ApiResponse<PagedTasks>> {
    return apiClient.get<PagedTasks>(`${FLOW_PREFIX}/tasks/me${buildMyTasksQuery(params)}`);
  },
  listRoleQueue(params: ListQueueParams = {}): Promise<ApiResponse<PagedTasks>> {
    return apiClient.get<PagedTasks>(`${FLOW_PREFIX}/tasks/role-queue${buildQueueQuery(params)}`);
  },
  listOrgQueue(params: ListQueueParams = {}): Promise<ApiResponse<PagedTasks>> {
    return apiClient.get<PagedTasks>(`${FLOW_PREFIX}/tasks/org-queue${buildQueueQuery(params)}`);
  },
  getTaskDetail(taskId: string): Promise<ApiResponse<MyTask>> {
    return apiClient.get<MyTask>(
      `${FLOW_PREFIX}/workflow-tasks/${encodeURIComponent(taskId)}`,
    );
  },

  // ------- lifecycle (E11.5/E11.7) -------
  start(taskId: string): Promise<ApiResponse<TaskTransitionResult>> {
    return apiClient.post<TaskTransitionResult>(
      `${FLOW_PREFIX}/workflow-tasks/${encodeURIComponent(taskId)}/start`,
      {},
    );
  },
  complete(taskId: string): Promise<ApiResponse<TaskCompletionResult>> {
    return apiClient.post<TaskCompletionResult>(
      `${FLOW_PREFIX}/workflow-tasks/${encodeURIComponent(taskId)}/complete`,
      {},
    );
  },
  cancel(taskId: string): Promise<ApiResponse<TaskTransitionResult>> {
    return apiClient.post<TaskTransitionResult>(
      `${FLOW_PREFIX}/workflow-tasks/${encodeURIComponent(taskId)}/cancel`,
      {},
    );
  },

  // ------- assignment (E14.2) -------
  claim(
    taskId: string,
    body: ClaimTaskRequest = {},
  ): Promise<ApiResponse<WorkflowTaskAssignmentResult>> {
    return apiClient.post<WorkflowTaskAssignmentResult>(
      `${FLOW_PREFIX}/workflow-tasks/${encodeURIComponent(taskId)}/claim`,
      body,
    );
  },
  reassign(
    taskId: string,
    body: ReassignTaskRequest,
  ): Promise<ApiResponse<WorkflowTaskAssignmentResult>> {
    return apiClient.post<WorkflowTaskAssignmentResult>(
      `${FLOW_PREFIX}/workflow-tasks/${encodeURIComponent(taskId)}/reassign`,
      body,
    );
  },
};

export type {
  MyTask,
  PagedTasks,
  TaskTransitionResult,
  TaskCompletionResult,
  WorkflowTaskAssignmentResult,
  ClaimTaskRequest,
  ReassignTaskRequest,
};
