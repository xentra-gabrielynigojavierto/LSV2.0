/**
 * GET /api/identity/admin/users/[id]/security
 *
 * BFF proxy — returns a security summary for a user:
 * lock state, last login, session version, recent password resets.
 *
 * Access: PlatformAdmin or TenantAdmin.
 * Tenant boundary is enforced by the identity service backend.
 */

import { type NextRequest, NextResponse } from 'next/server';
import { requireAdmin }                   from '@/lib/auth-guards';
import { controlCenterServerApi }         from '@/lib/control-center-api';

export async function GET(
  _request: NextRequest,
  { params }: { params: Promise<{ id: string }> },
): Promise<NextResponse> {
  const { id } = await params;
  try {
    await requireAdmin();
  } catch {
    return NextResponse.json({ message: 'Unauthorized' }, { status: 401 });
  }

  try {
    const data = await controlCenterServerApi.users.getSecurity(id);
    return NextResponse.json(data ?? {});
  } catch (err) {
    const message = err instanceof Error ? err.message : 'Failed to load security info.';
    return NextResponse.json({ message }, { status: 500 });
  }
}
