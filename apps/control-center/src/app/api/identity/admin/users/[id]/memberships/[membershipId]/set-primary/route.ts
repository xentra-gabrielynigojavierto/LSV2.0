/**
 * POST /api/identity/admin/users/[id]/memberships/[membershipId]/set-primary
 *
 * BFF proxy — marks an org membership as the user's primary org.
 *
 * Access: PlatformAdmin or TenantAdmin.
 * Tenant boundary is enforced by the identity service backend.
 */
import { type NextRequest, NextResponse } from 'next/server';
import { requireAdmin }                   from '@/lib/auth-guards';
import { controlCenterServerApi }         from '@/lib/control-center-api';

export async function POST(
  _request: NextRequest,
  { params }: { params: Promise<{ id: string; membershipId: string }> },
): Promise<NextResponse> {
  const { id, membershipId } = await params;
  try { await requireAdmin(); }
  catch { return NextResponse.json({ message: 'Unauthorized' }, { status: 401 }); }

  try {
    await controlCenterServerApi.users.setPrimaryMembership(id, membershipId);
    return NextResponse.json({ ok: true });
  } catch (err) {
    const message = err instanceof Error ? err.message : 'Failed to set primary membership.';
    return NextResponse.json({ message }, { status: 500 });
  }
}
