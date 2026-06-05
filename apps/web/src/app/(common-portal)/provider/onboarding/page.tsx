'use client';

/**
 * CC2-INT-B09 — Provider Tenant Self-Onboarding page.
 *
 * Allows a COMMON_PORTAL provider to set up their own dedicated tenant workspace.
 * Calls POST /api/provider/onboarding/provision-tenant via careconnect-api.
 * On success, redirects to the new portal URL (or shows instructions to navigate there).
 */

import { useState, useCallback } from 'react';
import Link from 'next/link';
import { careConnectApi } from '@/lib/careconnect-api';
import { ApiError } from '@/lib/api-client';

export const dynamic = 'force-dynamic';


// ── Types ─────────────────────────────────────────────────────────────────────

type FormState = 'idle' | 'checking' | 'submitting' | 'success' | 'error';

interface CodeStatus {
  checked:   boolean;
  available: boolean | null;
  normalized: string;
  message:   string | null;
}

interface SuccessResult {
  tenantCode:         string;
  subdomain:          string;
  provisioningStatus: string;
  portalUrl:          string | null;
  message:            string;
}

// ── Code availability check helper ────────────────────────────────────────────

const CODE_RE = /^[a-z0-9]([a-z0-9-]{0,28}[a-z0-9])?$/i;

function isValidCodeShape(code: string) {
  return code.length >= 2 && code.length <= 30 && CODE_RE.test(code);
}

// ── Field ──────────────────────────────────────────────────────────────────────

function Field({
  label, hint, error, children,
}: {
  label: string;
  hint?: string;
  error?: string | null;
  children: React.ReactNode;
}) {
  return (
    <div>
      <label className="block text-sm font-medium text-gray-700 mb-1">{label}</label>
      {children}
      {hint && !error && (
        <p className="mt-1 text-xs text-gray-400">{hint}</p>
      )}
      {error && (
        <p className="mt-1 text-xs text-red-600">{error}</p>
      )}
    </div>
  );
}

// ── Page ──────────────────────────────────────────────────────────────────────

