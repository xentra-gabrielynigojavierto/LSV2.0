'use client';

import { useState, useEffect, useMemo, type FormEvent } from 'react';
import { useRouter, useSearchParams } from 'next/navigation';
import { useSession } from '@/hooks/use-session';

// ── LS-ID-TNT-010: reason banner messages ────────────────────────────────────
const REASON_MESSAGES: Record<string, { icon: string; text: string }> = {
  idle: {
    icon: 'ri-time-line',
    text: 'Your session expired due to inactivity. Please sign in again.',
  },
  unauthenticated: {
    icon: 'ri-shield-keyhole-line',
    text: 'Your session has ended. Please sign in to continue.',
  },
  access_updated: {
    icon: 'ri-lock-unlock-line',
    text: 'Your access permissions have changed. Please sign in again to continue.',
  },
  'referral-view': {
    icon: 'ri-links-line',
    text: 'Sign in to view and manage this referral from your provider dashboard.',
  },
  'referral-portal': {
    icon: 'ri-links-line',
    text: 'Sign in to access the referral details and respond from your dashboard.',
  },
};

/**
 * Login form — calls the Next.js BFF route POST /api/auth/login.
 *
 * Tenant resolution:
 *   - Production: tenant is resolved from the subdomain (Host header).
 *     The tenant code field is hidden. If the subdomain doesn't map to a
 *     tenant, the BFF returns an error and we show a clear message.
 *   - Development/Replit: NEXT_PUBLIC_ENV=development enables the manual
 *     tenant code input, pre-populated from NEXT_PUBLIC_TENANT_CODE.
 *
 * isDev is deferred to after mount so the server render and the initial
 * client render always agree (both see isDev = false), eliminating the
 * hydration mismatch caused by NEXT_PUBLIC_ENV being available in the
 * Node.js process but not necessarily inlined in the browser bundle.
 *
 * Supports a `returnTo` query param for deep-linking after login
 * (e.g., LSCC-005 active-tenant provider referral view flow).
 */
