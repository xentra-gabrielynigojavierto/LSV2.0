'use client';

import { useState, type FormEvent } from 'react';
import { useRouter } from 'next/navigation';
import { careConnectApi } from '@/lib/careconnect-api';
import { ApiError } from '@/lib/api-client';
import { usePermission } from '@/hooks/use-permission';
import { PermissionCodes } from '@/lib/permission-codes';
import { PermissionTooltip } from '@/components/ui/permission-tooltip';
import { DisabledReasons } from '@/lib/disabled-reasons';
import type { AvailabilitySlot, CreateAppointmentRequest, ReferralDetail } from '@/types/careconnect';
import { formatPhoneDisplay, formatPhoneInput, stripPhone } from '@/lib/phone';

interface BookingPanelProps {
  providerId:   string;
  providerName: string;
  slot:         AvailabilitySlot;
  referral?:    ReferralDetail;   // pre-populates client fields when booked from a referral
  onClose:      () => void;
}

function formatDateTime(iso: string): string {
  return new Date(iso).toLocaleString('en-US', {
    weekday: 'short',
    month:   'short',
    day:     'numeric',
    hour:    'numeric',
    minute:  '2-digit',
    hour12:  true,
  });
}

/**
 * Booking form modal — presented after the referrer selects an availability slot.
 *
 * LS-ID-TNT-015-001: Permission gate added — CC.AppointmentCreate controls
 * whether the "Confirm Booking" submit button is active. The form remains
 * visible so the referrer can review the slot details, but submitting requires
 * the permission. The button shows a tooltip explaining the restriction when
 * the permission is absent.
 */
