import { type NextRequest, NextResponse } from 'next/server';
import { requirePlatformAdmin }          from '@/lib/auth-guards';
import { controlCenterServerApi }        from '@/lib/control-center-api';

export const dynamic = 'force-dynamic';

/**
 * E17 — Control Center BFF for the outbox item list.
 *
 * Path: `GET /api/admin/outbox`
 *
 * Forwards query parameters (status, eventType, tenantId,
 * workflowInstanceId, search, page, pageSize) to the Flow admin
 * endpoint and returns the normalised response.
 *
 * Auth: PlatformAdmin only at this boundary.
 */
export async function GET(request: NextRequest): Promise<NextResponse> {
  try {
    await requirePlatformAdmin();
  } catch {
    return NextResponse.json({ error: 'Unauthorized' }, { status: 401 });
  }

  const sp = request.nextUrl.searchParams;

  try {
    const result = await controlCenterServerApi.outbox.list({
      page:               parseInt(sp.get('page') ?? '1')     || 1,
      pageSize:           parseInt(sp.get('pageSize') ?? '20') || 20,
      status:             sp.get('status')             ?? undefined,
      eventType:          sp.get('eventType')          ?? undefined,
      tenantId:           sp.get('tenantId')           ?? undefined,
      workflowInstanceId: sp.get('workflowInstanceId') ?? undefined,
      search:             sp.get('search')             ?? undefined,
    });
    return NextResponse.json(result);
  } catch {
    return NextResponse.json(
      { error: 'Failed to load outbox items.' },
      { status: 502 },
    );
  }
}
