import { NextRequest, NextResponse } from 'next/server';
import { cookies } from 'next/headers';

const DOCS_URL = 'http://127.0.0.1:5006';

/**
 * GET /api/profile/avatar/[id]
 *
 * Proxies profile avatar bytes from the Documents service.
 *
 * The Documents service issues a 302 redirect to a storage-backed signed URL
 * (S3 presigned URL in production, local /internal/files?token=... in dev).
 * We intercept the redirect manually so that the original Authorization header
 * is NOT forwarded to S3 — S3 rejects requests that carry both presigned-URL
 * query params and an Authorization header ("conflicting auth mechanisms").
 */
export async function GET(
  _req: NextRequest,
  { params }: { params: Promise<{ id: string }> },
) {
  const jar   = await cookies();
  const token = jar.get('platform_session')?.value;
  if (!token) return new NextResponse(null, { status: 401 });

  const { id } = await params;

  // Step 1: ask the Documents service for the file — capture the redirect manually
  const res = await fetch(`${DOCS_URL}/documents/${id}/content`, {
    headers: { Authorization: `Bearer ${token}` },
    redirect: 'manual',
  });

  // 4xx from the docs service (auth / not found / scan-blocked) — surface as-is
  if (res.status >= 400) {
    return new NextResponse(null, { status: res.status });
  }

  // Step 2: resolve the redirect target
  let fileUrl: string | null = null;

  if (res.status === 302 || res.status === 301) {
    const location = res.headers.get('location');
    if (!location) return new NextResponse(null, { status: 502 });

    // Relative redirect (local storage: /internal/files?token=...) → make absolute
    fileUrl = location.startsWith('http')
      ? location
      : `${DOCS_URL}${location}`;
  } else if (res.ok) {
    // Docs service returned bytes directly (shouldn't normally happen, but handle it)
    const contentType = res.headers.get('content-type') ?? 'application/octet-stream';
    const buffer      = await res.arrayBuffer();
    return new NextResponse(buffer, {
      status:  200,
      headers: {
        'Content-Type':  contentType,
        'Cache-Control': 'private, max-age=3600',
      },
    });
  } else {
    return new NextResponse(null, { status: res.status });
  }

  // Step 3: fetch from the resolved URL — NO auth headers (presigned URL is self-authenticating)
  const fileRes = await fetch(fileUrl);
  if (!fileRes.ok) return new NextResponse(null, { status: fileRes.status });

  const contentType = fileRes.headers.get('content-type') ?? 'application/octet-stream';
  const buffer      = await fileRes.arrayBuffer();

  return new NextResponse(buffer, {
    status:  200,
    headers: {
      'Content-Type':  contentType,
      'Cache-Control': 'private, max-age=3600',
    },
  });
}
