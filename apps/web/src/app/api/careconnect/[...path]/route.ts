import { type NextRequest, NextResponse } from 'next/server';
import { cookies } from 'next/headers';

const GATEWAY_URL = process.env.GATEWAY_URL ?? 'http://127.0.0.1:5010';

/**
 * Catch-all BFF proxy for CareConnect API calls made by Client Components.
 *
 * Routing:
 *   Browser fetch → /api/careconnect/api/referrals
 *   → This handler (takes priority over next.config rewrites)
 *   → ${GATEWAY_URL}/careconnect/api/referrals  +  Authorization: Bearer <cookie>
 *   → CareConnect service at :5003
 *
 * Why this exists:
 *   The gateway validates JWT from the Authorization header only.
 *   The platform_session token lives in an HttpOnly cookie — JS can't read it.
 *   This handler bridges the gap: reads the cookie, adds Authorization: Bearer.
 *
 * Server Components: use lib/server-api-client.ts directly (no extra hop).
 * Client Components: use apiClient → this proxy → gateway.
 *
 * Cookie reading: uses cookies() from next/headers (server-side store) rather
 * than request.cookies — more reliable inside App Router Route Handlers.
 *
 * Multipart support (CC2-INT-B03):
 *   For multipart/form-data requests (file uploads), the raw binary body and
 *   the original Content-Type header (which carries the multipart boundary)
 *   are forwarded verbatim. Setting Content-Type to application/json for those
 *   requests would corrupt the boundary and make the backend reject the upload.
 */
type RouteContext = { params: Promise<{ path: string[] }> };

async function proxy(request: NextRequest, { params }: RouteContext): Promise<NextResponse> {
  // Use the server-side cookie store — same mechanism as server-api-client.ts
  const cookieStore = await cookies();
  const token = cookieStore.get('platform_session')?.value;

  // Reconstruct the gateway path: /api/careconnect/api/providers → /careconnect/api/providers
  const { path: pathSegments } = await params;
  const gatewayPath = `/careconnect/${pathSegments.join('/')}`;
  const qs = request.nextUrl.searchParams.toString();
  const url = `${GATEWAY_URL}${gatewayPath}${qs ? `?${qs}` : ''}`;

  const incomingContentType = request.headers.get('Content-Type') ?? '';
  const isMultipart = incomingContentType.startsWith('multipart/form-data');

  const reqHeaders: Record<string, string> = {};
  if (token) reqHeaders['Authorization'] = `Bearer ${token}`;

  let body: ArrayBuffer | string | undefined;

  if (!['GET', 'HEAD'].includes(request.method)) {
    if (isMultipart) {
      // Preserve the full Content-Type (which includes the multipart boundary)
      // and read the body as raw bytes so the file payload is not corrupted.
      reqHeaders['Content-Type'] = incomingContentType;
      try { body = await request.arrayBuffer(); } catch { /* empty body */ }
    } else {
      reqHeaders['Content-Type'] = 'application/json';
      try { body = await request.text(); } catch { /* empty body */ }
    }
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
