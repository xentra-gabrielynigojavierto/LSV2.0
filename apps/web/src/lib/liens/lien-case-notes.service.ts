import { lienCaseNotesApi } from './lien-case-notes.api';
import type {
  CaseNoteResponse,
  CaseNoteCategory,
  CreateCaseNoteRequest,
  UpdateCaseNoteRequest,
} from './lien-case-notes.types';

export type { CaseNoteResponse, CaseNoteCategory, CreateCaseNoteRequest, UpdateCaseNoteRequest };

export const lienCaseNotesService = {
  async getNotes(caseId: string): Promise<CaseNoteResponse[]> {
    const res = await lienCaseNotesApi.list(caseId);
    return res.data ?? [];
  },

  async createNote(
    caseId: string,
    content: string,
    category: CaseNoteCategory,
    createdByName: string,
  ): Promise<CaseNoteResponse> {
    const payload: CreateCaseNoteRequest = { content, category, createdByName };
    const res = await lienCaseNotesApi.create(caseId, payload);
    if (!res.data) throw new Error('Failed to create note');
    return res.data;
  },

  async updateNote(
    caseId: string,
    noteId: string,
    content: string,
    category?: CaseNoteCategory,
  ): Promise<CaseNoteResponse> {
    const payload: UpdateCaseNoteRequest = { content, category };
    const res = await lienCaseNotesApi.update(caseId, noteId, payload);
    if (!res.data) throw new Error('Failed to update note');
    return res.data;
  },

  async deleteNote(caseId: string, noteId: string): Promise<void> {
    await lienCaseNotesApi.remove(caseId, noteId);
  },

  async pinNote(caseId: string, noteId: string): Promise<CaseNoteResponse> {
    const res = await lienCaseNotesApi.pin(caseId, noteId);
    if (!res.data) throw new Error('Failed to pin note');
    return res.data;
  },

  async unpinNote(caseId: string, noteId: string): Promise<CaseNoteResponse> {
    const res = await lienCaseNotesApi.unpin(caseId, noteId);
    if (!res.data) throw new Error('Failed to unpin note');
    return res.data;
  },
};
