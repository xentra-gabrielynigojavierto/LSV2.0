/**
 * DELETE /api/identity/admin/users/[id]/memberships/[membershipId]
 *
 * BFF proxy — removes an org membership from the user.
 * Backend enforces safety rules:
 *   409 LAST_MEMBERSHIP — cannot remove last active membership
 *   409 PRIMARY_MEMBERSHIP — must designate another primary first
 *
 * Access: PlatformAdmin or TenantAdmin.
 * Tenant boundary is enforced by the identity service backend.
 */
import { type NextRequest, NextResponse } from 'next/server';
import { requireAdmin }                   from '@/lib/auth-guards';
import { controlCenterServerApi }         from '@/lib/control-center-api';

export async function DELETE(
  _request: NextRequest,
  { params }: { params: Promise<{ id: string; membershipId: string }> },
): Promise<NextResponse> {
  const { id, membershipId } = await params;
  try { await requireAdmin(); }
  catch { return NextResponse.json({ message: 'Unauthorized' }, { status: 401 }); }

  try {
    await controlCenterServerApi.users.removeMembership(id, membershipId);
    return NextResponse.json({ ok: true });
  } catch (err) {
    const message = err instanceof Error ? err.message : 'Failed to remove membership.';
    const isConflict = message.includes('409') || message.toLowerCase().includes('conflict')
      || message.toLowerCase().includes('last') || message.toLowerCase().includes('primary');
    return NextResponse.json({ message }, { status: isConflict ? 409 : 500 });
  }
}
