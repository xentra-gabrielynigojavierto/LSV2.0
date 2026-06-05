import { type NextRequest, NextResponse } from 'next/server';

const GATEWAY_URL = process.env.GATEWAY_URL ?? 'http://127.0.0.1:5000';

interface RateLimitEntry {
  count: number;
  resetAt: number;
}

const forgotPasswordRateLimit = new Map<string, RateLimitEntry>();
const FORGOT_PASSWORD_LIMIT  = 5;
const FORGOT_PASSWORD_WINDOW = 15 * 60 * 1000;

function checkForgotPasswordRateLimit(ip: string): boolean {
  const now   = Date.now();
  const entry = forgotPasswordRateLimit.get(ip);
  if (!entry || now > entry.resetAt) {
    forgotPasswordRateLimit.set(ip, { count: 1, resetAt: now + FORGOT_PASSWORD_WINDOW });
    return true;
  }
  if (entry.count >= FORGOT_PASSWORD_LIMIT) return false;
  entry.count++;
  return true;
}

function extractRawSubdomain(req: NextRequest): string | null {
  const host =
    req.headers.get('x-forwarded-host') ??
    req.headers.get('host') ??
    '';
  const hostClean = host.split(',')[0].trim();
  const hostWithoutPort = hostClean.includes(':') ? hostClean.split(':')[0] : hostClean;
  const lower = hostWithoutPort.toLowerCase();
  const parts = lower.split('.');
  if (parts.length < 3 || parts[0] === 'www') return null;
  return parts[0];
}

export async function POST(request: NextRequest) {
  const ip =
    request.headers.get('x-forwarded-for')?.split(',')[0].trim() ??
    request.headers.get('x-real-ip') ??
    'unknown';

  if (!checkForgotPasswordRateLimit(ip)) {
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

  const { email, tenantCode: explicitTenantCode } = body;

  if (!email) {
    return NextResponse.json({ message: 'Email is required' }, { status: 400 });
  }

  const rawSubdomain = extractRawSubdomain(request);
  const tenantCode = explicitTenantCode?.trim() || rawSubdomain;

  if (!tenantCode) {
    return NextResponse.json(
      { message: 'Tenant could not be resolved.' },
      { status: 400 },
    );
  }

  // AUTH-B01: resolve the real TenantId from the Tenant service so Identity can
  // use it as a fallback when its local code/subdomain lookup misses.
  let resolvedTenantId: string | null = null;
  let resolvedTenantCode: string = tenantCode;
  if (rawSubdomain) {
    try {
      const tenantRes = await fetch(
        `${GATEWAY_URL}/tenant/api/v1/public/resolve/by-subdomain/${encodeURIComponent(rawSubdomain)}`,
        { headers: { 'Content-Type': 'application/json' } },
      );
      if (tenantRes.ok) {
        const tenantData = await tenantRes.json();
        if (tenantData?.tenantId) {
          resolvedTenantId = tenantData.tenantId as string;
          if (tenantData?.code) resolvedTenantCode = tenantData.code as string;
        }
      }
    } catch {
      // Non-fatal — Identity will fall back to code+subdomain lookup as before.
    }
  }

  let identityRes: Response;
  try {
    identityRes = await fetch(`${GATEWAY_URL}/identity/api/auth/forgot-password`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        tenantCode: resolvedTenantCode,
        email,
        subdomain: rawSubdomain,
        tenantId: resolvedTenantId,
      }),
    });
  } catch (err) {
    console.error(`[forgot-password] Identity service fetch error:`, err);
    return NextResponse.json(
      { message: 'Password reset is temporarily unavailable. Please try again in a few moments.' },
      { status: 503 },
    );
  }

  if (!identityRes.ok) {
    const errBody = await identityRes.json().catch(() => ({}));
    const upstreamMessage = errBody.error ?? errBody.detail ?? errBody.title ?? null;
    console.log(`[forgot-password] Identity returned ${identityRes.status}: ${JSON.stringify(errBody)}`);

    if (identityRes.status >= 500) {
      console.error(`[forgot-password] Identity service error ${identityRes.status} — surfacing generic unavailable message`);
      return NextResponse.json(
        { message: 'Password reset is temporarily unavailable. Please try again in a few moments.' },
        { status: 503 },
      );
    }

    if (identityRes.status === 429) {
      return NextResponse.json(
        { message: 'Too many requests. Please wait before trying again.' },
        { status: 429 },
      );
    }

    return NextResponse.json(
      { message: upstreamMessage ?? 'Unable to start password reset. Please check your details and try again.' },
      { status: identityRes.status },
    );
  }

  const data = await identityRes.json();

  return NextResponse.json({ message: data.message });
}
