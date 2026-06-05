/**
 * Control Center deployment configuration.
 *
 * The Control Center supports two deployment modes:
 *
 *  1. EMBEDDED (default) — mounted under a path prefix inside the operator portal.
 *     URL: https://app.legalsynq.com/control-center/...
 *     Set: NEXT_PUBLIC_CC_STANDALONE=false (or omit)
 *
 *  2. STANDALONE — deployed as its own frontend on a dedicated subdomain.
 *     URL: https://controlcenter.legalsynq.com/...
 *     Set: NEXT_PUBLIC_CC_STANDALONE=true
 *          NEXT_PUBLIC_CC_ORIGIN=https://controlcenter.legalsynq.com
 *          NEXT_PUBLIC_CC_LOGIN_URL=/login   (or operator portal login URL if separate)
 *
 * All Control Center internal links MUST be built via CCRoutes (lib/control-center-routes.ts)
 * — never hardcode /control-center strings directly in components or pages.
 *
 * These are NEXT_PUBLIC_ vars so they are available in both Server and Client Components.
 * GATEWAY_URL (server-only) is unchanged — it is already env-configurable.
 */

// ── Mode flag ─────────────────────────────────────────────────────────────────

/**
 * True when Control Center is deployed as a standalone app on its own host.
 * False (default) when embedded under /control-center in the operator portal.
 */
export const CC_STANDALONE: boolean =
  process.env.NEXT_PUBLIC_CC_STANDALONE === 'true';

// ── Path prefix ───────────────────────────────────────────────────────────────

/**
 * The URL path prefix under which the Control Center is mounted.
 *
 * Embedded mode (default): '/control-center'
 * Standalone mode:         '' (empty — routes live at host root)
 *
 * Override with NEXT_PUBLIC_CC_BASE_PATH if needed (unusual).
 * In standalone mode this is always '' regardless of env override.
 */
export const CC_BASE_PATH: string = CC_STANDALONE
  ? ''
  : (process.env.NEXT_PUBLIC_CC_BASE_PATH ?? '/control-center');

// ── Origin (standalone only) ──────────────────────────────────────────────────

/**
 * The full public origin of the Control Center.
 * Only meaningful in standalone mode.
 * Example: 'https://controlcenter.legalsynq.com'
 *
 * Used by the operator portal when it needs to link to the standalone CC
 * (e.g. the "Control Center" link in the operator top bar).
 */
export const CC_ORIGIN: string =
  process.env.NEXT_PUBLIC_CC_ORIGIN ?? '';

// ── Auth redirect targets ─────────────────────────────────────────────────────

/**
 * Where to redirect after sign-out or when session is missing.
 *
 * Embedded: '/login'  (operator portal login page on same host)
 * Standalone: '/login' (Control Center will need its own login page,
 *             or set NEXT_PUBLIC_CC_LOGIN_URL to the operator portal login URL)
 *
 * NOTE: In true standalone mode with a separate cookie domain, the login page
 * must set platform_session cookie scoped to .legalsynq.com. This is a backend
 * concern (Identity.Api) not resolvable in frontend config alone.
 */
export const CC_LOGIN_URL: string =
  process.env.NEXT_PUBLIC_CC_LOGIN_URL ?? '/login';

/**
 * Where to redirect when a non-PlatformAdmin tries to access Control Center.
 *
 * Embedded: '/dashboard' (operator portal dashboard on same host)
 * Standalone: CC_LOGIN_URL (no operator portal dashboard available on this host)
 */
export const CC_ACCESS_DENIED_URL: string = CC_STANDALONE
  ? CC_LOGIN_URL
  : (process.env.NEXT_PUBLIC_CC_ACCESS_DENIED_URL ?? '/dashboard');
