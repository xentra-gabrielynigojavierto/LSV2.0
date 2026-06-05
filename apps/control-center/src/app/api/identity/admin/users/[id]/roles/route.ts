/**
 * POST /api/identity/admin/users/[id]/roles
 *
 * BFF proxy — assigns a role to a user (GLOBAL scope).
 * Body: { roleId: string }
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

  let body: { roleId?: string };
  try { body = await request.json(); }
  catch { return NextResponse.json({ message: 'Invalid request body.' }, { status: 400 }); }

  if (!body.roleId) {
    return NextResponse.json({ message: 'roleId is required.' }, { status: 400 });
  }

  try {
    await controlCenterServerApi.users.assignRole(id, body.roleId);
    return NextResponse.json({ ok: true });
  } catch (err) {
    const message = err instanceof Error ? err.message : 'Failed to assign role.';
    const lower = message.toLowerCase();
    let status = 500;
    if (lower.includes('409') || lower.includes('conflict') || lower.includes('already')) status = 409;
    else if (lower.includes('product_not_enabled') || lower.includes('invalid_org_type') || lower.includes('no_organization_membership')) status = 400;
    else if (lower.includes('not found') || lower.includes('404')) status = 404;
    else if (lower.includes('403') || lower.includes('forbid')) status = 403;
    return NextResponse.json({ message }, { status });
  }
}
