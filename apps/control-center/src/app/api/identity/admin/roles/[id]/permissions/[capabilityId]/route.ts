/**
 * DELETE /api/identity/admin/roles/[id]/permissions/[capabilityId]
 *
 * BFF proxy — revoke a capability permission from a role.
 *
 * Access: PlatformAdmin only (LS-ID-TNT-014 governance hardening).
 *         Product role → permission mapping is a platform governance action.
 *         Tenant Portal manages tenant roles via its own BFF.
 */
import { type NextRequest, NextResponse } from 'next/server';
import { requirePlatformAdmin }           from '@/lib/auth-guards';
import { controlCenterServerApi }         from '@/lib/control-center-api';

type Ctx = { params: Promise<{ id: string; capabilityId: string }> };

export async function DELETE(
  _request: NextRequest,
  { params }: Ctx,
): Promise<NextResponse> {
  const { id, capabilityId } = await params;
  try { await requirePlatformAdmin(); }
  catch { return NextResponse.json({ message: 'Unauthorized' }, { status: 401 }); }

  try {
    await controlCenterServerApi.roles.revokePermission(id, capabilityId);
    return new NextResponse(null, { status: 204 });
  } catch (err) {
    const message = err instanceof Error ? err.message : 'Failed to revoke permission.';
    const status  = message.includes('404') ? 404
                  : message.includes('403') ? 403
                  : 500;
    return NextResponse.json({ message }, { status });
  }
}
