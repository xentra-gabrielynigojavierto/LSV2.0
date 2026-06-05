'use client';

import { useState, useTransition, useCallback, useEffect } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import type { NotifDetail, ContactHealth, ContactSuppression, ActionEligibility } from '@/lib/notifications-shared';
import {
  retryNotification,
  resendNotification,
  fetchContactHealth,
  fetchContactSuppressions,
} from '../actions';

function parseRecipientFromJson(json: string): { value: string; type: string } {
  try {
    const r = JSON.parse(json) as Record<string, string>;
    if (r.email) return { value: r.email, type: 'email' };
    if (r.phone) return { value: r.phone, type: 'phone' };
    if (r.address) return { value: r.address, type: 'address' };
  } catch { /* ignore */ }
  return { value: '', type: '' };
}

function deriveEligibility(notification: NotifDetail): ActionEligibility {
  const status = notification.status.toLowerCase();

  if (['sent', 'delivered'].includes(status)) {
    return { eligible: false, reason: 'This notification has already been delivered successfully.' };
  }

  if (['accepted', 'processing', 'queued'].includes(status)) {
    return { eligible: false, reason: 'This notification is still being processed.' };
  }

  if (status === 'blocked') {
    const hasSuppression = !!(notification.suppressionReason || notification.blockedReason?.toLowerCase().includes('suppress'));
    if (hasSuppression) {
      return { eligible: false, reason: 'This notification was blocked due to a suppression policy. It cannot be resent while the contact remains suppressed.' };
    }
    return { eligible: false, reason: 'This notification was blocked by a delivery policy. Review the block reason for details.' };
  }

  if (status === 'failed') {
    const category = (notification.failureCategory ?? '').toLowerCase();
    const isBounce = category.includes('bounce') || category.includes('invalid');
    const isSuppressed = category.includes('suppress');

    if (isSuppressed) {
      return { eligible: false, reason: 'This notification failed due to a suppression. The contact may need to be reviewed before retrying.' };
    }

    if (isBounce) {
      return { eligible: true, actions: ['resend'] };
    }

    return { eligible: true, actions: ['retry', 'resend'] };
  }

  return { eligible: false, reason: 'This notification is not in a state that allows retry or resend.' };
}

const SUPPRESSION_LABELS: Record<string, string> = {
  bounce:      'Hard Bounce',
  complaint:   'Spam Complaint',
  unsubscribe: 'Unsubscribed',
  invalid:     'Invalid Address',
  manual:      'Manually Suppressed',
};

function SuppressionAwarenessPanel({ notification }: { notification: NotifDetail }) {
  const isBlocked = notification.status.toLowerCase() === 'blocked';
  const hasSuppression = !!(notification.suppressionReason || notification.blockedReason?.toLowerCase().includes('suppress'));
  const isFailed = notification.status.toLowerCase() === 'failed';

  if (!isBlocked && !hasSuppression && !isFailed) return null;
  if (isFailed && !hasSuppression && !notification.blockedReason) return null;

  const suppressionReason = notification.suppressionReason ?? notification.blockedReason ?? null;
  const reasonLabel = suppressionReason
    ? SUPPRESSION_LABELS[suppressionReason.toLowerCase()] ?? suppressionReason
    : null;

  return (
    <div className="bg-amber-50 rounded-lg border border-amber-200 p-5">
      <div className="flex items-center gap-2 mb-3">
        <i className="ri-shield-line text-amber-600 text-lg" />
        <h3 className="text-sm font-semibold text-amber-800">
          {hasSuppression ? 'Delivery Suppressed' : 'Delivery Blocked'}
        </h3>
      </div>
      <div className="space-y-2 text-sm text-amber-700">
        {hasSuppression && (
          <p>
            This notification was {isBlocked ? 'blocked' : 'affected'} because the recipient contact is suppressed.
            Suppressed contacts cannot receive notifications until the suppression is resolved.
          </p>
        )}
        {reasonLabel && (
          <div className="flex items-center gap-2 mt-2">
            <span className="text-xs font-semibold uppercase tracking-wide text-amber-600">Reason:</span>
            <span className="font-medium">{reasonLabel}</span>
          </div>
        )}
        {!hasSuppression && notification.blockedReason && (
          <p>
            This notification was blocked by a delivery policy: <span className="font-medium">{notification.blockedReason}</span>
          </p>
        )}
      </div>
    </div>
  );
}

