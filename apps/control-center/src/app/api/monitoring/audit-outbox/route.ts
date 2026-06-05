import { type NextRequest, NextResponse } from 'next/server';
import { requirePlatformAdmin } from '@/lib/auth-guards';
import {
  getOutboxStatus,
  listOutboxEntries,
  triggerRetryNow,
} from '@/lib/system-health-audit-outbox';

export const dynamic = 'force-dynamic';

/**
 * GET /api/monitoring/audit-outbox
 *
 * Returns the current outbox status plus a summary of every queued entry,
 * so the banner can refresh itself without a full page reload after an
 * operator action.
 */
export async function GET(): Promise<NextResponse> {
  try {
    await requirePlatformAdmin();
  } catch {
    return NextResponse.json({ error: 'Unauthorized' }, { status: 401 });
  }
  const [status, entries] = await Promise.all([
    getOutboxStatus(),
    listOutboxEntries(),
  ]);
  return NextResponse.json({ status, entries }, {
    headers: { 'Cache-Control': 'no-store, no-cache, must-revalidate' },
  });
}

/**
 * POST /api/monitoring/audit-outbox
 *
 * Body: { action: 'retry' }
 *
 * Forces an immediate retry pass against the Platform Audit Event Service
 * for every queued entry — including those previously marked as persistent
 * failures, since the operator's intent is "I believe the audit service is
 * healthy now". Returns the post-retry status so the banner can update.
 */
export async function POST(request: NextRequest): Promise<NextResponse> {
  try {
    await requirePlatformAdmin();
  } catch {
    return NextResponse.json({ error: 'Unauthorized' }, { status: 401 });
  }

  let body: Record<string, unknown> = {};
  try {
    body = await request.json() as Record<string, unknown>;
  } catch {
    // Empty/invalid body is fine — default to the retry action.
  }
  const action = typeof body.action === 'string' ? body.action : 'retry';
  if (action !== 'retry') {
    return NextResponse.json({ error: `Unsupported action: ${action}` }, { status: 400 });
  }

  const result  = await triggerRetryNow();
  const status  = await getOutboxStatus();
  const entries = await listOutboxEntries();
  return NextResponse.json({ result, status, entries }, {
    headers: { 'Cache-Control': 'no-store, no-cache, must-revalidate' },
  });
}
