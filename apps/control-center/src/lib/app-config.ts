/**
 * app-config.ts — Control Center origin and path configuration.
 *
 * Used by middleware and auth redirects so that no URL is ever
 * hard-coded to localhost or a specific port. All redirect targets
 * must be derived from these constants.
 *
 * All environment variable reads are delegated to env.ts — this module
 * derives app-level constants (URLs, cookie names, paths) from those values.
 *
 * TODO: support cross-subdomain auth (set cookie domain to .legalsynq.com)
 * TODO: persist tenant context in backend session
 * TODO: integrate with Identity service session validation
 */

import {
  CONTROL_CENTER_ORIGIN as _ORIGIN,
  BASE_PATH as _BASE_PATH,
} from '@/lib/env';

/**
 * The canonical public origin of the Control Center.
 *
 * In production this will be something like https://controlcenter.legalsynq.com.
 * In development it is derived from the Replit dev domain env var,
 * falling back to the standard local port.
 *
 * The NEXT_PUBLIC_ prefix on the underlying env var makes this available to
 * both server and client-side code without a separate server-action fetch.
 *
 * Resolution priority (see env.ts):
 *   1. NEXT_PUBLIC_CONTROL_CENTER_ORIGIN
 *   2. REPLIT_DEV_DOMAIN → https://<domain>
 *   3. http://localhost:5004
 */
export const CONTROL_CENTER_ORIGIN: string = _ORIGIN;

/**
 * Optional URL path prefix for the Control Center (e.g. "/admin").
 * Leave empty in most deployments — only needed when the app is
 * mounted at a sub-path behind a reverse proxy.
 */
export const BASE_PATH: string = _BASE_PATH;

/**
 * The name of the session cookie used by the Control Center BFF.
 *
 * Matches the cookie set by POST /api/auth/login.
 * Named platform_session (not cc_session) for cross-app compatibility
 * with the Identity service JWT flow.
 *
 * TODO: rename to cc_session once cross-subdomain cookie scoping is in place
 */
export const SESSION_COOKIE_NAME = 'platform_session' as const;

/**
 * Full login URL — absolute origin + path.
 * Used in middleware and server-side redirects so they never contain
 * a hard-coded host.
 */
export const LOGIN_URL = `${CONTROL_CENTER_ORIGIN}${BASE_PATH}/login` as const;

/**
 * Cookie name for the active tenant context selected by a platform admin.
 *
 * Stores a JSON-serialised TenantContext (tenantId, tenantName, tenantCode).
 * Not an auth credential — presence is additive context, not a gate.
 * Cleared on logout and when the admin explicitly exits a tenant context.
 *
 * TODO: persist tenant context in backend session
 */
export const TENANT_CONTEXT_COOKIE_NAME = 'cc_tenant_context' as const;

/**
 * Cookie name for the active user-level impersonation session.
 *
 * Stores a JSON-serialised UserImpersonationSession.
 * Not an auth credential — does not grant access; Identity service must
 * issue a real impersonation token before any tenant-facing requests are made.
 *
 * Impersonation takes priority over tenant context when both are set.
 * Cleared on logout and when the admin clicks "Exit Impersonation".
 *
 * TODO: integrate with Identity service impersonation endpoint
 * TODO: issue temporary impersonation token
 */
export const IMPERSONATION_COOKIE_NAME = 'cc_impersonation' as const;
