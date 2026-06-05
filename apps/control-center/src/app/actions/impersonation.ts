'use server';

/**
 * impersonation.ts — Server Actions for user-level impersonation.
 *
 * startImpersonationAction(user) — sets the cc_impersonation cookie and
 *   redirects to "/" so the shell immediately picks up the banner.
 *
 * stopImpersonationAction()      — clears the cookie and redirects to
 *   /tenant-users so the admin lands back in the user list.
 *
 * ── Security guards ──────────────────────────────────────────────────────────
 *
 *   Both actions call requirePlatformAdmin() before any mutation.
 *   This performs a full server-side session + role check:
 *     - No session cookie  → redirect /login?reason=unauthenticated
 *     - Session invalid    → redirect /login?reason=unauthenticated
 *     - Not PlatformAdmin  → redirect /login?reason=unauthorized
 *
 * ── Tenant context auto-alignment ────────────────────────────────────────────
 *
 *   startImpersonationAction writes the matching TenantContext whenever a
 *   context is not already set or is set to a different tenant. This ensures
 *   the cc_tenant_context and cc_impersonation cookies always agree on tenantId,
 *   satisfying the cross-tenant scope check in getImpersonation().
 *
 * ── Audit logging ─────────────────────────────────────────────────────────────
 *
 *   Both actions emit:
 *     1. A structured local log entry (visible in dev/prod log streams).
 *     2. A canonical audit event to the Platform Audit Event Service via
 *        controlCenterServerApi.auditIngest.emit() — fire-and-observe, never
 *        gates the impersonation operation if the audit pipeline is unavailable.
 */

import { redirect } from 'next/navigation';
import {
  requirePlatformAdmin,
  setImpersonation,
  clearImpersonation,
  getTenantContext,
  setTenantContext,
  getImpersonation,
  logImpersonationStart,
  logImpersonationStop,
} from '@/lib/auth';
import { controlCenterServerApi } from '@/lib/control-center-api';
import type { UserImpersonationSession, TenantContext } from '@/types/control-center';

// ── startImpersonationAction ──────────────────────────────────────────────────

/**
 * startImpersonationAction(user) — begin impersonating a tenant user.
 *
 * Accepts a minimal user descriptor (id, email, tenantId, tenantName) rather
 * than the full UserDetail shape so the action can be bound server-side:
 *
 *   const action = startImpersonationAction.bind(null, {
 *     id:         user.id,
 *     email:      user.email,
 *     tenantId:   user.tenantId,
 *     tenantName: user.tenantDisplayName,
 *   });
 *
 * Security:
 *   - requirePlatformAdmin() is called first — unauthenticated or non-admin
 *     callers are redirected before any mutation occurs.
 *   - If the active tenant context does not match the target user's tenantId,
 *     it is automatically overwritten to prevent a cross-tenant mismatch that
 *     would cause getImpersonation() to reject the session on first read.
 *
 * After writing the cookies, redirects to "/" so the global shell immediately
 * renders the impersonation banner.
 */
export async function startImpersonationAction(user: {
  id:         string;
  email:      string;
  tenantId:   string;
  tenantName: string;
}): Promise<never> {
  const session = await requirePlatformAdmin();

  // Auto-align tenant context with the impersonation target.
  // getImpersonation() enforces tenantId matching — if we write an impersonation
  // cookie without matching the tenant context, the first read would reject it.
  const currentTenantCtx = await getTenantContext();
  if (!currentTenantCtx || currentTenantCtx.tenantId !== user.tenantId) {
    const alignedCtx: TenantContext = {
      tenantId:   user.tenantId,
      tenantName: user.tenantName,
      tenantCode: user.tenantName.toUpperCase().replace(/\s+/g, '').slice(0, 10),
    };
    await setTenantContext(alignedCtx);
  }

  const impersonation: UserImpersonationSession = {
    adminId:               session.userId,
    impersonatedUserId:    user.id,
    impersonatedUserEmail: user.email,
    tenantId:              user.tenantId,
    tenantName:            user.tenantName,
    startedAtUtc:          new Date().toISOString(),
  };

  await setImpersonation(impersonation);

  // 1. Local structured log (visible in dev and prod NDJSON streams)
  logImpersonationStart(session.userId, user.id, user.tenantId);

  // 2. Canonical audit event — fire-and-observe, never gates redirect
  void controlCenterServerApi.auditIngest.emit({
    eventType:     'platform.admin.impersonation.started',
    eventCategory: 'Security',
    sourceSystem:  'control-center',
    sourceService: 'impersonation',
    visibility:    'Platform',
    severity:      'Warn',
    occurredAtUtc: new Date().toISOString(),
    scope:  { scopeType: 'Tenant', tenantId: user.tenantId },
    actor:  { id: session.userId, type: 'User', label: 'PlatformAdmin' },
    entity: { type: 'User', id: user.id },
    action:      'ImpersonationStarted',
    description: `Platform admin ${session.userId} started impersonating user ${user.email} (tenant: ${user.tenantId}).`,
    after:  JSON.stringify({ impersonatedUserId: user.id, impersonatedEmail: user.email, tenantId: user.tenantId }),
    tags:   ['impersonation', 'security', 'access-control'],
  }).catch(() => { /* audit pipeline unavailable — impersonation still succeeds */ });

  redirect('/');
}

// ── stopImpersonationAction ───────────────────────────────────────────────────

/**
 * stopImpersonationAction() — end the current impersonation session.
 *
 * Clears the cc_impersonation cookie. The cc_tenant_context cookie is
 * intentionally preserved so the admin returns to their previous scoped view.
 *
 * Security:
 *   - requirePlatformAdmin() is called first — ensures the session is still
 *     valid before attempting to read or clear the impersonation cookie.
 *
 * Redirects to /tenant-users after clearing so the admin lands in context.
 */
export async function stopImpersonationAction(): Promise<never> {
  const session = await requirePlatformAdmin();

  // Read current impersonation before clearing so we can include it in the
  // audit log entry (after clearing, the cookie is gone).
  const impersonation = await getImpersonation();

  await clearImpersonation();

  // 1. Local structured log
  if (impersonation) {
    logImpersonationStop(session.userId, impersonation.impersonatedUserId, impersonation.tenantId);
  } else {
    logImpersonationStop(session.userId, 'none', 'none');
  }

  // 2. Canonical audit event — fire-and-observe, never gates redirect
  void controlCenterServerApi.auditIngest.emit({
    eventType:     'platform.admin.impersonation.stopped',
    eventCategory: 'Security',
    sourceSystem:  'control-center',
    sourceService: 'impersonation',
    visibility:    'Platform',
    severity:      'Info',
    occurredAtUtc: new Date().toISOString(),
    scope:  { scopeType: 'Tenant', tenantId: impersonation?.tenantId ?? 'unknown' },
    actor:  { id: session.userId, type: 'User', label: 'PlatformAdmin' },
    entity: { type: 'User', id: impersonation?.impersonatedUserId ?? 'unknown' },
    action:      'ImpersonationStopped',
    description: `Platform admin ${session.userId} ended impersonation session${impersonation ? ` for user ${impersonation.impersonatedUserId}` : ''}.`,
    before: impersonation ? JSON.stringify({ impersonatedUserId: impersonation.impersonatedUserId, tenantId: impersonation.tenantId }) : undefined,
    tags:   ['impersonation', 'security', 'access-control'],
  }).catch(() => { /* audit pipeline unavailable — stop still succeeds */ });

  redirect('/tenant-users');
}
