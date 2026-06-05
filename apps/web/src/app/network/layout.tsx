/**
 * CC2-INT-B07 — Public network surface layout.
 *
 * Minimal, unauthenticated shell. No sidebar, no session requirement.
 * Renders cleanly for prospective clients browsing a tenant's provider network.
 */
import type { Metadata } from 'next';

export const dynamic = 'force-dynamic';


export const metadata: Metadata = {
  title: 'Provider Network',
};

export default function PublicNetworkLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return (
    <div className="min-h-screen bg-gray-50 flex flex-col">
      <header className="bg-white border-b border-gray-200 sticky top-0 z-30">
        <div className="max-w-5xl mx-auto px-4 h-14 flex items-center gap-3">
          <span className="text-sm font-semibold text-gray-900">LegalSynq</span>
          <span className="text-gray-300 select-none">|</span>
          <span className="text-sm text-gray-500">Provider Network</span>
        </div>
      </header>

      <main className="flex-1 max-w-5xl mx-auto w-full px-4 py-6">
        {children}
      </main>

      <footer className="border-t border-gray-100 bg-white py-4">
        <p className="text-center text-xs text-gray-400">
          LegalSynq CareConnect &mdash; Provider Directory
        </p>
      </footer>
    </div>
  );
}
