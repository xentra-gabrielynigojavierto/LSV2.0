/**
 * auth.ts — Control Center authentication facade.
 *
 * Provides getSession() and requirePlatformAdmin() as the
 * canonical entry points for all auth checks in this app.
 *
 * Also owns the tenant context cookie helpers that power
 * Tenant Context Switching and Impersonation flows.
 *
 * Implementation delegates to session.ts (which calls
 * GET /identity/api/auth/me via the gateway) and auth-guards.ts.
 *
 * ── Cookie security strategy ─────────────────────────────────────────────────
 *
 *   Cookie                  httpOnly   sameSite (prod)  Notes
 *   ─────────────────────── ─────────  ───────────────  ──────────────────────
 *   platform_session        true       strict           JWT — set by BFF login
 *                                                       route, never readable
 *                                                       by client JS
 *   cc_impersonation        true       strict           Impersonation session —
 *                                                       only needed server-side;
 *                                                       made httpOnly to prevent
 *                                                       XSS from exfiltrating
 *                                                       the impersonated identity
 *   cc_tenant_context       false      strict           Non-auth UI state — kept
 *                                                       readable for potential
 *                                                       client-side banner reads;
 *                                                       upgraded to strict in prod
 *
 * ── Impersonation scope ──────────────────────────────────────────────────────
 *
 *   getImpersonation() enforces cross-tenant scope:
 *     - If a tenant context is set and its tenantId does NOT match the
 *       impersonation session's tenantId, the impersonation is rejected
 *       (returns null) and a security warning is logged.
 *     - If no tenant context is set, the impersonation is accepted as-is.
 *       The caller (startImpersonationAction) should always set the matching
 *       tenant context when starting impersonation.
 *
 * TODO: integrate with Identity service session validation
 * TODO: support cross-subdomain auth
 * TODO: persist tenant context in backend session
 * TODO: validate impersonation token server-side on every read
 */

import { cookies }        from 'next/headers';
import { getServerSession }                 from '@/lib/session';
import { requirePlatformAdmin as _requirePlatformAdmin } from '@/lib/auth-guards';
import { logWarn, logInfo }                 from '@/lib/logger';
import {
  TENANT_CONTEXT_COOKIE_NAME,
  IMPERSONATION_COOKIE_NAME,
}                                           from '@/lib/app-config';
import type { SessionUser }                 from '@/types/auth';
import type { PlatformSession }             from '@/types';
import type { TenantContext, UserImpersonationSession } from '@/types/control-center';

const IS_PROD = process.env.NODE_ENV === 'production';

// ── Session ───────────────────────────────────────────────────────────────────

/**
 * getSession() — reads the current session from the platform_session cookie.
 *
 * Calls GET /identity/api/auth/me to validate the token and populate
 * the session shape. Returns null if the cookie is absent, invalid,
 * or the token has expired.
 *
 * Call only from Server Components, Server Actions, or Route Handlers.
 * Never call from Client Components.
 *
 * TODO: replace remote /auth/me call with a local JWT decode +
 *       periodic revalidation once the Identity key endpoint is stable.
 */
export async function getSession(): Promise<PlatformSession | null> {
  return getServerSession();
}

/**
 * requirePlatformAdmin() — session guard for PlatformAdmin-only pages.
 *
 * - No session cookie  → redirect to /login?reason=unauthenticated
 * - Session invalid    → redirect to /login?reason=unauthenticated
 * - Not PlatformAdmin  → redirect to /login?reason=unauthorized
 * - Otherwise          → returns the full PlatformSession
 *
 * Use at the top of every Control Center Server Component / layout and
 * at the start of every Server Action that performs privileged mutations.
 * The middleware provides a first-pass cookie presence check; this
 * guard performs the definitive role check.
 *
 * TODO: add SupportAdmin role bypass once that role is defined
 */
export async function requirePlatformAdmin(): Promise<PlatformSession> {
  return _requirePlatformAdmin();
}

/**
 * toSessionUser() — maps a full PlatformSession to the lighter SessionUser
 * shape used by display components and client-safe auth checks.
 */
export function toSessionUser(session: PlatformSession): SessionUser {
  return {
    id:              session.userId,
    email:           session.email,
    roles:           session.systemRoles,
    isPlatformAdmin: session.isPlatformAdmin,
  };
}

// ── Tenant Context ────────────────────────────────────────────────────────────

/**
 * getTenantContext() — reads the active tenant context cookie.
 *
 * Returns a TenantContext if the platform admin has switched into a tenant,
 * or null when no tenant is selected (global admin view).
 *
 * Safe to call from Server Components, Server Actions, and Route Handlers.
 *
 * Validation:
 *   - All three required fields (tenantId, tenantName, tenantCode) must be
 *     non-empty strings. Malformed or incomplete cookies return null.
 *   - A security warning is logged when the cookie is present but invalid,
 *     so the developer can detect tampering or serialisation bugs.
 *
 * TODO: validate tenantId ownership against backend session
 * TODO: persist tenant context in backend session
 */
