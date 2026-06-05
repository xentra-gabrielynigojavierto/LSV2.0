import { NextRequest, NextResponse } from 'next/server';
import { cookies } from 'next/headers';
import { SESSION_COOKIE_NAME } from '@/lib/app-config';

const DOCS_URL = 'http://127.0.0.1:5006';

// GET /api/profile/avatar/[id]
export async function GET(
  _req: NextRequest,
  { params }: { params: Promise<{ id: string }> },
) {
  const jar   = await cookies();
  const token = jar.get(SESSION_COOKIE_NAME)?.value;
  if (!token) return new NextResponse(null, { status: 401 });

  const { id } = await params;

  const res = await fetch(`${DOCS_URL}/documents/${id}/content`, {
    headers: { Authorization: `Bearer ${token}` },
  });

  if (!res.ok) return new NextResponse(null, { status: res.status });

  const contentType = res.headers.get('content-type') ?? 'application/octet-stream';
  const buffer      = await res.arrayBuffer();

  return new NextResponse(buffer, {
    status:  200,
    headers: {
      'Content-Type':  contentType,
      'Cache-Control': 'private, max-age=3600',
    },
  });
}
