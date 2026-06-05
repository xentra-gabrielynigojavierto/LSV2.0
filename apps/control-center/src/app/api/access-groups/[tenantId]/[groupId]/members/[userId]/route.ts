import { type NextRequest, NextResponse } from 'next/server';
import { requireAdmin }                   from '@/lib/auth-guards';
import { controlCenterServerApi }         from '@/lib/control-center-api';
import { ServerApiError }                 from '@/lib/server-api-client';

export async function DELETE(
  _request: NextRequest,
  { params }: { params: Promise<{ tenantId: string; groupId: string; userId: string }> },
): Promise<NextResponse> {
  const { tenantId, groupId, userId } = await params;
  try { await requireAdmin(); }
  catch { return NextResponse.json({ message: 'Unauthorized' }, { status: 401 }); }

  try {
    await controlCenterServerApi.accessGroups.removeMember(tenantId, groupId, userId);
    return NextResponse.json({ ok: true });
  } catch (err) {
    if (err instanceof ServerApiError) {
      return NextResponse.json({ message: err.message }, { status: err.status });
    }
    const message = err instanceof Error ? err.message : 'Failed to remove member.';
    return NextResponse.json({ message }, { status: 500 });
  }
}
