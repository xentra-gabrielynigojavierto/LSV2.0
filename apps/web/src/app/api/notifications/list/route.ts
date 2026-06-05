import { NextRequest, NextResponse } from 'next/server';
import { cookies } from 'next/headers';

const GATEWAY_URL = process.env.GATEWAY_URL ?? 'http://127.0.0.1:5000';

async function getTenantId(token: string): Promise<string | null> {
  try {
    const res = await fetch(
      `${GATEWAY_URL}/identity/api/auth/me`,
      { headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' }, cache: 'no-store' },
    );
    if (!res.ok) return null;
    const me = await res.json();
    return me.tenantId ?? null;
  } catch {
    return null;
  }
}

export async function GET(req: NextRequest) {
  const cookieStore = await cookies();
  const token = cookieStore.get('platform_session')?.value;
  if (!token) return NextResponse.json({ error: 'Unauthorized' }, { status: 401 });

  const tenantId = await getTenantId(token);
  if (!tenantId) return NextResponse.json({ error: 'Unauthorized' }, { status: 401 });

  const { searchParams } = req.nextUrl;
  const qs = new URLSearchParams();
  for (const key of ['status', 'channel', 'limit', 'offset']) {
    const val = searchParams.get(key);
    if (val) qs.set(key, val);
  }
  const q = qs.toString();

  const url = `${GATEWAY_URL}/notifications/v1/notifications${q ? `?${q}` : ''}`;

  try {
    const res = await fetch(url, {
      headers: {
        Authorization: `Bearer ${token}`,
        'Content-Type': 'application/json',
        'X-Tenant-Id': tenantId,
      },
      cache: 'no-store',
    });

    if (!res.ok) {
      const text = await res.text().catch(() => '');
      return NextResponse.json({ error: text || `HTTP ${res.status}` }, { status: res.status });
    }

    const data = await res.json();
    return NextResponse.json(data);
  } catch {
    return NextResponse.json({ error: 'Notification service unavailable' }, { status: 503 });
  }
}
