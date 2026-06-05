/**
 * POST /api/identity/admin/users/[id]/cancel-invite
 *
 * BFF proxy — revokes all pending invitations for a user without issuing a
 * new one. The user record remains but their status transitions from "Invited"
 * to "Inactive" once the pending invitations are revoked.
 *
 * Returns 409 (forwarded as 500 with message) when no pending invitation
 * exists — the UI guard on isInvited status should prevent this in practice.
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
    await controlCenterServerApi.users.cancelInvite(id);
    return NextResponse.json({ ok: true });
  } catch (err) {
    const message = err instanceof Error ? err.message : 'Failed to cancel invitation.';
    return NextResponse.json({ message }, { status: 500 });
  }
}
