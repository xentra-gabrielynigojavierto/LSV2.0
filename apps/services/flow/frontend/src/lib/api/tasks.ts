import { apiFetch } from "@/lib/api/client";
import type {
  TaskResponse,
  PagedResponse,
  TaskListQuery,
  CreateTaskRequest,
  UpdateTaskRequest,
  UpdateTaskStatusRequest,
  AssignTaskRequest,
} from "@/types/task";

function buildQueryString(params: Record<string, string | number | undefined>): string {
  const entries = Object.entries(params).filter(
    ([, v]) => v !== undefined && v !== ""
  );
  if (entries.length === 0) return "";
  return "?" + entries.map(([k, v]) => `${k}=${encodeURIComponent(String(v))}`).join("&");
}

export async function listTasks(
  query: TaskListQuery
): Promise<PagedResponse<TaskResponse>> {
  const qs = buildQueryString({
    status: query.status,
    assignedToUserId: query.assignedToUserId,
    assignedToRoleKey: query.assignedToRoleKey,
    assignedToOrgId: query.assignedToOrgId,
    contextType: query.contextType,
    contextId: query.contextId,
    productKey: query.productKey,
    page: query.page,
    pageSize: query.pageSize,
    sortBy: query.sortBy,
    sortDirection: query.sortDirection,
  });
  return apiFetch<PagedResponse<TaskResponse>>(`/api/v1/tasks${qs}`);
}

export async function getTask(id: string): Promise<TaskResponse> {
  return apiFetch<TaskResponse>(`/api/v1/tasks/${id}`);
}

export async function updateTask(
  id: string,
  request: UpdateTaskRequest
): Promise<TaskResponse> {
  return apiFetch<TaskResponse>(`/api/v1/tasks/${id}`, {
    method: "PUT",
    body: JSON.stringify(request),
  });
}

export async function updateTaskStatus(
  id: string,
  request: UpdateTaskStatusRequest
): Promise<TaskResponse> {
  return apiFetch<TaskResponse>(`/api/v1/tasks/${id}/status`, {
    method: "PATCH",
    body: JSON.stringify(request),
  });
}

export async function createTask(
  request: CreateTaskRequest
): Promise<TaskResponse> {
  return apiFetch<TaskResponse>("/api/v1/tasks", {
    method: "POST",
    body: JSON.stringify(request),
  });
}

export async function assignTask(
  id: string,
  request: AssignTaskRequest
): Promise<TaskResponse> {
  return apiFetch<TaskResponse>(`/api/v1/tasks/${id}/assign`, {
    method: "PATCH",
    body: JSON.stringify(request),
  });
}
