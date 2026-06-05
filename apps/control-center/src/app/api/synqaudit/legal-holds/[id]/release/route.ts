/**
 * POST /api/synqaudit/legal-holds/[id]/release
 *
 * Proxies to controlCenterServerApi.auditLegalHolds.release().
 * [id] is the holdId of the hold to release.
 * Called by the LegalHoldManager client component.
 */

import { type NextRequest, NextResponse } from 'next/server';
import { requirePlatformAdmin }           from '@/lib/auth-guards';
import { controlCenterServerApi }         from '@/lib/control-center-api';

export async function POST(
  _request: NextRequest,
  { params }: { params: Promise<{ id: string }> },
): Promise<NextResponse> {
  const { id } = await params;
  try {
    await requirePlatformAdmin();
  } catch {
    return NextResponse.json({ message: 'Unauthorized' }, { status: 401 });
  }

  const holdId = id;
  if (!holdId) {
    return NextResponse.json({ message: 'holdId is required' }, { status: 400 });
  }

  try {
    const result = await controlCenterServerApi.auditLegalHolds.release(holdId);
    return NextResponse.json(result);
  } catch (err) {
    const message = err instanceof Error ? err.message : 'Legal hold release failed';
    return NextResponse.json({ message }, { status: 500 });
  }
}
