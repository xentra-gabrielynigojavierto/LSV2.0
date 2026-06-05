'use client';

import { useCallback, useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';
import { careConnectApi } from '@/lib/careconnect-api';
import { ApiError } from '@/lib/api-client';
import { useToast } from '@/lib/toast-context';
import { usePermission } from '@/hooks/use-permission';
import { PermissionCodes } from '@/lib/permission-codes';
import { ForbiddenBanner } from '@/components/ui/forbidden-banner';
import { PermissionTooltip } from '@/components/ui/permission-tooltip';
import { DisabledReasons } from '@/lib/disabled-reasons';
import { AvailabilityList } from './availability-list';
import type { AppointmentDetail, AvailabilitySlot, ProviderAvailabilityResponse } from '@/types/careconnect';

interface AppointmentActionsProps {
  appointment: AppointmentDetail;
  isReceiver:  boolean;
  isReferrer:  boolean;
}

function isoDate(d: Date): string {
  return d.toLocaleDateString('en-CA');
}

function addDays(d: Date, n: number): Date {
  const r = new Date(d);
  r.setDate(r.getDate() + n);
  return r;
}

function formatDateTime(iso: string): string {
  return new Date(iso).toLocaleString('en-US', {
    weekday: 'short', month: 'short', day: 'numeric',
    hour: 'numeric', minute: '2-digit', hour12: true,
  });
}

/**
 * Appointment action buttons for the detail page.
 *
 * Receiver (provider) only — actions available by status:
 *   - Confirm         → POST /api/appointments/{id}/confirm
 *   - Mark Completed  → POST /api/appointments/{id}/complete
 *   - Mark NoShow     → PUT  /api/appointments/{id}   { status: 'NoShow' }
 *   - Reschedule      → POST /api/appointments/{id}/reschedule { newAppointmentSlotId, notes? }
 *
 * Cancel is handled separately by AppointmentCancelButton.
 *
 * LS-ID-TNT-015-001: Permission gates layered on top of role + status gates:
 *   - Confirm / Complete / NoShow  → CC.AppointmentUpdate
 *   - Reschedule                   → CC.AppointmentManage (higher-privilege operation)
 *
 * Partial-permission scenario: when the receiver has status access but only
 * some permissions are held, blocked actions are shown as disabled-with-tooltip.
 * ForbiddenBanner is shown only when ALL action permissions are absent.
 */
export function AppointmentActions({ appointment, isReceiver, isReferrer: _isReferrer }: AppointmentActionsProps) {
  const router = useRouter();
  const { show: showToast } = useToast();

  // LS-ID-TNT-015-001: Permission checks (UX layer only; backend enforces authoritatively).
  const canApptUpdatePerm = usePermission(PermissionCodes.CC.AppointmentUpdate);
  const canApptManagePerm = usePermission(PermissionCodes.CC.AppointmentManage);

  const status     = appointment.status;
  const providerId = appointment.providerId;

  const isTerminal = ['Cancelled', 'Completed', 'NoShow'].includes(status);

  // ── Role + status gates ───────────────────────────────────────────────────
  const roleCanConfirm    = isReceiver && ['Scheduled', 'Pending', 'Rescheduled'].includes(status);
  const roleCanComplete   = isReceiver && status === 'Confirmed';
  const roleCanNoShow     = isReceiver && status === 'Confirmed';
  const roleCanReschedule = isReceiver && ['Scheduled', 'Pending', 'Confirmed'].includes(status);

  // ── Permission gates ──────────────────────────────────────────────────────
  const canConfirm    = roleCanConfirm    && canApptUpdatePerm;
  const canComplete   = roleCanComplete   && canApptUpdatePerm;
  const canNoShow     = roleCanNoShow     && canApptUpdatePerm;
  const canReschedule = roleCanReschedule && canApptManagePerm;

  const hasAnyRoleAccess = roleCanConfirm || roleCanComplete || roleCanNoShow || roleCanReschedule;
  const hasAnyPermAccess = canConfirm     || canComplete     || canNoShow     || canReschedule;

  const [loading,  setLoading]  = useState<string | null>(null);
  const [error,    setError]    = useState<string | null>(null);

  // ── Reschedule modal state ─────────────────────────────────────────────────
  const [showReschedule, setShowReschedule] = useState(false);
  const [availability,   setAvailability]  = useState<ProviderAvailabilityResponse | null>(null);
  const [slotsLoading,   setSlotsLoading]  = useState(false);
  const [slotsError,     setSlotsError]    = useState<string | null>(null);
  const [selectedSlot,   setSelectedSlot]  = useState<AvailabilitySlot | null>(null);
  const [rescheduleNotes, setRescheduleNotes] = useState('');
  const today = new Date();

  const loadSlots = useCallback(async () => {
    setSlotsLoading(true);
    setSlotsError(null);
    try {
      const { data } = await careConnectApi.providers.getAvailability(providerId, {
        from: isoDate(today),
        to:   isoDate(addDays(today, 13)),
      });
      setAvailability(data);
    } catch {
      setSlotsError('Could not load availability. Please try again.');
    } finally {
      setSlotsLoading(false);
    }
  }, [providerId]);

  useEffect(() => {
    if (showReschedule && !availability) {
      loadSlots();
    }
  }, [showReschedule, availability, loadSlots]);

  if (isTerminal) return null;
  if (!hasAnyRoleAccess) return null;

  // ── Mutation helpers ───────────────────────────────────────────────────────
  async function doConfirm() {
    setLoading('Confirmed');
    setError(null);
    try {
      await careConnectApi.appointments.confirm(appointment.id);
      showToast('Appointment confirmed.', 'success');
      router.refresh();
    } catch (err) {
      if (err instanceof ApiError) {
        if (err.isUnauthorized) { router.push('/login'); return; }
        if (err.isForbidden)    { setError('You do not have permission to confirm this appointment.'); return; }
        if (err.isConflict)     { setError('This appointment cannot be confirmed in its current state.'); return; }
        setError(err.message);
      } else {
        setError('Failed to confirm appointment.');
      }
      showToast('Failed to confirm appointment.', 'error');
    } finally {
      setLoading(null);
    }
  }

  async function doComplete() {
    setLoading('Completed');
    setError(null);
    try {
      await careConnectApi.appointments.complete(appointment.id);
      showToast('Appointment marked as completed.', 'success');
      router.refresh();
    } catch (err) {
      if (err instanceof ApiError) {
        if (err.isUnauthorized) { router.push('/login'); return; }
        if (err.isForbidden)    { setError('You do not have permission to complete this appointment.'); return; }
        if (err.isConflict)     { setError('This appointment cannot be completed in its current state.'); return; }
        setError(err.message);
      } else {
        setError('Failed to complete appointment.');
      }
      showToast('Failed to complete appointment.', 'error');
    } finally {
      setLoading(null);
    }
  }

  async function doNoShow() {
    setLoading('NoShow');
    setError(null);
    try {
      await careConnectApi.appointments.update(appointment.id, { status: 'NoShow' });
      showToast('Appointment marked as no-show.', 'success');
      router.refresh();
    } catch (err) {
      if (err instanceof ApiError) {
        if (err.isUnauthorized) { router.push('/login'); return; }
        if (err.isForbidden)    { setError('You do not have permission to update this appointment.'); return; }
        setError(err.message);
      } else {
        setError('Failed to update appointment.');
      }
      showToast('Failed to update appointment.', 'error');
    } finally {
      setLoading(null);
    }
  }

  async function doReschedule() {
    if (!selectedSlot) return;
    setLoading('reschedule');
    setError(null);
    try {
      await careConnectApi.appointments.reschedule(appointment.id, {
        newAppointmentSlotId: selectedSlot.id,
        notes: rescheduleNotes.trim() || undefined,
      });
      showToast('Appointment rescheduled.', 'success');
      setShowReschedule(false);
      setSelectedSlot(null);
      setRescheduleNotes('');
      router.refresh();
    } catch (err) {
      if (err instanceof ApiError) {
        if (err.isUnauthorized) { router.push('/login'); return; }
        if (err.isForbidden)    { setError('You do not have permission to reschedule.'); return; }
        if (err.isConflict) {
          setSlotsError('That slot was just booked. Please choose another.');
          setSelectedSlot(null);
          setAvailability(null);
          loadSlots();
          return;
        }
        setError(err.message);
      } else {
        setError('Failed to reschedule appointment.');
      }
      showToast('Failed to reschedule appointment.', 'error');
    } finally {
      setLoading(null);
    }
  }

  return (
    <>
      <div className="bg-white border border-gray-200 rounded-lg px-5 py-4 space-y-3">
        <h3 className="text-xs font-semibold text-gray-400 uppercase tracking-wider">Actions</h3>

        {error && (
          <div className="bg-red-50 border border-red-200 rounded-md px-3 py-2 text-sm text-red-700">
            {error}
          </div>
        )}

        {/*
          LS-ID-TNT-015-001: ForbiddenBanner when role qualifies for ALL actions
          but ALL permissions are absent. Individual disabled-with-tooltip when
          only some permissions are absent and at least one action is enabled.
        */}
        {!hasAnyPermAccess ? (
          <ForbiddenBanner action="manage this appointment" />
        ) : (
          <div className="flex flex-wrap items-center gap-3">
            {roleCanConfirm && (
              <PermissionTooltip
                show={!canConfirm}
                message={DisabledReasons.noPermission('confirm this appointment').message}
              >
                <button
                  onClick={doConfirm}
                  disabled={!!loading || !canConfirm}
                  className="bg-green-600 text-white text-sm font-medium px-4 py-2 rounded-md hover:bg-green-700 disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
                >
                  {loading === 'Confirmed' ? 'Confirming…' : 'Confirm Appointment'}
                </button>
              </PermissionTooltip>
            )}

            {roleCanComplete && (
              <PermissionTooltip
                show={!canComplete}
                message={DisabledReasons.noPermission('mark this appointment as completed').message}
              >
                <button
                  onClick={doComplete}
                  disabled={!!loading || !canComplete}
                  className="bg-blue-600 text-white text-sm font-medium px-4 py-2 rounded-md hover:bg-blue-700 disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
                >
                  {loading === 'Completed' ? 'Completing…' : 'Mark Completed'}
                </button>
              </PermissionTooltip>
            )}

            {roleCanNoShow && (
              <PermissionTooltip
                show={!canNoShow}
                message={DisabledReasons.noPermission('mark this appointment as no-show').message}
              >
                <button
                  onClick={doNoShow}
                  disabled={!!loading || !canNoShow}
                  className="border border-orange-300 text-orange-600 text-sm font-medium px-4 py-2 rounded-md hover:bg-orange-50 disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
                >
                  {loading === 'NoShow' ? 'Marking…' : 'Mark No-Show'}
                </button>
              </PermissionTooltip>
            )}

            {roleCanReschedule && (
              <PermissionTooltip
                show={!canReschedule}
                message={DisabledReasons.noPermission('reschedule this appointment').message}
              >
                <button
                  onClick={() => { if (canReschedule) setShowReschedule(true); }}
                  disabled={!!loading || !canReschedule}
                  className="border border-gray-300 text-gray-700 text-sm font-medium px-4 py-2 rounded-md hover:bg-gray-50 disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
                >
                  Reschedule
                </button>
              </PermissionTooltip>
            )}
          </div>
        )}
      </div>

      {/* ── Reschedule modal — only reachable when canReschedule is true ───── */}
      {showReschedule && canReschedule && (
        <div className="fixed inset-0 z-50 overflow-y-auto">
          <div
            className="fixed inset-0 bg-black/30 backdrop-blur-sm"
            onClick={() => { setShowReschedule(false); setSelectedSlot(null); }}
          />

          <div className="relative min-h-screen flex items-start justify-center p-4 py-8">
            <div className="relative w-full max-w-2xl bg-white rounded-xl shadow-xl">
              {/* Header */}
              <div className="flex items-start justify-between px-6 py-4 border-b border-gray-100">
                <div>
                  <h2 className="text-base font-semibold text-gray-900">Reschedule Appointment</h2>
                  <p className="text-sm text-gray-500 mt-0.5">
                    {appointment.clientFirstName} {appointment.clientLastName}
                    {' · '}
                    <span className="text-gray-400 line-through">{formatDateTime(appointment.scheduledAtUtc)}</span>
                  </p>
                </div>
                <button
                  onClick={() => { setShowReschedule(false); setSelectedSlot(null); }}
                  className="text-gray-400 hover:text-gray-700 transition-colors p-1"
                  aria-label="Close"
                >
                  <span className="ri-close-line text-lg" />
                </button>
              </div>

              <div className="px-6 py-5 space-y-5">
                {slotsError && (
                  <div className="bg-red-50 border border-red-200 rounded-md px-3 py-2 text-sm text-red-700">
                    {slotsError}
                  </div>
                )}

                {slotsLoading ? (
                  <div className="space-y-3 animate-pulse">
                    {[1, 2, 3].map(i => <div key={i} className="h-10 bg-gray-100 rounded" />)}
                  </div>
                ) : availability ? (
                  <AvailabilityList
                    slots={availability.slots}
                    selectedSlotId={selectedSlot?.id ?? null}
                    onSelectSlot={setSelectedSlot}
                  />
                ) : (
                  <p className="text-sm text-gray-400 text-center py-8">No availability found.</p>
                )}

                {/* Selected slot preview */}
                {selectedSlot && (
                  <div className="bg-blue-50 border border-blue-200 rounded-md px-3 py-2 text-sm text-blue-700">
                    New time: <strong>{formatDateTime(selectedSlot.startUtc)}</strong>
                    {selectedSlot.serviceType ? ` · ${selectedSlot.serviceType}` : ''}
                  </div>
                )}

                {/* Notes */}
                <div>
                  <label className="block text-xs font-medium text-gray-600 mb-1">
                    Notes (optional)
                  </label>
                  <textarea
                    value={rescheduleNotes}
                    onChange={e => setRescheduleNotes(e.target.value)}
                    rows={2}
                    placeholder="Reason for rescheduling…"
                    className="w-full border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary resize-none"
                  />
                </div>

                <div className="flex items-center justify-end gap-3 pt-2 border-t border-gray-100">
                  <button
                    type="button"
                    onClick={() => { setShowReschedule(false); setSelectedSlot(null); }}
                    disabled={!!loading}
                    className="text-sm text-gray-600 hover:text-gray-900 px-4 py-2 rounded-md transition-colors disabled:opacity-50"
                  >
                    Cancel
                  </button>
                  <button
                    onClick={doReschedule}
                    disabled={!selectedSlot || !!loading}
                    className="bg-primary text-white text-sm font-medium px-5 py-2 rounded-md hover:opacity-90 disabled:opacity-50 transition-opacity"
                  >
                    {loading === 'reschedule' ? 'Rescheduling…' : 'Confirm Reschedule'}
                  </button>
                </div>
              </div>
            </div>
          </div>
        </div>
      )}
    </>
  );
}
