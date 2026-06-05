import { lienTaskNotesApi } from './lien-task-notes.api';
import type {
  TaskNoteResponse,
  CreateTaskNoteRequest,
  UpdateTaskNoteRequest,
} from './lien-task-notes.types';

export const lienTaskNotesService = {
  async getNotes(taskId: string): Promise<TaskNoteResponse[]> {
    const res = await lienTaskNotesApi.list(taskId);
    return res.data ?? [];
  },

  async createNote(taskId: string, content: string): Promise<TaskNoteResponse> {
    const request: CreateTaskNoteRequest = { content };
    const res = await lienTaskNotesApi.create(taskId, request);
    if (!res.data) throw new Error('Failed to create note');
    return res.data;
  },

  async updateNote(taskId: string, noteId: string, content: string): Promise<TaskNoteResponse> {
    const request: UpdateTaskNoteRequest = { content };
    const res = await lienTaskNotesApi.update(taskId, noteId, request);
    if (!res.data) throw new Error('Failed to update note');
    return res.data;
  },

  async deleteNote(taskId: string, noteId: string): Promise<void> {
    await lienTaskNotesApi.delete(taskId, noteId);
  },
};
