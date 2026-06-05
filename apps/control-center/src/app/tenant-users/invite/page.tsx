/**
 * /tenant-users/invite — Invite a new user to a tenant.
 *
 * Server component: resolves the active tenant context from the session and
 * passes it to the client form.  When a tenant context is active (TenantAdmin
 * or a PlatformAdmin who has selected a tenant), the Tenant ID field is
 * pre-filled and locked — no UUID copy-pasting required.
 */

import { getSession, getTenantContext } from '@/lib/auth';
import { InviteUserForm }               from './invite-form';
import { redirect }                     from 'next/navigation';

export const dynamic = 'force-dynamic';

export default async function InviteUserPage() {
  const session = await getSession();
  if (!session) redirect('/login');

  const tenantCtx = await getTenantContext();

  return (
    <InviteUserForm
      resolvedTenantId={tenantCtx?.tenantId}
      resolvedTenantName={tenantCtx?.tenantName}
    />
  );
}
