'use client';

import { useState, useRef } from 'react';

type Status = 'idle' | 'loading' | 'success' | 'error';

export function ChangePasswordForm() {
  const [status, setStatus]   = useState<Status>('idle');
  const [message, setMessage] = useState('');
  const formRef               = useRef<HTMLFormElement>(null);

  async function handleSubmit(e: React.FormEvent<HTMLFormElement>) {
    e.preventDefault();
    setStatus('loading');
    setMessage('');

    const data = new FormData(e.currentTarget);
    const currentPassword = data.get('currentPassword') as string;
    const newPassword     = data.get('newPassword')     as string;
    const confirmPassword = data.get('confirmPassword') as string;

    if (!currentPassword || !newPassword || !confirmPassword) {
      setStatus('error');
      setMessage('All fields are required.');
      return;
    }

    if (newPassword.length < 8) {
      setStatus('error');
      setMessage('New password must be at least 8 characters.');
      return;
    }

    if (newPassword !== confirmPassword) {
      setStatus('error');
      setMessage('New password and confirmation do not match.');
      return;
    }

    if (currentPassword === newPassword) {
      setStatus('error');
      setMessage('New password must differ from the current password.');
      return;
    }

    try {
      const res = await fetch('/api/auth/change-password', {
        method:      'POST',
        credentials: 'include',
        headers:     { 'Content-Type': 'application/json' },
        body:        JSON.stringify({ currentPassword, newPassword }),
      });

      if (res.ok) {
        setStatus('success');
        setMessage('Password changed successfully.');
        formRef.current?.reset();
      } else {
        const body = await res.json().catch(() => ({}));
        const detail =
          body?.error ??
          body?.title ??
          body?.detail ??
          body?.message ??
          'Failed to change password. Please try again.';
        setStatus('error');
        setMessage(detail);
      }
    } catch {
      setStatus('error');
      setMessage('Network error. Please check your connection and try again.');
    }
  }

  return (
    <form ref={formRef} onSubmit={handleSubmit} className="space-y-5">

      {/* Current password */}
      <div>
        <label htmlFor="currentPassword" className="block text-sm font-medium text-gray-700 mb-1">
          Current password
        </label>
        <input
          id="currentPassword"
          name="currentPassword"
          type="password"
          autoComplete="current-password"
          required
          disabled={status === 'loading'}
          className="w-full h-10 rounded-lg border border-gray-300 px-3 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500 disabled:bg-gray-50 disabled:text-gray-400"
          placeholder="Enter your current password"
        />
      </div>

      {/* Divider */}
      <div className="border-t border-gray-100" />

      {/* New password */}
      <div>
        <label htmlFor="newPassword" className="block text-sm font-medium text-gray-700 mb-1">
          New password
        </label>
        <input
          id="newPassword"
          name="newPassword"
          type="password"
          autoComplete="new-password"
          required
          minLength={8}
          disabled={status === 'loading'}
          className="w-full h-10 rounded-lg border border-gray-300 px-3 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500 disabled:bg-gray-50 disabled:text-gray-400"
          placeholder="At least 8 characters"
        />
      </div>

      {/* Confirm new password */}
      <div>
        <label htmlFor="confirmPassword" className="block text-sm font-medium text-gray-700 mb-1">
          Confirm new password
        </label>
        <input
          id="confirmPassword"
          name="confirmPassword"
          type="password"
          autoComplete="new-password"
          required
          disabled={status === 'loading'}
          className="w-full h-10 rounded-lg border border-gray-300 px-3 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500 disabled:bg-gray-50 disabled:text-gray-400"
          placeholder="Repeat your new password"
        />
      </div>

      {/* Feedback banner */}
      {message && (
        <div
          className={[
            'rounded-lg px-4 py-3 text-sm',
            status === 'success'
              ? 'bg-green-50 border border-green-200 text-green-800'
              : 'bg-red-50   border border-red-200   text-red-700',
          ].join(' ')}
        >
          {status === 'success' && <i className="ri-checkbox-circle-line mr-1.5" />}
          {status === 'error'   && <i className="ri-error-warning-line mr-1.5" />}
          {message}
        </div>
      )}

      {/* Submit */}
      <div className="pt-1">
        <button
          type="submit"
          disabled={status === 'loading'}
          className="inline-flex items-center gap-2 px-5 py-2.5 text-sm font-medium text-white bg-indigo-600 hover:bg-indigo-700 disabled:bg-indigo-400 rounded-lg transition-colors"
        >
          {status === 'loading' ? (
            <>
              <span className="w-3.5 h-3.5 border-2 border-white/40 border-t-white rounded-full animate-spin" />
              Saving…
            </>
          ) : (
            'Update password'
          )}
        </button>
      </div>

    </form>
  );
}
