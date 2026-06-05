import { type NextRequest, NextResponse } from 'next/server';
import { requireAdmin }                   from '@/lib/auth-guards';
import { controlCenterServerApi }         from '@/lib/control-center-api';
import { ServerApiError }                 from '@/lib/server-api-client';

export async function POST(
  request: NextRequest,
  { params }: { params: Promise<{ tenantId: string; groupId: string }> },
): Promise<NextResponse> {
  const { tenantId, groupId } = await params;
  try { await requireAdmin(); }
  catch { return NextResponse.json({ message: 'Unauthorized' }, { status: 401 }); }

  let body: { userId?: string };
  try { body = await request.json(); }
  catch { return NextResponse.json({ message: 'Invalid request body.' }, { status: 400 }); }

  if (!body.userId) {
    return NextResponse.json({ message: 'userId is required.' }, { status: 400 });
  }

  try {
    await controlCenterServerApi.accessGroups.addMember(tenantId, groupId, body.userId);
    return NextResponse.json({ ok: true });
  } catch (err) {
    if (err instanceof ServerApiError) {
      return NextResponse.json({ message: err.message }, { status: err.status });
    }
    const message = err instanceof Error ? err.message : 'Failed to add member.';
    return NextResponse.json({ message }, { status: 500 });
  }
}
