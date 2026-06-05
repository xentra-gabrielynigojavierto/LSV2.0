'use client';

import { useRef, useState, useEffect } from 'react';
import { useRouter } from 'next/navigation';
import type { RoleSummary } from '@/types/control-center';
import { invitePlatformUserAction } from '@/app/platform-users/actions';

interface InvitePlatformUserModalProps {
  open:          boolean;
  onClose:       () => void;
  platformRoles: RoleSummary[];
}

interface FormState {
  email:     string;
  firstName: string;
  lastName:  string;
  roleId:    string;
}

const INITIAL: FormState = { email: '', firstName: '', lastName: '', roleId: '' };

export function InvitePlatformUserModal({
  open,
  onClose,
  platformRoles,
}: InvitePlatformUserModalProps) {
  const router        = useRouter();
  const overlayRef    = useRef<HTMLDivElement>(null);
  const firstInputRef = useRef<HTMLInputElement>(null);

  const [form, setForm]               = useState<FormState>(INITIAL);
  const [submitting, setSubmitting]   = useState(false);
  const [error, setError]             = useState<string | null>(null);
  const [activationLink, setActivationLink] = useState<string | null>(null);

  useEffect(() => {
    if (open) {
      setForm(INITIAL);
      setError(null);
      setActivationLink(null);
      setTimeout(() => firstInputRef.current?.focus(), 50);
    }
  }, [open]);

  useEffect(() => {
    function onKey(e: KeyboardEvent) {
      if (e.key === 'Escape' && open) onClose();
    }
    document.addEventListener('keydown', onKey);
    return () => document.removeEventListener('keydown', onKey);
  }, [open, onClose]);

  if (!open) return null;

  function handleOverlayClick(e: React.MouseEvent) {
    if (e.target === overlayRef.current) onClose();
  }

  function setField(field: keyof FormState, value: string) {
    setForm(prev => ({ ...prev, [field]: value }));
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    setSubmitting(true);

    try {
      const result = await invitePlatformUserAction({
        email:     form.email.trim(),
        firstName: form.firstName.trim(),
        lastName:  form.lastName.trim(),
        roleId:    form.roleId || undefined,
      });

      if (!result.success) {
        setError(result.error ?? 'Invite failed. Please try again.');
        return;
      }

      if (result.activationLink) {
        setActivationLink(result.activationLink);
      } else {
        router.refresh();
        onClose();
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Unexpected error. Please try again.');
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div
      ref={overlayRef}
      onClick={handleOverlayClick}
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 backdrop-blur-sm"
      role="dialog"
      aria-modal="true"
      aria-labelledby="invite-platform-user-title"
    >
      <div className="w-full max-w-md bg-white rounded-xl shadow-2xl border border-gray-200 overflow-hidden">

        {/* Header */}
        <div className="flex items-center justify-between px-6 py-4 border-b border-gray-100">
          <div>
            <h2 id="invite-platform-user-title" className="text-base font-semibold text-gray-900">
              Invite Platform User
            </h2>
            <p className="text-xs text-gray-400 mt-0.5">
              Add a LegalSynq staff account (PlatformInternal type)
            </p>
          </div>
          <button
            onClick={onClose}
            className="text-gray-400 hover:text-gray-600 transition-colors text-xl leading-none"
            aria-label="Close"
          >
            ✕
          </button>
        </div>

        {/* Activation link (shown in non-prod when email delivery is skipped) */}
        {activationLink && (
          <div className="mx-6 mt-4 mb-2 p-3 bg-green-50 border border-green-200 rounded-lg">
            <p className="text-xs font-semibold text-green-700 mb-1">
              User invited — activation link (non-prod):
            </p>
            <a
              href={activationLink}
              target="_blank"
              rel="noopener noreferrer"
              className="text-xs text-green-800 underline break-all"
            >
              {activationLink}
            </a>
            <div className="mt-3 flex justify-end">
              <button
                onClick={() => { router.refresh(); onClose(); }}
                className="text-xs px-3 py-1.5 rounded-md bg-green-600 text-white hover:bg-green-700 transition-colors"
              >
                Done
              </button>
            </div>
          </div>
        )}

        {!activationLink && (
          <form onSubmit={handleSubmit} className="px-6 py-5 space-y-4">

            {error && (
              <div className="bg-red-50 border border-red-200 rounded-lg px-3 py-2 text-xs text-red-700">
                {error}
              </div>
            )}

            {/* Email */}
            <div>
              <label htmlFor="pum-email" className="block text-xs font-medium text-gray-700 mb-1">
                Email address <span className="text-red-500">*</span>
              </label>
              <input
                ref={firstInputRef}
                id="pum-email"
                type="email"
                required
                autoComplete="off"
                value={form.email}
                onChange={e => setField('email', e.target.value)}
                placeholder="staff@legalsynq.com"
                className="w-full text-sm border border-gray-200 rounded-md px-3 py-2 text-gray-900 placeholder-gray-400 focus:outline-none focus:ring-1 focus:ring-indigo-400 focus:border-indigo-400"
              />
            </div>

            {/* Name row */}
            <div className="flex gap-3">
              <div className="flex-1">
                <label htmlFor="pum-first" className="block text-xs font-medium text-gray-700 mb-1">
                  First name <span className="text-red-500">*</span>
                </label>
                <input
                  id="pum-first"
                  type="text"
                  required
                  autoComplete="off"
                  value={form.firstName}
                  onChange={e => setField('firstName', e.target.value)}
                  placeholder="First"
                  className="w-full text-sm border border-gray-200 rounded-md px-3 py-2 text-gray-900 placeholder-gray-400 focus:outline-none focus:ring-1 focus:ring-indigo-400 focus:border-indigo-400"
                />
              </div>
              <div className="flex-1">
                <label htmlFor="pum-last" className="block text-xs font-medium text-gray-700 mb-1">
                  Last name <span className="text-red-500">*</span>
                </label>
                <input
                  id="pum-last"
                  type="text"
                  required
                  autoComplete="off"
                  value={form.lastName}
                  onChange={e => setField('lastName', e.target.value)}
                  placeholder="Last"
                  className="w-full text-sm border border-gray-200 rounded-md px-3 py-2 text-gray-900 placeholder-gray-400 focus:outline-none focus:ring-1 focus:ring-indigo-400 focus:border-indigo-400"
                />
              </div>
            </div>

            {/* Initial role — filtered to Platform-prefixed roles */}
            {platformRoles.length > 0 && (
              <div>
                <label htmlFor="pum-role" className="block text-xs font-medium text-gray-700 mb-1">
                  Initial platform role{' '}
                  <span className="text-gray-400 font-normal">(optional)</span>
                </label>
                <select
                  id="pum-role"
                  value={form.roleId}
                  onChange={e => setField('roleId', e.target.value)}
                  className="w-full text-sm border border-gray-200 rounded-md px-3 py-2 text-gray-900 focus:outline-none focus:ring-1 focus:ring-indigo-400 focus:border-indigo-400"
                >
                  <option value="">— No initial role —</option>
                  {platformRoles.map(role => (
                    <option key={role.id} value={role.id}>
                      {role.name}
                    </option>
                  ))}
                </select>
                <p className="text-[11px] text-gray-400 mt-1">
                  You can assign or change roles from the user detail page at any time.
                </p>
              </div>
            )}

            {/* Actions */}
            <div className="flex items-center justify-end gap-2 pt-2 border-t border-gray-100">
              <button
                type="button"
                onClick={onClose}
                disabled={submitting}
                className="text-sm px-4 py-2 rounded-md border border-gray-200 bg-white text-gray-600 hover:bg-gray-50 transition-colors disabled:opacity-50"
              >
                Cancel
              </button>
              <button
                type="submit"
                disabled={submitting}
                className="text-sm px-4 py-2 rounded-md bg-indigo-600 text-white hover:bg-indigo-700 transition-colors disabled:opacity-60 flex items-center gap-1.5"
              >
                {submitting && (
                  <span className="animate-spin inline-block w-3.5 h-3.5 border-2 border-white border-t-transparent rounded-full" />
                )}
                {submitting ? 'Sending invite…' : 'Send invite'}
              </button>
            </div>
          </form>
        )}
      </div>
    </div>
  );
}
