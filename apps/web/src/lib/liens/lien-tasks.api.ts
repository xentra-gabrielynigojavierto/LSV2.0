import { apiClient } from '@/lib/api-client';
import type {
  TaskDto,
  PaginatedTasksDto,
  CreateTaskRequest,
  UpdateTaskRequest,
  AssignTaskRequest,
  UpdateTaskStatusRequest,
  TasksQuery,
} from './lien-tasks.types';

function toQs(params: Record<string, unknown>): string {
  const pairs = Object.entries(params)
    .filter(([, v]) => v !== undefined && v !== null && v !== '')
    .map(([k, v]) => `${encodeURIComponent(k)}=${encodeURIComponent(String(v))}`);
  return pairs.length ? `?${pairs.join('&')}` : '';
}

export const lienTasksApi = {
  list(query: TasksQuery = {}) {
    return apiClient.get<PaginatedTasksDto>(
      `/lien/api/liens/tasks${toQs(query as Record<string, unknown>)}`,
    );
  },

  getById(id: string) {
    return apiClient.get<TaskDto>(`/lien/api/liens/tasks/${id}`);
  },

  create(request: CreateTaskRequest) {
    return apiClient.post<TaskDto>('/lien/api/liens/tasks', request);
  },

  update(id: string, request: UpdateTaskRequest) {
    return apiClient.put<TaskDto>(`/lien/api/liens/tasks/${id}`, request);
  },

  assign(id: string, request: AssignTaskRequest) {
    return apiClient.post<TaskDto>(`/lien/api/liens/tasks/${id}/assign`, request);
  },

  updateStatus(id: string, request: UpdateTaskStatusRequest) {
    return apiClient.post<TaskDto>(`/lien/api/liens/tasks/${id}/status`, request);
  },

  complete(id: string) {
    return apiClient.post<TaskDto>(`/lien/api/liens/tasks/${id}/complete`, {});
  },

  cancel(id: string) {
    return apiClient.post<TaskDto>(`/lien/api/liens/tasks/${id}/cancel`, {});
  },
};
