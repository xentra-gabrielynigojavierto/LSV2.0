import { type NextRequest, NextResponse } from 'next/server';
import { cookies } from 'next/headers';

/**
 * Catch-all BFF proxy for Flow service calls (LS-FLOW-E11.6).
 *
 * Client Components call  /api/flow/api/v1/...
 * This handler forwards    → GATEWAY_URL/flow/api/v1/...
 * with the platform_session cookie forwarded as Authorization: Bearer.
 *
 * The gateway YARP route `flow-protected` matches `/flow/{**catch-all}`
 * and strips the `/flow` prefix before forwarding to Flow.Api on :5012.
 *
 * Cookie reading uses cookies() from next/headers (server-side store) to
 * stay consistent with the other product BFF proxies in this app
 * (lien, careconnect, fund, ...).
 */
const GATEWAY_URL = process.env.GATEWAY_URL ?? 'http://127.0.0.1:5010';

async function proxy(req: NextRequest, segments: string[]): Promise<NextResponse> {
  const path   = segments.join('/');
  const search = req.nextUrl.search;
  const url    = `${GATEWAY_URL}/flow/${path}${search}`;

  const cookieStore = await cookies();
  const token = cookieStore.get('platform_session')?.value;
  const headers: Record<string, string> = {
    'Content-Type': 'application/json',
  };
  if (token) {
    headers['Authorization'] = `Bearer ${token}`;
  }

  let body: string | undefined;
  if (req.method !== 'GET' && req.method !== 'HEAD') {
    try { body = await req.text(); } catch { /* no body */ }
  }

  const res = await fetch(url, {
    method:  req.method,
    headers,
    body,
  });

  const responseHeaders: Record<string, string> = {};
  const correlationId = res.headers.get('X-Correlation-Id');
  if (correlationId) responseHeaders['X-Correlation-Id'] = correlationId;
  responseHeaders['Content-Type'] = res.headers.get('Content-Type') ?? 'application/json';

  if (res.status === 204) {
    return new NextResponse(null, { status: 204, headers: responseHeaders });
  }

  const data = await res.text();
  return new NextResponse(data, { status: res.status, headers: responseHeaders });
}

export async function GET(req: NextRequest, { params }: { params: Promise<{ path: string[] }> }) {
  return proxy(req, (await params).path);
}
export async function POST(req: NextRequest, { params }: { params: Promise<{ path: string[] }> }) {
  return proxy(req, (await params).path);
}
export async function PUT(req: NextRequest, { params }: { params: Promise<{ path: string[] }> }) {
  return proxy(req, (await params).path);
}
export async function PATCH(req: NextRequest, { params }: { params: Promise<{ path: string[] }> }) {
  return proxy(req, (await params).path);
}
export async function DELETE(req: NextRequest, { params }: { params: Promise<{ path: string[] }> }) {
  return proxy(req, (await params).path);
}
