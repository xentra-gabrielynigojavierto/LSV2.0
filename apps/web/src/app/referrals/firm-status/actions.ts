'use server';

const GATEWAY_URL = process.env.GATEWAY_URL ?? 'http://127.0.0.1:5010';

export interface PostCommentResult {
  success:  boolean;
  error?:   string;
  comment?: {
    id:         string;
    senderType: string;
    senderName: string;
    message:    string;
    createdAt:  string;
  };
}

export async function postReferrerComment(
  token:      string,
  senderName: string,
  message:    string,
): Promise<PostCommentResult> {
  if (!senderName?.trim()) {
    return { success: false, error: 'Please enter your name.' };
  }
  if (!message?.trim() || message.length > 4000) {
    return { success: false, error: 'Message is required and must be under 4000 characters.' };
  }

  try {
    const resp = await fetch(
      `${GATEWAY_URL}/careconnect/api/public/referrals/thread/comments?token=${encodeURIComponent(token)}`,
      {
        method:  'POST',
        headers: { 'Content-Type': 'application/json' },
        body:    JSON.stringify({ senderType: 'referrer', senderName: senderName.trim(), message: message.trim() }),
        cache:   'no-store',
      },
    );

    if (!resp.ok) {
      const body = await resp.json().catch(() => ({}));
      return { success: false, error: (body as { error?: string }).error ?? 'Failed to send message. Please try again.' };
    }

    const comment = await resp.json();
    return { success: true, comment };
  } catch {
    return { success: false, error: 'Network error. Please check your connection and try again.' };
  }
}
