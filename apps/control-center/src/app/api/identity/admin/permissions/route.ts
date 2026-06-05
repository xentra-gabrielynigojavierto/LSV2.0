/**
 * GET /api/identity/admin/permissions
 *
 * BFF proxy — returns the platform permission/capability catalog.
 * Used by the Permissions panel in the tenant User Management section.
 *
 * Access: PlatformAdmin or TenantAdmin.
 */
import { type NextRequest, NextResponse } from 'next/server';
import { requireAdmin }                   from '@/lib/auth-guards';
import { controlCenterServerApi }         from '@/lib/control-center-api';

export async function GET(request: NextRequest): Promise<NextResponse> {
  try { await requireAdmin(); }
  catch { return NextResponse.json({ message: 'Unauthorized' }, { status: 401 }); }

  const { searchParams } = request.nextUrl;
  const productId = searchParams.get('productId') ?? undefined;
  const search    = searchParams.get('search') ?? undefined;

  try {
    const items = await controlCenterServerApi.permissions.list({ productId, search });
    return NextResponse.json(items);
  } catch (err) {
    const message = err instanceof Error ? err.message : 'Failed to load permissions.';
    return NextResponse.json({ message }, { status: 500 });
  }
}
