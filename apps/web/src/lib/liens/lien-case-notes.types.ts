export type CaseNoteCategory = 'general' | 'internal' | 'follow-up';

export interface CaseNoteResponse {
  id: string;
  caseId: string;
  content: string;
  category: CaseNoteCategory;
  isPinned: boolean;
  createdByUserId: string;
  createdByName: string;
  isEdited: boolean;
  createdAtUtc: string;
  updatedAtUtc: string | null;
}

export interface CreateCaseNoteRequest {
  content: string;
  category: CaseNoteCategory;
  createdByName: string;
}

export interface UpdateCaseNoteRequest {
  content: string;
  category?: CaseNoteCategory;
}
