import { NextResponse } from 'next/server';
import { requireAdmin } from '@/lib/auth-guards';
import { controlCenterServerApi } from '@/lib/control-center-api';

export async function GET(
  _request: Request,
  { params }: { params: Promise<{ id: string }> },
): Promise<NextResponse> {
  const { id } = await params;
  try { await requireAdmin(); }
  catch { return NextResponse.json({ message: 'Unauthorized' }, { status: 401 }); }

  try {
    const data = await controlCenterServerApi.users.getAssignableRoles(id);
    return NextResponse.json(data);
  } catch (err) {
    const message = err instanceof Error ? err.message : 'Failed to fetch assignable roles.';
    return NextResponse.json({ message }, { status: 500 });
  }
}
