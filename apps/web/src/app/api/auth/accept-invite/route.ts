import { type NextRequest, NextResponse } from 'next/server';

const GATEWAY_URL = process.env.GATEWAY_URL ?? 'http://127.0.0.1:5000';

export async function POST(request: NextRequest) {
  let body: Record<string, string>;
  try {
    body = await request.json();
  } catch {
    return NextResponse.json({ message: 'Invalid request body' }, { status: 400 });
  }

  const { token, newPassword } = body;

  if (!token) {
    return NextResponse.json({ message: 'Invitation token is required' }, { status: 400 });
  }
  if (!newPassword || newPassword.length < 8) {
    return NextResponse.json({ message: 'Password must be at least 8 characters' }, { status: 400 });
  }

  let identityRes: Response;
  try {
    identityRes = await fetch(`${GATEWAY_URL}/identity/api/auth/accept-invite`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ token, newPassword }),
    });
  } catch (err) {
    console.error(`[accept-invite] Identity service fetch error:`, err);
    return NextResponse.json(
      { message: 'Invitation acceptance is temporarily unavailable. Please try again in a few moments.' },
      { status: 503 },
    );
  }

  const data = await identityRes.json().catch(() => ({}));

  if (!identityRes.ok) {
    const upstreamMessage = data.error ?? data.detail ?? data.title ?? null;
    console.log(`[accept-invite] Identity returned ${identityRes.status}: ${JSON.stringify(data)}`);

    if (identityRes.status >= 500) {
      console.error(`[accept-invite] Identity service error ${identityRes.status}`);
      return NextResponse.json(
        { message: 'Invitation acceptance is temporarily unavailable. Please try again in a few moments.' },
        { status: 503 },
      );
    }

    return NextResponse.json(
      { message: upstreamMessage ?? 'Failed to accept invitation. The link may have expired.' },
      { status: identityRes.status },
    );
  }

  return NextResponse.json({
    message: data.message ?? 'Invitation accepted. Your account is now active.',
    tenantPortalUrl: data.tenantPortalUrl ?? null,
  });
}
