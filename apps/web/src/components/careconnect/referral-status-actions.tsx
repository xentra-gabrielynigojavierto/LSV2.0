'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { careConnectApi } from '@/lib/careconnect-api';
import { ApiError } from '@/lib/api-client';
import { useToast } from '@/lib/toast-context';
import { usePermission } from '@/hooks/use-permission';
import { PermissionCodes } from '@/lib/permission-codes';
import { ForbiddenBanner } from '@/components/ui/forbidden-banner';
import { PermissionTooltip } from '@/components/ui/permission-tooltip';
import { DisabledReasons } from '@/lib/disabled-reasons';
import type { ReferralDetail } from '@/types/careconnect';

interface ReferralStatusActionsProps {
  referral:   ReferralDetail;
  isReceiver: boolean;
  isReferrer: boolean;
}

const STATUS_LABELS: Record<string, string> = {
  Accepted:   'Referral accepted.',
  InProgress: 'Referral marked as in progress.',
  Declined:   'Referral declined.',
  Cancelled:  'Referral cancelled.',
};

/**
 * Inline status-action buttons for a referral detail page.
 *
 * Receiver (provider):
 *   - New / Received / Contacted → Accept | Decline (with optional notes)
 *   - Accepted                   → Mark In Progress | Decline
 *
 * Referrer (law firm):
 *   - Non-terminal statuses → Cancel (with confirmation)
 *
 * LSCC-01-001-01: InProgress is the canonical active state after Accepted.
 * Accepted → Completed is blocked; the receiver must explicitly mark In Progress first.
 * Appointment booking is decoupled from referral status and handled separately.
 *
 * LS-ID-TNT-015: Actions are permission-gated. Checks are UX-only — the
 * backend (LS-ID-TNT-012) remains authoritative for all protected operations.
 *
 * LS-ID-TNT-015-004: Partial permission scenario — when the user has the role
 * to perform an action but lacks the specific permission, the button is shown
 * as disabled with an explanatory tooltip instead of being hidden. The
 * ForbiddenBanner is retained only for the fully-blocked case (no permissions
 * at all for the current role + status context).
 *
 * Uses PUT /api/referrals/{id} which routes through ReferralWorkflowRules.
 * All actions show toast notifications on success or failure.
 */
