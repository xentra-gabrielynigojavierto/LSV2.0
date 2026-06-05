/**
 * POST /api/identity/admin/users/[id]/reset-password
 *
 * BFF proxy — triggers an admin-initiated password reset for a user.
 * Creates a reset token; in dev mode the token is logged, in production
 * an email is sent.
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
    await controlCenterServerApi.users.resetPassword(id);
    return NextResponse.json({ ok: true });
  } catch (err) {
    const message = err instanceof Error ? err.message : 'Failed to trigger password reset.';
    return NextResponse.json({ message }, { status: 500 });
  }
}
