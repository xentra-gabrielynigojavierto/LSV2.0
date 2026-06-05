/**
 * POST /api/identity/admin/users/invite
 *
 * BFF proxy — sends a new user invitation.
 * Called by the InviteUserForm client component.
 *
 * Access: PlatformAdmin or TenantAdmin.
 * TenantAdmin scope: the tenantId in the request body must match the
 * caller's own tenant — enforced here (BFF layer) AND downstream in the
 * identity service (ClaimsPrincipal check in AdminEndpoints.cs).
 *
 * Response includes inviteToken in non-production environments so the admin
 * can hand-deliver the activation link when email delivery is unavailable.
 */

import { type NextRequest, NextResponse } from 'next/server';
import { requireAdmin }                   from '@/lib/auth-guards';
import { controlCenterServerApi }         from '@/lib/control-center-api';

interface InviteUserBody {
  email:           string;
  firstName:       string;
  lastName:        string;
  tenantId:        string;
  organizationId?: string;
  memberRole?:     string;
}

export async function POST(request: NextRequest): Promise<NextResponse> {
  let session;
  try {
    session = await requireAdmin();
  } catch {
    return NextResponse.json({ message: 'Unauthorized' }, { status: 401 });
  }

  let body: InviteUserBody;
  try {
    body = await request.json() as InviteUserBody;
  } catch {
    return NextResponse.json({ message: 'Invalid JSON body' }, { status: 400 });
  }

  if (!body.email || !body.firstName || !body.lastName || !body.tenantId) {
    return NextResponse.json(
      { message: 'email, firstName, lastName, and tenantId are required.' },
      { status: 400 },
    );
  }

  // TenantAdmin scope check — may only invite users into their own tenant.
  if (!session.isPlatformAdmin && body.tenantId !== session.tenantId) {
    return NextResponse.json(
      { message: 'TenantAdmin may only invite users into their own tenant.' },
      { status: 403 },
    );
  }

  try {
    const result = await controlCenterServerApi.users.invite(body);
    return NextResponse.json({ ok: true, activationLink: result.activationLink ?? null });
  } catch (err) {
    const message = err instanceof Error ? err.message : 'Failed to send invitation.';
    return NextResponse.json({ message }, { status: 500 });
  }
}
