'use client';

import { useState, useTransition } from 'react';
import { addSuppression }          from '@/app/notifications/actions';
import type { NotifChannel }       from '@/lib/notifications-api';

const CHANNELS: NotifChannel[]    = ['email', 'sms', 'push', 'in-app'];
const SUPPRESSION_TYPES = [
  { value: 'manual',           label: 'Manual' },
  { value: 'unsubscribe',      label: 'Unsubscribe' },
  { value: 'bounce',           label: 'Bounce' },
  { value: 'complaint',        label: 'Complaint' },
  { value: 'invalid_contact',  label: 'Invalid Contact' },
  { value: 'system_protection',label: 'System Protection' },
];

export function AddSuppressionForm() {
  const [open,        setOpen]        = useState(false);
  const [channel,     setChannel]     = useState<NotifChannel>('email');
  const [contact,     setContact]     = useState('');
  const [type,        setType]        = useState('manual');
  const [reason,      setReason]      = useState('');
  const [isPending,   startTransition] = useTransition();
  const [errorMsg,    setErrorMsg]    = useState<string | null>(null);
  const [successMsg,  setSuccessMsg]  = useState<string | null>(null);

  function reset() {
    setContact('');
    setReason('');
    setType('manual');
    setChannel('email');
    setErrorMsg(null);
    setSuccessMsg(null);
  }

  function handleSubmit() {
    if (!contact.trim()) { setErrorMsg('Contact value is required.'); return; }
    setErrorMsg(null);
    setSuccessMsg(null);
    startTransition(async () => {
      const res = await addSuppression({
        channel,
        contactValue:    contact.trim(),
        suppressionType: type,
        reason:          reason.trim() || undefined,
      });
      if (res.success) {
        setSuccessMsg('Suppression added.');
        reset();
        setTimeout(() => { setSuccessMsg(null); setOpen(false); }, 2000);
      } else {
        setErrorMsg(res.error ?? 'Failed to add suppression.');
      }
    });
  }

  return (
    <>
      <button
        onClick={() => { reset(); setOpen(true); }}
        className="inline-flex items-center gap-1.5 h-9 px-3 text-sm font-semibold text-white bg-red-600 hover:bg-red-700 rounded-md transition-colors"
      >
        <i className="ri-user-forbid-line" />
        Add Suppression
      </button>

      {open && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 backdrop-blur-sm">
          <div className="bg-white rounded-xl shadow-2xl w-full max-w-md mx-4">
            {/* Header */}
            <div className="flex items-center justify-between px-5 py-4 border-b border-gray-100">
              <h2 className="text-base font-semibold text-gray-900">Add Suppression</h2>
              <button onClick={() => setOpen(false)} className="text-gray-400 hover:text-gray-600 text-xl">
                <i className="ri-close-line" />
              </button>
            </div>

            {/* Body */}
            <div className="px-5 py-4 space-y-4">
              {/* Channel */}
              <div>
                <label className="block text-xs font-medium text-gray-700 mb-1">Channel *</label>
                <select
                  value={channel}
                  onChange={e => setChannel(e.target.value as NotifChannel)}
                  className="w-full text-sm border border-gray-300 rounded-md px-3 py-1.5 focus:outline-none focus:ring-2 focus:ring-red-400"
                >
                  {CHANNELS.map(c => (
                    <option key={c} value={c}>{c}</option>
                  ))}
                </select>
              </div>

              {/* Contact value */}
              <div>
                <label className="block text-xs font-medium text-gray-700 mb-1">Contact Value *</label>
                <input
                  type="text"
                  value={contact}
                  onChange={e => setContact(e.target.value)}
                  placeholder={channel === 'email' ? 'user@example.com' : channel === 'sms' ? '+15555551234' : 'contact identifier'}
                  className="w-full text-sm border border-gray-300 rounded-md px-3 py-1.5 focus:outline-none focus:ring-2 focus:ring-red-400"
                />
              </div>

              {/* Type */}
              <div>
                <label className="block text-xs font-medium text-gray-700 mb-1">Suppression Type *</label>
                <select
                  value={type}
                  onChange={e => setType(e.target.value)}
                  className="w-full text-sm border border-gray-300 rounded-md px-3 py-1.5 focus:outline-none focus:ring-2 focus:ring-red-400"
                >
                  {SUPPRESSION_TYPES.map(t => (
                    <option key={t.value} value={t.value}>{t.label}</option>
                  ))}
                </select>
              </div>

              {/* Reason */}
              <div>
                <label className="block text-xs font-medium text-gray-700 mb-1">Reason (optional)</label>
                <input
                  type="text"
                  value={reason}
                  onChange={e => setReason(e.target.value)}
                  placeholder="e.g. GDPR opt-out request"
                  className="w-full text-sm border border-gray-300 rounded-md px-3 py-1.5 focus:outline-none focus:ring-2 focus:ring-red-400"
                />
              </div>

              {errorMsg && (
                <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
                  {errorMsg}
                </div>
              )}

              {successMsg && (
                <div className="rounded-md border border-green-200 bg-green-50 px-3 py-2 text-sm text-green-700">
                  <i className="ri-check-line mr-1" />{successMsg}
                </div>
              )}
            </div>

            {/* Footer */}
            <div className="flex items-center justify-end gap-2 px-5 py-3 border-t border-gray-100 bg-gray-50">
              <button
                onClick={() => setOpen(false)}
                className="px-3 py-1.5 rounded-md border border-gray-300 bg-white text-gray-600 text-sm font-medium hover:bg-gray-50"
              >
                Cancel
              </button>
              <button
                onClick={handleSubmit}
                disabled={isPending}
                className="inline-flex items-center gap-1.5 px-4 py-1.5 rounded-md bg-red-600 text-white text-sm font-semibold hover:bg-red-700 disabled:opacity-50 disabled:cursor-not-allowed"
              >
                {isPending
                  ? <><i className="ri-loader-4-line animate-spin" /> Adding…</>
                  : <><i className="ri-user-forbid-line" /> Add Suppression</>
                }
              </button>
            </div>
          </div>
        </div>
      )}
    </>
  );
}
