import { type NextRequest, NextResponse } from 'next/server';
import { cookies } from 'next/headers';

const GATEWAY_URL = process.env.GATEWAY_URL ?? 'http://127.0.0.1:5000';

const FALLBACK_CONFIG = {
  organizationId: null,
  productCode: 'LIENS',
  settings: { providerMode: 'sell' },
};

export async function GET(_request: NextRequest) {
  const cookieStore = await cookies();
  const token = cookieStore.get('platform_session')?.value;

  if (!token) {
    return NextResponse.json(FALLBACK_CONFIG, { status: 200 });
  }

  try {
    const res = await fetch(`${GATEWAY_URL}/identity/api/organizations/my/config`, {
      headers: {
        'Authorization': `Bearer ${token}`,
        'Content-Type': 'application/json',
      },
      cache: 'no-store',
    });

    if (!res.ok) {
      return NextResponse.json(FALLBACK_CONFIG, { status: 200 });
    }

    const data = await res.json();
    return NextResponse.json(data, { status: 200 });
  } catch {
    return NextResponse.json(FALLBACK_CONFIG, { status: 200 });
  }
}
