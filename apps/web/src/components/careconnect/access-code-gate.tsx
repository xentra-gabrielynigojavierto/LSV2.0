'use client';

import { useState, useEffect, type FormEvent, type ReactNode } from 'react';
import Image from 'next/image';

const ACCESS_CODE       = 'Password123';
const SESSION_KEY       = 'cc-network-unlocked';

interface Props {
  children: ReactNode;
}

export function AccessCodeGate({ children }: Props) {
  const [unlocked,  setUnlocked]  = useState(false);
  const [ready,     setReady]     = useState(false);
  const [code,      setCode]      = useState('');
  const [error,     setError]     = useState('');
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    if (sessionStorage.getItem(SESSION_KEY) === '1') setUnlocked(true);
    setReady(true);
  }, []);

  function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setSubmitting(true);
    if (code === ACCESS_CODE) {
      sessionStorage.setItem(SESSION_KEY, '1');
      setError('');
      setUnlocked(true);
    } else {
      setError('Incorrect access code. Please try again.');
      setCode('');
    }
    setSubmitting(false);
  }

  if (!ready) return null;

  return (
    <div className="relative h-full w-full">
      {/* Page content — blurred when locked */}
      <div
        className={unlocked ? 'h-full w-full' : 'h-full w-full blur-sm pointer-events-none select-none'}
        aria-hidden={!unlocked}
      >
        {children}
      </div>

      {/* Access-code modal */}
      {!unlocked && (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center"
          style={{ background: 'rgba(10, 16, 30, 0.55)' }}
        >
          <div className="bg-white rounded-2xl shadow-2xl w-full max-w-sm mx-4 overflow-hidden">
            {/* Header strip */}
            <div className="bg-gradient-to-r from-slate-900 to-slate-800 px-8 py-6 flex flex-col items-center gap-3">
              <Image
                src="/careconnect-logo.png"
                alt="CareConnect"
                width={140}
                height={36}
                style={{ width: 140, height: 'auto' }}
                className="object-contain"
                priority
              />
              <p className="text-slate-300 text-xs tracking-wide text-center">
                Provider Network
              </p>
            </div>

            {/* Body */}
            <form onSubmit={handleSubmit} className="px-8 py-7 flex flex-col gap-5">
              <div>
                <h2 className="text-base font-semibold text-gray-900 text-center">
                  Enter Access Code
                </h2>
                <p className="text-xs text-gray-500 text-center mt-1">
                  This directory is protected. Please enter the access code provided by your network coordinator.
                </p>
              </div>

              <div className="flex flex-col gap-1.5">
                <label htmlFor="cc-access-code" className="text-xs font-medium text-gray-700">
                  Access Code
                </label>
                <input
                  id="cc-access-code"
                  type="password"
                  autoFocus
                  autoComplete="off"
                  value={code}
                  onChange={e => { setCode(e.target.value); setError(''); }}
                  placeholder="Enter access code"
                  className="w-full rounded-lg border border-gray-300 px-3.5 py-2.5 text-sm text-gray-900 placeholder-gray-400 shadow-sm focus:outline-none focus:ring-2 focus:ring-orange-500 focus:border-transparent transition"
                />
                {error && (
                  <p className="text-xs text-red-600 flex items-center gap-1 mt-0.5">
                    <i className="ri-error-warning-line" />
                    {error}
                  </p>
                )}
              </div>

              <button
                type="submit"
                disabled={!code.trim() || submitting}
                className="w-full rounded-lg bg-orange-500 hover:bg-orange-600 disabled:bg-orange-300 text-white text-sm font-semibold py-2.5 transition-colors focus:outline-none focus:ring-2 focus:ring-orange-500 focus:ring-offset-2"
              >
                Unlock Directory
              </button>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}
