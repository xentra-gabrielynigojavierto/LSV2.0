'use client';

import { useState, useEffect, type FormEvent } from 'react';
import { useSearchParams } from 'next/navigation';

export function AcceptInviteForm() {
  const searchParams = useSearchParams();
  const token = searchParams?.get('token');

  const [newPassword,     setNewPassword]     = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [error,           setError]           = useState<string | null>(null);
  const [loading,         setLoading]         = useState(false);
  const [success,         setSuccess]         = useState(false);
  const [showPw,          setShowPw]          = useState(false);
  // loginUrl is derived from the current page origin on mount (the invite link already
  // points to the correct tenant subdomain). The API response may refine this value.
  const [loginUrl,        setLoginUrl]        = useState('/login');

  useEffect(() => {
    // The invite email links to https://{slug}.{domain}/accept-invite?token=…
    // so window.location.origin already contains the correct subdomain base URL.
    setLoginUrl(`${window.location.origin}/login`);
  }, []);

  if (!token) {
    return (
      <div className="space-y-5">
        <div className="flex items-start gap-2.5 rounded-lg border border-red-200 bg-red-50 px-3.5 py-3">
          <i className="ri-error-warning-line text-[15px] text-red-500 shrink-0 mt-0.5" />
          <p className="text-[13px] text-red-700 leading-snug">
            Invalid or missing invitation token. Please use the link from your invitation email or contact your administrator.
          </p>
        </div>
      </div>
    );
  }

  if (success) {
    return (
      <div className="space-y-5">
        <div className="flex items-start gap-2.5 rounded-lg border border-green-200 bg-green-50 px-3.5 py-3">
          <i className="ri-check-line text-[15px] text-green-600 shrink-0 mt-0.5" />
          <p className="text-[13px] text-green-700 leading-snug">
            Your account has been activated. You can now sign in with your new password.
          </p>
        </div>
        <a
          href={loginUrl}
          className="w-full flex items-center justify-center gap-2 rounded-lg px-4 py-2.5 text-sm font-semibold text-white transition-opacity"
          style={{ backgroundColor: '#f97316' }}
        >
          Sign in
        </a>
      </div>
    );
  }

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setError(null);

    if (newPassword.length < 8) {
      setError('Password must be at least 8 characters.');
      return;
    }
    if (newPassword !== confirmPassword) {
      setError('Passwords do not match.');
      return;
    }

    setLoading(true);
    try {
      const res = await fetch('/api/auth/accept-invite', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ token, newPassword }),
      });

      const data = await res.json().catch(() => ({}));

      if (!res.ok) {
        setError(data.message ?? 'Failed to accept invitation. The link may have expired.');
        return;
      }

      // The identity service returns the tenant portal base URL so we can redirect
      // to the correct subdomain login page. If absent, the origin-derived URL
      // set on mount already points to the right place (same subdomain).
      if (data.tenantPortalUrl) {
        setLoginUrl(`${data.tenantPortalUrl}/login`);
      }
      setSuccess(true);
    } catch {
      setError('Network error. Please check your connection and try again.');
    } finally {
      setLoading(false);
    }
  }

  return (
    <form onSubmit={handleSubmit} className="space-y-5" noValidate>
      <div className="space-y-1.5">
        <label className="flex items-center gap-1.5 text-[13px] font-medium text-gray-700">
          New password
        </label>
        <div className="relative">
          <input
            type={showPw ? 'text' : 'password'}
            required
            value={newPassword}
            onChange={e => setNewPassword(e.target.value)}
            autoComplete="new-password"
            placeholder="At least 8 characters"
            className={`${inputCls} pr-10`}
            autoFocus
          />
          <button
            type="button"
            tabIndex={-1}
            onClick={() => setShowPw(v => !v)}
            className="absolute right-3 top-1/2 -translate-y-1/2 text-gray-400 hover:text-gray-600 transition-colors"
            aria-label={showPw ? 'Hide password' : 'Show password'}
          >
            <i className={`${showPw ? 'ri-eye-off-line' : 'ri-eye-line'} text-[16px]`} />
          </button>
        </div>
      </div>

      <div className="space-y-1.5">
        <label className="flex items-center gap-1.5 text-[13px] font-medium text-gray-700">
          Confirm password
        </label>
        <input
          type={showPw ? 'text' : 'password'}
          required
          value={confirmPassword}
          onChange={e => setConfirmPassword(e.target.value)}
          autoComplete="new-password"
          placeholder="Re-enter your new password"
          className={inputCls}
        />
      </div>

      {error && (
        <div className="flex items-start gap-2.5 rounded-lg border border-red-200 bg-red-50 px-3.5 py-3">
          <i className="ri-error-warning-line text-[15px] text-red-500 shrink-0 mt-0.5" />
          <p className="text-[13px] text-red-700 leading-snug">{error}</p>
        </div>
      )}

      <button
        type="submit"
        disabled={loading}
        className="w-full flex items-center justify-center gap-2 rounded-lg px-4 py-2.5 text-sm font-semibold text-white transition-opacity disabled:opacity-60"
        style={{ backgroundColor: '#f97316' }}
      >
        {loading
          ? <><i className="ri-loader-4-line animate-spin text-[15px]" /> Activating…</>
          : 'Activate account'
        }
      </button>

      <p className="text-center text-xs text-gray-400">
        <a
          href={loginUrl}
          className="text-gray-600 hover:text-gray-900 underline underline-offset-2 transition-colors"
        >
          Back to sign in
        </a>
      </p>
    </form>
  );
}

const inputCls = [
  'w-full rounded-lg border border-gray-200 bg-white px-3.5 py-2.5 text-sm text-gray-900',
  'placeholder:text-gray-400',
  'focus:outline-none focus:ring-2 focus:border-transparent',
  'transition-shadow',
].join(' ') + ' focus:ring-[#f97316]/40 focus:border-[#f97316]';