export function BookingPanel({
  providerId,
  providerName,
  slot,
  referral,
  onClose,
}: BookingPanelProps) {
  const router = useRouter();

  // LS-ID-TNT-015-001: Permission check (UX layer only; backend enforces authoritatively).
  const canBookPerm = usePermission(PermissionCodes.CC.AppointmentCreate);

  // Pre-populate from referral context
  const [clientFirstName, setClientFirstName] = useState(referral?.clientFirstName ?? '');
  const [clientLastName,  setClientLastName]  = useState(referral?.clientLastName  ?? '');
  const [clientDob,       setClientDob]       = useState(referral?.clientDob       ?? '');
  const [clientPhone,     setClientPhone]     = useState(formatPhoneDisplay(referral?.clientPhone) ?? '');
  const [clientEmail,     setClientEmail]     = useState(referral?.clientEmail     ?? '');
  const [caseNumber,      setCaseNumber]      = useState(referral?.caseNumber      ?? '');
  const [notes,           setNotes]           = useState('');

  const [loading,     setLoading]     = useState(false);
  const [error,       setError]       = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});

  function validate(): boolean {
    const errs: Record<string, string> = {};
    if (!clientFirstName.trim()) errs.clientFirstName = 'First name is required';
    if (!clientLastName.trim())  errs.clientLastName  = 'Last name is required';
    setFieldErrors(errs);
    return Object.keys(errs).length === 0;
  }

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    if (!validate()) return;

    setError(null);
    setLoading(true);

    const payload: CreateAppointmentRequest = {
      providerId,
      referralId:      referral?.id,
      slotId:          slot.id,
      scheduledAtUtc:  slot.startUtc,
      durationMinutes: slot.durationMinutes,
      serviceType:     slot.serviceType ?? referral?.requestedService,
      notes:           notes.trim() || undefined,
      clientFirstName: clientFirstName.trim(),
      clientLastName:  clientLastName.trim(),
      clientDob:       clientDob || undefined,
      clientPhone:     stripPhone(clientPhone) || undefined,
      clientEmail:     clientEmail.trim() || undefined,
      caseNumber:      caseNumber.trim()  || undefined,
    };

    try {
      const { data: appt } = await careConnectApi.appointments.create(payload);
      router.push(`/careconnect/appointments/${appt.id}`);
      onClose();
    } catch (err) {
      if (err instanceof ApiError) {
        if (err.isUnauthorized) { router.push('/login'); return; }
        if (err.isConflict) {
          setError('This slot has just been booked by someone else. Please select a different time.');
          return;
        }
        if (err.isForbidden) {
          setError('You do not have permission to book appointments.');
          return;
        }
        setError(err.message);
      } else {
        setError('An unexpected error occurred. Please try again.');
      }
    } finally {
      setLoading(false);
    }
  }

  function InputError({ field }: { field: string }) {
    return fieldErrors[field]
      ? <p className="mt-1 text-xs text-red-600">{fieldErrors[field]}</p>
      : null;
  }

  return (
    <div className="fixed inset-0 z-50 overflow-y-auto">
      {/* Backdrop */}
      <div className="fixed inset-0 bg-black/30 backdrop-blur-sm" onClick={onClose} />

      <div className="relative min-h-screen flex items-center justify-center p-4">
        <div className="relative w-full max-w-xl bg-white rounded-xl shadow-xl">
          {/* Header */}
          <div className="flex items-start justify-between px-6 py-4 border-b border-gray-100">
            <div>
              <h2 className="text-base font-semibold text-gray-900">Book Appointment</h2>
              <p className="text-sm text-gray-500 mt-0.5">{providerName}</p>
              <p className="text-sm font-medium text-primary mt-1">
                {formatDateTime(slot.startUtc)}
                {' '}({slot.durationMinutes} min)
              </p>
            </div>
            <button
              onClick={onClose}
              className="text-gray-400 hover:text-gray-700 transition-colors p-1 mt-0.5"
              aria-label="Close"
            >
              ✕
            </button>
          </div>

          <form onSubmit={handleSubmit} className="px-6 py-5 space-y-5">
            {/* Referral context badge */}
            {referral && (
              <div className="bg-blue-50 border border-blue-200 rounded-md px-3 py-2 text-xs text-blue-700">
                Booking for referral #{referral.caseNumber ?? referral.id.slice(0, 8)}
                {referral.requestedService ? ` · ${referral.requestedService}` : ''}
              </div>
            )}

            {/* Global error */}
            {error && (
              <div className="bg-red-50 border border-red-200 rounded-md px-4 py-3 text-sm text-red-700">
                {error}
              </div>
            )}

            {/* Client fields */}
            <fieldset>
              <legend className="text-xs font-semibold text-gray-400 uppercase tracking-wider mb-3">
                Client
              </legend>
              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">
                    First name <span className="text-red-500">*</span>
                  </label>
                  <input
                    type="text"
                    value={clientFirstName}
                    onChange={e => { setClientFirstName(e.target.value); setFieldErrors(fe => ({ ...fe, clientFirstName: '' })); }}
                    className="w-full border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary"
                  />
                  <InputError field="clientFirstName" />
                </div>

                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">
                    Last name <span className="text-red-500">*</span>
                  </label>
                  <input
                    type="text"
                    value={clientLastName}
                    onChange={e => { setClientLastName(e.target.value); setFieldErrors(fe => ({ ...fe, clientLastName: '' })); }}
                    className="w-full border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary"
                  />
                  <InputError field="clientLastName" />
                </div>

                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">Date of birth</label>
                  <input
                    type="date"
                    value={clientDob}
                    onChange={e => setClientDob(e.target.value)}
                    className="w-full border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary"
                  />
                </div>

                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">Phone</label>
                  <input
                    type="tel"
                    value={clientPhone}
                    placeholder="(555) 555-5555"
                    onChange={e => setClientPhone(formatPhoneInput(e.target.value))}
                    className="w-full border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary"
                  />
                </div>

                <div className="col-span-2">
                  <label className="block text-sm font-medium text-gray-700 mb-1">Email</label>
                  <input
                    type="email"
                    value={clientEmail}
                    onChange={e => setClientEmail(e.target.value)}
                    className="w-full border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary"
                  />
                </div>

                <div className="col-span-2">
                  <label className="block text-sm font-medium text-gray-700 mb-1">Case number</label>
                  <input
                    type="text"
                    value={caseNumber}
                    onChange={e => setCaseNumber(e.target.value)}
                    placeholder="Optional"
                    className="w-full border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary"
                  />
                </div>
              </div>
            </fieldset>

            {/* Notes */}
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">Notes for provider</label>
              <textarea
                value={notes}
                onChange={e => setNotes(e.target.value)}
                rows={3}
                placeholder="Optional context for the provider…"
                className="w-full border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary resize-none"
              />
            </div>

            {/* Actions */}
            <div className="flex items-center justify-end gap-3 pt-2 border-t border-gray-100">
              <button
                type="button"
                onClick={onClose}
                disabled={loading}
                className="text-sm text-gray-600 hover:text-gray-900 px-4 py-2 rounded-md transition-colors disabled:opacity-50"
              >
                Cancel
              </button>
              {/*
                LS-ID-TNT-015-001: Disable-with-tooltip when CC.AppointmentCreate
                permission is absent. The form remains fillable so the user can
                review the booking details, but submitting requires the permission.
              */}
              <PermissionTooltip
                show={!canBookPerm}
                message={DisabledReasons.noPermission('book appointments').message}
              >
                <button
                  type="submit"
                  disabled={loading || !canBookPerm}
                  className="bg-primary text-white text-sm font-medium px-5 py-2 rounded-md hover:opacity-90 disabled:opacity-60 disabled:cursor-not-allowed transition-opacity"
                >
                  {loading ? 'Booking…' : 'Confirm Booking'}
                </button>
              </PermissionTooltip>
            </div>
          </form>
        </div>
      </div>
    </div>
  );
}
