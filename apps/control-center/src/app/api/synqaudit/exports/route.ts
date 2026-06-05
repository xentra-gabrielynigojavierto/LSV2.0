/**
 * POST /api/synqaudit/exports
 *
 * Proxies to controlCenterServerApi.auditExports.create().
 * Called by the ExportRequestForm client component.
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

  let body: Record<string, unknown>;
  try {
    body = await request.json() as Record<string, unknown>;
  } catch {
    return NextResponse.json({ message: 'Invalid JSON body' }, { status: 400 });
  }

  try {
    const result = await controlCenterServerApi.auditExports.create({
      format:                (body.format as 'Json' | 'Csv' | 'Ndjson') ?? 'Json',
      tenantId:              body.tenantId              as string | undefined,
      eventType:             body.eventType             as string | undefined,
      category:              body.category              as string | undefined,
      severity:              body.severity              as string | undefined,
      correlationId:         body.correlationId         as string | undefined,
      dateFrom:              body.dateFrom              as string | undefined,
      dateTo:                body.dateTo                as string | undefined,
      includeStateSnapshots: body.includeStateSnapshots as boolean | undefined,
      includeTags:           body.includeTags           as boolean | undefined,
    });
    return NextResponse.json(result, { status: 201 });
  } catch (err) {
    const message = err instanceof Error ? err.message : 'Export creation failed';
    return NextResponse.json({ message }, { status: 500 });
  }
}
