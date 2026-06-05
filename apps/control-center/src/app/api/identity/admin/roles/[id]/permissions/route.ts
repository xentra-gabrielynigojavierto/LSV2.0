/**
 * GET  /api/identity/admin/roles/[id]/permissions
 * POST /api/identity/admin/roles/[id]/permissions
 *
 * BFF proxy — list and assign capability permissions on a role.
 *
 * GET:  Returns all capabilities currently assigned to the role.
 * POST: Assigns a capability to the role.
 *       Body: { capabilityId: string }
 *
 * Access: PlatformAdmin only (LS-ID-TNT-014 governance hardening).
 *         Product role → permission mapping is a platform governance action.
 *         Tenant Portal manages tenant roles via its own BFF.
 */
import { type NextRequest, NextResponse } from 'next/server';
import { requirePlatformAdmin }           from '@/lib/auth-guards';
import { controlCenterServerApi }         from '@/lib/control-center-api';

type Ctx = { params: Promise<{ id: string }> };

export async function GET(
  _request: NextRequest,
  { params }: Ctx,
): Promise<NextResponse> {
  const { id } = await params;
  try { await requirePlatformAdmin(); }
  catch { return NextResponse.json({ message: 'Unauthorized' }, { status: 401 }); }

  try {
    const items = await controlCenterServerApi.roles.getPermissions(id);
    return NextResponse.json(items);
  } catch (err) {
    const message = err instanceof Error ? err.message : 'Failed to load role permissions.';
    return NextResponse.json({ message }, { status: 500 });
  }
}

export async function POST(
  request: NextRequest,
  { params }: Ctx,
): Promise<NextResponse> {
  const { id } = await params;
  try { await requirePlatformAdmin(); }
  catch { return NextResponse.json({ message: 'Unauthorized' }, { status: 401 }); }

  let body: { capabilityId?: string };
  try { body = await request.json(); }
  catch { return NextResponse.json({ message: 'Invalid request body.' }, { status: 400 }); }

  if (!body.capabilityId) {
    return NextResponse.json({ message: 'capabilityId is required.' }, { status: 400 });
  }

  try {
    await controlCenterServerApi.roles.assignPermission(id, body.capabilityId);
    return NextResponse.json({ ok: true }, { status: 201 });
  } catch (err) {
    const message = err instanceof Error ? err.message : 'Failed to assign permission.';
    const status  = message.includes('409') || message.toLowerCase().includes('conflict') ? 409
                  : message.includes('404') ? 404
                  : message.includes('403') ? 403
                  : 500;
    return NextResponse.json({ message }, { status });
  }
}
