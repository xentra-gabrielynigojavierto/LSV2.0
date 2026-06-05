'use client';

import { Suspense } from 'react';
import Image from 'next/image';
import { ForgotPasswordForm } from './forgot-password-form';

export const dynamic = 'force-dynamic';


export default function ForgotPasswordPage() {
  return (
    <div className="min-h-screen flex flex-col lg:flex-row">

      <div
        className="hidden lg:flex lg:w-[45%] xl:w-[42%] flex-col p-10 xl:p-14 relative overflow-hidden"
        style={{ backgroundColor: '#0f1928' }}
      >
        <div
          className="absolute -bottom-40 -left-40 w-[520px] h-[520px] rounded-full opacity-[0.04]"
          style={{ border: '80px solid #f97316' }}
          aria-hidden
        />
        <div
          className="absolute -top-24 -right-24 w-[320px] h-[320px] rounded-full opacity-[0.03]"
          style={{ border: '60px solid #f97316' }}
          aria-hidden
        />

        <div className="relative z-10 mb-auto">
          <Image
            src="/legalsynq-logo-white.png"
            alt="LegalSynq"
            width={220}
            height={52}
            priority
            unoptimized
            className="h-12 w-auto"
          />
        </div>

        <div className="relative z-10 py-12">
          <div
            className="w-10 h-0.5 mb-6 rounded-full"
            style={{ backgroundColor: '#f97316' }}
          />
          <h2 className="text-3xl xl:text-4xl font-bold text-white leading-tight tracking-tight mb-4">
            Reset your password
          </h2>
          <p className="text-[15px] text-slate-400 leading-relaxed max-w-xs">
            Enter your email address and we&apos;ll help you get back into your account
          </p>
        </div>

        <div className="relative z-10 pt-6 border-t" style={{ borderColor: 'rgba(255,255,255,0.08)' }}>
          <div className="flex items-center gap-3">
            <p className="text-[11px] text-slate-500">
              &copy; {new Date().getFullYear()} LegalSynq
            </p>
          </div>
        </div>
      </div>

      <div className="flex-1 flex flex-col items-center justify-center min-h-screen lg:min-h-0 px-6 py-12 bg-gray-50">

        <div className="lg:hidden mb-10">
          <Image
            src="/legalsynq-logo.png"
            alt="LegalSynq"
            width={140}
            height={34}
            priority
            unoptimized
            className="h-8 w-auto mx-auto"
          />
        </div>

        <div className="w-full max-w-sm">
          <div className="mb-8">
            <h1 className="text-2xl font-bold text-gray-900 tracking-tight">Forgot password?</h1>
            <p className="mt-1.5 text-sm text-gray-500">
              Enter your email address to receive a password reset link
            </p>
          </div>

          <Suspense fallback={null}>
            <ForgotPasswordForm />
          </Suspense>

          <p className="mt-6 text-center text-xs text-gray-400">
            <a
              href="/login"
              className="text-gray-600 hover:text-gray-900 underline underline-offset-2 transition-colors"
            >
              Back to sign in
            </a>
          </p>
        </div>
      </div>

    </div>
  );
}
