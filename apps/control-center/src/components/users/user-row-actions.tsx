'use client';

import { useState }              from 'react';
import { useRouter }             from 'next/navigation';
import Link                      from 'next/link';
import type { UserStatus }       from '@/types/control-center';
import { Routes }                from '@/lib/routes';
import { SetPasswordModal }      from './set-password-modal';

interface UserRowActionsProps {
  userId:        string;
  userName:      string;
  userEmail:     string;
  currentStatus: UserStatus;
}

type RowAction = 'activate' | 'deactivate' | 'resend-invite' | 'cancel-invite';

export function UserRowActions({ userId, userName, userEmail, currentStatus }: UserRowActionsProps) {
  const router = useRouter();

  const [pending,    setPending]    = useState<RowAction | null>(null);
  const [confirming, setConfirming] = useState<'deactivate' | 'cancel-invite' | null>(null);
  const [error,      setError]      = useState<string | null>(null);
  const [showSetPassword, setShowSetPassword] = useState(false);

  const isActive   = currentStatus === 'Active';
  const isInactive = currentStatus === 'Inactive';
  const isInvited  = currentStatus === 'Invited';

  async function run(action: RowAction) {
    setError(null);
    setPending(action);
    try {
      const res = await fetch(
        `/api/identity/admin/users/${encodeURIComponent(userId)}/${action}`,
        { method: 'POST' },
      );
      if (!res.ok) {
        const body = await res.json().catch(() => ({})) as { message?: string };
        throw new Error(body.message ?? 'Action failed.');
      }
      router.refresh();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'An error occurred.');
    } finally {
      setPending(null);
      setConfirming(null);
    }
  }

  return (
    <div className="flex items-center gap-1.5 flex-wrap">

      {/* View detail */}
      <Link
        href={Routes.userDetail(userId)}
        className="text-xs px-2 py-1 rounded border border-gray-200 bg-white text-gray-600 hover:bg-gray-50 hover:text-gray-900 transition-colors whitespace-nowrap"
      >
        View
      </Link>

      {/* Activate — shown for Inactive or Invited */}
      {(isInactive || isInvited) && (
        <ActionButton
          label="Activate"
          isPending={pending === 'activate'}
          variant="success"
          onClick={() => run('activate')}
        />
      )}

      {/* Deactivate — shown for Active; requires confirmation */}
      {isActive && confirming !== 'deactivate' && (
        <ActionButton
          label="Deactivate"
          isPending={false}
          variant="danger"
          onClick={() => setConfirming('deactivate')}
        />
      )}

      {/* Inline confirm prompt for Deactivate */}
      {isActive && confirming === 'deactivate' && (
        <span className="inline-flex items-center gap-1 text-xs">
          <span className="text-red-700 font-medium">Deactivate?</span>
          <button
            type="button"
            onClick={() => run('deactivate')}
            disabled={pending === 'deactivate'}
            className="px-2 py-0.5 rounded bg-red-600 text-white text-[11px] font-medium hover:bg-red-700 disabled:opacity-50 transition-colors"
          >
            {pending === 'deactivate' ? '…' : 'Yes'}
          </button>
          <button
            type="button"
            onClick={() => setConfirming(null)}
            className="px-2 py-0.5 rounded border border-gray-200 bg-white text-gray-500 text-[11px] hover:bg-gray-50 transition-colors"
          >
            No
          </button>
        </span>
      )}

      {/* Set Password */}
      {!isInvited && (
        <ActionButton
          label="Set Password"
          isPending={false}
          variant="neutral"
          onClick={() => setShowSetPassword(true)}
        />
      )}

      {/* Resend Invite — only for Invited status */}
      {isInvited && (
        <ActionButton
          label="Resend"
          isPending={pending === 'resend-invite'}
          variant="neutral"
          onClick={() => run('resend-invite')}
        />
      )}

      {/* Cancel Invite — only for Invited status; requires confirmation */}
      {isInvited && confirming !== 'cancel-invite' && (
        <ActionButton
          label="Cancel Invite"
          isPending={false}
          variant="danger"
          onClick={() => setConfirming('cancel-invite')}
        />
      )}

      {/* Inline confirm prompt for Cancel Invite */}
      {isInvited && confirming === 'cancel-invite' && (
        <span className="inline-flex items-center gap-1 text-xs">
          <span className="text-red-700 font-medium">Cancel invite?</span>
          <button
            type="button"
            onClick={() => run('cancel-invite')}
            disabled={pending === 'cancel-invite'}
            className="px-2 py-0.5 rounded bg-red-600 text-white text-[11px] font-medium hover:bg-red-700 disabled:opacity-50 transition-colors"
          >
            {pending === 'cancel-invite' ? '…' : 'Yes'}
          </button>
          <button
            type="button"
            onClick={() => setConfirming(null)}
            className="px-2 py-0.5 rounded border border-gray-200 bg-white text-gray-500 text-[11px] hover:bg-gray-50 transition-colors"
          >
            No
          </button>
        </span>
      )}

      {/* Inline error */}
      {error && (
        <span className="text-[11px] text-red-600" title={error}>
          ⚠ {error.length > 30 ? error.slice(0, 28) + '…' : error}
        </span>
      )}

      {showSetPassword && (
        <SetPasswordModal
          userId={userId}
          userName={userName}
          userEmail={userEmail}
          onClose={() => setShowSetPassword(false)}
        />
      )}
    </div>
  );
}

// ── Internal helpers ──────────────────────────────────────────────────────────

type Variant = 'success' | 'danger' | 'neutral';

const variantClass: Record<Variant, string> = {
  success: 'border-green-200 bg-white text-green-700 hover:bg-green-50',
  danger:  'border-red-200 bg-white text-red-600 hover:bg-red-50',
  neutral: 'border-gray-200 bg-white text-gray-600 hover:bg-gray-50',
};

function ActionButton({
  label,
  isPending,
  variant,
  onClick,
}: {
  label:     string;
  isPending: boolean;
  variant:   Variant;
  onClick:   () => void;
}) {
  return (
    <button
      type="button"
      disabled={isPending}
      onClick={onClick}
      className={`text-xs px-2 py-1 rounded border transition-colors disabled:opacity-40 disabled:cursor-not-allowed whitespace-nowrap ${variantClass[variant]}`}
    >
      {isPending ? `${label}…` : label}
    </button>
  );
}
