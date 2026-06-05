'use server';

/**
 * tenant-context.ts — Server Actions for Tenant Context Switching.
 *
 * These actions are the only write path for the cc_tenant_context cookie.
 * All cookie mutations (set/clear) must go through Server Actions — calling
 * cookies().set() inside a Server Component render throws.
 *
 * ── Security guards ──────────────────────────────────────────────────────────
 *
 *   Both actions call requirePlatformAdmin() before any mutation.
 *   This performs a full server-side session + role check:
 *     - No session cookie  → redirect /login?reason=unauthenticated
 *     - Session invalid    → redirect /login?reason=unauthenticated
 *     - Not PlatformAdmin  → redirect /login?reason=unauthorized
 *
 * ── Audit logging ─────────────────────────────────────────────────────────────
 *
 *   Both actions emit structured audit log entries (audit.tenant_context.switch)
 *   visible in dev and captured in production NDJSON logs.
 *
 * TODO: persist tenant context in backend session
 * TODO: integrate impersonation with Identity service
 * TODO: add CSRF protection
 */

import { redirect } from 'next/navigation';
import {
  requirePlatformAdmin,
  setTenantContext,
  clearTenantContext,
  logTenantContextSwitch,
} from '@/lib/auth';
import type { TenantContext } from '@/types/control-center';

/**
 * switchTenantContextAction — activates a tenant context for the current admin.
 *
 * Writes the TenantContext to the cc_tenant_context cookie and redirects to
 * the root page. The CCShell banner will appear on every subsequent page
 * while the context is active.
 *
 * Security:
 *   - requirePlatformAdmin() is called first — unauthenticated or non-admin
 *     callers are redirected before any mutation occurs.
 *
 * Usage (Server Component with bound action):
 *   const action = switchTenantContextAction.bind(null, tenantCtx);
 *   <form action={action}><button type="submit">Switch</button></form>
 *
 * TODO: persist tenant context in backend session
 * TODO: validate tenantId ownership against backend session
 */
export async function switchTenantContextAction(tenant: TenantContext): Promise<never> {
  const session = await requirePlatformAdmin();

  await setTenantContext(tenant);

  // Emit audit log after writing the cookie
  logTenantContextSwitch(session.userId, tenant.tenantId, 'enter');

  // TODO: persist tenant context in backend session
  // TODO: emit audit event to AuditLog table via Identity service

  redirect('/');
}

/**
 * exitTenantContextAction — clears the active tenant context.
 *
 * Removes the cc_tenant_context cookie and redirects to the tenants list,
 * returning the admin to the global platform view.
 *
 * Security:
 *   - requirePlatformAdmin() is called first — ensures the session is still
 *     valid before clearing the context.
 *
 * TODO: persist tenant context in backend session
 * TODO: integrate impersonation with Identity service
 */
export async function exitTenantContextAction(): Promise<never> {
  const session = await requirePlatformAdmin();

  await clearTenantContext();

  // Emit audit log after clearing the cookie
  logTenantContextSwitch(session.userId, null, 'exit');

  // TODO: persist tenant context in backend session
  // TODO: emit audit event to AuditLog table via Identity service

  redirect('/tenants');
}
