import { type NextRequest, NextResponse } from 'next/server';
import { cookies } from 'next/headers';

const GATEWAY_URL = process.env.GATEWAY_URL ?? 'http://127.0.0.1:5010';

/**
 * Catch-all BFF proxy for Support API calls made by Client Components.
 *
 * Routing:
 *   Browser fetch → /api/support/api/tickets
 *   → This handler
 *   → ${GATEWAY_URL}/support/api/tickets  +  Authorization: Bearer <cookie>
 *   → Support service at :5017
 *
 * The gateway validates the JWT from the Authorization header.
 * The platform_session token lives in an HttpOnly cookie — JS cannot read it.
 * This handler bridges the gap: reads the cookie, adds Authorization: Bearer.
 *
 * Multipart uploads (file attachments) are passed through transparently:
 * the Content-Type header (including the multipart boundary) is forwarded
 * as-is so the upstream ASP.NET Core endpoint can parse the form.
 *
 * Server Components: use lib/support-server-api.ts directly (no extra hop).
 * Client Components: use this proxy.
 */
type RouteContext = { params: Promise<{ path: string[] }> };

async function proxy(request: NextRequest, { params }: RouteContext): Promise<NextResponse> {
  const cookieStore = await cookies();
  const token = cookieStore.get('platform_session')?.value;

  const { path: pathSegments } = await params;
  const gatewayPath = `/support/${pathSegments.join('/')}`;
  const qs = request.nextUrl.searchParams.toString();
  const url = `${GATEWAY_URL}${gatewayPath}${qs ? `?${qs}` : ''}`;

  const reqHeaders: Record<string, string> = {};
  if (token) reqHeaders['Authorization'] = `Bearer ${token}`;

  let body: BodyInit | undefined;
  if (!['GET', 'HEAD'].includes(request.method)) {
    const ct = request.headers.get('Content-Type') ?? '';
    if (ct.includes('multipart/form-data')) {
      // Pass Content-Type through verbatim so the multipart boundary is preserved.
      // Using blob() reads the raw bytes without any text re-encoding.
      reqHeaders['Content-Type'] = ct;
      try { body = await request.blob(); } catch { /* empty body */ }
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

  const contentType   = gatewayRes.headers.get('Content-Type') ?? 'application/json';
  const isTextOrJson  = contentType.startsWith('application/json') ||
                        contentType.startsWith('text/') ||
                        contentType.startsWith('application/problem');

  const resHeaders: Record<string, string> = { 'Content-Type': contentType };
  const correlationId = gatewayRes.headers.get('X-Correlation-Id');
  if (correlationId) resHeaders['X-Correlation-Id'] = correlationId;
  const contentDisposition = gatewayRes.headers.get('Content-Disposition');
  if (contentDisposition) resHeaders['Content-Disposition'] = contentDisposition;

  if (isTextOrJson) {
    const responseBody = await gatewayRes.text();
    return new NextResponse(responseBody, {
      status:  gatewayRes.status,
      headers: resHeaders,
    });
  }

  // Binary/stream response (e.g. file download) — pipe without buffering as text.
  return new NextResponse(gatewayRes.body, {
    status:  gatewayRes.status,
    headers: resHeaders,
  });
}

export const GET    = proxy;
export const POST   = proxy;
export const PUT    = proxy;
export const PATCH  = proxy;
export const DELETE = proxy;
