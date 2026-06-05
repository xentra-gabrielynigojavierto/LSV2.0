/**
 * CC2-INT-B05 — Common Portal root layout.
 *
 * Serves Provider and Law Firm users accessing CareConnect referrals.
 * Distinct from the Tenant Portal layout — no tenant sidebar, simpler nav.
 *
 * Auth: Server-side session validation via requireExternalPortal().
 * JWT is NEVER exposed to the browser; HttpOnly platform_session cookie only.
 */

import Link from 'next/link';
import { requireExternalPortal } from '@/lib/auth-guards';
import { OrgType } from '@/types';

export const dynamic = 'force-dynamic';

export const metadata = { title: 'CareConnect Portal' };

export default async function CommonPortalLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  const session = await requireExternalPortal();

  const orgLabel =
    session.orgType === OrgType.LawFirm ? 'Law Firm Portal' : 'Provider Portal';

  return (
    <div className="min-h-screen bg-gray-50 flex flex-col">
      {/* Top navigation bar */}
      <header className="bg-white border-b border-gray-200 sticky top-0 z-30">
        <div className="max-w-5xl mx-auto px-4 h-14 flex items-center justify-between gap-4">
          {/* Brand + portal label */}
          <div className="flex items-center gap-3">
            {/* CareConnect wordmark */}
            <Link
              href="/provider/dashboard"
              className="flex items-center gap-2 group"
            >
              <span
                className="w-6 h-6 rounded-md flex items-center justify-center shrink-0 transition-colors"
                style={{ backgroundColor: '#e0f2fe' }}
              >
                <i className="ri-heart-pulse-line text-[13px]" style={{ color: '#0284c7' }} />
              </span>
              <span className="text-sm font-semibold text-gray-900 group-hover:text-sky-700 transition-colors">
                CareConnect
              </span>
            </Link>
            <span className="text-gray-300 select-none">|</span>
            <span className="text-sm text-gray-500">{orgLabel}</span>
          </div>

          {/* Nav links */}
          <nav className="flex items-center gap-1">
            <Link
              href="/provider/dashboard"
              className="text-sm font-medium text-gray-600 hover:text-gray-900 px-3 py-1.5 rounded-md hover:bg-gray-100 transition-colors"
            >
              Referrals
            </Link>
          </nav>

          {/* User info + sign out */}
          <div className="flex items-center gap-4">
            <span className="text-xs text-gray-500 hidden sm:block truncate max-w-[200px]">
              {session.email}
            </span>
            <Link
              href="/api/auth/logout"
              className="text-xs text-gray-500 hover:text-gray-800 transition-colors"
            >
              Sign out
            </Link>
          </div>
        </div>
      </header>

      {/* Page content */}
      <main className="flex-1 max-w-5xl mx-auto w-full px-4 py-6">
        {children}
      </main>

      {/* Footer */}
      <footer className="border-t border-gray-100 bg-white py-4">
        <p className="text-center text-xs text-gray-400">
          CareConnect &mdash; Secure Provider Portal
        </p>
      </footer>
    </div>
  );
}
