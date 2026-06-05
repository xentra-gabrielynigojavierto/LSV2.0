'use client';

/**
 * UserActions — user account control buttons with confirmation dialogs.
 *
 * Groups:
 *   1. Account state — Activate / Deactivate
 *   2. Security      — Lock / Unlock, Reset Password, Force Logout
 *   3. Invite        — Resend Invite, Cancel Invite (only when status === 'Invited')
 *
 * UIX-003-03: lock, unlock, reset-password, force-logout are now fully wired
 * to real BFF proxy routes (no more simulateAction stubs for these actions).
 */

import { useState }           from 'react';
import { useRouter }          from 'next/navigation';
import type { UserStatus }    from '@/types/control-center';
import { ConfirmDialog }      from '@/components/ui/confirm-dialog';
import { track }              from '@/lib/analytics';

interface UserActionsProps {
  userId:        string;
  currentStatus: UserStatus;
  isLocked?:     boolean;
}

type UserAction =
  | 'activate'
  | 'deactivate'
  | 'lock'
  | 'unlock'
  | 'reset-password'
  | 'force-logout'
  | 'resend-invite'
  | 'cancel-invite';

interface FeedbackState {
  type:    'success' | 'error';
  message: string;
}

const ACTION_LABELS: Record<UserAction, string> = {
  'activate':      'Activated',
  'deactivate':    'Deactivated',
  'lock':          'Account locked. All sessions revoked.',
  'unlock':        'Account unlocked',
  'reset-password':'Password reset email sent',
  'force-logout':  'All sessions signed out',
  'resend-invite': 'Invitation resent',
  'cancel-invite': 'Invitation cancelled',
};

export function UserActions({ userId, currentStatus, isLocked = false }: UserActionsProps) {
  const router = useRouter();

  const [confirming, setConfirming] = useState<UserAction | null>(null);
  const [pending, setPending]       = useState<UserAction | null>(null);
  const [feedback, setFeedback]     = useState<FeedbackState | null>(null);

  const isActive   = currentStatus === 'Active';
  const isInactive = currentStatus === 'Inactive';
  const isInvited  = currentStatus === 'Invited';

  function showFeedback(type: 'success' | 'error', message: string) {
    setFeedback({ type, message });
    setTimeout(() => setFeedback(null), 3500);
  }

  async function executeAction(action: UserAction) {
    setConfirming(null);
    setPending(action);

    try {
      const res = await fetch(
        `/api/identity/admin/users/${encodeURIComponent(userId)}/${action}`,
        { method: 'POST' },
      );
      if (!res.ok) {
        const body = await res.json().catch(() => ({})) as { message?: string };
        throw new Error(body.message ?? 'Action failed');
      }
      router.refresh();

      const trackEvent: Record<UserAction, string> = {
        'activate':      'user.status.change',
        'deactivate':    'user.status.change',
        'lock':          'user.lock',
        'unlock':        'user.unlock',
        'reset-password':'user.password.reset',
        'force-logout':  'user.force.logout',
        'resend-invite': 'user.invite.resend',
        'cancel-invite': 'user.invite.cancel',
      };
      track(trackEvent[action] as import('@/lib/analytics').TrackEvent, { userId, action });
      showFeedback('success', `${ACTION_LABELS[action]}.`);
    } catch (err) {
      const msg = err instanceof Error ? err.message : `Failed to ${action.replace(/-/g, ' ')}.`;
      showFeedback('error', `${msg} Please try again.`);
    } finally {
      setPending(null);
    }
  }

  const isPendingAny = pending !== null;

  return (
    <div>
      <div className="flex flex-wrap items-center gap-2">

        {/* ── Account state ────────────────────────────────────────────── */}
        <ActionButton
          label="Activate"
          variant="success"
          disabled={isActive || isInvited || isPendingAny}
          isPending={pending === 'activate'}
          aria-label={isActive ? 'User is already active' : isInvited ? 'Accept the invitation first' : 'Activate this user'}
          title={isActive ? 'User is already active' : isInvited ? 'Accept the invitation first' : 'Activate this user'}
          onClick={() => executeAction('activate')}
        />
        <ActionButton
          label="Deactivate"
          variant="neutral"
          disabled={isInactive || isPendingAny}
          isPending={pending === 'deactivate'}
          aria-label={isInactive ? 'User is already inactive' : 'Deactivate this user'}
          title={isInactive ? 'User is already inactive' : 'Deactivate this user'}
          onClick={() => setConfirming('deactivate')}
        />

        {/* ── Divider ──────────────────────────────────────────────────── */}
        <div className="w-px h-5 bg-gray-200" aria-hidden />

        {/* ── Security ─────────────────────────────────────────────────── */}
        {isLocked ? (
          <ActionButton
            label="Unlock"
            variant="neutral"
            disabled={isPendingAny}
            isPending={pending === 'unlock'}
            aria-label="Unlock this user account"
            title="Unlock this user account"
            onClick={() => executeAction('unlock')}
          />
        ) : (
          <ActionButton
            label="Lock"
            variant="danger"
            disabled={isInvited || isPendingAny}
            isPending={pending === 'lock'}
            aria-label={isInvited ? 'Cannot lock a pending invitation' : 'Lock this user account'}
            title={isInvited ? 'Cannot lock a pending invitation' : 'Lock this user account'}
            onClick={() => setConfirming('lock')}
          />
        )}

        <ActionButton
          label="Reset Password"
          variant="neutral"
          disabled={isInvited || isPendingAny}
          isPending={pending === 'reset-password'}
          aria-label={isInvited ? 'User has not set a password yet' : 'Send a password reset email'}
          title={isInvited ? 'User has not set a password yet' : 'Trigger an admin password reset'}
          onClick={() => executeAction('reset-password')}
        />

        <ActionButton
          label="Force Logout"
          variant="danger"
          disabled={isInvited || isPendingAny}
          isPending={pending === 'force-logout'}
          aria-label={isInvited ? 'User has no active sessions' : 'Sign out all active sessions'}
          title={isInvited ? 'User has no active sessions' : 'Revoke all active sessions immediately'}
          onClick={() => setConfirming('force-logout')}
        />

        {/* ── Invite ───────────────────────────────────────────────────── */}
        {isInvited && (
          <>
            <div className="w-px h-5 bg-gray-200" aria-hidden />
            <ActionButton
              label="Resend Invite"
              variant="primary"
              disabled={isPendingAny}
              isPending={pending === 'resend-invite'}
              aria-label="Resend the invitation email"
              title="Resend the invitation email"
              onClick={() => executeAction('resend-invite')}
            />
            <ActionButton
              label="Cancel Invite"
              variant="danger"
              disabled={isPendingAny}
              isPending={pending === 'cancel-invite'}
              aria-label="Cancel the pending invitation"
              title="Revoke the pending invitation — user status will change to Inactive"
              onClick={() => setConfirming('cancel-invite')}
            />
          </>
        )}

      </div>

      {/* Inline feedback */}
      {feedback && (
        <p
          role="status"
          aria-live="polite"
          className={`mt-2 text-xs font-medium ${
            feedback.type === 'success' ? 'text-green-700' : 'text-red-600'
          }`}
        >
          {feedback.message}
        </p>
      )}

      {/* Deactivate confirmation */}
      {confirming === 'deactivate' && (
        <ConfirmDialog
          title="Deactivate this user?"
          description="The user will immediately lose access to the platform. You can reactivate the account at any time."
          confirmLabel="Deactivate"
          variant="warning"
          isPending={pending === 'deactivate'}
          onConfirm={() => executeAction('deactivate')}
          onCancel={() => setConfirming(null)}
        />
      )}

      {/* Lock confirmation */}
      {confirming === 'lock' && (
        <ConfirmDialog
          title="Lock this user account?"
          description="The user will be signed out immediately and blocked from signing in. All active sessions will be terminated. You can unlock the account at any time."
          confirmLabel="Lock Account"
          variant="danger"
          isPending={pending === 'lock'}
          onConfirm={() => executeAction('lock')}
          onCancel={() => setConfirming(null)}
        />
      )}

      {/* Force logout confirmation */}
      {confirming === 'force-logout' && (
        <ConfirmDialog
          title="Force sign out this user?"
          description="All of this user's active sessions will be immediately revoked. They will need to sign in again. This does not affect their account status or login ability."
          confirmLabel="Force Sign Out"
          variant="warning"
          isPending={pending === 'force-logout'}
          onConfirm={() => executeAction('force-logout')}
          onCancel={() => setConfirming(null)}
        />
      )}

      {/* Cancel invite confirmation */}
      {confirming === 'cancel-invite' && (
        <ConfirmDialog
          title="Cancel this invitation?"
          description="The invitation link will be revoked immediately. The user account will remain but their status will change to Inactive. You can send a new invitation at any time."
          confirmLabel="Cancel Invitation"
          variant="warning"
          isPending={pending === 'cancel-invite'}
          onConfirm={() => executeAction('cancel-invite')}
          onCancel={() => setConfirming(null)}
        />
      )}
    </div>
  );
}

