'use client';

import { Suspense, useState, useEffect } from 'react';
import Image from 'next/image';
import { LoginForm } from './login-form';
import { useTenantBranding } from '@/providers/tenant-branding-provider';

// ── Tenant-branded logo (right panel) ─────────────────────────────────────────

function TenantLogo() {
  const branding = useTenantBranding();

  // Build source list in priority order. Mirror the top-bar strategy so the
  // login page shows whichever logo variant is actually available.
  // logoDocumentId  = standard logo (dark, good on light background — try first)
  // logoWhiteDocumentId = white variant (needs dark pill wrapper)
  // logoUrl         = direct CDN URL
  const sources: Array<{ url: string; isWhite: boolean }> = [];
  if (branding.logoDocumentId)
    sources.push({ url: `/api/branding/logo/${branding.logoDocumentId}`, isWhite: false });
  if (branding.logoWhiteDocumentId)
    sources.push({ url: `/api/branding/logo/${branding.logoWhiteDocumentId}`, isWhite: true });
  if (branding.logoUrl)
    sources.push({ url: branding.logoUrl, isWhite: false });

  const [srcIndex, setSrcIndex] = useState(0);
  const [exhausted, setExhausted] = useState(false);

  const sourcesKey = sources.map(s => s.url).join('|');
  useEffect(() => {
    setSrcIndex(0);
    setExhausted(false);
  }, [sourcesKey]);

  if (sources.length === 0 || exhausted) return null;

  function handleError() {
    const next = srcIndex + 1;
    if (next < sources.length) {
      setSrcIndex(next);
    } else {
      setExhausted(true);
    }
  }

  const current = sources[srcIndex];

  return (
    <div className="mb-6">
      {/* White-variant logos are invisible on the light login background —
          wrap them in a small dark pill so they remain legible. */}
      <div
        className={current?.isWhite
          ? 'inline-flex items-center justify-center px-4 py-2 rounded-lg'
          : undefined}
        style={current?.isWhite ? { backgroundColor: '#0f1928' } : undefined}
      >
        <img
          src={current?.url}
          alt={branding.displayName || 'Organization logo'}
          className="max-h-14 max-w-[200px] object-contain"
          onError={handleError}
        />
      </div>
    </div>
  );
}

// ── LegalSynq left-panel highlights ───────────────────────────────────────────

const LS_HIGHLIGHTS = [
  {
    icon: 'ri-scales-3-line',
    text: 'Coordinate providers, referrals, and case workflows in one place',
  },
  {
    icon: 'ri-hospital-line',
    text: 'Built for law firms, medical providers, and operations teams',
  },
  {
    icon: 'ri-money-dollar-circle-line',
    text: 'Streamline lien management, funding, and settlement tracking',
  },
  {
    icon: 'ri-shield-check-line',
    text: 'Secure, auditable, and operationally efficient',
  },
];

// ── CareConnect left-panel highlights ─────────────────────────────────────────

const CC_HIGHLIGHTS = [
  {
    icon: 'ri-heart-pulse-line',
    text: 'Receive and manage referrals from law firms and case managers',
  },
  {
    icon: 'ri-calendar-check-line',
    text: 'Track appointment scheduling, treatment status, and outcomes',
  },
  {
    icon: 'ri-shield-check-line',
    text: 'Secure, compliant communication across the care network',
  },
  {
    icon: 'ri-team-line',
    text: 'One portal for providers, specialists, and law firm partners',
  },
];

// ── Main export ────────────────────────────────────────────────────────────────

export function LoginPageClient({ isPortal }: { isPortal: boolean }) {
  const [year, setYear] = useState<number | null>(null);
  useEffect(() => { setYear(new Date().getFullYear()); }, []);

  if (isPortal) {
    return <CareConnectLoginLayout year={year} />;
  }

  return <LegalSynqLoginLayout year={year} />;
}

// ── CareConnect layout ─────────────────────────────────────────────────────────

