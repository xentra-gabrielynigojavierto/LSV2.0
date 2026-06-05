'use client';

import { useState, useTransition } from 'react';
import Link from 'next/link';
import { autoProvision } from './actions';

interface ReferralPublicSummary {
  referralId:       string;
  clientFirstName:  string;
  clientLastName:   string;
  referrerName:     string;
  providerName:     string;
  requestedService: string;
  status:           string;
}

interface ActivationFormProps {
  summary:    ReferralPublicSummary;
  token:      string;
  referralId: string;
}

type ProvisionOutcome = 'provisioned' | 'alreadyActive' | 'fallback';

interface ProvisionState {
  outcome:  ProvisionOutcome;
  loginUrl: string | null;
  name:     string;
}

export function ActivationForm({ summary, token, referralId }: ActivationFormProps) {
  const [name,     setName]     = useState('');
  const [email,    setEmail]    = useState('');
  const [status,   setStatus]   = useState<'idle' | 'loading' | 'error'>('idle');
  const [error,    setError]    = useState('');
  const [provision, setProvision] = useState<ProvisionState | null>(null);
  const [, startTransition]    = useTransition();

  const clientName = [summary.clientFirstName, summary.clientLastName].filter(Boolean).join(' ');

  // CC2-INT-B05: land providers in the Common Portal after login, not the Tenant Portal.
  // Fallback login URL used if the auto-provision endpoint does not return one.
  const fallbackLoginUrl = `/login?returnTo=${encodeURIComponent(`/provider/referrals/${referralId}`)}&reason=referral-view`;

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!name.trim() || !email.trim()) {
      setError('Please enter your name and email address.');
      return;
    }
    setStatus('loading');
    setError('');

    startTransition(async () => {
      const result = await autoProvision(referralId, token, name.trim(), email.trim());

      if (result.error) {
        setStatus('error');
        setError(result.error);
        return;
      }

      const loginUrl = result.loginUrl ?? fallbackLoginUrl;

      if (result.success && !result.alreadyActive) {
        setProvision({ outcome: 'provisioned', loginUrl, name: name.trim() });
      } else if (result.success && result.alreadyActive) {
        setProvision({ outcome: 'alreadyActive', loginUrl, name: name.trim() });
      } else {
        setProvision({ outcome: 'fallback', loginUrl: null, name: name.trim() });
      }
    });
  }

  // ── Provisioned (happy path) ──────────────────────────────────────────────

  if (provision?.outcome === 'provisioned') {
    const loginUrl = provision.loginUrl ?? fallbackLoginUrl;
    return (
      <div className="bg-white rounded-xl shadow-sm border border-gray-200 overflow-hidden">
        <div className="h-1.5 w-full bg-green-400" />
        <div className="p-8 text-center">
          <div className="w-14 h-14 bg-green-100 rounded-full flex items-center justify-center mx-auto mb-4">
            <svg className="w-7 h-7 text-green-600" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 13l4 4L19 7" />
            </svg>
          </div>
          <h2 className="text-xl font-semibold text-gray-900 mb-2">Account Ready</h2>
          <p className="text-sm text-gray-500 leading-relaxed mb-4">
            Welcome, <strong>{provision.name}</strong>! Your provider account has been set up instantly.
          </p>
          <p className="text-sm text-gray-500 leading-relaxed mb-6">
            You can now log in to accept the referral
            {clientName ? ` for ${clientName}` : ''} directly from your dashboard.
          </p>
          <Link
            href={loginUrl}
            className="inline-block w-full bg-primary text-white text-sm font-medium py-2.5 rounded-lg hover:opacity-90 transition-opacity text-center"
          >
            Log In to Your Account
          </Link>
          <p className="text-xs text-gray-400 mt-4">
            If you have any questions, please contact the referring party directly.
          </p>
        </div>
      </div>
    );
  }

  // ── Already active (idempotent) ───────────────────────────────────────────

  if (provision?.outcome === 'alreadyActive') {
    const loginUrl = provision.loginUrl ?? fallbackLoginUrl;
    return (
      <div className="bg-white rounded-xl shadow-sm border border-gray-200 overflow-hidden">
        <div className="h-1.5 w-full bg-blue-400" />
        <div className="p-8 text-center">
          <div className="w-14 h-14 bg-blue-100 rounded-full flex items-center justify-center mx-auto mb-4">
            <svg className="w-7 h-7 text-blue-600" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2}
                d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" />
            </svg>
          </div>
          <h2 className="text-xl font-semibold text-gray-900 mb-2">Account Already Active</h2>
          <p className="text-sm text-gray-500 leading-relaxed mb-6">
            Your account is already set up. Log in to view the referral
            {clientName ? ` for ${clientName}` : ''}.
          </p>
          <Link
            href={loginUrl}
            className="inline-block w-full bg-primary text-white text-sm font-medium py-2.5 rounded-lg hover:opacity-90 transition-opacity text-center"
          >
            Log In
          </Link>
          <p className="text-xs text-gray-400 mt-4">
            If you have any questions, please contact the referring party directly.
          </p>
        </div>
      </div>
    );
  }

  // ── Fallback (manual queue, LSCC-009) ────────────────────────────────────

  if (provision?.outcome === 'fallback') {
    return (
      <div className="bg-white rounded-xl shadow-sm border border-gray-200 overflow-hidden">
        <div className="h-1.5 w-full bg-amber-400" />
        <div className="p-8 text-center">
          <div className="w-14 h-14 bg-amber-100 rounded-full flex items-center justify-center mx-auto mb-4">
            <svg className="w-7 h-7 text-amber-600" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2}
                d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z" />
            </svg>
          </div>
          <h2 className="text-xl font-semibold text-gray-900 mb-2">Activation Request Received</h2>
          <p className="text-sm text-gray-500 leading-relaxed mb-4">
            Thank you, <strong>{provision.name}</strong>. Your activation request has been submitted.
          </p>
          <p className="text-sm text-gray-500 leading-relaxed mb-6">
            A member of our team will set up your account and send you login details shortly.
            Once your account is active, you can log in and accept the referral
            {clientName ? ` for ${clientName}` : ''} directly from your dashboard.
          </p>
          <div className="bg-blue-50 border border-blue-200 rounded-lg p-4 text-sm text-blue-800 mb-6">
            <strong>Already have an account?</strong>{' '}
            <Link href={fallbackLoginUrl} className="text-primary hover:underline font-medium">
              Log in now
            </Link>{' '}
            to view this referral immediately.
          </div>
          <p className="text-xs text-gray-400">
            If you have any questions, please contact the referring party directly.
          </p>
        </div>
      </div>
    );
  }

  // ── Default form ──────────────────────────────────────────────────────────

  return (
    <form onSubmit={handleSubmit} className="bg-white rounded-xl shadow-sm border border-gray-200 px-6 py-5 space-y-4">
      <div>
        <h2 className="text-sm font-semibold text-gray-900 mb-1">Your details</h2>
        <p className="text-xs text-gray-500">
          We&apos;ll use these to set up your account instantly.
        </p>
      </div>

      <div>
        <label htmlFor="activate-name" className="block text-xs font-medium text-gray-700 mb-1">
          Full name <span className="text-red-500">*</span>
        </label>
        <input
          id="activate-name"
          type="text"
          required
          value={name}
          onChange={e => setName(e.target.value)}
          placeholder="Your full name"
          className="w-full border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary/30 focus:border-primary"
        />
      </div>

      <div>
        <label htmlFor="activate-email" className="block text-xs font-medium text-gray-700 mb-1">
          Email address <span className="text-red-500">*</span>
        </label>
        <input
          id="activate-email"
          type="email"
          required
          value={email}
          onChange={e => setEmail(e.target.value)}
          placeholder="you@yourpractice.com"
          className="w-full border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary/30 focus:border-primary"
        />
      </div>

      {error && (
        <div className="bg-red-50 border border-red-200 rounded-md px-3 py-2 text-xs text-red-700">
          {error}
        </div>
      )}

      <button
        type="submit"
        disabled={status === 'loading'}
        className="w-full bg-primary text-white text-sm font-medium py-2.5 rounded-lg hover:opacity-90 disabled:opacity-60 transition-opacity"
      >
        {status === 'loading' ? 'Setting up your account…' : 'Activate My Account'}
      </button>

      <p className="text-xs text-gray-400 text-center">
        Already have an account?{' '}
        <Link href={fallbackLoginUrl} className="text-primary hover:underline">
          Log in instead
        </Link>
      </p>
    </form>
  );
}
