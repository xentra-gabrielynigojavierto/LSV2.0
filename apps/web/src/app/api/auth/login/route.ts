import { type NextRequest, NextResponse } from 'next/server';

const GATEWAY_URL = process.env.GATEWAY_URL ?? 'http://127.0.0.1:5000';
const IS_PROD     = process.env.NODE_ENV === 'production';

interface RateLimitEntry {
  count: number;
  resetAt: number;
}

const loginRateLimit  = new Map<string, RateLimitEntry>();
const LOGIN_LIMIT     = 20;
const LOGIN_WINDOW    = 5 * 60 * 1000;

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
 * Flow:
 *   1. Receive { email, password, tenantCode? } from the login form
 *   2. Resolve tenantCode: use the form value if present (dev mode),
 *      otherwise derive from the Host / X-Forwarded-Host header (prod)
 *   3. Forward to POST ${GATEWAY_URL}/identity/api/auth/login
 *   4. Receive { accessToken, expiresAtUtc, user } from Identity service
 *   5. Store accessToken in an HttpOnly cookie (platform_session)
 *   6. Return a session envelope to the client — raw token is NEVER sent to JS
 *
 * Cookie attributes:
 *   - HttpOnly:   browser JS cannot read the token (XSS protection)
 *   - SameSite:   Strict in production; Lax in development
 *   - Secure:     true in production only (HTTPS required)
 *   - Path:       / (sent with every request to this origin)
 *   - Domain:     NOT set — scopes cookie to the exact subdomain only
 *                 Setting Domain=.legalsynq.com would share cookies across tenants
 *   - Max-Age:    matches token expiry from Identity service
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

  const rawHost = request.headers.get('x-forwarded-host') ?? request.headers.get('host') ?? '';
  const rawSubdomain = extractRawSubdomain(rawHost);
  const tenantCode = explicitTenantCode?.trim() || rawSubdomain || null;

  const redactedEmail = email.replace(/(.{2}).+@/, '$1***@');
  console.log(`[login] host=${rawHost}, subdomain=${rawSubdomain}, explicitTenant=${explicitTenantCode}, resolvedTenantCode=${tenantCode}, email=${redactedEmail}`);

  if (!tenantCode) {
    return NextResponse.json(
      { message: 'Tenant could not be resolved. Please provide a tenant code.' },
      { status: 400 },
    );
  }

  // AUTH-B01 / AUTH-CC01: Resolve tenant from the Tenant service.
  // - If the subdomain maps to a known tenant, pass tenantId + code (AUTH-B01 fallback path).
  // - If the Tenant service returns 404, this is the common portal (multi-tenant); tell
  //   Identity to resolve the tenant from the user's email instead (AUTH-CC01).
  let resolvedTenantId: string | null = null;
  let resolvedTenantCode: string = tenantCode;
  let resolveByEmail = false;

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
          console.log(`[login] AUTH-B01 tenant resolved: id=${resolvedTenantId} code=${resolvedTenantCode}`);
        }
      } else if (tenantRes.status === 404) {
        // Common portal — subdomain is not a tenant identifier.
        // Identity will resolve the tenant from the user's email.
        resolveByEmail = true;
        console.log(`[login] AUTH-CC01 subdomain=${rawSubdomain} not found in Tenant service — resolving by email`);
      } else {
        console.log(`[login] AUTH-B01 tenant resolve by-subdomain returned ${tenantRes.status}, proceeding without tenantId fallback`);
      }
    } catch (err) {
      // Non-fatal — Identity will still try code+subdomain lookup as before.
      console.warn(`[login] AUTH-B01 tenant resolve fetch failed:`, err);
    }
  }

  const outgoingBody = JSON.stringify({
    tenantCode: resolveByEmail ? null : resolvedTenantCode,
    email,
    password,
    subdomain: rawSubdomain,
    tenantId: resolvedTenantId,
    resolveByEmail,
  });
  const outgoingBytes = new TextEncoder().encode(outgoingBody);

  let identityRes: Response;
  try {
    identityRes = await fetch(`${GATEWAY_URL}/identity/api/auth/login`, {
      method:  'POST',
      headers: {
        'Content-Type': 'application/json',
        'Content-Length': String(outgoingBytes.byteLength),
      },
      body: outgoingBody,
    });
  } catch (err) {
    console.error(`[login] Identity service fetch error:`, err);
    return NextResponse.json(
      { message: 'Login is temporarily unavailable. Please try again in a few moments.' },
      { status: 503 },
    );
  }

  if (!identityRes.ok) {
    const errBody = await identityRes.json().catch(() => ({}));
    const upstreamMessage = errBody.detail ?? errBody.title ?? null;
    console.log(`[login] Identity returned ${identityRes.status}: ${JSON.stringify(errBody)}`);

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
      console.error(`[login] Identity service error ${identityRes.status} — surfacing generic unavailable message`);
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

  // Compute Max-Age in seconds
  const expiresDate = new Date(expiresAtUtc);
  const maxAgeSeconds = Math.floor((expiresDate.getTime() - Date.now()) / 1000);

  // Build the session envelope — accessToken is intentionally excluded
  const sessionEnvelope = {
    userId:       user.id,
    email:        user.email,
    tenantId:     user.tenantId,
    tenantCode:   user.tenantCode ?? resolvedTenantCode,
    orgId:        user.organizationId ?? null,
    orgType:      user.orgType ?? null,
    productRoles: user.productRoles ?? [],
    systemRoles:  user.roles ?? [],
    expiresAtUtc,
  };

  const response = NextResponse.json(sessionEnvelope, { status: 200 });

  // Set the HttpOnly cookie — this is the only place the raw token touches HTTP headers
  response.cookies.set('platform_session', accessToken, {
    httpOnly: true,
    secure:   IS_PROD,
    sameSite: IS_PROD ? 'strict' : 'lax',
    path:     '/',
    maxAge:   maxAgeSeconds,
    // domain: intentionally omitted — scopes to exact request origin only
  });

  const cookieTenantCode = user.tenantCode ?? resolvedTenantCode;
  response.cookies.set('tenant_code', cookieTenantCode, {
    httpOnly: false,
    secure:   IS_PROD,
    sameSite: IS_PROD ? 'strict' : 'lax',
    path:     '/',
    maxAge:   maxAgeSeconds,
  });

  return response;
}

/**
 * Extracts the raw subdomain slug from a hostname string.
 *
 * Examples:
 *   "liens-company.demo.legalsynq.com" → "liens-company"
 *   "legalsynq.demo.legalsynq.com"     → "legalsynq"
 *   "localhost:3000"                     → null (no subdomain)
 */
function extractRawSubdomain(rawHost: string): string | null {
  const host = rawHost.split(',')[0].trim();
  const hostWithoutPort = host.includes(':') ? host.split(':')[0] : host;
  const lower = hostWithoutPort.toLowerCase();

  if (!/^[a-z0-9.-]+$/.test(lower)) return null;

  const parts = lower.split('.');

  if (parts.length >= 3) {
    const sub = parts[0];
    if (sub === 'www') return null;
    return sub;
  }

  return null;
}