function CareConnectLoginLayout({ year }: { year: number | null }) {
  // Deep teal-blue palette — healthcare / CareConnect brand
  const bg      = '#0c4a6e';
  const accent  = '#38bdf8';
  const accentA = 'rgba(56,189,248,0.12)';
  const accentB = 'rgba(56,189,248,0.04)';

  return (
    <div className="min-h-screen flex flex-col lg:flex-row">

      {/* ── Left panel — CareConnect branded ──────────────────────────────── */}
      <div
        className="hidden lg:flex lg:w-[45%] xl:w-[42%] flex-col p-10 xl:p-14 relative overflow-hidden"
        style={{ backgroundColor: bg }}
      >
        {/* Subtle background texture rings */}
        <div
          className="absolute -bottom-40 -left-40 w-[520px] h-[520px] rounded-full opacity-[0.05]"
          style={{ border: `80px solid ${accent}` }}
          aria-hidden
        />
        <div
          className="absolute -top-24 -right-24 w-[320px] h-[320px] rounded-full opacity-[0.04]"
          style={{ border: `60px solid ${accent}` }}
          aria-hidden
        />

        {/* Logo */}
        <div className="relative z-10 mb-auto">
          <Image
            src="/careconnect-logo.png"
            alt="CareConnect"
            width={300}
            height={66}
            priority
            unoptimized
            className="w-full max-w-[300px] h-auto"
            data-testid="cc-desktop-logo"
          />
        </div>

        {/* Hero copy */}
        <div className="relative z-10 py-12">
          <div
            className="w-10 h-0.5 mb-6 rounded-full"
            style={{ backgroundColor: accent }}
          />

          <h2 className="text-3xl xl:text-4xl font-bold text-white leading-tight tracking-tight mb-4">
            Your referral network,<br />all in one place
          </h2>

          <p className="text-[15px] leading-relaxed mb-10 max-w-xs" style={{ color: 'rgba(186,230,253,0.8)' }}>
            For medical providers, specialists, and law firm partners coordinating care
          </p>

          <ul className="space-y-5">
            {CC_HIGHLIGHTS.map(({ icon, text }) => (
              <li key={text} className="flex items-start gap-3">
                <span
                  className="shrink-0 w-7 h-7 rounded-lg flex items-center justify-center mt-0.5"
                  style={{ backgroundColor: accentA }}
                >
                  <i className={`${icon} text-[14px]`} style={{ color: accent }} />
                </span>
                <span className="text-[13px] leading-snug" style={{ color: 'rgba(186,230,253,0.85)' }}>
                  {text}
                </span>
              </li>
            ))}
          </ul>
        </div>

        {/* Footer */}
        <div className="relative z-10 pt-6 border-t" style={{ borderColor: 'rgba(255,255,255,0.08)' }}>
          <div className="flex items-center gap-3">
            <p className="text-[11px]" style={{ color: 'rgba(186,230,253,0.4)' }} suppressHydrationWarning>
              &copy; {year ?? ''} LegalSynq CareConnect
            </p>
            <span className="text-[10px]" style={{ color: 'rgba(186,230,253,0.2)' }}>&bull;</span>
            <a href="/privacy-policy" className="text-[11px] transition-colors" style={{ color: 'rgba(186,230,253,0.4)' }}>
              Privacy Policy
            </a>
          </div>
        </div>
      </div>

      {/* ── Right panel — login form ───────────────────────────────────────── */}
      <div className="flex-1 flex flex-col items-center justify-center min-h-screen lg:min-h-0 px-6 py-12 bg-gray-50">

        <div className="w-full max-w-sm">
          <div className="mb-8">
            <h1 className="text-2xl font-bold text-gray-900 tracking-tight">Welcome back</h1>
            <p className="mt-1.5 text-sm text-gray-500">Sign in to your CareConnect portal</p>
          </div>

          <Suspense fallback={null}>
            <LoginForm />
          </Suspense>

          <p className="mt-6 text-center text-xs text-gray-400">
            Need access?{' '}
            <a
              href="mailto:support@legalsynq.com"
              className="text-gray-600 hover:text-gray-900 underline underline-offset-2 transition-colors"
            >
              Contact support
            </a>
          </p>
        </div>
      </div>

    </div>
  );
}

// ── LegalSynq layout (unchanged) ──────────────────────────────────────────────

