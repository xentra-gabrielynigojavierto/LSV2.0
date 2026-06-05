/**
 * GET /api/identity/admin/users/[id]/permissions
 *
 * BFF proxy — returns the effective (union) permissions for a user.
 *
 * Effective permissions are the union of all capabilities across the user's
 * active role assignments. Each item includes source attribution (which
 * role(s) grant it).
 *
 * Access: PlatformAdmin or TenantAdmin (boundary enforced by identity service).
 * UIX-005
 */
import { type NextRequest, NextResponse } from 'next/server';
import { requireAdmin }                   from '@/lib/auth-guards';
import { controlCenterServerApi }         from '@/lib/control-center-api';

type Ctx = { params: Promise<{ id: string }> };

export async function GET(
  _request: NextRequest,
  { params }: Ctx,
): Promise<NextResponse> {
  const { id } = await params;
  try { await requireAdmin(); }
  catch { return NextResponse.json({ message: 'Unauthorized' }, { status: 401 }); }

  try {
    const result = await controlCenterServerApi.users.getEffectivePermissions(id);
    return NextResponse.json(result);
  } catch (err) {
    const message = err instanceof Error ? err.message : 'Failed to load user permissions.';
    const status  = message.includes('404') ? 404 : 500;
    return NextResponse.json({ message }, { status });
  }
}
