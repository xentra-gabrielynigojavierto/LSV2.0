/**
 * POST /api/identity/admin/users/[id]/force-logout
 *
 * BFF proxy — revokes all active sessions for a user by incrementing
 * their session version. Existing JWTs with an older session_version
 * will be rejected by auth/me.
 * Called by the UserActions client component.
 *
 * Access: PlatformAdmin or TenantAdmin.
 * Tenant boundary is enforced by the identity service backend.
 */

import { type NextRequest, NextResponse } from 'next/server';
import { requireAdmin }                   from '@/lib/auth-guards';
import { controlCenterServerApi }         from '@/lib/control-center-api';

export async function POST(
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
    await controlCenterServerApi.users.forceLogout(id);
    return NextResponse.json({ ok: true });
  } catch (err) {
    const message = err instanceof Error ? err.message : 'Failed to force logout user.';
    return NextResponse.json({ message }, { status: 500 });
  }
}
