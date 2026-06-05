import { type NextRequest, NextResponse } from 'next/server';
import { requireAdmin }                   from '@/lib/auth-guards';
import { controlCenterServerApi }         from '@/lib/control-center-api';
import { ServerApiError }                 from '@/lib/server-api-client';

export async function PATCH(
  request: NextRequest,
  { params }: { params: Promise<{ tenantId: string; groupId: string }> },
): Promise<NextResponse> {
  const { tenantId, groupId } = await params;
  try { await requireAdmin(); }
  catch { return NextResponse.json({ message: 'Unauthorized' }, { status: 401 }); }

  let body: { name?: string; description?: string };
  try { body = await request.json(); }
  catch { return NextResponse.json({ message: 'Invalid request body.' }, { status: 400 }); }

  if (!body.name) {
    return NextResponse.json({ message: 'name is required.' }, { status: 400 });
  }

  try {
    const result = await controlCenterServerApi.accessGroups.update(tenantId, groupId, {
      name:        body.name,
      description: body.description,
    });
    return NextResponse.json(result);
  } catch (err) {
    if (err instanceof ServerApiError) {
      return NextResponse.json({ message: err.message }, { status: err.status });
    }
    const message = err instanceof Error ? err.message : 'Failed to update group.';
    return NextResponse.json({ message }, { status: 500 });
  }
}

export async function DELETE(
  _request: NextRequest,
  { params }: { params: Promise<{ tenantId: string; groupId: string }> },
): Promise<NextResponse> {
  const { tenantId, groupId } = await params;
  try { await requireAdmin(); }
  catch { return NextResponse.json({ message: 'Unauthorized' }, { status: 401 }); }

  try {
    await controlCenterServerApi.accessGroups.archive(tenantId, groupId);
    return NextResponse.json({ ok: true });
  } catch (err) {
    if (err instanceof ServerApiError) {
      return NextResponse.json({ message: err.message }, { status: err.status });
    }
    const message = err instanceof Error ? err.message : 'Failed to archive group.';
    return NextResponse.json({ message }, { status: 500 });
  }
}
