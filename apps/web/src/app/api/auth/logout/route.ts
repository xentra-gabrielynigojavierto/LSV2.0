import { type NextRequest, NextResponse } from 'next/server';

const GATEWAY_URL = process.env.GATEWAY_URL ?? 'http://127.0.0.1:5000';
const IS_PROD     = process.env.NODE_ENV === 'production';

/**
 * BFF Logout route — POST /api/auth/logout
 *
 * Flow:
 *   1. Optionally notify the backend (POST /identity/api/auth/logout)
 *      — backend is stateless JWT so this is a no-op for now, but future
 *        refresh-token revocation would hook in here
 *   2. Delete the platform_session cookie by setting Max-Age=0
 *   3. Return 200
 *
 * Cookie deletion: must match the exact same attributes used at cookie-set time
 * (path, sameSite, secure, httpOnly) or the browser won't delete it.
 */
export async function POST(request: NextRequest) {
  const token = request.cookies.get('platform_session')?.value;

  // Attempt backend logout (fire-and-forget; don't block on failure)
  if (token) {
    fetch(`${GATEWAY_URL}/identity/api/auth/logout`, {
      method:  'POST',
      headers: { 'Authorization': `Bearer ${token}` },
    }).catch(() => { /* ignore — backend logout is best-effort */ });
  }

  const response = NextResponse.json({ ok: true }, { status: 200 });

  // Delete the cookies by setting maxAge to 0 and matching original attributes
  response.cookies.set('platform_session', '', {
    httpOnly: true,
    secure:   IS_PROD,
    sameSite: IS_PROD ? 'strict' : 'lax',
    path:     '/',
    maxAge:   0,
  });

  // NOTE: tenant_code cookie is intentionally NOT cleared on logout.
  // It is non-sensitive (stores only the tenant code, e.g. "MANER") and
  // keeping it allows the login page to display the correct tenant branding
  // for returning users without requiring subdomain DNS resolution.

  return response;
}
