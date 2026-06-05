import { NextRequest, NextResponse } from 'next/server';

const DOCS_URL = 'http://127.0.0.1:5006';

/**
 * GET /api/branding/logo/[docId]
 *
 * Proxies a tenant logo from the Documents service.
 * Logos are public-facing so we use the public endpoint directly —
 * this avoids the S3 presigned-URL + Authorization-header conflict
 * that occurs when following a 302 redirect with auth headers.
 */
export async function GET(
  _req: NextRequest,
  { params }: { params: Promise<{ docId: string }> },
) {
  const { docId } = await params;

  try {
    const res = await fetch(`${DOCS_URL}/public/logo/${docId}`, {
      redirect: 'follow',
    });

    if (res.ok) {
      const contentType = res.headers.get('content-type') ?? 'image/png';
      const buffer      = await res.arrayBuffer();
      return new NextResponse(buffer, {
        status:  200,
        headers: {
          'Content-Type':  contentType,
          'Cache-Control': 'public, max-age=3600, s-maxage=3600',
        },
      });
    }
  } catch {
    // fall through to 404
  }

  return new NextResponse(null, { status: 404 });
}