export async function getTenantContext(): Promise<TenantContext | null> {
  const cookieStore = await cookies();
  const raw = cookieStore.get(TENANT_CONTEXT_COOKIE_NAME)?.value;
  if (!raw) return null;
  try {
    const parsed = JSON.parse(raw) as unknown;
    if (
      parsed !== null &&
      typeof parsed === 'object' &&
      'tenantId'   in parsed &&
      typeof (parsed as Record<string, unknown>).tenantId   === 'string' &&
      ((parsed as Record<string, unknown>).tenantId as string).trim().length > 0 &&
      'tenantName' in parsed &&
      typeof (parsed as Record<string, unknown>).tenantName === 'string' &&
      ((parsed as Record<string, unknown>).tenantName as string).trim().length > 0 &&
      'tenantCode' in parsed &&
      typeof (parsed as Record<string, unknown>).tenantCode === 'string' &&
      ((parsed as Record<string, unknown>).tenantCode as string).trim().length > 0
    ) {
      return parsed as TenantContext;
    }
    // Cookie present but fails shape validation — log a security warning so
    // developers catch tampering or serialisation regressions early.
    logWarn('security.tenant_context.invalid_shape', {
      endpoint: TENANT_CONTEXT_COOKIE_NAME,
    });
    return null;
  } catch {
    logWarn('security.tenant_context.parse_error', {
      endpoint: TENANT_CONTEXT_COOKIE_NAME,
    });
    return null;
  }
}

/**
 * setTenantContext() — writes the active tenant context cookie.
 *
 * IMPORTANT: Must only be called from a Server Action or Route Handler.
 * Calling this inside a Server Component render will throw:
 *   "Cookies can only be modified in a Server Action or Route Handler."
 *
 * Cookie options:
 *   - httpOnly: false — not an auth credential; client JS may read it for
 *               optimistic UI state (e.g. banners) without a server round-trip.
 *   - sameSite: 'strict' in production — prevents the cookie from being sent
 *               on cross-site navigations, reducing CSRF exposure.
 *   - sameSite: 'lax' in development — allows hot-reload across origins.
 *   - path: '/' — available across all Control Center routes.
 *   - No maxAge — expires with the browser session; cleared on logout anyway.
 *
 * TODO: persist tenant context in backend session
 * TODO: add CSRF protection
 */
export async function setTenantContext(tenant: TenantContext): Promise<void> {
  const cookieStore = await cookies();
  cookieStore.set(TENANT_CONTEXT_COOKIE_NAME, JSON.stringify(tenant), {
    httpOnly: false,
    secure:   IS_PROD,
    sameSite: IS_PROD ? 'strict' : 'lax',
    path:     '/',
  });
}

/**
 * clearTenantContext() — removes the active tenant context cookie.
 *
 * IMPORTANT: Must only be called from a Server Action or Route Handler.
 *
 * Called:
 *   - on logout (BFF /api/auth/logout)
 *   - when the admin clicks "Exit tenant context"
 *   - when navigating back to the global admin view
 *
 * TODO: persist tenant context in backend session
 */
export async function clearTenantContext(): Promise<void> {
  const cookieStore = await cookies();
  cookieStore.delete(TENANT_CONTEXT_COOKIE_NAME);
}

// ── User Impersonation ────────────────────────────────────────────────────────

/**
 * getImpersonation() — reads the active user impersonation cookie.
 *
 * Returns a UserImpersonationSession when a platform admin is actively
 * impersonating a tenant user, or null otherwise.
 *
 * Safe to call from Server Components, Server Actions, and Route Handlers.
 *
 * Security validations:
 *   1. Cookie shape — all five required fields must be non-empty strings.
 *      Malformed cookies return null.
 *   2. Cross-tenant scope check — if a tenant context is active, the
 *      impersonation session's tenantId MUST match. A mismatch means the
 *      cookies are out of sync (tampered or state bug) and the impersonation
 *      is rejected. This prevents a platform admin from accidentally or
 *      maliciously impersonating a user from a different tenant while
 *      scoped to another tenant.
 *
 * TODO: integrate with Identity service impersonation endpoint
 * TODO: validate impersonation token server-side
 */
