/**
 * GET /api/identity/admin/users/[id]/activity
 *
 * UIX-004 BFF proxy — returns the local AuditLog activity trail for a user.
 * Proxies to GET /identity/api/admin/users/{id}/activity.
 *
 * Access: PlatformAdmin or TenantAdmin.
 * Tenant boundary is enforced by the Identity service backend.
 */

import { type NextRequest, NextResponse } from 'next/server';
import { requireAdmin }                   from '@/lib/auth-guards';
import { controlCenterServerApi }         from '@/lib/control-center-api';

export async function GET(
  request: NextRequest,
  { params }: { params: Promise<{ id: string }> },
): Promise<NextResponse> {
  const { id } = await params;
  try {
    await requireAdmin();
  } catch {
    return NextResponse.json({ message: 'Unauthorized' }, { status: 401 });
  }

  const { searchParams } = request.nextUrl;
  const page     = parseInt(searchParams.get('page')     ?? '1',  10);
  const pageSize = parseInt(searchParams.get('pageSize') ?? '20', 10);
  const category = searchParams.get('category') ?? '';

  try {
    const result = await controlCenterServerApi.users.getActivity(id, {
      page,
      pageSize,
      category: category || undefined,
    });
    return NextResponse.json(result);
  } catch (err) {
    const message = err instanceof Error ? err.message : 'Failed to load user activity.';
    return NextResponse.json({ message }, { status: 500 });
  }
}
