import { type NextRequest, NextResponse } from 'next/server';
import { cookies } from 'next/headers';

const GATEWAY_URL = process.env.GATEWAY_URL ?? 'http://127.0.0.1:5010';

/**
 * CC BFF upload proxy for ticket file attachments.
 *
 * Routing:
 *   CC client fetch → /api/support/tickets/{id}/attachments/upload
 *   → This handler
 *   → ${GATEWAY_URL}/support/api/tickets/{id}/attachments/upload (multipart)
 *   → Support service at :5017
 *
 * The gateway validates the JWT from the Authorization header.
 * The platform_session token lives in an HttpOnly cookie — JS cannot read it.
 * This handler bridges the gap by reading the cookie and forwarding the token,
 * while passing the multipart body through verbatim (preserving the boundary).
 */
type RouteContext = { params: Promise<{ id: string }> };

export async function POST(request: NextRequest, { params }: RouteContext): Promise<NextResponse> {
  const cookieStore = await cookies();
  const token       = cookieStore.get('platform_session')?.value;

  const { id } = await params;
  const url    = `${GATEWAY_URL}/support/api/tickets/${encodeURIComponent(id)}/attachments/upload`;

  const ct = request.headers.get('Content-Type') ?? '';
  if (!ct.includes('multipart/form-data')) {
    return NextResponse.json({ message: 'multipart/form-data required' }, { status: 400 });
  }

  let body: Blob;
  try {
    body = await request.blob();
  } catch {
    return NextResponse.json({ message: 'Could not read request body' }, { status: 400 });
  }

  let gatewayRes: Response;
  try {
    gatewayRes = await fetch(url, {
      method:  'POST',
      headers: {
        'Content-Type':  ct,
        ...(token ? { 'Authorization': `Bearer ${token}` } : {}),
      },
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
