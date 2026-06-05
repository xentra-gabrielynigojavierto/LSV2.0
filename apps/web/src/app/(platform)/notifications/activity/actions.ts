'use server';

import { requireOrg } from '@/lib/auth-guards';
import { notificationsServerApi, NotifApiError } from '@/lib/notifications-server-api';
import type { RetryResult, ContactHealth, ContactSuppression } from '@/lib/notifications-shared';

export type ActionResult<T = void> =
  | { success: true; data: T }
  | { success: false; error: string };

const UUID_RE = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
const VALID_CHANNELS = new Set(['email', 'sms', 'push', 'in-app', 'slack', 'webhook']);
const MAX_CONTACT_LEN = 320;

function validateNotificationId(id: unknown): string | null {
  if (typeof id !== 'string' || !id.trim()) return 'Notification ID is required.';
  if (!UUID_RE.test(id.trim())) return 'Invalid notification ID format.';
  return null;
}

function validateChannel(ch: unknown): string | null {
  if (typeof ch !== 'string' || !ch.trim()) return 'Channel is required.';
  if (!VALID_CHANNELS.has(ch.trim().toLowerCase())) return `Unsupported channel: ${ch}`;
  return null;
}

function validateContactValue(cv: unknown): string | null {
  if (typeof cv !== 'string' || !cv.trim()) return 'Contact value is required.';
  if (cv.length > MAX_CONTACT_LEN) return 'Contact value is too long.';
  return null;
}

export async function retryNotification(
  notificationId: string,
): Promise<ActionResult<RetryResult>> {
  const idErr = validateNotificationId(notificationId);
  if (idErr) return { success: false, error: idErr };

  const session = await requireOrg();
  try {
    const res = await notificationsServerApi.retry(session.tenantId, notificationId.trim());
    return { success: true, data: res.data };
  } catch (err) {
    if (err instanceof NotifApiError) {
      if (err.status === 404 || err.status === 405 || err.status === 501) {
        return { success: false, error: 'Retry is not available for this notification.' };
      }
      if (err.status === 409) {
        return { success: false, error: 'This notification cannot be retried in its current state.' };
      }
      if (err.status === 422) {
        return { success: false, error: 'This notification is not eligible for retry.' };
      }
    }
    return { success: false, error: err instanceof Error ? err.message : 'Retry failed.' };
  }
}

export async function resendNotification(
  notificationId: string,
): Promise<ActionResult<RetryResult>> {
  const idErr = validateNotificationId(notificationId);
  if (idErr) return { success: false, error: idErr };

  const session = await requireOrg();
  try {
    const res = await notificationsServerApi.resend(session.tenantId, notificationId.trim());
    return { success: true, data: res.data };
  } catch (err) {
    if (err instanceof NotifApiError) {
      if (err.status === 404 || err.status === 405 || err.status === 501) {
        return { success: false, error: 'Resend is not available for this notification.' };
      }
      if (err.status === 409) {
        return { success: false, error: 'This notification cannot be resent in its current state.' };
      }
      if (err.status === 422) {
        return { success: false, error: 'This notification is not eligible for resend. The contact may be suppressed.' };
      }
    }
    return { success: false, error: err instanceof Error ? err.message : 'Resend failed.' };
  }
}

export async function fetchContactHealth(
  channel: string,
  contactValue: string,
): Promise<ActionResult<ContactHealth>> {
  const chErr = validateChannel(channel);
  if (chErr) return { success: false, error: chErr };
  const cvErr = validateContactValue(contactValue);
  if (cvErr) return { success: false, error: cvErr };

  const session = await requireOrg();
  try {
    const res = await notificationsServerApi.contactHealth(session.tenantId, channel.trim().toLowerCase(), contactValue.trim());
    return { success: true, data: res.data };
  } catch (err) {
    if (err instanceof NotifApiError) {
      if (err.status === 404 || err.status === 405 || err.status === 501) {
        return { success: false, error: 'Contact health data is not available.' };
      }
    }
    return { success: false, error: err instanceof Error ? err.message : 'Unable to load contact health.' };
  }
}

export async function fetchContactSuppressions(
  channel: string,
  contactValue: string,
): Promise<ActionResult<ContactSuppression[]>> {
  const chErr = validateChannel(channel);
  if (chErr) return { success: false, error: chErr };
  const cvErr = validateContactValue(contactValue);
  if (cvErr) return { success: false, error: cvErr };

  const session = await requireOrg();
  try {
    const res = await notificationsServerApi.contactSuppressions(session.tenantId, channel.trim().toLowerCase(), contactValue.trim());
    return { success: true, data: res.data };
  } catch (err) {
    if (err instanceof NotifApiError) {
      if (err.status === 404 || err.status === 405 || err.status === 501) {
        return { success: false, error: 'Suppression data is not available.' };
      }
    }
    return { success: false, error: err instanceof Error ? err.message : 'Unable to load suppressions.' };
  }
}