function LegalSynqLoginLayout({ year }: { year: number | null }) {
  return (
    <div className="min-h-screen flex flex-col lg:flex-row">

      {/* ── Left panel — branded ──────────────────────────────────────────── */}
      <div
        className="hidden lg:flex lg:w-[45%] xl:w-[42%] flex-col p-10 xl:p-14 relative overflow-hidden"
        style={{ backgroundColor: '#0f1928' }}
      >
        {/* Subtle background texture ring */}
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

        {/* Logo */}
        <div className="relative z-10 mb-auto">
          <Image
            src="/legalsynq-logo-white.png"
            alt="LegalSynq"
            width={220}
            height={52}
            priority
            unoptimized
            className="h-12 w-auto"
            data-testid="ls-desktop-logo"
          />
        </div>

        {/* Hero copy */}
        <div className="relative z-10 py-12">
          <div
            className="w-10 h-0.5 mb-6 rounded-full"
            style={{ backgroundColor: '#f97316' }}
          />

          <h2 className="text-3xl xl:text-4xl font-bold text-white leading-tight tracking-tight mb-4">
            Synchronizing legal &amp; medical workflows
          </h2>

          <p className="text-[15px] text-slate-400 leading-relaxed mb-10 max-w-xs">
            For law firms, medical providers, lien owners, and case managers
          </p>

          <ul className="space-y-5">
            {LS_HIGHLIGHTS.map(({ icon, text }) => (
              <li key={text} className="flex items-start gap-3">
                <span
                  className="shrink-0 w-7 h-7 rounded-lg flex items-center justify-center mt-0.5"
                  style={{ backgroundColor: 'rgba(249,115,22,0.12)' }}
                >
                  <i className={`${icon} text-[14px]`} style={{ color: '#f97316' }} />
                </span>
                <span className="text-[13px] text-slate-300 leading-snug">{text}</span>
              </li>
            ))}
          </ul>
        </div>

        {/* Footer */}
        <div className="relative z-10 pt-6 border-t" style={{ borderColor: 'rgba(255,255,255,0.08)' }}>
          <div className="flex items-center gap-3">
            <p className="text-[11px] text-slate-500" suppressHydrationWarning>
              &copy; {year ?? ''} LegalSynq
            </p>
            <span className="text-slate-700 text-[10px]">&bull;</span>
            <a href="/privacy-policy" className="text-[11px] text-slate-600 hover:text-slate-400 transition-colors">
              Privacy Policy
            </a>
            <span className="text-slate-700 text-[10px]">&bull;</span>
            <a href="/terms" className="text-[11px] text-slate-600 hover:text-slate-400 transition-colors">
              Terms &amp; Conditions
            </a>
          </div>
        </div>
      </div>

      {/* ── Right panel — login form ──────────────────────────────────────── */}
      <div className="flex-1 flex flex-col items-center justify-center min-h-screen lg:min-h-0 px-6 py-12 bg-gray-50">

        {/* Mobile-only logo */}
        <div className="lg:hidden mb-10" data-testid="ls-mobile-logo-wrap">
          <Image
            src="/legalsynq-logo.png"
            alt="LegalSynq"
            width={140}
            height={34}
            priority
            unoptimized
            className="h-8 w-auto mx-auto"
            data-testid="ls-mobile-logo"
          />
        </div>

        <div className="w-full max-w-sm">
          <TenantLogo />

          {/* Heading */}
          <div className="mb-8">
            <h1 className="text-2xl font-bold text-gray-900 tracking-tight">Welcome back</h1>
            <p className="mt-1.5 text-sm text-gray-500">Sign in to your LegalSynq account</p>
          </div>

          <Suspense fallback={null}>
            <LoginForm />
          </Suspense>

          {/* Footer links */}
          <p className="mt-6 text-center text-xs text-gray-400">
            Need access?{' '}
            <a
              href="mailto:support@legalsynq.com"
              className="text-gray-600 hover:text-gray-900 underline underline-offset-2 transition-colors"
            >
              Contact support
            </a>
          </p>
        </div>
      </div>

    </div>
  );
}
