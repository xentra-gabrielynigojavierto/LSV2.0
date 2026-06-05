'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { careConnectApi } from '@/lib/careconnect-api';
import { ApiError } from '@/lib/api-client';
import { useToast } from '@/lib/toast-context';
import { usePermission } from '@/hooks/use-permission';
import { PermissionCodes } from '@/lib/permission-codes';
import { PermissionTooltip } from '@/components/ui/permission-tooltip';
import { DisabledReasons } from '@/lib/disabled-reasons';
import type { AppointmentDetail } from '@/types/careconnect';

interface AppointmentCancelButtonProps {
  appointment: AppointmentDetail;
}

/**
 * Cancel button for appointment detail page.
 *
 * Calls POST /api/appointments/{id}/cancel.
 * Shows a confirmation dialog with an optional notes field.
 * Shows a toast notification on success or failure.
 * Only rendered for non-terminal statuses.
 *
 * LS-ID-TNT-015-001: Permission gate added — CC.AppointmentUpdate controls
 * whether the cancel button is interactive. The button is shown as
 * disabled-with-tooltip when the role qualifies but the permission is absent,
 * so the user understands the feature exists and who to contact.
 */
export function AppointmentCancelButton({ appointment }: AppointmentCancelButtonProps) {
  const router = useRouter();
  const { show: showToast } = useToast();

  // LS-ID-TNT-015-001: Permission check (UX layer only; backend enforces authoritatively).
  const canCancelPerm = usePermission(PermissionCodes.CC.AppointmentUpdate);

  const isTerminal = ['Cancelled', 'Completed', 'NoShow'].includes(appointment.status);
  const [confirming, setConfirming] = useState(false);
  const [notes,      setNotes]      = useState('');
  const [loading,    setLoading]    = useState(false);
  const [error,      setError]      = useState<string | null>(null);

  if (isTerminal) return null;

  async function handleCancel() {
    setLoading(true);
    setError(null);
    try {
      await careConnectApi.appointments.cancel(appointment.id, { notes: notes.trim() || undefined });
      showToast('Appointment cancelled.', 'success');
      router.refresh();
    } catch (err) {
      if (err instanceof ApiError) {
        if (err.isUnauthorized) { router.push('/login'); return; }
        if (err.isForbidden)    { setError('You do not have permission to cancel this appointment.'); return; }
        setError(err.message);
      } else {
        setError('Failed to cancel the appointment. Please try again.');
      }
      showToast('Failed to cancel appointment.', 'error');
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="bg-white border border-gray-200 rounded-lg px-5 py-4 space-y-3">
      <h3 className="text-xs font-semibold text-gray-400 uppercase tracking-wider">Cancel</h3>

      {error && (
        <div className="bg-red-50 border border-red-200 rounded-md px-3 py-2 text-sm text-red-700">
          {error}
        </div>
      )}

      {/*
        LS-ID-TNT-015-001: Disable-with-tooltip when permission is absent.
        The cancel section is always visible (not hidden) so the user
        understands cancellation is a feature and knows to contact their admin.
      */}
      {!confirming ? (
        <PermissionTooltip
          show={!canCancelPerm}
          message={DisabledReasons.noPermission('cancel this appointment').message}
        >
          <button
            onClick={() => { if (canCancelPerm) setConfirming(true); }}
            disabled={!canCancelPerm}
            className="border border-red-300 text-red-600 text-sm font-medium px-4 py-2 rounded-md hover:bg-red-50 disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
          >
            Cancel Appointment
          </button>
        </PermissionTooltip>
      ) : (
        <div className="space-y-3 border border-red-100 rounded-md p-3 bg-red-50">
          <p className="text-sm font-medium text-red-800">
            Are you sure you want to cancel this appointment?
          </p>
          <div>
            <label className="block text-xs font-medium text-red-700 mb-1">
              Reason (optional)
            </label>
            <textarea
              value={notes}
              onChange={e => setNotes(e.target.value)}
              rows={2}
              placeholder="Provide a reason for cancellation…"
              className="w-full border border-red-200 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-red-400 resize-none bg-white"
            />
          </div>
          <div className="flex items-center gap-3">
            <button
              onClick={handleCancel}
              disabled={loading}
              className="bg-red-600 text-white text-sm font-medium px-4 py-1.5 rounded-md hover:bg-red-700 disabled:opacity-60 transition-colors"
            >
              {loading ? 'Cancelling…' : 'Yes, Cancel'}
            </button>
            <button
              onClick={() => { setConfirming(false); setNotes(''); setError(null); }}
              disabled={loading}
              className="text-sm text-gray-500 hover:text-gray-800 transition-colors"
            >
              Keep Appointment
            </button>
          </div>
        </div>
      )}
    </div>
  );
}
