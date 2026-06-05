'use client';

/**
 * InviteUserForm — UIX-003 / UIX-003-02
 *
 * Client-side form for /tenant-users/invite.
 *
 * Props:
 *   resolvedTenantId   — if set, the tenant is pre-resolved (TenantAdmin
 *                        context or selected tenant). The field is hidden and
 *                        the value is submitted silently.
 *   resolvedTenantName — human-readable tenant name shown in the locked badge.
 *
 * UX improvements (UIX-003-02):
 *   - Shows an inline success state after submission (instead of immediate redirect)
 *   - Explains what happens next before navigating away
 *   - Better help text on each field
 *   - Tenant context is clearly communicated
 *
 * INVITE-FIX: When the backend returns an inviteToken (non-production only),
 * the success screen shows the activation link so admins can hand-deliver it
 * as a fallback when email delivery is unavailable or delayed.
 */

import { useState, useEffect, FormEvent } from 'react';
import Link                               from 'next/link';
import { useRouter }                      from 'next/navigation';
import { Routes }                         from '@/lib/routes';

interface Props {
  resolvedTenantId?:   string;
  resolvedTenantName?: string;
}

interface FormState {
  email:      string;
  firstName:  string;
  lastName:   string;
  tenantId:   string;
  memberRole: string;
}

interface SuccessState {
  email:          string;
  firstName:      string;
  lastName:       string;
  activationLink: string | null;
}

const MEMBER_ROLES = [
  { value: 'Member',  label: 'Member',  hint: 'Standard access — can view and use resources' },
  { value: 'Admin',   label: 'Admin',   hint: 'Can manage resources within the organization' },
  { value: 'Owner',   label: 'Owner',   hint: 'Full control, including billing and membership' },
  { value: 'Viewer',  label: 'Viewer',  hint: 'Read-only access to organization resources' },
];

