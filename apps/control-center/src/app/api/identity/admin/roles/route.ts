/**
 * GET /api/identity/admin/roles
 *
 * BFF proxy — returns the full list of platform roles.
 * Used by the Permissions panel in the tenant User Management section.
 *
 * Access: PlatformAdmin or TenantAdmin.
 */
import { NextResponse }               from 'next/server';
import { requireAdmin }               from '@/lib/auth-guards';
import { controlCenterServerApi }     from '@/lib/control-center-api';

export async function GET(): Promise<NextResponse> {
  try { await requireAdmin(); }
  catch { return NextResponse.json({ message: 'Unauthorized' }, { status: 401 }); }

  try {
    const roles = await controlCenterServerApi.roles.list();
    return NextResponse.json(roles);
  } catch (err) {
    const message = err instanceof Error ? err.message : 'Failed to load roles.';
    return NextResponse.json({ message }, { status: 500 });
  }
}