function ContactHealthCard({ notification }: { notification: NotifDetail }) {
  const [health, setHealth] = useState<ContactHealth | null>(null);
  const [suppressions, setSuppressions] = useState<ContactSuppression[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [loaded, setLoaded] = useState(false);

  const recipient = parseRecipientFromJson(notification.recipientJson);

  const loadHealth = useCallback(async () => {
    if (!recipient.value || loaded) return;
    setLoading(true);
    setError(null);

    const [healthRes, supRes] = await Promise.allSettled([
      fetchContactHealth(notification.channel, recipient.value),
      fetchContactSuppressions(notification.channel, recipient.value),
    ]);

    let healthOk = false;
    let supOk = false;
    const errors: string[] = [];

    if (healthRes.status === 'fulfilled' && healthRes.value.success) {
      setHealth(healthRes.value.data);
      healthOk = true;
    } else if (healthRes.status === 'fulfilled' && !healthRes.value.success) {
      errors.push(healthRes.value.error);
    } else if (healthRes.status === 'rejected') {
      errors.push('Failed to load contact health.');
    }

    if (supRes.status === 'fulfilled' && supRes.value.success) {
      setSuppressions(supRes.value.data);
      supOk = true;
    } else if (supRes.status === 'fulfilled' && !supRes.value.success) {
      errors.push(supRes.value.error);
    } else if (supRes.status === 'rejected') {
      errors.push('Failed to load suppression data.');
    }

    if (!healthOk && !supOk) {
      setError(errors[0] ?? 'Contact health data is not available.');
      setLoaded(false);
    } else {
      setLoaded(true);
    }

    setLoading(false);
  }, [notification.channel, recipient.value, loaded]);

  if (!recipient.value) return null;

  const healthStatusColor = health?.status === 'healthy' ? 'text-emerald-600'
    : health?.status === 'degraded' ? 'text-amber-600'
    : health?.status === 'unhealthy' ? 'text-red-600'
    : 'text-gray-600';

  return (
    <div className="bg-white rounded-lg border border-gray-200 p-5">
      <div className="flex items-center justify-between mb-3">
        <h3 className="text-sm font-semibold text-gray-700">Contact Health</h3>
        {(!loaded || error) && (
          <button
            type="button"
            onClick={loadHealth}
            disabled={loading}
            className="text-xs text-indigo-600 hover:text-indigo-500 font-medium disabled:opacity-50"
          >
            {loading ? 'Loading...' : error ? 'Retry' : 'Check Health'}
          </button>
        )}
      </div>

      <div className="text-sm text-gray-600 mb-2">
        <span className="text-xs font-semibold uppercase tracking-wide text-gray-400">Contact: </span>
        <span className="font-mono">{recipient.value}</span>
        <span className="text-gray-400 ml-2">({notification.channel})</span>
      </div>

      {error && (
        <p className="text-xs text-gray-400 mt-2">{error}</p>
      )}

      {health && (
        <div className="mt-3 space-y-2">
          <div className="flex items-center gap-3">
            <span className="text-xs font-semibold uppercase tracking-wide text-gray-400">Status:</span>
            <span className={`text-sm font-semibold capitalize ${healthStatusColor}`}>
              {health.status}
            </span>
            {health.isSuppressed && (
              <span className="inline-flex items-center px-2 py-0.5 rounded-full text-[10px] font-semibold uppercase bg-red-50 text-red-700 border border-red-200">
                Suppressed
              </span>
            )}
          </div>
          {(health.bounceCount > 0 || health.complaintCount > 0) && (
            <div className="flex items-center gap-4 text-xs text-gray-500">
              {health.bounceCount > 0 && <span>Bounces: {health.bounceCount}</span>}
              {health.complaintCount > 0 && <span>Complaints: {health.complaintCount}</span>}
            </div>
          )}
          {health.lastEvent && (
            <div className="text-xs text-gray-400">
              Last event: {health.lastEvent}
              {health.lastEventAt && ` (${new Date(health.lastEventAt).toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' })})`}
            </div>
          )}
          {health.isSuppressed && health.suppressionReason && (
            <div className="text-xs text-red-600 mt-1">
              Suppression reason: {SUPPRESSION_LABELS[health.suppressionReason.toLowerCase()] ?? health.suppressionReason}
            </div>
          )}
        </div>
      )}

      {suppressions.length > 0 && (
        <div className="mt-4 border-t border-gray-100 pt-3">
          <p className="text-xs font-semibold uppercase tracking-wide text-gray-400 mb-2">Active Suppressions</p>
          <div className="space-y-2">
            {suppressions.map(sup => (
              <div key={sup.id} className="bg-red-50 rounded border border-red-200 px-3 py-2">
                <div className="flex items-center gap-2">
                  <span className="text-xs font-semibold text-red-700">
                    {SUPPRESSION_LABELS[sup.reason.toLowerCase()] ?? sup.reason}
                  </span>
                  <span className="text-[10px] text-red-500">via {sup.source}</span>
                </div>
                {sup.detail && <p className="text-xs text-red-600 mt-0.5">{sup.detail}</p>}
                <p className="text-[10px] text-red-400 mt-1">
                  Since {new Date(sup.createdAt).toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' })}
                </p>
              </div>
            ))}
          </div>
        </div>
      )}
    </div>
  );
}

function ConfirmDialog({ open, title, description, confirmLabel, confirmVariant, onConfirm, onCancel, pending }: {
  open: boolean;
  title: string;
  description: string;
  confirmLabel: string;
  confirmVariant: 'danger' | 'primary';
  onConfirm: () => void;
  onCancel: () => void;
  pending: boolean;
}) {
  if (!open) return null;

  const confirmCls = confirmVariant === 'danger'
    ? 'bg-red-600 hover:bg-red-500 text-white'
    : 'bg-indigo-600 hover:bg-indigo-500 text-white';

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="bg-white rounded-xl shadow-xl max-w-md w-full mx-4 p-6">
        <h3 className="text-lg font-semibold text-gray-900">{title}</h3>
        <p className="mt-2 text-sm text-gray-600">{description}</p>
        <div className="mt-6 flex items-center justify-end gap-3">
          <button
            type="button"
            onClick={onCancel}
            disabled={pending}
            className="px-4 py-2 text-sm font-medium text-gray-700 bg-white border border-gray-300 rounded-lg hover:bg-gray-50 disabled:opacity-50"
          >
            Cancel
          </button>
          <button
            type="button"
            onClick={onConfirm}
            disabled={pending}
            className={`px-4 py-2 text-sm font-semibold rounded-lg disabled:opacity-50 ${confirmCls}`}
          >
            {pending ? 'Processing...' : confirmLabel}
          </button>
        </div>
      </div>
    </div>
  );
}

export default function DeliveryActionsClient({ notification }: { notification: NotifDetail }) {
  const router = useRouter();
  const eligibility = deriveEligibility(notification);

  const [confirmAction, setConfirmAction] = useState<'retry' | 'resend' | null>(null);
  const [isPending, startTransition] = useTransition();
  const [actionResult, setActionResult] = useState<{ type: 'success' | 'error'; message: string; newId?: string } | null>(null);

  const handleAction = useCallback(() => {
    if (!confirmAction) return;
    const action = confirmAction;
    startTransition(async () => {
      const result = action === 'retry'
        ? await retryNotification(notification.id)
        : await resendNotification(notification.id);

      if (result.success) {
        setActionResult({
          type: 'success',
          message: result.data.message || (action === 'retry' ? 'Retry submitted successfully.' : 'Resend created successfully.'),
          newId: result.data.newNotificationId,
        });
        router.refresh();
      } else {
        setActionResult({ type: 'error', message: result.error });
      }
      setConfirmAction(null);
    });
  }, [confirmAction, notification.id, router]);

  const isFailedOrBlocked = ['failed', 'blocked'].includes(notification.status.toLowerCase());

  return (
    <div className="space-y-4">
      <SuppressionAwarenessPanel notification={notification} />

      {isFailedOrBlocked && <ContactHealthCard notification={notification} />}

      <div className="bg-white rounded-lg border border-gray-200 p-5">
        <h3 className="text-sm font-semibold text-gray-700 mb-3">Delivery Actions</h3>

        {actionResult && (
          <div className={`mb-4 rounded-lg border px-4 py-3 text-sm ${
            actionResult.type === 'success'
              ? 'bg-emerald-50 border-emerald-200 text-emerald-700'
              : 'bg-red-50 border-red-200 text-red-700'
          }`}>
            <div className="flex items-start gap-2">
              <i className={actionResult.type === 'success' ? 'ri-check-line text-lg' : 'ri-error-warning-line text-lg'} />
              <div>
                <p>{actionResult.message}</p>
                {actionResult.newId && (
                  <Link
                    href={`/notifications/activity/${actionResult.newId}`}
                    className="mt-1 inline-flex items-center gap-1 text-xs font-medium text-emerald-800 hover:text-emerald-600"
                  >
                    View new notification <i className="ri-arrow-right-s-line" />
                  </Link>
                )}
              </div>
            </div>
          </div>
        )}

        {eligibility.eligible ? (
          <div className="space-y-3">
            <p className="text-xs text-gray-500">
              This notification can be {eligibility.actions.join(' or ')}d. Choose an action below.
            </p>
            <div className="flex items-center gap-3">
              {eligibility.actions.includes('retry') && (
                <button
                  type="button"
                  onClick={() => setConfirmAction('retry')}
                  disabled={isPending}
                  className="inline-flex items-center gap-1.5 px-4 py-2 text-sm font-semibold text-white bg-indigo-600 rounded-lg hover:bg-indigo-500 disabled:opacity-50 transition-colors"
                >
                  <i className="ri-refresh-line" />
                  Retry
                </button>
              )}
              {eligibility.actions.includes('resend') && (
                <button
                  type="button"
                  onClick={() => setConfirmAction('resend')}
                  disabled={isPending}
                  className="inline-flex items-center gap-1.5 px-4 py-2 text-sm font-semibold text-white bg-orange-600 rounded-lg hover:bg-orange-500 disabled:opacity-50 transition-colors"
                >
                  <i className="ri-send-plane-line" />
                  Resend
                </button>
              )}
            </div>
          </div>
        ) : (
          <div className="flex items-start gap-3 text-sm text-gray-500">
            <i className="ri-information-line text-gray-400 text-lg shrink-0 mt-0.5" />
            <p>{eligibility.reason}</p>
          </div>
        )}
      </div>

      <ConfirmDialog
        open={confirmAction === 'retry'}
        title="Retry Notification"
        description="This will attempt to resend this notification using the same content and recipient. The backend will validate whether the notification is still eligible for retry."
        confirmLabel="Retry Now"
        confirmVariant="primary"
        onConfirm={handleAction}
        onCancel={() => setConfirmAction(null)}
        pending={isPending}
      />

      <ConfirmDialog
        open={confirmAction === 'resend'}
        title="Resend Notification"
        description="This will create a new notification attempt to the same recipient with the same content. The backend will check whether the contact is still eligible to receive notifications."
        confirmLabel="Resend Now"
        confirmVariant="primary"
        onConfirm={handleAction}
        onCancel={() => setConfirmAction(null)}
        pending={isPending}
      />
    </div>
  );
}
