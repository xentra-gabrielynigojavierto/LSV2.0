import { type NextRequest, NextResponse } from 'next/server';

const GATEWAY_URL = process.env.GATEWAY_URL ?? 'http://127.0.0.1:5000';

/**
 * BFF change-password route — POST /api/auth/change-password
 *
 * Flow:
 *   1. Read the platform_session HttpOnly cookie.
 *   2. If absent → 401.
 *   3. Forward the request body to POST ${GATEWAY_URL}/identity/api/auth/change-password
 *      with Authorization: Bearer <token>.
 *   4. Return the identity service response to the client, distinguishing genuine
 *      4xx user-facing errors from 5xx upstream failures.
 *
 * The raw JWT never leaves the server. The browser only sends { currentPassword, newPassword }.
 */
export async function POST(request: NextRequest) {
  const token = request.cookies.get('platform_session')?.value;

  if (!token) {
    return NextResponse.json({ error: 'Not authenticated' }, { status: 401 });
  }

  let body: string;
  try { body = await request.text(); } catch { body = '{}'; }

  let identityRes: Response;
  try {
    identityRes = await fetch(`${GATEWAY_URL}/identity/api/auth/change-password`, {
      method:  'POST',
      headers: {
        'Authorization': `Bearer ${token}`,
        'Content-Type':  'application/json',
      },
      body,
    });
  } catch (err) {
    console.error(`[change-password] Identity service fetch error:`, err);
    return NextResponse.json(
      { error: 'Password change is temporarily unavailable. Please try again in a few moments.' },
      { status: 503 },
    );
  }

  const data = await identityRes.json().catch(() => ({}));

  if (!identityRes.ok) {
    console.log(`[change-password] Identity returned ${identityRes.status}: ${JSON.stringify(data)}`);

    // Upstream service failure (5xx) — do NOT surface as a credential/validation error.
    // The user's current password may well be correct; the identity service is broken.
    if (identityRes.status >= 500) {
      console.error(`[change-password] Identity service error ${identityRes.status} — surfacing generic unavailable message`);
      return NextResponse.json(
        { error: 'Password change is temporarily unavailable. Please try again in a few moments.' },
        { status: 503 },
      );
    }
  }

  return NextResponse.json(data, { status: identityRes.status });
}
