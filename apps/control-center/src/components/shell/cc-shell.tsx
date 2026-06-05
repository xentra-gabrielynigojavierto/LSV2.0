import type { ReactNode } from 'react';
import Image from 'next/image';
import Link from 'next/link';
import { SignOutButton } from './sign-out-button';
import { CCSidebar } from './cc-sidebar';
import { TenantContextBanner } from '@/components/layout/tenant-context-banner';
import { ImpersonationBanner } from '@/components/layout/impersonation-banner';
import { getTenantContext, getImpersonation } from '@/lib/auth';
import { AnalyticsProvider } from '@/components/analytics/analytics-provider';
import { getServerSession } from '@/lib/session';

interface CCShellProps {
  children:  ReactNode;
  userEmail: string;
}

/**
 * Control Center shell — wraps every authenticated CC page.
 *
 * Layout:
 *   [navy top bar — full width: logo + "Control Center" badge + user area]
 *   [rose ImpersonationBanner  — only when impersonating a user]
 *   [amber TenantContextBanner — only when a tenant context is active]
 *   [light sidebar + scrollable main content]
 */
export async function CCShell({ children, userEmail }: CCShellProps) {
  const tenantCtx     = await getTenantContext();
  const impersonation = await getImpersonation();
  const session       = await getServerSession();
  const avatarDocId   = session?.avatarDocumentId;

  return (
    <div className="flex flex-col h-screen overflow-hidden">
      {/* ── Navy top bar ────────────────────────────────────────────────────── */}
      <header
        className="h-14 flex items-stretch px-5 shrink-0 gap-4"
        style={{ backgroundColor: '#0f1928' }}
      >
        {/* Logo */}
        <Link href="/tenants" className="flex items-center shrink-0 mr-2">
          <Image
            src="/legalsynq-logo-white.png"
            alt="LegalSynq"
            width={130}
            height={32}
            priority
            unoptimized
            className="h-7 w-auto"
          />
        </Link>

        {/* Divider */}
        <div className="self-center h-5 w-px shrink-0" style={{ backgroundColor: 'rgba(255,255,255,0.12)' }} />

        {/* Control Center badge */}
        <div className="flex items-center">
          <div className="flex items-center gap-1.5 px-2.5 py-1 rounded-md" style={{ backgroundColor: 'rgba(249,115,22,0.15)', border: '1px solid rgba(249,115,22,0.3)' }}>
            <i className="ri-shield-star-line text-sm" style={{ color: '#f97316' }} />
            <span className="text-xs font-semibold tracking-wide uppercase" style={{ color: '#f97316' }}>
              Control Center
            </span>
          </div>
        </div>

        <div className="flex-1" />

        {/* User area */}
        <div className="flex items-center gap-3 shrink-0">
          <Link href="/profile" title="My profile" className="shrink-0">
            {avatarDocId ? (
              <img
                src={`/api/profile/avatar/${avatarDocId}`}
                alt="Profile"
                className="w-7 h-7 rounded-full object-cover hover:ring-2 hover:ring-orange-400 transition-all"
              />
            ) : (
              <div
                className="w-7 h-7 rounded-full flex items-center justify-center hover:ring-2 hover:ring-orange-400 transition-all"
                style={{ backgroundColor: 'rgba(255,255,255,0.1)' }}
              >
                <i className="ri-user-3-line text-[13px] text-slate-300" />
              </div>
            )}
          </Link>
          <span className="hidden sm:block text-xs text-slate-300">{userEmail}</span>
          <div className="h-4 w-px" style={{ backgroundColor: 'rgba(255,255,255,0.12)' }} />
          <SignOutButton />
        </div>
      </header>

      {/* Impersonation banner — rose/red */}
      {impersonation && <ImpersonationBanner session={impersonation} />}

      {/* Tenant context banner — amber */}
      {tenantCtx && <TenantContextBanner context={tenantCtx} />}

      {/* ── Body ────────────────────────────────────────────────────────────── */}
      <div className="flex flex-1 overflow-hidden">
        <CCSidebar />
        <AnalyticsProvider>
          <main className="flex-1 overflow-y-auto bg-gray-50 p-6">
            {children}
          </main>
        </AnalyticsProvider>
      </div>
    </div>
  );
}