// ── Internal helpers ──────────────────────────────────────────────────────────

type ButtonVariant = 'primary' | 'success' | 'neutral' | 'danger';

function ActionButton({
  label,
  variant,
  disabled,
  isPending,
  title,
  onClick,
  'aria-label': ariaLabel,
}: {
  label:      string;
  variant:    ButtonVariant;
  disabled:   boolean;
  isPending:  boolean;
  title:      string;
  onClick:    () => void;
  'aria-label'?: string;
}) {
  const base = [
    'text-sm font-medium px-3 py-1.5 rounded-md border transition-colors',
    'disabled:opacity-40 disabled:cursor-not-allowed',
    'focus:outline-none focus-visible:ring-2 focus-visible:ring-offset-1',
  ].join(' ');

  const styles: Record<ButtonVariant, string> = {
    primary: 'bg-indigo-600 text-white border-indigo-600 hover:bg-indigo-700 focus-visible:ring-indigo-500',
    success: 'bg-green-600 text-white border-green-600 hover:bg-green-700 focus-visible:ring-green-500',
    neutral: 'bg-white text-gray-700 border-gray-200 hover:bg-gray-50 focus-visible:ring-gray-400',
    danger:  'bg-white text-red-600 border-red-200 hover:bg-red-50 focus-visible:ring-red-400',
  };

  return (
    <button
      type="button"
      disabled={disabled || isPending}
      title={title}
      aria-label={ariaLabel ?? label}
      aria-busy={isPending}
      onClick={onClick}
      className={`${base} ${styles[variant]}`}
    >
      {isPending ? (
        <span className="flex items-center gap-1.5">
          <span
            aria-hidden="true"
            className="h-3.5 w-3.5 rounded-full border-2 border-current/40 border-t-transparent animate-spin"
          />
          {label}…
        </span>
      ) : (
        label
      )}
    </button>
  );
}
