import { NextResponse }         from 'next/server';
import { requirePlatformAdmin } from '@/lib/auth-guards';
import { controlCenterServerApi } from '@/lib/control-center-api';

export const dynamic = 'force-dynamic';

/**
 * E17 — Control Center BFF for the outbox summary counts.
 *
 * Path: `GET /api/admin/outbox/summary`
 *
 * Returns grouped counts (pending, processing, failed, deadLettered,
 * succeeded) for the summary cards on the outbox ops page.
 *
 * Auth: PlatformAdmin only. Not cached — operators need current state.
 */
export async function GET(): Promise<NextResponse> {
  try {
    await requirePlatformAdmin();
  } catch {
    return NextResponse.json({ error: 'Unauthorized' }, { status: 401 });
  }

  try {
    const summary = await controlCenterServerApi.outbox.summary();
    return NextResponse.json(summary);
  } catch {
    return NextResponse.json(
      { error: 'Failed to load outbox summary.' },
      { status: 502 },
    );
  }
}
