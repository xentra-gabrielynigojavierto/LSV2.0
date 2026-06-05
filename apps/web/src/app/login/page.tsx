import { headers } from 'next/headers';
import { LoginPageClient } from './login-page-client';

export const dynamic = 'force-dynamic';

/**
 * Login page — server component.
 *
 * Reads the incoming hostname via the x-forwarded-host (proxy) or host header
 * to determine whether the visitor is on the CareConnect common portal domain.
 * Passes `isPortal` to the client layout so it can render CareConnect branding
 * without a client-side flash or extra round-trips.
 */
export default async function LoginPage() {
  const hdrs = await headers();
  const forwardedHost = hdrs.get('x-forwarded-host') ?? '';
  const rawHost       = hdrs.get('host') ?? '';
  const hostname      = (forwardedHost || rawHost).split(':')[0].toLowerCase();

  const portalHostname = (process.env.CC_COMMON_PORTAL_HOSTNAME ?? '').trim().toLowerCase();
  const isPortal = !!portalHostname && hostname === portalHostname;

  return <LoginPageClient isPortal={isPortal} />;
}
