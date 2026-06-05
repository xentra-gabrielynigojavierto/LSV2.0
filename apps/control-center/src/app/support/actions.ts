'use server';

import { requirePlatformAdmin } from '@/lib/auth';
import { controlCenterServerApi } from '@/lib/control-center-api';
import type { SupportCaseDetail, SupportCaseStatus, SupportNote, SupportCase } from '@/types/control-center';

export interface UpdateStatusResult {
  success:  boolean;
  case?:    SupportCase;
  error?:   string;
}

export interface AddNoteResult {
  success: boolean;
  note?:   SupportNote;
  error?:  string;
}

export interface CreateCaseResult {
  success: boolean;
  case?:   SupportCaseDetail;
  error?:  string;
}

export interface AssignResult {
  success: boolean;
  case?:   SupportCase;
  error?:  string;
}

export async function updateCaseStatus(
  caseId: string,
  status: SupportCaseStatus,
): Promise<UpdateStatusResult> {
  await requirePlatformAdmin();
  try {
    const updated = await controlCenterServerApi.support.updateStatus(caseId, status);
    return { success: true, case: updated };
  } catch (err) {
    return { success: false, error: err instanceof Error ? err.message : 'Failed to update status.' };
  }
}

export async function addCaseNote(
  caseId:  string,
  message: string,
): Promise<AddNoteResult> {
  const session = await requirePlatformAdmin();
  if (!message.trim()) return { success: false, error: 'Note cannot be empty.' };
  try {
    const note = await controlCenterServerApi.support.addNote(caseId, message.trim(), {
      commentType:  'InternalNote',
      visibility:   'Internal',
      authorUserId: session.userId,
      authorEmail:  session.email,
    });
    return { success: true, note };
  } catch (err) {
    return { success: false, error: err instanceof Error ? err.message : 'Failed to add note.' };
  }
}

export async function addPublicReply(
  caseId:  string,
  message: string,
): Promise<AddNoteResult> {
  const session = await requirePlatformAdmin();
  if (!message.trim()) return { success: false, error: 'Reply cannot be empty.' };
  try {
    const note = await controlCenterServerApi.support.addNote(caseId, message.trim(), {
      commentType:  'CustomerReply',
      visibility:   'CustomerVisible',
      authorUserId: session.userId,
      authorEmail:  session.email,
    });
    return { success: true, note };
  } catch (err) {
    return { success: false, error: err instanceof Error ? err.message : 'Failed to send reply.' };
  }
}

export async function assignCaseToMe(caseId: string): Promise<AssignResult> {
  const session = await requirePlatformAdmin();
  try {
    const updated = await controlCenterServerApi.support.assignTicket(caseId, {
      assignedUserId: session.userId,
    });
    return { success: true, case: updated };
  } catch (err) {
    return { success: false, error: err instanceof Error ? err.message : 'Failed to assign ticket.' };
  }
}

export async function unassignCase(caseId: string): Promise<AssignResult> {
  await requirePlatformAdmin();
  try {
    const updated = await controlCenterServerApi.support.assignTicket(caseId, {
      clearAssignment: true,
    });
    return { success: true, case: updated };
  } catch (err) {
    return { success: false, error: err instanceof Error ? err.message : 'Failed to unassign ticket.' };
  }
}

export async function createSupportCase(data: {
  title:      string;
  tenantId:   string;
  tenantName: string;
  userId?:    string;
  userName?:  string;
  category:   string;
  priority:   SupportCase['priority'];
}): Promise<CreateCaseResult> {
  await requirePlatformAdmin();
  if (!data.title.trim()) return { success: false, error: 'Title is required.' };
  try {
    const created = await controlCenterServerApi.support.create(data);
    return { success: true, case: created };
  } catch (err) {
    return { success: false, error: err instanceof Error ? err.message : 'Failed to create case.' };
  }
}
