import { type NextRequest, NextResponse } from 'next/server';
import { cookies } from 'next/headers';

const GATEWAY_URL = process.env.GATEWAY_URL ?? 'http://127.0.0.1:5010';

/**
 * Catch-all BFF proxy for Identity API calls made by Client Components.
 *
 * Routing:
 *   Browser fetch → /api/identity/api/users
 *   → This handler (takes priority over next.config rewrites)
 *   → ${GATEWAY_URL}/identity/api/users  +  Authorization: Bearer <cookie>
 *   → Identity service at :5001
 *
 * Why this exists:
 *   The gateway validates JWT from the Authorization header only.
 *   The platform_session token lives in an HttpOnly cookie — JS can't read it.
 *   This handler bridges the gap: reads the cookie, adds Authorization: Bearer.
 *
 * Server Components: use lib/server-api-client.ts directly (no extra hop).
 * Client Components: use controlCenterApi → this proxy → gateway.
 *
 * Cookie reading: uses cookies() from next/headers (server-side store) rather
 * than request.cookies — more reliable inside App Router Route Handlers.
 */
type RouteContext = { params: Promise<{ path: string[] }> };

async function proxy(request: NextRequest, { params }: RouteContext): Promise<NextResponse> {
  const cookieStore = await cookies();
  const token = cookieStore.get('platform_session')?.value;

  const { path: pathSegments } = await params;
  const gatewayPath = `/identity/${pathSegments.join('/')}`;
  const qs = request.nextUrl.searchParams.toString();
  const url = `${GATEWAY_URL}${gatewayPath}${qs ? `?${qs}` : ''}`;

  const reqHeaders: Record<string, string> = {
    'Content-Type': 'application/json',
  };
  if (token) reqHeaders['Authorization'] = `Bearer ${token}`;

  const joinedPath = pathSegments.join('/');
  const isBrandingRoute = joinedPath === 'api/tenants/current/branding'
    || joinedPath === 'tenants/current/branding';
  if (isBrandingRoute) {
    const tenantCode = request.headers.get('X-Tenant-Code');
    if (tenantCode) reqHeaders['X-Tenant-Code'] = tenantCode;

    const forwardedHost = request.headers.get('x-forwarded-host') ?? request.headers.get('host');
    if (forwardedHost) reqHeaders['X-Forwarded-Host'] = forwardedHost;
  }

  let body: string | undefined;
  if (!['GET', 'HEAD'].includes(request.method)) {
    try { body = await request.text(); } catch { /* empty body */ }
  }

  let gatewayRes: Response;
  try {
    gatewayRes = await fetch(url, {
      method:  request.method,
      headers: reqHeaders,
      body,
    });
  } catch {
    return NextResponse.json({ message: 'Gateway unavailable' }, { status: 503 });
  }

  const responseBody = await gatewayRes.text();

  const resHeaders: Record<string, string> = {
    'Content-Type': gatewayRes.headers.get('Content-Type') ?? 'application/json',
  };
  const correlationId = gatewayRes.headers.get('X-Correlation-Id');
  if (correlationId) resHeaders['X-Correlation-Id'] = correlationId;

  return new NextResponse(responseBody, {
    status:  gatewayRes.status,
    headers: resHeaders,
  });
}

export const GET    = proxy;
export const POST   = proxy;
export const PUT    = proxy;
export const PATCH  = proxy;
export const DELETE = proxy;
