import { NextRequest, NextResponse } from 'next/server';
import { cookies } from 'next/headers';

const GATEWAY_URL = process.env.GATEWAY_URL ?? 'http://127.0.0.1:5010';

// PATCH /api/profile/phone — set or clear the signed-in user's phone number.
// Thin BFF passthrough: the identity service does the E.164 normalisation
// and validation; we just forward the body and propagate the response.
export async function PATCH(req: NextRequest) {
  const jar   = await cookies();
  const token = jar.get('platform_session')?.value;
  if (!token) return NextResponse.json({ error: 'UNAUTHENTICATED' }, { status: 401 });

  let body: unknown = null;
  try {
    body = await req.json();
  } catch {
    return NextResponse.json({ error: 'INVALID_JSON' }, { status: 400 });
  }

  const upstream = await fetch(`${GATEWAY_URL}/identity/api/profile/phone`, {
    method:  'PATCH',
    headers: {
      Authorization:  `Bearer ${token}`,
      'Content-Type': 'application/json',
    },
    body: JSON.stringify(body ?? {}),
  });

  const text = await upstream.text();
  return new NextResponse(text, {
    status:  upstream.status,
    headers: { 'Content-Type': upstream.headers.get('content-type') ?? 'application/json' },
  });
}