export function LoginForm() {
  const router       = useRouter();
  const searchParams = useSearchParams();
  const { refresh }  = useSession();

  const [mounted,    setMounted]    = useState(false);
  useEffect(() => { setMounted(true); }, []);

  // LS-ID-TNT-010: read reason param to show appropriate context banner.
  const reasonBanner = useMemo(() => {
    const r = searchParams?.get('reason') ?? '';
    return REASON_MESSAGES[r] ?? null;
  }, [searchParams]);

  const hasSubdomain = mounted && (() => {
    const host = window.location.hostname;
    const parts = host.split('.');
    return parts.length >= 3 && !host.startsWith('localhost');
  })();
  const showTenantField = mounted && !hasSubdomain && process.env.NEXT_PUBLIC_ENV === 'development';

  const [email,      setEmail]      = useState('');
  const [password,   setPassword]   = useState('');
  const [tenantCode, setTenantCode] = useState(process.env.NEXT_PUBLIC_TENANT_CODE ?? '');
  const [error,      setError]      = useState<string | null>(null);
  const [loading,    setLoading]    = useState(false);
  const [showPw,     setShowPw]     = useState(false);

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setError(null);
    setLoading(true);
    try {
      const body: Record<string, string> = { email, password };
      if (showTenantField && tenantCode) body.tenantCode = tenantCode;

      const res = await fetch('/api/auth/login', {
        method:  'POST',
        headers: { 'Content-Type': 'application/json' },
        body:    JSON.stringify(body),
      });

      if (!res.ok) {
        const err = await res.json().catch(() => ({}));
        const msg = err.message ?? 'Invalid credentials. Please try again.';
        if (msg.includes('Tenant could not be resolved')) {
          setError('This login page is not associated with an active organization. Please check the URL or contact your administrator.');
        } else {
          setError(msg);
        }
        return;
      }

      await refresh();

      const rawReturnTo = searchParams?.get('returnTo') ?? '';
      const safeDest    = rawReturnTo.startsWith('/') && !rawReturnTo.startsWith('//')
        ? rawReturnTo
        : '/dashboard';
      router.push(safeDest);
    } catch {
      setError('Network error. Please check your connection and try again.');
    } finally {
      setLoading(false);
    }
  }

  return (
    <form onSubmit={handleSubmit} className="space-y-5" noValidate>

      {/* Reason context banner (idle / unauthenticated / access_updated) */}
      {reasonBanner && (
        <div className="flex items-start gap-2.5 rounded-lg border border-blue-200 bg-blue-50 px-3.5 py-3">
          <i className={`${reasonBanner.icon} text-[15px] text-blue-500 shrink-0 mt-0.5`} />
          <p className="text-[13px] text-blue-700 leading-snug">{reasonBanner.text}</p>
        </div>
      )}

      {/* Dev-only tenant code — hidden when a subdomain is detected (tenant resolved from Host) */}
      {showTenantField && (
        <Field label="Tenant Code" hint="dev only">
          <input
            type="text"
            value={tenantCode}
            onChange={e => setTenantCode(e.target.value)}
            placeholder="e.g. HARTWELL"
            className={inputCls}
          />
        </Field>
      )}

      {/* Email */}
      <Field label="Email address">
        <input
          type="email"
          required
          value={email}
          onChange={e => setEmail(e.target.value)}
          autoComplete="email"
          placeholder="you@example.com"
          className={inputCls}
        />
      </Field>

      {/* Password */}
      <Field label="Password">
        <div className="relative">
          <input
            type={showPw ? 'text' : 'password'}
            required
            value={password}
            onChange={e => setPassword(e.target.value)}
            autoComplete="current-password"
            placeholder="••••••••"
            className={`${inputCls} pr-10`}
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
      </Field>

      <div className="flex justify-end -mt-1">
        <a
          href="/forgot-password"
          className="text-[13px] text-gray-500 hover:text-gray-700 underline underline-offset-2 transition-colors"
        >
          Forgot password?
        </a>
      </div>

      {/* Error banner */}
      {error && (
        <div className="flex items-start gap-2.5 rounded-lg border border-red-200 bg-red-50 px-3.5 py-3">
          <i className="ri-error-warning-line text-[15px] text-red-500 shrink-0 mt-0.5" />
          <p className="text-[13px] text-red-700 leading-snug">{error}</p>
        </div>
      )}

      {/* Submit */}
      <button
        type="submit"
        disabled={loading}
        className="w-full flex items-center justify-center gap-2 rounded-lg px-4 py-2.5 text-sm font-semibold text-white transition-opacity disabled:opacity-60"
        style={{ backgroundColor: '#f97316' }}
      >
        {loading
          ? <><i className="ri-loader-4-line animate-spin text-[15px]" /> Signing in…</>
          : 'Sign in'
        }
      </button>
    </form>
  );
}

// ── Helpers ───────────────────────────────────────────────────────────────────

const inputCls = [
  'w-full rounded-lg border border-gray-200 bg-white px-3.5 py-2.5 text-sm text-gray-900',
  'placeholder:text-gray-400',
  'focus:outline-none focus:ring-2 focus:border-transparent',
  'transition-shadow',
].join(' ') + ' focus:ring-[#f97316]/40 focus:border-[#f97316]';

function Field({
  label,
  hint,
  children,
}: {
  label: string;
  hint?: string;
  children: React.ReactNode;
}) {
  return (
    <div className="space-y-1.5">
      <label className="flex items-center gap-1.5 text-[13px] font-medium text-gray-700">
        {label}
        {hint && (
          <span className="text-[11px] font-normal text-gray-400 bg-gray-100 px-1.5 py-0.5 rounded">
            {hint}
          </span>
        )}
      </label>
      {children}
    </div>
  );
}
