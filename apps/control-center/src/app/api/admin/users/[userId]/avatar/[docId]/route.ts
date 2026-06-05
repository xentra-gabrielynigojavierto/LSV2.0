import { NextRequest, NextResponse } from 'next/server';
import { cookies } from 'next/headers';
import { SESSION_COOKIE_NAME } from '@/lib/app-config';

const DOCS_URL = 'http://127.0.0.1:5006';

/**
 * GET /api/admin/users/[userId]/avatar/[docId]
 *
 * Proxy for fetching another user's avatar from the Documents service
 * using the admin's session token.
 *
 * Query param: tenantId — the tenant that owns the document.
 * Passed as X-Admin-Target-Tenant so PlatformAdmins can access cross-tenant docs.
 */
export async function GET(
  req: NextRequest,
  { params }: { params: Promise<{ userId: string; docId: string }> },
) {
  const jar   = await cookies();
  const token = jar.get(SESSION_COOKIE_NAME)?.value;
  if (!token) return new NextResponse(null, { status: 401 });

  const { docId } = await params;
  const tenantId  = req.nextUrl.searchParams.get('tenantId');

  const headers: Record<string, string> = {
    Authorization: `Bearer ${token}`,
  };
  if (tenantId) {
    headers['X-Admin-Target-Tenant'] = tenantId;
  }

  const res = await fetch(`${DOCS_URL}/documents/${docId}/content`, { headers });

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
