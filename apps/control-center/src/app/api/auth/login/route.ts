import { type NextRequest, NextResponse } from 'next/server';
import { SESSION_COOKIE_NAME }            from '@/lib/app-config';
import { CONTROL_CENTER_API_BASE, IS_PROD } from '@/lib/env';

// All env var resolution is delegated to env.ts — no process.env reads here.
// TODO: integrate with Identity service session validation
// TODO: support cross-subdomain auth (scope cookie to .legalsynq.com)

interface RateLimitEntry {
  count: number;
  resetAt: number;
}

const loginRateLimit = new Map<string, RateLimitEntry>();
const LOGIN_LIMIT    = 20;
const LOGIN_WINDOW   = 5 * 60 * 1000;

function checkLoginRateLimit(ip: string): boolean {
  const now   = Date.now();
  const entry = loginRateLimit.get(ip);
  if (!entry || now > entry.resetAt) {
    loginRateLimit.set(ip, { count: 1, resetAt: now + LOGIN_WINDOW });
    return true;
  }
  if (entry.count >= LOGIN_LIMIT) return false;
  entry.count++;
  return true;
}

/**
 * BFF Login route — POST /api/auth/login
 *
 * Accepts { email, password, tenantCode? } from the login form.
 * Forwards credentials to Identity.Api via the gateway.
 * Sets the platform_session HttpOnly cookie on success.
 * Returns a session envelope — the raw token never reaches browser JS.
 */
export async function POST(request: NextRequest) {
  const ip =
    request.headers.get('x-forwarded-for')?.split(',')[0].trim() ??
    request.headers.get('x-real-ip') ??
    'unknown';

  if (!checkLoginRateLimit(ip)) {
    return NextResponse.json(
      { message: 'Too many requests. Please wait before trying again.' },
      { status: 429 },
    );
  }

  let body: Record<string, string>;
  try {
    body = await request.json();
  } catch {
    return NextResponse.json({ message: 'Invalid request body' }, { status: 400 });
  }

  const { email, password, tenantCode: explicitTenantCode } = body;

  if (!email || !password) {
    return NextResponse.json({ message: 'Email and password are required' }, { status: 400 });
  }

  const tenantCode = explicitTenantCode?.trim() || extractTenantCodeFromHost(request) || 'legalsynq';


  const outgoingBody = JSON.stringify({ tenantCode, email, password });
  const outgoingBytes = new TextEncoder().encode(outgoingBody);

  let identityRes: Response;
  try {
    identityRes = await fetch(`${CONTROL_CENTER_API_BASE}/identity/api/auth/login`, {
      method:  'POST',
      headers: {
        'Content-Type': 'application/json',
        'Content-Length': String(outgoingBytes.byteLength),
      },
      body: outgoingBody,
    });
  } catch (err) {
    console.error('[cc-login] Identity service fetch error:', err);
    return NextResponse.json(
      { message: 'Login is temporarily unavailable. Please try again in a few moments.' },
      { status: 503 },
    );
  }

  if (!identityRes.ok) {
    const errBody = await identityRes.json().catch(() => ({}));
    const upstreamMessage = errBody.detail ?? errBody.title ?? null;
    console.log(`[cc-login] Identity returned ${identityRes.status}: ${JSON.stringify(errBody)}`);

    const isVerifying = typeof upstreamMessage === 'string' && upstreamMessage.includes('verifying DNS configuration');
    if (isVerifying) {
      return NextResponse.json(
        { message: 'Your workspace is verifying DNS configuration. This typically completes within a few minutes. Please try again shortly.' },
        { status: 503 },
      );
    }

    const isNotProvisioned = typeof upstreamMessage === 'string' && upstreamMessage.includes('not fully provisioned');
    if (isNotProvisioned) {
      return NextResponse.json(
        { message: 'This tenant is still being set up. Please try again shortly.' },
        { status: 503 },
      );
    }

    // Upstream service failure (5xx) — do NOT surface as "Invalid credentials".
    // The user's password may well be correct; the identity service is broken.
    if (identityRes.status >= 500) {
      console.error(`[cc-login] Identity service error ${identityRes.status} — surfacing generic unavailable message`);
      return NextResponse.json(
        { message: 'Login is temporarily unavailable. Please try again in a few moments.' },
        { status: 503 },
      );
    }

    // Genuine credential failure
    if (identityRes.status === 401) {
      return NextResponse.json(
        { message: upstreamMessage ?? 'Invalid credentials.' },
        { status: 401 },
      );
    }

    // Other 4xx — pass through upstream message if available, else a neutral fallback
    return NextResponse.json(
      { message: upstreamMessage ?? 'Unable to sign in. Please check your details and try again.' },
      { status: 400 },
    );
  }

  const data = await identityRes.json();
  const { accessToken, expiresAtUtc, user } = data;

  const systemRoles: string[] = user.roles ?? [];
  if (!systemRoles.includes('PlatformAdmin')) {
    return NextResponse.json(
      { message: 'Access denied. Control Center requires a platform administrator account.' },
      { status: 403 },
    );
  }

  const expiresDate   = new Date(expiresAtUtc);
  const maxAgeSeconds = Math.floor((expiresDate.getTime() - Date.now()) / 1000);

  const sessionEnvelope = {
    userId:       user.id,
    email:        user.email,
    tenantId:     user.tenantId,
    tenantCode:   user.tenantCode ?? tenantCode,
    productRoles: user.productRoles ?? [],
    systemRoles,
    expiresAtUtc,
  };

  const response = NextResponse.json(sessionEnvelope, { status: 200 });

  response.cookies.set(SESSION_COOKIE_NAME, accessToken, {
    httpOnly: true,
    secure:   IS_PROD,
    sameSite: IS_PROD ? 'strict' : 'lax',
    path:     '/',
    maxAge:   maxAgeSeconds,
  });

  return response;
}

function extractTenantCodeFromHost(request: NextRequest): string | null {
  const host = request.headers.get('x-forwarded-host')
    ?? request.headers.get('host')
    ?? '';
  const hostWithoutPort = host.includes(':') ? host.split(':')[0] : host;
  const parts = hostWithoutPort.split('.');
  if (parts.length >= 3) return parts[0].toLowerCase();
  return null;
}
