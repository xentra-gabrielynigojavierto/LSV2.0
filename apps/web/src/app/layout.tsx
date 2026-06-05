import type { Metadata } from 'next';
import './globals.css';
import { TenantBrandingProvider } from '@/providers/tenant-branding-provider';
import { SessionProvider, type SerializableSession } from '@/providers/session-provider';
import { ProviderModeProvider } from '@/providers/provider-mode-provider';
import { getServerSession } from '@/lib/session';

export const dynamic = 'force-dynamic';

export const metadata: Metadata = {
  title: 'LegalSynq',
  description: 'LegalSynq Platform',
};

/**
 * Root layout — wraps the entire app in:
 *   1. TenantBrandingProvider (anonymous, loaded before auth)
 *   2. SessionProvider       (seeded with the SSR session, no client-side loading gap)
 *
 * getServerSession() is called once here at the root so the SessionProvider
 * starts pre-populated.  Unauthenticated pages (e.g. /login) receive null,
 * which triggers the normal client-side fetch on mount.
 *
 * Provider order matters: branding must load first so the login page
 * shows the correct tenant logo before the user is authenticated.
 */
export default async function RootLayout({ children }: { children: React.ReactNode }) {
  const session = await getServerSession();

  // PlatformSession.expiresAt is a Date — not serializable across the RSC boundary.
  // Convert to an ISO string so it can be safely passed as a prop to the client provider.
  const initialSession: SerializableSession | null = session
    ? { ...session, expiresAt: session.expiresAt.toISOString() }
    : null;

  return (
    <html lang="en">
      <body className="antialiased">
        <TenantBrandingProvider>
          <SessionProvider initialSession={initialSession}>
            <ProviderModeProvider>
              {children}
            </ProviderModeProvider>
          </SessionProvider>
        </TenantBrandingProvider>
      </body>
    </html>
  );
}
