import { redirect } from 'next/navigation';
import { getServerSession } from '@/lib/session';
import { BASE_PATH } from '@/lib/app-config';
import type { PlatformSession } from '@/types';

// TODO: integrate with Identity service session validation
// TODO: support cross-subdomain auth

/**
 * Control Center auth guard — requires PlatformAdmin system role.
 *
 * Used at the top of every Control Center Server Component and layout.
 *
 *   No session       → redirect to /login?reason=unauthenticated
 *   Not PlatformAdmin → redirect to /login?reason=unauthorized
 *
 * This is the strict admin guard — only PlatformAdmins pass through.
 * For pages that TenantAdmins are also permitted to access, use requireAdmin().
 */
export async function requirePlatformAdmin(): Promise<PlatformSession> {
  const session = await getServerSession();
  if (!session)               redirect(`${BASE_PATH}/login?reason=unauthenticated`);
  if (!session.isPlatformAdmin) redirect(`${BASE_PATH}/login?reason=unauthorized`);
  return session;
}

/**
 * Control Center auth guard — requires PlatformAdmin OR TenantAdmin.
 *
 * Used on pages and BFF routes that TenantAdmins are permitted to access
 * (user detail, role/membership/group mutations on their own tenant's users).
 *
 *   No session              → redirect to /login?reason=unauthenticated
 *   Neither admin role      → redirect to /login?reason=unauthorized
 *
 * Tenant isolation is NOT enforced here — it is enforced at two lower layers:
 *   1. BFF scope check (invite route: tenantId must match session.tenantId)
 *   2. Backend ClaimsPrincipal check (AdminEndpoints.cs — all mutation handlers)
 */
export async function requireAdmin(): Promise<PlatformSession> {
  const session = await getServerSession();
  if (!session) redirect(`${BASE_PATH}/login?reason=unauthenticated`);
  if (!session.isPlatformAdmin && !session.isTenantAdmin)
    redirect(`${BASE_PATH}/login?reason=unauthorized`);
  return session;
}
