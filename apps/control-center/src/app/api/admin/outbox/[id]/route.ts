import { type NextRequest, NextResponse } from 'next/server';
import { requirePlatformAdmin }          from '@/lib/auth-guards';
import { controlCenterServerApi }        from '@/lib/control-center-api';

export const dynamic = 'force-dynamic';

/**
 * E17 — Control Center BFF for a single outbox item detail.
 *
 * Path: `GET /api/admin/outbox/{id}`
 *
 * Returns the full detail including last error, payload summary, and
 * retry eligibility flag. Returns 404 when the item is not found or is
 * invisible to the caller's tenant scope.
 *
 * Auth: PlatformAdmin only.
 */
export async function GET(
  _request: NextRequest,
  context:  { params: Promise<{ id: string }> },
): Promise<NextResponse> {
  try {
    await requirePlatformAdmin();
  } catch {
    return NextResponse.json({ error: 'Unauthorized' }, { status: 401 });
  }

  const { id } = await context.params;

  try {
    const detail = await controlCenterServerApi.outbox.getById(id);
    if (!detail) {
      return NextResponse.json({ error: 'Outbox item not found.' }, { status: 404 });
    }
    return NextResponse.json(detail);
  } catch {
    return NextResponse.json(
      { error: 'Failed to load outbox item.' },
      { status: 502 },
    );
  }
}
