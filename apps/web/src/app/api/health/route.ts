import { NextResponse } from 'next/server';

/**
 * Liveness probe — GET /api/health
 *
 * Returns the current status of the Next.js web app process.
 * Used by scripts/run-prod.sh startup health probes, load balancers,
 * and container orchestrators.  No auth required.
 */
export async function GET(): Promise<NextResponse> {
  return NextResponse.json(
    { status: 'ok', service: 'web', uptime: process.uptime(), ts: new Date().toISOString() },
    { status: 200, headers: { 'Cache-Control': 'no-store, no-cache, must-revalidate' } },
  );
}