export function ReferralStatusActions({ referral, isReceiver, isReferrer }: ReferralStatusActionsProps) {
  const router = useRouter();
  const { show: showToast } = useToast();

  // LS-ID-TNT-015: Permission checks (UX layer only; backend enforces authoritatively).
  // Fail-open when permissions array is empty (old token) — backend will deny if needed.
  const canAcceptPerm       = usePermission(PermissionCodes.CC.ReferralAccept);
  const canDeclinePerm      = usePermission(PermissionCodes.CC.ReferralDecline);
  const canCancelPerm       = usePermission(PermissionCodes.CC.ReferralCancel);
  const canUpdateStatusPerm = usePermission(PermissionCodes.CC.ReferralUpdateStatus);

  const [optimisticStatus, setOptimisticStatus] = useState<string | null>(null);
  const [loading, setLoading] = useState<string | null>(null);
  const [error,   setError]   = useState<string | null>(null);
  const [notes,   setNotes]   = useState('');

  // Confirm flows
  const [showDeclineNotes,  setShowDeclineNotes]  = useState(false);
  const [showCancelConfirm, setShowCancelConfirm] = useState(false);

  const currentStatus = optimisticStatus ?? referral.status;
  const isTerminal    = ['Completed', 'Cancelled', 'Declined'].includes(currentStatus);
  if (isTerminal) return null;

  async function doUpdate(toStatus: string, notesValue?: string) {
    setLoading(toStatus);
    setError(null);
    setOptimisticStatus(toStatus);

    try {
      await careConnectApi.referrals.update(referral.id, {
        requestedService: referral.requestedService,
        urgency:          referral.urgency,
        status:           toStatus,
        notes:            notesValue || undefined,
      });
      showToast(STATUS_LABELS[toStatus] ?? 'Referral updated.', 'success');
      router.refresh();
    } catch (err) {
      setOptimisticStatus(null);
      if (err instanceof ApiError) {
        if (err.isUnauthorized) { router.push('/login'); return; }
        if (err.isForbidden)    { setError('You do not have permission to update this referral.'); return; }
        setError(err.message);
      } else {
        setError('Failed to update referral status. Please try again.');
      }
      showToast('Failed to update referral.', 'error');
    } finally {
      setLoading(null);
    }
  }

  // ── Role + status gates (unchanged from LS-ID-TNT-015) ───────────────────────
  const roleCanAccept         = isReceiver && ['New', 'NewOpened', 'Received', 'Contacted'].includes(currentStatus);
  // LSCC-01-001-01: receiver can mark In Progress once referral is Accepted
  const roleCanMarkInProgress = isReceiver && currentStatus === 'Accepted';
  const roleCanDecline        = isReceiver && ['New', 'NewOpened', 'Received', 'Contacted', 'Accepted'].includes(currentStatus);
  const roleCanCancel         = (isReferrer || isReceiver) && !['Completed', 'Cancelled', 'Declined'].includes(currentStatus);

  // ── LS-ID-TNT-015: Permission gates layered on top of role + status gates ───
  const canAccept         = roleCanAccept         && canAcceptPerm;
  const canMarkInProgress = roleCanMarkInProgress && canUpdateStatusPerm;
  const canDecline        = roleCanDecline        && canDeclinePerm;
  const canCancel         = roleCanCancel         && canCancelPerm;

  // A user has no role-level access at all — don't render the panel.
  const hasAnyRoleAccess = roleCanAccept || roleCanMarkInProgress || roleCanDecline || roleCanCancel;
  if (!hasAnyRoleAccess) return null;

  // A user has the role but ALL permissions are absent — show a single clear
  // notice instead of a group of disabled buttons (LS-ID-TNT-015-004).
  const hasAnyPermAccess = canAccept || canMarkInProgress || canDecline || canCancel;
  if (!hasAnyPermAccess) {
    return (
      <div className="bg-white border border-gray-200 rounded-lg px-5 py-4 space-y-3">
        <h3 className="text-xs font-semibold text-gray-400 uppercase tracking-wider">Actions</h3>
        <ForbiddenBanner action="manage this referral" />
      </div>
    );
  }

  // ── Partial-permission: at least one perm exists — render all role-applicable
  // buttons, disabling those whose specific permission is missing with tooltip. ──
  const hasReceiverSection = roleCanAccept || roleCanMarkInProgress || roleCanDecline;

  return (
    <div className="bg-white border border-gray-200 rounded-lg px-5 py-4 space-y-3">
      <h3 className="text-xs font-semibold text-gray-400 uppercase tracking-wider">Actions</h3>

      {error && (
        <div className="bg-red-50 border border-red-200 rounded-md px-3 py-2 text-sm text-red-700">
          {error}
        </div>
      )}

      {/* Receiver: Accept / Mark In Progress / Decline */}
      {hasReceiverSection && (
        <div className="space-y-3">
          <div className="flex items-center gap-3 flex-wrap">
            {/* Accept — disabled-with-tooltip when perm missing (LS-ID-TNT-015-004) */}
            {roleCanAccept && !showDeclineNotes && (
              <PermissionTooltip
                show={!canAccept}
                message={DisabledReasons.noPermission('accept this referral').message}
              >
                <button
                  onClick={() => doUpdate('Accepted')}
                  disabled={!!loading || !canAccept}
                  className="bg-green-600 text-white text-sm font-medium px-4 py-2 rounded-md hover:bg-green-700 disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
                >
                  {loading === 'Accepted' ? 'Accepting…' : 'Accept Referral'}
                </button>
              </PermissionTooltip>
            )}

            {/* Mark In Progress — disabled-with-tooltip when perm missing */}
            {roleCanMarkInProgress && !showDeclineNotes && (
              <PermissionTooltip
                show={!canMarkInProgress}
                message={DisabledReasons.noPermission('update this referral status').message}
              >
                <button
                  onClick={() => doUpdate('InProgress')}
                  disabled={!!loading || !canMarkInProgress}
                  className="bg-amber-500 text-white text-sm font-medium px-4 py-2 rounded-md hover:bg-amber-600 disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
                >
                  {loading === 'InProgress' ? 'Updating…' : 'Mark In Progress'}
                </button>
              </PermissionTooltip>
            )}

            {/* Decline — disabled-with-tooltip when perm missing */}
            {roleCanDecline && !showDeclineNotes && (
              <PermissionTooltip
                show={!canDecline}
                message={DisabledReasons.noPermission('decline this referral').message}
              >
                <button
                  onClick={() => setShowDeclineNotes(true)}
                  disabled={!!loading || !canDecline}
                  className="border border-red-300 text-red-600 text-sm font-medium px-4 py-2 rounded-md hover:bg-red-50 disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
                >
                  Decline
                </button>
              </PermissionTooltip>
            )}
          </div>

          {/* Decline with optional notes — only reachable when canDecline is true */}
          {showDeclineNotes && (
            <div className="space-y-2 border border-red-100 rounded-md p-3 bg-red-50">
              <label className="block text-xs font-medium text-red-700">
                Reason for declining (optional)
              </label>
              <textarea
                value={notes}
                onChange={e => setNotes(e.target.value)}
                rows={2}
                placeholder="Let the referring party know why…"
                className="w-full border border-red-200 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-red-400 resize-none bg-white"
              />
              <div className="flex items-center gap-2">
                <button
                  onClick={() => doUpdate('Declined', notes)}
                  disabled={!!loading}
                  className="bg-red-600 text-white text-sm font-medium px-4 py-1.5 rounded-md hover:bg-red-700 disabled:opacity-60 transition-colors"
                >
                  {loading === 'Declined' ? 'Declining…' : 'Confirm Decline'}
                </button>
                <button
                  onClick={() => { setShowDeclineNotes(false); setNotes(''); }}
                  disabled={!!loading}
                  className="text-sm text-gray-500 hover:text-gray-800 transition-colors"
                >
                  Go back
                </button>
              </div>
            </div>
          )}
        </div>
      )}

      {/* Cancel — with inline confirmation; disabled-with-tooltip when perm missing */}
      {roleCanCancel && (
        <div className="pt-1 border-t border-gray-100">
          {!showCancelConfirm ? (
            <PermissionTooltip
              show={!canCancel}
              message={DisabledReasons.noPermission('cancel this referral').message}
            >
              <button
                onClick={() => { if (canCancel) setShowCancelConfirm(true); }}
                disabled={!!loading || !canCancel}
                className="text-sm text-gray-500 hover:text-gray-800 transition-colors disabled:opacity-40 disabled:cursor-not-allowed"
              >
                Cancel Referral
              </button>
            </PermissionTooltip>
          ) : (
            <div className="space-y-2 border border-gray-200 rounded-md p-3 bg-gray-50">
              <p className="text-sm font-medium text-gray-800">
                Cancel this referral?
              </p>
              <div className="flex items-center gap-2">
                <button
                  onClick={() => doUpdate('Cancelled')}
                  disabled={!!loading}
                  className="bg-gray-700 text-white text-sm font-medium px-4 py-1.5 rounded-md hover:bg-gray-900 disabled:opacity-60 transition-colors"
                >
                  {loading === 'Cancelled' ? 'Cancelling…' : 'Yes, Cancel'}
                </button>
                <button
                  onClick={() => setShowCancelConfirm(false)}
                  disabled={!!loading}
                  className="text-sm text-gray-500 hover:text-gray-800 transition-colors"
                >
                  Keep Referral
                </button>
              </div>
            </div>
          )}
        </div>
      )}
    </div>
  );
}
