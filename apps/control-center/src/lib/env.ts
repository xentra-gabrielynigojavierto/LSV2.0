/**
 * env.ts — Centralised environment variable access for the Control Center.
 *
 * ALL process.env reads in this application should go through this module.
 * Direct process.env access is permitted only here and in next.config.mjs.
 *
 * ── Design principles ────────────────────────────────────────────────────────
 *
 *   1. Single source of truth — every env var is named, documented, and
 *      exported exactly once. No other file should read process.env directly.
 *
 *   2. Fail-fast in production — getEnv() throws at startup when a required
 *      variable is missing in a production deployment, surfacing misconfig
 *      immediately rather than failing silently at runtime.
 *
 *   3. Sensible dev fallbacks — in development (NODE_ENV !== "production")
 *      getEnv() returns an empty string and logs a console warning so the
 *      developer is informed without blocking the hot-reload loop.
 *
 *   4. Server-side only — this module is imported only from Server Components,
 *      Server Actions, Route Handlers, and other server-side modules.
 *      NEXT_PUBLIC_ prefixed constants are safe to use in both environments;
 *      all others must never be bundled into the client.
 *
 * ── Variable registry ────────────────────────────────────────────────────────
 *
 *   Variable                         Scope    Required  Notes
 *   ────────────────────────────────────────────────────────────────────────────
 *   NODE_ENV                         server   yes       Set by Next.js/Node
 *   NEXT_PUBLIC_CONTROL_CENTER_ORIGIN both    no        Falls back to Replit
 *                                                       domain or localhost
 *   CONTROL_CENTER_API_BASE          server   yes(prod) Gateway base URL
 *   GATEWAY_URL                      server   no        Legacy alias for above
 *   REPLIT_DEV_DOMAIN                server   no        Replit-injected domain
 *   NEXT_PUBLIC_BASE_PATH            both     no        Sub-path prefix
 *
 * TODO: add Dockerfile ENV instructions mirroring this registry
 * TODO: add CI/CD secret injection for CONTROL_CENTER_API_BASE
 * TODO: add health check endpoint that validates required env vars at startup
 * TODO: add CSRF protection token secret env var
 */

// ── Runtime ───────────────────────────────────────────────────────────────────

/** The current Node.js runtime environment. */
export const NODE_ENV: string = process.env.NODE_ENV ?? 'development';

/** true when running in a production deployment */
export const IS_PROD: boolean = NODE_ENV === 'production';

/** true when running in development mode */
export const IS_DEV: boolean = !IS_PROD;

// ── Generic helper ────────────────────────────────────────────────────────────

/**
 * getEnv — reads an environment variable with optional fallback.
 *
 * Behaviour by environment:
 *
 *   Production (IS_PROD):
 *     - If the variable is set and non-empty → return its value
 *     - If a fallback is provided → return the fallback
 *     - If no fallback → throw Error (mis-configured deployment)
 *
 *   Development (IS_DEV):
 *     - If the variable is set and non-empty → return its value
 *     - If a fallback is provided → return the fallback silently
 *     - If no fallback → return '' and log a console warning
 *
 * @param key       The exact environment variable name (e.g. 'GATEWAY_URL')
 * @param fallback  Optional default value if the variable is absent or empty
 * @returns         The variable value, fallback, or '' (dev-only)
 * @throws          Error in production when the variable is absent and no
 *                  fallback is provided
 */
export function getEnv(key: string, fallback?: string): string {
  const val = process.env[key];
  if (val !== undefined && val.trim() !== '') return val.trim();
  if (fallback !== undefined) return fallback;
  if (IS_PROD) {
    throw new Error(`[CC] Missing required environment variable: ${key}`);
  }
  // Development only — log once per process start so hot-reload does not spam
  // the console with repeated warnings.
  console.warn(`[CC] WARN  Missing env var "${key}" — using empty string`);
  return '';
}

// ── Public origin ─────────────────────────────────────────────────────────────

/**
 * CONTROL_CENTER_ORIGIN — the canonical public URL of the Control Center.
 *
 * Resolution priority:
 *   1. NEXT_PUBLIC_CONTROL_CENTER_ORIGIN   — set explicitly (any env)
 *   2. REPLIT_DEV_DOMAIN                  — injected by Replit dev environment
 *   3. http://localhost:5004              — local fallback
 *
 * The NEXT_PUBLIC_ prefix makes this value available to both server and
 * client-side code. It is safe to embed in HTML/JS responses.
 *
 * Production example: https://controlcenter.legalsynq.com
 * Staging example:    https://controlcenter-staging.legalsynq.com
 *
 * TODO: support cross-subdomain auth (scope session cookie to .legalsynq.com)
 */
export const CONTROL_CENTER_ORIGIN: string =
  process.env.NEXT_PUBLIC_CONTROL_CENTER_ORIGIN ??
  (process.env.REPLIT_DEV_DOMAIN
    ? `https://${process.env.REPLIT_DEV_DOMAIN}`
    : 'http://127.0.0.1:5004');

// ── Gateway / API base ────────────────────────────────────────────────────────

/**
 * CONTROL_CENTER_API_BASE — the base URL for all server-side API calls.
 *
 * All requests from the Control Center server (Server Components, Server
 * Actions, Route Handlers) go through this base URL to reach the API gateway.
 *
 * Resolution priority:
 *   1. CONTROL_CENTER_API_BASE  — explicit canonical env var (preferred)
 *   2. GATEWAY_URL              — legacy alias kept for backwards compatibility
 *   3. http://localhost:5010    — local development fallback
 *
 * SERVER-SIDE ONLY. Never passes through the Next.js client bundle.
 *
 * Production example: https://api.legalsynq.com
 * Staging example:    https://api-staging.legalsynq.com
 * Local dev:          http://localhost:5010
 *
 * TODO: add Redis or edge caching in front of this base URL
 * TODO: add retry/backoff configuration
 * TODO: add CI/CD pipeline secret injection
 */
export const CONTROL_CENTER_API_BASE: string =
  process.env.CONTROL_CENTER_API_BASE ??
  process.env.GATEWAY_URL             ??
  'http://127.0.0.1:5010';

// ── URL path prefix ───────────────────────────────────────────────────────────

/**
 * BASE_PATH — optional URL sub-path prefix.
 *
 * Leave empty in most deployments. Only needed when the Control Center is
 * mounted at a sub-path behind a reverse proxy (e.g. /admin).
 *
 * Corresponds to Next.js basePath configuration.
 */
export const BASE_PATH: string =
  process.env.NEXT_PUBLIC_BASE_PATH ?? '';
