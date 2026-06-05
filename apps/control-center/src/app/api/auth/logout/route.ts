import { type NextRequest, NextResponse } from 'next/server';
import { SESSION_COOKIE_NAME }            from '@/lib/app-config';
import { CONTROL_CENTER_API_BASE, IS_PROD } from '@/lib/env';

// All env var resolution is delegated to env.ts — no process.env reads here.
// TODO: support cross-subdomain auth (clear cookie scoped to .legalsynq.com)

/**
 * BFF Logout route — POST /api/auth/logout
 * Clears the platform_session cookie and optionally notifies the backend.
 */
export async function POST(request: NextRequest) {
  const token = request.cookies.get(SESSION_COOKIE_NAME)?.value;

  if (token) {
    fetch(`${CONTROL_CENTER_API_BASE}/identity/api/auth/logout`, {
      method:  'POST',
      headers: { 'Authorization': `Bearer ${token}` },
    }).catch(() => { /* best-effort — backend logout is stateless */ });
  }

  const response = NextResponse.json({ ok: true }, { status: 200 });

  response.cookies.set(SESSION_COOKIE_NAME, '', {
    httpOnly: true,
    secure:   IS_PROD,
    sameSite: IS_PROD ? 'strict' : 'lax',
    path:     '/',
    maxAge:   0,
  });

  return response;
}
