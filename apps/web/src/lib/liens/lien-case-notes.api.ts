import { apiClient } from '@/lib/api-client';
import type {
  CaseNoteResponse,
  CreateCaseNoteRequest,
  UpdateCaseNoteRequest,
} from './lien-case-notes.types';

export const lienCaseNotesApi = {
  list(caseId: string) {
    return apiClient.get<CaseNoteResponse[]>(`/lien/api/liens/cases/${caseId}/notes`);
  },

  create(caseId: string, request: CreateCaseNoteRequest) {
    return apiClient.post<CaseNoteResponse>(
      `/lien/api/liens/cases/${caseId}/notes`,
      request,
    );
  },

  update(caseId: string, noteId: string, request: UpdateCaseNoteRequest) {
    return apiClient.put<CaseNoteResponse>(
      `/lien/api/liens/cases/${caseId}/notes/${noteId}`,
      request,
    );
  },

  remove(caseId: string, noteId: string) {
    return apiClient.delete<void>(`/lien/api/liens/cases/${caseId}/notes/${noteId}`);
  },

  pin(caseId: string, noteId: string) {
    return apiClient.post<CaseNoteResponse>(
      `/lien/api/liens/cases/${caseId}/notes/${noteId}/pin`,
      {},
    );
  },

  unpin(caseId: string, noteId: string) {
    return apiClient.post<CaseNoteResponse>(
      `/lien/api/liens/cases/${caseId}/notes/${noteId}/unpin`,
      {},
    );
  },
};