export async function getImpersonation(): Promise<UserImpersonationSession | null> {
  const cookieStore = await cookies();
  const raw = cookieStore.get(IMPERSONATION_COOKIE_NAME)?.value;
  if (!raw) return null;
  try {
    const parsed = JSON.parse(raw) as unknown;
    if (
      parsed !== null &&
      typeof parsed === 'object' &&
      'adminId'               in parsed &&
      typeof (parsed as Record<string, unknown>).adminId               === 'string' &&
      ((parsed as Record<string, unknown>).adminId as string).trim().length > 0 &&
      'impersonatedUserId'    in parsed &&
      typeof (parsed as Record<string, unknown>).impersonatedUserId    === 'string' &&
      ((parsed as Record<string, unknown>).impersonatedUserId as string).trim().length > 0 &&
      'impersonatedUserEmail' in parsed &&
      typeof (parsed as Record<string, unknown>).impersonatedUserEmail === 'string' &&
      ((parsed as Record<string, unknown>).impersonatedUserEmail as string).trim().length > 0 &&
      'tenantId'              in parsed &&
      typeof (parsed as Record<string, unknown>).tenantId              === 'string' &&
      ((parsed as Record<string, unknown>).tenantId as string).trim().length > 0 &&
      'startedAtUtc'          in parsed &&
      typeof (parsed as Record<string, unknown>).startedAtUtc          === 'string' &&
      ((parsed as Record<string, unknown>).startedAtUtc as string).trim().length > 0
    ) {
      const session = parsed as UserImpersonationSession;

      // ── Cross-tenant scope check ────────────────────────────────────────
      // If an active tenant context exists, the impersonation tenantId must
      // match it. A mismatch means cookies are out of sync; reject the
      // impersonation to prevent cross-tenant access.
      const tenantCtx = await getTenantContext();
      if (tenantCtx && tenantCtx.tenantId !== session.tenantId) {
        logWarn('security.impersonation.tenant_mismatch', {
          impersonatedUserId: session.impersonatedUserId,
          tenantId:           session.tenantId,
        });
        return null;
      }

      return session;
    }

    // Cookie present but fails shape validation
    logWarn('security.impersonation.invalid_shape', {
      endpoint: IMPERSONATION_COOKIE_NAME,
    });
    return null;
  } catch {
    logWarn('security.impersonation.parse_error', {
      endpoint: IMPERSONATION_COOKIE_NAME,
    });
    return null;
  }
}

/**
 * setImpersonation() — writes the user impersonation cookie.
 *
 * IMPORTANT: Must only be called from a Server Action or Route Handler.
 *
 * Cookie options:
 *   - httpOnly: true — the impersonation session contains a user identity
 *               (adminId + impersonatedUserId). Making it httpOnly prevents
 *               XSS from exfiltrating the impersonated identity or forging
 *               a new impersonation cookie client-side.
 *   - sameSite: 'strict' in production — prevents the cookie from being
 *               sent on cross-site navigations.
 *   - sameSite: 'lax' in development — allows hot-reload across origins.
 *   - path: '/' — available across all Control Center routes.
 *   - No maxAge — expires with the browser session.
 *
 * TODO: integrate with Identity service impersonation endpoint
 * TODO: issue temporary impersonation token
 */
export async function setImpersonation(session: UserImpersonationSession): Promise<void> {
  const cookieStore = await cookies();
  cookieStore.set(IMPERSONATION_COOKIE_NAME, JSON.stringify(session), {
    httpOnly: true,
    secure:   IS_PROD,
    sameSite: IS_PROD ? 'strict' : 'lax',
    path:     '/',
  });
}

/**
 * clearImpersonation() — removes the user impersonation cookie.
 *
 * IMPORTANT: Must only be called from a Server Action or Route Handler.
 *
 * Called:
 *   - on logout (BFF /api/auth/logout)
 *   - when the admin clicks "Exit Impersonation"
 *
 * Tenant context (cc_tenant_context) is NOT cleared — it persists so the admin
 * returns to the scoped view they had before starting impersonation.
 *
 * TODO: revoke impersonation token on Identity service
 */
export async function clearImpersonation(): Promise<void> {
  const cookieStore = await cookies();
  cookieStore.delete(IMPERSONATION_COOKIE_NAME);
}

// ── Audit log helpers ─────────────────────────────────────────────────────────

/**
 * logImpersonationStart — emits a structured audit log entry when a platform
 * admin starts impersonating a tenant user.
 *
 * Called from startImpersonationAction immediately after the cookie is written.
 * Uses logInfo so the event always appears in both dev and prod log streams.
 *
 * TODO: integrate with Identity service audit endpoint
 * TODO: persist to AuditLog table via Identity service
 */
export function logImpersonationStart(
  adminId:               string,
  impersonatedUserId:    string,
  tenantId:              string,
): void {
  logInfo('audit.impersonation.start', {
    impersonatedUserId,
    tenantId,
    adminId,
  });
}

/**
 * logImpersonationStop — emits a structured audit log entry when an admin
 * ends an impersonation session.
 *
 * Called from stopImpersonationAction immediately before the cookie is cleared.
 *
 * TODO: integrate with Identity service audit endpoint
 * TODO: persist to AuditLog table via Identity service
 */
export function logImpersonationStop(
  adminId:            string,
  impersonatedUserId: string,
  tenantId:           string,
): void {
  logInfo('audit.impersonation.stop', {
    impersonatedUserId,
    tenantId,
    adminId,
  });
}

/**
 * logTenantContextSwitch — emits a structured audit log entry when an admin
 * switches into or out of a tenant context.
 *
 * @param adminId   The platform admin's userId
 * @param tenantId  The target tenantId (null when exiting context)
 * @param action    'enter' | 'exit'
 *
 * TODO: integrate with Identity service audit endpoint
 * TODO: persist to AuditLog table via Identity service
 */
export function logTenantContextSwitch(
  adminId:  string,
  tenantId: string | null,
  action:   'enter' | 'exit',
): void {
  logInfo('audit.tenant_context.switch', {
    adminId,
    tenantId:   tenantId ?? undefined,
    auditAction: action,
  });
}
