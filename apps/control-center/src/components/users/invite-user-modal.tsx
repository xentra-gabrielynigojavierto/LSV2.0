'use client';

import { useState, useEffect, useCallback } from 'react';

const EMAIL_RE = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

interface FormState {
  firstName: string;
  lastName:  string;
  email:     string;
  roleId:    string;
}

interface FormErrors {
  firstName?: string;
  lastName?:  string;
  email?:     string;
}

const EMPTY: FormState = { firstName: '', lastName: '', email: '', roleId: '' };

interface Role { id: string; name: string; code?: string; }

function validate(f: FormState): FormErrors {
  const e: FormErrors = {};
  if (!f.firstName.trim()) e.firstName = 'First name is required.';
  if (!f.lastName.trim())  e.lastName  = 'Last name is required.';
  if (!f.email.trim())           e.email = 'Email is required.';
  else if (!EMAIL_RE.test(f.email.trim())) e.email = 'Enter a valid email.';
  return e;
}

interface Props {
  open:      boolean;
  tenantId:  string;
  onClose:   () => void;
  onSuccess: () => void;
}

export function InviteUserModal({ open, tenantId, onClose, onSuccess }: Props) {
  const [form,        setForm]        = useState<FormState>(EMPTY);
  const [errors,      setErrors]      = useState<FormErrors>({});
  const [apiError,    setApiError]    = useState<string | null>(null);
  const [loading,     setLoading]     = useState(false);
  const [roles,       setRoles]       = useState<Role[]>([]);
  const [rolesLoading,setRolesLoading]= useState(false);
  const [activationLink, setActivationLink] = useState<string | null>(null);
  const [copyLabel,   setCopyLabel]   = useState('Copy link');

  const fetchRoles = useCallback(async () => {
    setRolesLoading(true);
    try {
      const res  = await fetch('/api/identity/admin/roles');
      const data = await res.json() as Role[];
      setRoles(Array.isArray(data) ? data : []);
    } catch {
      setRoles([]);
    } finally {
      setRolesLoading(false);
    }
  }, []);

  useEffect(() => {
    if (open) {
      setForm(EMPTY);
      setErrors({});
      setApiError(null);
      setActivationLink(null);
      setCopyLabel('Copy link');
      fetchRoles();
    }
  }, [open, fetchRoles]);

  function field<K extends keyof FormState>(key: K, val: FormState[K]) {
    setForm(p => ({ ...p, [key]: val }));
    if ((errors as Record<string, unknown>)[key]) setErrors(p => ({ ...p, [key]: undefined }));
    if (apiError) setApiError(null);
  }

  async function handleSubmit() {
    const errs = validate(form);
    if (Object.keys(errs).length > 0) { setErrors(errs); return; }

    setLoading(true);
    setApiError(null);
    try {
      const res = await fetch('/api/identity/admin/users/invite', {
        method:  'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          tenantId,
          firstName: form.firstName.trim(),
          lastName:  form.lastName.trim(),
          email:     form.email.trim(),
          ...(form.roleId ? { memberRole: form.roleId } : {}),
        }),
      });
      const data = await res.json() as { activationLink?: string; message?: string };
      if (!res.ok) { setApiError(data.message ?? 'Failed to send invitation.'); return; }

      if (data.activationLink) {
        setActivationLink(data.activationLink);
      } else {
        onSuccess();
      }
    } catch {
      setApiError('Network error. Please try again.');
    } finally {
      setLoading(false);
    }
  }

  function copyLink() {
    if (!activationLink) return;
    navigator.clipboard.writeText(activationLink).then(() => {
      setCopyLabel('Copied!');
      setTimeout(() => setCopyLabel('Copy link'), 2500);
    });
  }

  if (!open) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
      <div className="absolute inset-0 bg-black/40" onClick={onClose} />
      <div className="relative bg-white rounded-xl shadow-2xl w-full max-w-md p-6 space-y-5">

        <div className="flex items-center justify-between">
          <h2 className="text-base font-semibold text-gray-900">Invite User</h2>
          <button onClick={onClose} className="p-1.5 rounded hover:bg-gray-100 text-gray-400 hover:text-gray-600 transition-colors">
            <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" /></svg>
          </button>
        </div>

        {/* ── Success / activation-link state ─────────────────────────────── */}
        {activationLink ? (
          <div className="space-y-4">
            <div className="bg-green-50 border border-green-200 rounded-lg px-4 py-3 text-sm text-green-800">
              Invitation created for <strong>{form.email}</strong>. Email delivery may be unavailable in this environment — copy the activation link to share manually.
            </div>
            <div className="space-y-1">
              <label className="block text-xs font-medium text-gray-700">Activation Link</label>
              <div className="flex items-center gap-2">
                <input
                  readOnly
                  value={activationLink}
                  className="flex-1 text-xs font-mono border border-gray-200 rounded-md px-2 py-1.5 bg-gray-50 text-gray-700 focus:outline-none"
                />
                <button
                  onClick={copyLink}
                  className="shrink-0 text-xs px-3 py-1.5 rounded-md border border-indigo-300 bg-indigo-50 text-indigo-700 hover:bg-indigo-100 transition-colors"
                >
                  {copyLabel}
                </button>
              </div>
            </div>
            <div className="flex justify-end">
              <button
                onClick={onSuccess}
                className="text-sm px-4 py-2 rounded-md bg-indigo-600 hover:bg-indigo-700 text-white font-medium transition-colors"
              >
                Done
              </button>
            </div>
          </div>
        ) : (
          /* ── Invite form ──────────────────────────────────────────────── */
          <div className="space-y-4">
            {apiError && (
              <div className="bg-red-50 border border-red-200 rounded-lg px-3 py-2 text-sm text-red-700">
                {apiError}
              </div>
            )}

            <div className="grid grid-cols-2 gap-3">
              <div className="space-y-1">
                <label className="block text-xs font-medium text-gray-700">
                  First name <span className="text-red-500">*</span>
                </label>
                <input
                  type="text"
                  value={form.firstName}
                  onChange={e => field('firstName', e.target.value)}
                  placeholder="Jane"
                  className={`w-full rounded-md border py-2 px-3 text-sm text-gray-900 placeholder-gray-400 focus:outline-none focus:ring-2 focus:ring-indigo-400/50 focus:border-indigo-400 ${errors.firstName ? 'border-red-400 bg-red-50' : 'border-gray-200'}`}
                />
                {errors.firstName && <p className="text-xs text-red-600">{errors.firstName}</p>}
              </div>
              <div className="space-y-1">
                <label className="block text-xs font-medium text-gray-700">
                  Last name <span className="text-red-500">*</span>
                </label>
                <input
                  type="text"
                  value={form.lastName}
                  onChange={e => field('lastName', e.target.value)}
                  placeholder="Doe"
                  className={`w-full rounded-md border py-2 px-3 text-sm text-gray-900 placeholder-gray-400 focus:outline-none focus:ring-2 focus:ring-indigo-400/50 focus:border-indigo-400 ${errors.lastName ? 'border-red-400 bg-red-50' : 'border-gray-200'}`}
                />
                {errors.lastName && <p className="text-xs text-red-600">{errors.lastName}</p>}
              </div>
            </div>

            <div className="space-y-1">
              <label className="block text-xs font-medium text-gray-700">
                Email <span className="text-red-500">*</span>
              </label>
              <input
                type="email"
                value={form.email}
                onChange={e => field('email', e.target.value)}
                placeholder="jane.doe@company.com"
                autoComplete="email"
                className={`w-full rounded-md border py-2 px-3 text-sm text-gray-900 placeholder-gray-400 focus:outline-none focus:ring-2 focus:ring-indigo-400/50 focus:border-indigo-400 ${errors.email ? 'border-red-400 bg-red-50' : 'border-gray-200'}`}
              />
              {errors.email && <p className="text-xs text-red-600">{errors.email}</p>}
            </div>

            <div className="space-y-1">
              <label className="block text-xs font-medium text-gray-700">Role (optional)</label>
              <select
                value={form.roleId}
                onChange={e => field('roleId', e.target.value)}
                disabled={rolesLoading}
                className="w-full rounded-md border border-gray-200 py-2 px-3 text-sm text-gray-900 focus:outline-none focus:ring-2 focus:ring-indigo-400/50 focus:border-indigo-400 disabled:bg-gray-50 disabled:text-gray-400"
              >
                <option value="">{rolesLoading ? 'Loading roles…' : '— No initial role —'}</option>
                {roles.map(r => <option key={r.id} value={r.id}>{r.name}</option>)}
              </select>
            </div>

            <p className="text-[11px] text-gray-400 flex items-center gap-1.5">
              <svg className="w-3.5 h-3.5 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M3 8l7.89 5.26a2 2 0 002.22 0L21 8M5 19h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v10a2 2 0 002 2z" /></svg>
              The user will receive an email with a link to set their password and activate their account.
            </p>

            <div className="flex items-center justify-end gap-2 pt-1">
              <button
                onClick={onClose}
                className="text-sm px-4 py-2 rounded-md border border-gray-200 text-gray-600 hover:bg-gray-50 transition-colors"
              >
                Cancel
              </button>
              <button
                onClick={handleSubmit}
                disabled={loading}
                className="text-sm px-4 py-2 rounded-md bg-indigo-600 hover:bg-indigo-700 text-white font-medium transition-colors disabled:opacity-50"
              >
                {loading ? 'Sending…' : 'Send Invitation'}
              </button>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}
