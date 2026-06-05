'use client';

/**
 * TenantActions — tenant status control buttons with confirmation dialogs.
 *
 * Renders Activate, Deactivate, and Suspend action buttons for a tenant.
 * Destructive and state-changing actions (Deactivate, Suspend) show a
 * confirmation dialog before proceeding.
 *
 * ── UX improvements over the previous version ────────────────────────────────
 *
 *   - Destructive actions (Deactivate, Suspend) prompt a ConfirmDialog
 *     before executing, preventing accidental status changes.
 *   - Pending state: each button shows a spinner while its action is in flight.
 *   - Success / error toast-style inline feedback appears for 3 seconds.
 *   - Accessibility: all buttons have aria-label, focus-visible rings,
 *     and the confirm dialog is properly keyboard-navigable (Escape to cancel,
 *     Tab to move between Cancel and Confirm).
 *
 * ── Wiring guide ─────────────────────────────────────────────────────────────
 *
 *   When the backend endpoints are ready:
 *   1. Create a BFF proxy at app/api/identity/admin/tenants/[id]/[action]/route.ts
 *   2. In each handler below, replace the `await simulateAction()` stub with:
 *        const res = await fetch(`/api/identity/api/admin/tenants/${tenantId}/${action}`, { method: 'POST' });
 *        if (!res.ok) throw new Error('Action failed');
 *   3. Call router.refresh() after success to re-fetch the Server Component.
 *
 * TODO: POST /identity/api/admin/tenants/{tenantId}/activate
 * TODO: POST /identity/api/admin/tenants/{tenantId}/deactivate
 * TODO: POST /identity/api/admin/tenants/{tenantId}/suspend
 */

import { useState }          from 'react';
import type { TenantStatus } from '@/types/control-center';
import { ConfirmDialog }     from '@/components/ui/confirm-dialog';
import { track }             from '@/lib/analytics';

interface TenantActionsProps {
  tenantId:      string;
  currentStatus: TenantStatus;
}

type TenantAction = 'activate' | 'deactivate' | 'suspend';

interface FeedbackState {
  type:    'success' | 'error';
  message: string;
}

export function TenantActions({ tenantId, currentStatus }: TenantActionsProps) {
  const [confirming, setConfirming]   = useState<TenantAction | null>(null);
  const [pending, setPending]         = useState<TenantAction | null>(null);
  const [feedback, setFeedback]       = useState<FeedbackState | null>(null);

  const isActive    = currentStatus === 'Active';
  const isInactive  = currentStatus === 'Inactive';
  const isSuspended = currentStatus === 'Suspended';

  function showFeedback(type: 'success' | 'error', message: string) {
    setFeedback({ type, message });
    setTimeout(() => setFeedback(null), 3500);
  }

  async function executeAction(action: TenantAction) {
    setConfirming(null);
    setPending(action);

    try {
      // TODO: replace with real BFF proxy call
      // const res = await fetch(`/api/identity/api/admin/tenants/${tenantId}/${action}`, { method: 'POST' });
      // if (!res.ok) throw new Error('Action failed');
      await simulateAction(action); // stub — remove when wired

      track('tenant.status.change', { tenantId, action });
      showFeedback('success', `Tenant ${action}d successfully.`);
    } catch {
      showFeedback('error', `Failed to ${action} tenant. Please try again.`);
    } finally {
      setPending(null);
    }
  }

  const isPendingAny = pending !== null;

  return (
    <div>
      <div className="flex items-center gap-2 shrink-0">

        {/* Activate */}
        <ActionButton
          label="Activate"
          variant="success"
          disabled={isActive || isPendingAny}
          isPending={pending === 'activate'}
          aria-label={isActive ? 'Tenant is already active' : 'Activate this tenant'}
          title={isActive ? 'Tenant is already active' : 'Activate this tenant'}
          onClick={() => executeAction('activate')}
        />

        {/* Deactivate */}
        <ActionButton
          label="Deactivate"
          variant="neutral"
          disabled={isInactive || isPendingAny}
          isPending={pending === 'deactivate'}
          aria-label={isInactive ? 'Tenant is already inactive' : 'Deactivate this tenant'}
          title={isInactive ? 'Tenant is already inactive' : 'Deactivate this tenant'}
          onClick={() => setConfirming('deactivate')}
        />

        {/* Suspend */}
        <ActionButton
          label="Suspend"
          variant="danger"
          disabled={isSuspended || isPendingAny}
          isPending={pending === 'suspend'}
          aria-label={isSuspended ? 'Tenant is already suspended' : 'Suspend this tenant'}
          title={isSuspended ? 'Tenant is already suspended' : 'Suspend this tenant'}
          onClick={() => setConfirming('suspend')}
        />

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
          title="Deactivate this tenant?"
          description="The tenant's users will lose access to the platform until the tenant is reactivated."
          confirmLabel="Deactivate"
          variant="warning"
          isPending={pending === 'deactivate'}
          onConfirm={() => executeAction('deactivate')}
          onCancel={() => setConfirming(null)}
        />
      )}

      {/* Suspend confirmation */}
      {confirming === 'suspend' && (
        <ConfirmDialog
          title="Suspend this tenant?"
          description="Suspension immediately locks all users out of the platform. Only a platform admin can unsuspend the tenant."
          confirmLabel="Suspend"
          variant="danger"
          isPending={pending === 'suspend'}
          onConfirm={() => executeAction('suspend')}
          onCancel={() => setConfirming(null)}
        />
      )}
    </div>
  );
}

// ── Internal helpers ──────────────────────────────────────────────────────────

type ButtonVariant = 'success' | 'neutral' | 'danger';

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

/**
 * simulateAction — stub that resolves after 800ms.
 * Remove once real BFF proxy routes are wired.
 * TODO: delete when POST /identity/api/admin/tenants/{id}/{action} is live.
 */
async function simulateAction(_action: string): Promise<void> {
  return new Promise(resolve => setTimeout(resolve, 800));
}
