/**
 * POST /api/identity/admin/users/[id]/memberships
 *
 * BFF proxy — assigns the user to an organization.
 * Body: { organizationId: string; memberRole?: string }
 *
 * Access: PlatformAdmin or TenantAdmin.
 * Tenant boundary is enforced by the identity service backend.
 */
import { type NextRequest, NextResponse } from 'next/server';
import { requireAdmin }                   from '@/lib/auth-guards';
import { controlCenterServerApi }         from '@/lib/control-center-api';

export async function POST(
  request: NextRequest,
  { params }: { params: Promise<{ id: string }> },
): Promise<NextResponse> {
  const { id } = await params;
  try { await requireAdmin(); }
  catch { return NextResponse.json({ message: 'Unauthorized' }, { status: 401 }); }

  let body: { organizationId?: string; memberRole?: string };
  try { body = await request.json(); }
  catch { return NextResponse.json({ message: 'Invalid request body.' }, { status: 400 }); }

  if (!body.organizationId) {
    return NextResponse.json({ message: 'organizationId is required.' }, { status: 400 });
  }

  try {
    await controlCenterServerApi.users.assignMembership(id, {
      organizationId: body.organizationId,
      memberRole:     body.memberRole,
    });
    return NextResponse.json({ ok: true });
  } catch (err) {
    const message = err instanceof Error ? err.message : 'Failed to assign membership.';
    const status  = message.includes('409') || message.toLowerCase().includes('conflict') ? 409 : 500;
    return NextResponse.json({ message }, { status });
  }
}
