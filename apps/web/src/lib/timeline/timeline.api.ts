/**
 * LS-FLOW-E16 — typed API client for the unified Task & Workflow
 * timeline endpoints. Routes through the BFF proxy at /api/flow/*
 * (see app/api/flow/[...path]/route.ts → Gateway → Flow service),
 * matching the pattern in `lib/tasks/tasks.api.ts`.
 */
import { apiClient } from '@/lib/api-client';
import type { ApiResponse } from '@/types';
import type {
  TaskTimelineResponse,
  WorkflowInstanceTimelineResponse,
} from './timeline.types';

const FLOW_PREFIX = '/flow/api/v1';

export const timelineApi = {
  /** GET /api/v1/workflow-tasks/{id}/timeline */
  getTaskTimeline(taskId: string): Promise<ApiResponse<TaskTimelineResponse>> {
    return apiClient.get<TaskTimelineResponse>(
      `${FLOW_PREFIX}/workflow-tasks/${encodeURIComponent(taskId)}/timeline`,
    );
  },

  /** GET /api/v1/workflow-instances/{id}/timeline (tenant-scoped). */
  getWorkflowInstanceTimeline(
    workflowInstanceId: string,
  ): Promise<ApiResponse<WorkflowInstanceTimelineResponse>> {
    return apiClient.get<WorkflowInstanceTimelineResponse>(
      `${FLOW_PREFIX}/workflow-instances/${encodeURIComponent(workflowInstanceId)}/timeline`,
    );
  },
};
