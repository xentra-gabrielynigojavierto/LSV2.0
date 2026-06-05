import { apiClient } from '@/lib/api-client';
import type {
  TaskNoteResponse,
  CreateTaskNoteRequest,
  UpdateTaskNoteRequest,
} from './lien-task-notes.types';

export const lienTaskNotesApi = {
  list(taskId: string) {
    return apiClient.get<TaskNoteResponse[]>(`/lien/api/liens/tasks/${taskId}/notes`);
  },

  create(taskId: string, request: CreateTaskNoteRequest) {
    return apiClient.post<TaskNoteResponse>(
      `/lien/api/liens/tasks/${taskId}/notes`,
      request,
    );
  },

  update(taskId: string, noteId: string, request: UpdateTaskNoteRequest) {
    return apiClient.put<TaskNoteResponse>(
      `/lien/api/liens/tasks/${taskId}/notes/${noteId}`,
      request,
    );
  },

  delete(taskId: string, noteId: string) {
    return apiClient.delete<void>(`/lien/api/liens/tasks/${taskId}/notes/${noteId}`);
  },
};
