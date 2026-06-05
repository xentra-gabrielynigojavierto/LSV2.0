import { type NextRequest, NextResponse } from 'next/server';
import { cookies } from 'next/headers';

const GATEWAY_URL = process.env.GATEWAY_URL ?? 'http://127.0.0.1:5010';

/**
 * CC BFF download proxy for ticket file attachments.
 *
 * Routing:
 *   CC client → /api/support/tickets/{id}/attachments/{attachmentId}/download
 *   → This handler
 *   → ${GATEWAY_URL}/support/api/tickets/{id}/attachments/{attachmentId}/download
 *   → Support service at :5017 (which proxies to the file storage backend)
 *
 * The gateway validates the JWT from the Authorization header.
 * Streams the response body so large files are not buffered into memory.
 */
type RouteContext = { params: Promise<{ id: string; attachmentId: string }> };

export async function GET(request: NextRequest, { params }: RouteContext): Promise<NextResponse> {
  const cookieStore = await cookies();
  const token       = cookieStore.get('platform_session')?.value;

  const { id, attachmentId } = await params;
  const url = `${GATEWAY_URL}/support/api/tickets/${encodeURIComponent(id)}/attachments/${encodeURIComponent(attachmentId)}/download`;

  let gatewayRes: Response;
  try {
    gatewayRes = await fetch(url, {
      headers: token ? { 'Authorization': `Bearer ${token}` } : {},
    });
  } catch {
    return NextResponse.json({ message: 'Gateway unavailable' }, { status: 503 });
  }

  if (!gatewayRes.ok) {
    const body = await gatewayRes.text();
    return new NextResponse(body, {
      status:  gatewayRes.status,
      headers: { 'Content-Type': gatewayRes.headers.get('Content-Type') ?? 'application/json' },
    });
  }

  const contentType        = gatewayRes.headers.get('Content-Type') ?? 'application/octet-stream';
  const contentDisposition = gatewayRes.headers.get('Content-Disposition');

  const resHeaders: Record<string, string> = { 'Content-Type': contentType };
  if (contentDisposition) resHeaders['Content-Disposition'] = contentDisposition;

  return new NextResponse(gatewayRes.body, {
    status:  200,
    headers: resHeaders,
  });
}
