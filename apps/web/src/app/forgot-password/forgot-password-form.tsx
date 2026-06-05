'use client';

import { useState, useEffect, type FormEvent } from 'react';

export function ForgotPasswordForm() {
  const [mounted, setMounted] = useState(false);
  useEffect(() => { setMounted(true); }, []);

  // Hide the tenant code field whenever a subdomain is present in the URL —
  // the BFF resolves the tenant from the Host header on any *.legalsynq.com domain
  // (including the common portal careconnect-demo.legalsynq.com).
  const hasTenantSubdomain = mounted && (() => {
    const host = window.location.hostname;
    const parts = host.split('.');
    return parts.length >= 3 && !host.startsWith('localhost');
  })();
  const showTenantField = mounted && !hasTenantSubdomain;

  const [email, setEmail] = useState('');
  const [tenantCode, setTenantCode] = useState(process.env.NEXT_PUBLIC_TENANT_CODE ?? '');
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [resetLink, setResetLink] = useState<string | null>(null);
  const [submitted, setSubmitted] = useState(false);

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setError(null);
    setResetLink(null);
    setLoading(true);
    try {
      const body: Record<string, string> = { email };
      if (tenantCode) body.tenantCode = tenantCode;

      const res = await fetch('/api/auth/forgot-password', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(body),
      });

      const data = await res.json().catch(() => ({}));

      if (!res.ok) {
        setError(data.message ?? 'Something went wrong. Please try again.');
        return;
      }

      setSubmitted(true);

      if (data.resetLink) {
        setResetLink(data.resetLink);
      }
    } catch {
      setError('Network error. Please check your connection and try again.');
    } finally {
      setLoading(false);
    }
  }

  if (submitted) {
    return (
      <div className="space-y-5">
        <div className="flex items-start gap-2.5 rounded-lg border border-green-200 bg-green-50 px-3.5 py-3">
          <i className="ri-check-line text-[15px] text-green-600 shrink-0 mt-0.5" />
          <p className="text-[13px] text-green-700 leading-snug">
            If an account exists with that email address, a password reset link has been generated.
          </p>
        </div>

        {resetLink && (
          <div className="rounded-lg border border-amber-200 bg-amber-50 px-3.5 py-3 space-y-2">
            <div className="flex items-start gap-2">
              <i className="ri-information-line text-[15px] text-amber-600 shrink-0 mt-0.5" />
              <p className="text-[13px] text-amber-700 leading-snug">
                <span className="font-medium">Temporary:</span> In the future, this link will be sent via email. For now, click below to reset your password.
              </p>
            </div>
            <a
              href={resetLink}
              className="inline-flex items-center gap-1.5 text-[13px] font-medium underline underline-offset-2 transition-colors"
              style={{ color: '#f97316' }}
            >
              <i className="ri-lock-password-line text-[14px]" />
              Reset your password
            </a>
          </div>
        )}

        <button
          type="button"
          onClick={() => {
            setSubmitted(false);
            setResetLink(null);
            setEmail('');
          }}
          className="w-full flex items-center justify-center gap-2 rounded-lg border border-gray-200 px-4 py-2.5 text-sm font-medium text-gray-700 bg-white hover:bg-gray-50 transition-colors"
        >
          Try another email
        </button>
      </div>
    );
  }

  return (
    <form onSubmit={handleSubmit} className="space-y-5" noValidate>
      {showTenantField && (
        <div className="space-y-1.5">
          <label className="flex items-center gap-1.5 text-[13px] font-medium text-gray-700">
            Tenant Code
          </label>
          <input
            type="text"
            value={tenantCode}
            onChange={e => setTenantCode(e.target.value)}
            placeholder="e.g. MANER-LAW"
            className={inputCls}
          />
        </div>
      )}

      <div className="space-y-1.5">
        <label className="flex items-center gap-1.5 text-[13px] font-medium text-gray-700">
          Email address
        </label>
        <input
          type="email"
          required
          value={email}
          onChange={e => setEmail(e.target.value)}
          autoComplete="email"
          placeholder="you@example.com"
          className={inputCls}
          autoFocus
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
          ? <><i className="ri-loader-4-line animate-spin text-[15px]" /> Sending…</>
          : 'Send reset link'
        }
      </button>
    </form>
  );
}

const inputCls = [
  'w-full rounded-lg border border-gray-200 bg-white px-3.5 py-2.5 text-sm text-gray-900',
  'placeholder:text-gray-400',
  'focus:outline-none focus:ring-2 focus:border-transparent',
  'transition-shadow',
].join(' ') + ' focus:ring-[#f97316]/40 focus:border-[#f97316]';
