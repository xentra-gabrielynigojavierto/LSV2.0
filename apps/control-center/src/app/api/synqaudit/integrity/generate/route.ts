/**
 * POST /api/synqaudit/integrity/generate
 *
 * Proxies to controlCenterServerApi.auditIntegrity.generate().
 * Called by the IntegrityPanel client component.
 */

import { type NextRequest, NextResponse } from 'next/server';
import { requirePlatformAdmin }           from '@/lib/auth-guards';
import { controlCenterServerApi }         from '@/lib/control-center-api';

export async function POST(request: NextRequest): Promise<NextResponse> {
  try {
    await requirePlatformAdmin();
  } catch {
    return NextResponse.json({ message: 'Unauthorized' }, { status: 401 });
  }

  let body: Record<string, unknown> = {};
  try {
    body = await request.json() as Record<string, unknown>;
  } catch { }

  try {
    const result = await controlCenterServerApi.auditIntegrity.generate({
      checkpointType:    body.checkpointType    as string | undefined,
      fromRecordedAtUtc: body.fromRecordedAtUtc as string | undefined,
      toRecordedAtUtc:   body.toRecordedAtUtc   as string | undefined,
    });
    return NextResponse.json(result, { status: 201 });
  } catch (err) {
    const message = err instanceof Error ? err.message : 'Checkpoint generation failed';
    return NextResponse.json({ message }, { status: 500 });
  }
}
