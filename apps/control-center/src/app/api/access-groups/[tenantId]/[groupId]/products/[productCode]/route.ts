import { type NextRequest, NextResponse } from 'next/server';
import { requireAdmin }                   from '@/lib/auth-guards';
import { controlCenterServerApi }         from '@/lib/control-center-api';
import { ServerApiError }                 from '@/lib/server-api-client';

export async function PUT(
  _request: NextRequest,
  { params }: { params: Promise<{ tenantId: string; groupId: string; productCode: string }> },
): Promise<NextResponse> {
  const { tenantId, groupId, productCode } = await params;
  try { await requireAdmin(); }
  catch { return NextResponse.json({ message: 'Unauthorized' }, { status: 401 }); }

  try {
    await controlCenterServerApi.accessGroups.grantProduct(tenantId, groupId, productCode);
    return NextResponse.json({ ok: true });
  } catch (err) {
    if (err instanceof ServerApiError) {
      return NextResponse.json({ message: err.message }, { status: err.status });
    }
    const message = err instanceof Error ? err.message : 'Failed to grant product.';
    return NextResponse.json({ message }, { status: 500 });
  }
}

export async function DELETE(
  _request: NextRequest,
  { params }: { params: Promise<{ tenantId: string; groupId: string; productCode: string }> },
): Promise<NextResponse> {
  const { tenantId, groupId, productCode } = await params;
  try { await requireAdmin(); }
  catch { return NextResponse.json({ message: 'Unauthorized' }, { status: 401 }); }

  try {
    await controlCenterServerApi.accessGroups.revokeProduct(tenantId, groupId, productCode);
    return NextResponse.json({ ok: true });
  } catch (err) {
    if (err instanceof ServerApiError) {
      return NextResponse.json({ message: err.message }, { status: err.status });
    }
    const message = err instanceof Error ? err.message : 'Failed to revoke product.';
    return NextResponse.json({ message }, { status: 500 });
  }
}
