/**
 * POST /api/synqaudit/legal-holds/[id]
 *
 * Proxies to controlCenterServerApi.auditLegalHolds.create().
 * [id] is the auditId of the record to place a hold on.
 * Called by the LegalHoldManager client component.
 */

import { type NextRequest, NextResponse } from 'next/server';
import { requirePlatformAdmin }           from '@/lib/auth-guards';
import { controlCenterServerApi }         from '@/lib/control-center-api';

export async function POST(
  request: NextRequest,
  { params }: { params: Promise<{ id: string }> },
): Promise<NextResponse> {
  const { id } = await params;
  try {
    await requirePlatformAdmin();
  } catch {
    return NextResponse.json({ message: 'Unauthorized' }, { status: 401 });
  }

  const auditId = id;
  if (!auditId) {
    return NextResponse.json({ message: 'auditId is required' }, { status: 400 });
  }

  let body: Record<string, unknown>;
  try {
    body = await request.json() as Record<string, unknown>;
  } catch {
    return NextResponse.json({ message: 'Invalid JSON body' }, { status: 400 });
  }

  const legalAuthority = body.legalAuthority as string | undefined;
  if (!legalAuthority?.trim()) {
    return NextResponse.json({ message: 'legalAuthority is required' }, { status: 400 });
  }

  try {
    const result = await controlCenterServerApi.auditLegalHolds.create(auditId, {
      legalAuthority,
      notes: body.notes as string | undefined,
    });
    return NextResponse.json(result, { status: 201 });
  } catch (err) {
    const message = err instanceof Error ? err.message : 'Legal hold creation failed';
    return NextResponse.json({ message }, { status: 500 });
  }
}