export function InviteUserForm({ resolvedTenantId, resolvedTenantName }: Props) {
  const router = useRouter();

  const [form, setForm]         = useState<FormState>({
    email:      '',
    firstName:  '',
    lastName:   '',
    tenantId:   resolvedTenantId ?? '',
    memberRole: 'Member',
  });
  const [pending,   setPending]   = useState(false);
  const [error,     setError]     = useState<string | null>(null);
  const [success,   setSuccess]   = useState<SuccessState | null>(null);
  const [countdown, setCountdown] = useState(5);
  const [copied,    setCopied]    = useState(false);

  /* After success, count down and redirect */
  useEffect(() => {
    if (!success) return;
    if (countdown <= 0) {
      router.push(Routes.tenantUsers);
      router.refresh();
      return;
    }
    const t = setTimeout(() => setCountdown(c => c - 1), 1000);
    return () => clearTimeout(t);
  }, [success, countdown, router]);

  function handleChange(e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement>) {
    const { name, value } = e.target;
    setForm(prev => ({ ...prev, [name]: value }));
  }

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setError(null);
    setPending(true);

    try {
      const res = await fetch('/api/identity/admin/users/invite', {
        method:  'POST',
        headers: { 'Content-Type': 'application/json' },
        body:    JSON.stringify({
          email:      form.email.trim(),
          firstName:  form.firstName.trim(),
          lastName:   form.lastName.trim(),
          tenantId:   form.tenantId.trim(),
          memberRole: form.memberRole || undefined,
        }),
      });

      if (!res.ok) {
        const body = await res.json().catch(() => ({})) as { message?: string };
        throw new Error(body.message ?? 'Failed to send invitation.');
      }

      const data = await res.json().catch(() => ({})) as { activationLink?: string | null };

      setSuccess({
        email:          form.email.trim(),
        firstName:      form.firstName.trim(),
        lastName:       form.lastName.trim(),
        activationLink: data.activationLink ?? null,
      });
    } catch (err) {
      setError(err instanceof Error ? err.message : 'An unexpected error occurred.');
    } finally {
      setPending(false);
    }
  }

  async function copyLink(link: string) {
    try {
      await navigator.clipboard.writeText(link);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    } catch {
      // clipboard not available
    }
  }

  const tenantIsLocked = Boolean(resolvedTenantId);

  /* ── Success state ───────────────────────────────────────────────────── */
  if (success) {
    return (
      <div className="min-h-screen bg-gray-50 flex items-start justify-center pt-20 px-4">
        <div className="w-full max-w-md">
          <div className="bg-white border border-green-200 rounded-xl shadow-sm overflow-hidden">
            <div className="px-6 py-5 border-b border-green-100 bg-green-50 flex items-center gap-3">
              <span className="w-2.5 h-2.5 rounded-full bg-green-500 inline-block flex-shrink-0" />
              <h1 className="text-base font-semibold text-green-800">Invitation Sent</h1>
            </div>
            <div className="px-6 py-5 space-y-4">
              <p className="text-sm text-gray-700">
                An invitation email has been sent to{' '}
                <span className="font-semibold">{success.firstName} {success.lastName}</span> at{' '}
                <span className="font-mono text-indigo-700">{success.email}</span>.
              </p>

              <div className="bg-gray-50 border border-gray-100 rounded-lg px-4 py-3 text-sm text-gray-600 space-y-1.5">
                <p className="font-medium text-gray-700 text-xs uppercase tracking-wide">What happens next</p>
                <ul className="space-y-1 text-xs text-gray-500 list-none">
                  <li className="flex items-start gap-2">
                    <span className="mt-1 w-1 h-1 rounded-full bg-gray-400 flex-shrink-0 inline-block" />
                    The user receives an email with a secure invitation link.
                  </li>
                  <li className="flex items-start gap-2">
                    <span className="mt-1 w-1 h-1 rounded-full bg-gray-400 flex-shrink-0 inline-block" />
                    They set a password and complete their profile on first login.
                  </li>
                  <li className="flex items-start gap-2">
                    <span className="mt-1 w-1 h-1 rounded-full bg-gray-400 flex-shrink-0 inline-block" />
                    Their status appears as <span className="font-semibold text-blue-700">Invited</span> until they accept.
                  </li>
                  <li className="flex items-start gap-2">
                    <span className="mt-1 w-1 h-1 rounded-full bg-gray-400 flex-shrink-0 inline-block" />
                    You can resend the invitation from their profile if needed.
                  </li>
                </ul>
              </div>

              {/* Dev-mode fallback: show the activation link when provided */}
              {success.activationLink && (
                <div className="bg-amber-50 border border-amber-200 rounded-lg px-4 py-3 space-y-2">
                  <p className="text-xs font-semibold text-amber-800 uppercase tracking-wide">
                    Activation link (share manually if email is delayed)
                  </p>
                  <p className="text-[11px] text-amber-700 break-all font-mono leading-relaxed">
                    {success.activationLink}
                  </p>
                  <button
                    type="button"
                    onClick={() => copyLink(success.activationLink!)}
                    className="text-[11px] font-medium text-amber-800 hover:text-amber-900 underline transition-colors"
                  >
                    {copied ? 'Copied!' : 'Copy link'}
                  </button>
                </div>
              )}

              <p className="text-xs text-gray-400">
                Redirecting to user list in {countdown}s…
              </p>

              <div className="flex items-center gap-3 pt-1">
                <button
                  type="button"
                  onClick={() => { router.push(Routes.tenantUsers); router.refresh(); }}
                  className="flex-1 bg-indigo-600 text-white text-sm font-medium px-4 py-2 rounded-lg hover:bg-indigo-700 transition-colors text-center"
                >
                  Go to User List
                </button>
                <button
                  type="button"
                  onClick={() => {
                    setSuccess(null);
                    setCountdown(5);
                    setCopied(false);
                    setForm({
                      email:      '',
                      firstName:  '',
                      lastName:   '',
                      tenantId:   resolvedTenantId ?? '',
                      memberRole: 'Member',
                    });
                  }}
                  className="flex-1 bg-white border border-gray-200 text-gray-600 text-sm font-medium px-4 py-2 rounded-lg hover:bg-gray-50 transition-colors text-center"
                >
                  Invite Another
                </button>
              </div>
            </div>
          </div>
        </div>
      </div>
    );
  }

  /* ── Form ────────────────────────────────────────────────────────────── */
  return (
    <div className="min-h-screen bg-gray-50 flex items-start justify-center pt-20 px-4">
      <div className="w-full max-w-md">

        {/* Card */}
        <div className="bg-white border border-gray-200 rounded-xl shadow-sm overflow-hidden">

          {/* Header */}
          <div className="px-6 py-5 border-b border-gray-100">
            <div className="flex items-center justify-between">
              <div>
                <h1 className="text-lg font-semibold text-gray-900">Invite User</h1>
                <p className="text-sm text-gray-500 mt-0.5">
                  An invitation email will be sent with a secure sign-up link.
                </p>
              </div>
              <Link
                href={Routes.tenantUsers}
                className="text-sm text-gray-400 hover:text-gray-700 transition-colors"
              >
                Cancel
              </Link>
            </div>
          </div>

          {/* Form */}
          <form onSubmit={handleSubmit} className="px-6 py-5 space-y-4">

            {error && (
              <div className="bg-red-50 border border-red-200 rounded-lg px-4 py-3 text-sm text-red-700">
                {error}
              </div>
            )}

            <Field label="First Name" required hint="The user's given name — used in the invitation email.">
              <input
                type="text"
                name="firstName"
                value={form.firstName}
                onChange={handleChange}
                required
                autoComplete="given-name"
                placeholder="Jane"
                className={inputClass}
              />
            </Field>

            <Field label="Last Name" required hint="The user's family name.">
              <input
                type="text"
                name="lastName"
                value={form.lastName}
                onChange={handleChange}
                required
                autoComplete="family-name"
                placeholder="Smith"
                className={inputClass}
              />
            </Field>

            <Field label="Email Address" required hint="Must be a valid email. This is where the invitation will be sent.">
              <input
                type="email"
                name="email"
                value={form.email}
                onChange={handleChange}
                required
                autoComplete="email"
                placeholder="jane@example.com"
                className={inputClass}
              />
            </Field>

            {/* Tenant — locked (auto-resolved) or manual UUID entry */}
            {tenantIsLocked ? (
              <Field label="Tenant" hint="The user will be added to this tenant automatically.">
                <div className="flex items-center gap-2 px-3 py-1.5 border border-gray-200 rounded-md bg-gray-50">
                  <span className="h-2 w-2 rounded-full bg-amber-400 flex-shrink-0" />
                  <span className="text-sm text-gray-700 font-medium truncate">
                    {resolvedTenantName ?? resolvedTenantId}
                  </span>
                  <span className="ml-auto text-[10px] font-semibold uppercase tracking-wide text-gray-400">
                    auto
                  </span>
                </div>
                {/* Hidden field carries the value for submit */}
                <input type="hidden" name="tenantId" value={resolvedTenantId} />
              </Field>
            ) : (
              <Field label="Tenant ID" required hint="The UUID of the tenant this user belongs to. Find it on the Tenants page.">
                <input
                  type="text"
                  name="tenantId"
                  value={form.tenantId}
                  onChange={handleChange}
                  required
                  placeholder="xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
                  className={`${inputClass} font-mono text-xs`}
                />
              </Field>
            )}

            <Field label="Member Role" hint="Sets the user's role within their primary organization.">
              <select
                name="memberRole"
                value={form.memberRole}
                onChange={handleChange}
                className={inputClass}
              >
                {MEMBER_ROLES.map(r => (
                  <option key={r.value} value={r.value}>{r.label}</option>
                ))}
              </select>
              {form.memberRole && (
                <p className="mt-1 text-[11px] text-gray-400">
                  {MEMBER_ROLES.find(r => r.value === form.memberRole)?.hint}
                </p>
              )}
            </Field>

            <div className="pt-2 space-y-2">
              <button
                type="submit"
                disabled={pending}
                className="w-full bg-indigo-600 text-white text-sm font-medium px-4 py-2.5 rounded-lg hover:bg-indigo-700 disabled:opacity-50 disabled:cursor-not-allowed transition-colors focus:outline-none focus-visible:ring-2 focus-visible:ring-indigo-500 focus-visible:ring-offset-1"
              >
                {pending ? (
                  <span className="flex items-center justify-center gap-2">
                    <span className="h-4 w-4 rounded-full border-2 border-white/40 border-t-white animate-spin" />
                    Sending invitation…
                  </span>
                ) : (
                  'Send Invitation'
                )}
              </button>
              <p className="text-center text-[11px] text-gray-400">
                The user will receive an email with a secure sign-up link.
                {' '}Fields marked <span className="text-red-500">*</span> are required.
              </p>
            </div>
          </form>
        </div>

        {/* Back link */}
        <p className="text-center mt-4">
          <Link href={Routes.tenantUsers} className="text-sm text-gray-400 hover:text-gray-700 underline transition-colors">
            ← Back to Tenant Users
          </Link>
        </p>
      </div>
    </div>
  );
}

const inputClass =
  'w-full text-sm border border-gray-200 rounded-md px-3 py-1.5 text-gray-900 placeholder-gray-400 bg-white focus:outline-none focus:ring-1 focus:ring-indigo-400 focus:border-indigo-400';

function Field({
  label,
  required,
  hint,
  children,
}: {
  label:     string;
  required?: boolean;
  hint?:     string;
  children:  React.ReactNode;
}) {
  return (
    <div>
      <label className="block text-xs font-medium text-gray-700 mb-1">
        {label}
        {required && <span className="text-red-500 ml-0.5">*</span>}
      </label>
      {children}
      {hint && <p className="mt-1 text-[11px] text-gray-400">{hint}</p>}
    </div>
  );
}
