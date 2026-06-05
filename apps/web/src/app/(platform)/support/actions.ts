'use server';

import { revalidatePath } from 'next/cache';
import { requireSession } from '@/lib/session';
import { supportServerApi, type TicketPriority } from '@/lib/support-server-api';

// ── Create Ticket ─────────────────────────────────────────────────────────────

export interface CreateTicketResult {
  success:   boolean;
  ticketId?: string;
  error?:    string;
}

export async function createTicketAction(data: {
  title:       string;
  description: string;
  priority:    TicketPriority;
  category:    string;
}): Promise<CreateTicketResult> {
  const session = await requireSession();

  if (!data.title.trim()) {
    return { success: false, error: 'Title is required.' };
  }

  try {
    const ticket = await supportServerApi.tickets.create({
      title:           data.title.trim(),
      description:     data.description.trim() || undefined,
      priority:        data.priority,
      category:        data.category.trim() || undefined,
      tenantId:        session.tenantId,
      requesterUserId: session.userId,
      requesterName:   session.email,
      requesterEmail:  session.email,
      source:          'Portal',
    });
    revalidatePath('/support');
    return { success: true, ticketId: ticket.id };
  } catch (err) {
    return {
      success: false,
      error: err instanceof Error ? err.message : 'Failed to create ticket.',
    };
  }
}

// ── Add Comment ────────────────────────────────────────────────────────────────

export interface AddCommentResult {
  success: boolean;
  error?:  string;
}

export async function addCommentAction(
  ticketId: string,
  body: string,
): Promise<AddCommentResult> {
  await requireSession();

  if (!body.trim()) {
    return { success: false, error: 'Reply cannot be empty.' };
  }

  try {
    await supportServerApi.tickets.addComment(ticketId, body.trim(), {
      visibility: 'CustomerVisible',
    });
    revalidatePath(`/support/${ticketId}`);
    return { success: true };
  } catch (err) {
    return {
      success: false,
      error: err instanceof Error ? err.message : 'Failed to send reply.',
    };
  }
}