export default function ProviderOnboardingPage() {
  const [tenantName, setTenantName] = useState('');
  const [tenantCode, setTenantCode] = useState('');
  const [codeStatus, setCodeStatus] = useState<CodeStatus>({
    checked: false, available: null, normalized: '', message: null,
  });
  const [formState, setFormState] = useState<FormState>('idle');
  const [serverError, setServerError] = useState<string | null>(null);
  const [success, setSuccess] = useState<SuccessResult | null>(null);

  // ── Tenant code check ────────────────────────────────────────────────────
  const checkCode = useCallback(async (code: string) => {
    if (!isValidCodeShape(code)) return;
    setFormState('checking');
    setCodeStatus({ checked: false, available: null, normalized: '', message: null });
    try {
      const res = await careConnectApi.onboarding.checkCode(code);
      const normalized = res.data.normalizedCode || code;
      setCodeStatus({
        checked:   true,
        available: res.data.available,
        normalized,
        message:   res.data.message ?? null,
      });
      // Reflect the server-normalized code back into the input field (e.g. strips edge hyphens).
      if (normalized !== code) {
        setTenantCode(normalized);
      }
    } catch {
      // Silently ignore — provision step enforces uniqueness.
      setCodeStatus({ checked: true, available: true, normalized: code.toLowerCase(), message: null });
    } finally {
      setFormState('idle');
    }
  }, []);

  const handleCodeBlur = () => {
    const trimmed = tenantCode.trim().toLowerCase();
    if (trimmed.length >= 2) {
      setTenantCode(trimmed); // Normalize immediately on blur even before the API call.
      checkCode(trimmed);
    }
  };

  const handleCodeChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const val = e.target.value.toLowerCase().replace(/[^a-z0-9-]/g, '');
    setTenantCode(val);
    setCodeStatus({ checked: false, available: null, normalized: '', message: null });
    setServerError(null);
  };

  // ── Form submit ──────────────────────────────────────────────────────────
  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (formState === 'submitting') return;

    const name = tenantName.trim();
    const code = tenantCode.trim();

    if (name.length < 2 || code.length < 2) return;
    if (codeStatus.checked && codeStatus.available === false) return;

    setFormState('submitting');
    setServerError(null);

    try {
      const res = await careConnectApi.onboarding.provisionTenant({
        tenantName: name,
        tenantCode: code,
      });

      setSuccess({
        tenantCode:         res.data.tenantCode,
        subdomain:          res.data.subdomain,
        provisioningStatus: res.data.provisioningStatus,
        portalUrl:          res.data.portalUrl,
        message:            res.data.message,
      });
      setFormState('success');
    } catch (err: unknown) {
      // 409 Conflict → tenant code already taken; surface back into the code field.
      if (err instanceof ApiError && err.isConflict) {
        setCodeStatus({
          checked:   true,
          available: false,
          normalized: code,
          message:   err.message,
        });
        setServerError(null);
        setFormState('error');
        return;
      }

      const msg =
        err instanceof ApiError
          ? err.message
          : err && typeof err === 'object' && 'message' in err
          ? String((err as { message?: unknown }).message)
          : 'Something went wrong. Please try again or contact support.';
      setServerError(msg);
      setFormState('error');
    }
  };

  // ── Success screen ───────────────────────────────────────────────────────
  if (formState === 'success' && success) {
    return (
      <div className="max-w-lg mx-auto space-y-6">
        <div className="rounded-xl border border-green-200 bg-green-50 p-6 text-center space-y-3">
          <div className="w-12 h-12 rounded-full bg-green-100 flex items-center justify-center mx-auto">
            <svg className="w-6 h-6 text-green-600" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 13l4 4L19 7" />
            </svg>
          </div>
          <h2 className="text-lg font-semibold text-green-900">Workspace created</h2>
          <p className="text-sm text-green-700">{success.message}</p>
          {success.portalUrl && (
            <a
              href={success.portalUrl}
              className="inline-flex items-center gap-1.5 px-5 py-2.5 rounded-lg
                         bg-green-600 hover:bg-green-700 text-white text-sm font-medium transition-colors"
              target="_blank"
              rel="noreferrer"
            >
              Open my workspace
              <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2}
                  d="M10 6H6a2 2 0 00-2 2v10a2 2 0 002 2h10a2 2 0 002-2v-4M14 4h6m0 0v6m0-6L10 14" />
              </svg>
            </a>
          )}
          {!success.portalUrl && (
            <div className="mt-2 text-sm text-green-700">
              Your subdomain: <strong>{success.subdomain}</strong>
              <br />
              DNS provisioning may take a few minutes to complete.
            </div>
          )}
        </div>
        <div className="text-center">
          <Link href="/provider/dashboard" className="text-sm text-indigo-600 hover:text-indigo-700 font-medium">
            Back to dashboard
          </Link>
        </div>
      </div>
    );
  }

  // ── Form ──────────────────────────────────────────────────────────────────
  const isSubmitting  = formState === 'submitting';
  const isChecking    = formState === 'checking';
  const codeBlocked   = codeStatus.checked && codeStatus.available === false;
  const codeAvailable = codeStatus.checked && codeStatus.available === true;
  const canSubmit     = !isSubmitting && !isChecking && !codeBlocked
                        && tenantName.trim().length >= 2
                        && tenantCode.trim().length >= 2;

  return (
    <div className="max-w-lg mx-auto space-y-6">
      {/* Header */}
      <div>
        <Link href="/provider/dashboard"
          className="text-xs text-gray-400 hover:text-gray-600 flex items-center gap-1 mb-4">
          <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 19l-7-7 7-7" />
          </svg>
          Back to dashboard
        </Link>
        <h1 className="text-xl font-semibold text-gray-900">Set up your workspace</h1>
        <p className="text-sm text-gray-500 mt-1">
          Create a dedicated tenant portal for your practice. Your existing account and referral history
          will remain accessible from your new workspace.
        </p>
      </div>

      {/* Info box */}
      <div className="rounded-lg border border-blue-100 bg-blue-50 px-4 py-3 text-sm text-blue-700 space-y-1">
        <p className="font-medium">What happens next</p>
        <ul className="list-disc list-inside space-y-0.5 text-blue-600">
          <li>A secure tenant workspace is created for your practice</li>
          <li>CareConnect is provisioned in your new portal</li>
          <li>Your account moves to the new workspace — no new login required</li>
          <li>DNS setup may take a few minutes to propagate</li>
        </ul>
      </div>

      {/* Form */}
      <form onSubmit={handleSubmit} className="bg-white rounded-xl border border-gray-200 p-6 space-y-5">
        <Field
          label="Organisation name"
          hint="The display name for your practice or organisation."
          error={tenantName.trim().length > 0 && tenantName.trim().length < 2
            ? 'Must be at least 2 characters.' : null}
        >
          <input
            type="text"
            value={tenantName}
            onChange={e => { setTenantName(e.target.value); setServerError(null); }}
            placeholder="e.g. Riverside Physiotherapy"
            disabled={isSubmitting}
            required
            minLength={2}
            maxLength={100}
            className="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm
                       focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:border-indigo-500
                       disabled:bg-gray-50 disabled:text-gray-400"
          />
        </Field>

        <Field
          label="Subdomain / workspace code"
          hint="Lowercase letters, numbers, and hyphens only. 2–30 characters."
          error={
            codeBlocked
              ? (codeStatus.message ?? 'This code is not available. Please choose another.')
              : tenantCode.length > 0 && !isValidCodeShape(tenantCode)
              ? 'Only lowercase letters, numbers, and hyphens. Must start and end with a letter or number.'
              : null
          }
        >
          <div className="relative">
            <input
              type="text"
              value={tenantCode}
              onChange={handleCodeChange}
              onBlur={handleCodeBlur}
              placeholder="e.g. riverside-physio"
              disabled={isSubmitting}
              required
              minLength={2}
              maxLength={30}
              className={[
                'w-full rounded-lg border px-3 py-2 text-sm pr-8',
                'focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:border-indigo-500',
                'disabled:bg-gray-50 disabled:text-gray-400',
                codeBlocked   ? 'border-red-400 focus:ring-red-400'   : '',
                codeAvailable ? 'border-green-400 focus:ring-green-400' : 'border-gray-300',
              ].join(' ')}
            />
            {/* Availability indicator */}
            {isChecking && (
              <span className="absolute right-2.5 top-2.5">
                <svg className="w-4 h-4 text-gray-400 animate-spin" fill="none" viewBox="0 0 24 24">
                  <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
                  <path className="opacity-75" fill="currentColor"
                    d="M4 12a8 8 0 018-8v4a4 4 0 00-4 4H4z" />
                </svg>
              </span>
            )}
            {!isChecking && codeAvailable && (
              <span className="absolute right-2.5 top-2.5">
                <svg className="w-4 h-4 text-green-500" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 13l4 4L19 7" />
                </svg>
              </span>
            )}
            {!isChecking && codeBlocked && (
              <span className="absolute right-2.5 top-2.5">
                <svg className="w-4 h-4 text-red-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2}
                    d="M6 18L18 6M6 6l12 12" />
                </svg>
              </span>
            )}
          </div>
          {codeAvailable && (
            <p className="mt-1 text-xs text-green-600">
              Available. Your workspace will be at <strong>{codeStatus.normalized}.legalsynq.com</strong>
            </p>
          )}
        </Field>

        {/* Server error */}
        {serverError && (
          <div className="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
            {serverError}
          </div>
        )}

        {/* Submit */}
        <div className="flex items-center justify-between pt-1">
          <Link href="/provider/dashboard"
            className="text-sm text-gray-500 hover:text-gray-700 transition-colors">
            Cancel
          </Link>
          <button
            type="submit"
            disabled={!canSubmit}
            className="inline-flex items-center gap-2 px-5 py-2.5 rounded-lg
                       bg-indigo-600 hover:bg-indigo-700 disabled:bg-indigo-300
                       text-white text-sm font-medium transition-colors"
          >
            {isSubmitting && (
              <svg className="w-4 h-4 animate-spin" fill="none" viewBox="0 0 24 24">
                <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
                <path className="opacity-75" fill="currentColor"
                  d="M4 12a8 8 0 018-8v4a4 4 0 00-4 4H4z" />
              </svg>
            )}
            {isSubmitting ? 'Creating workspace...' : 'Create workspace'}
          </button>
        </div>
      </form>
    </div>
  );
}
