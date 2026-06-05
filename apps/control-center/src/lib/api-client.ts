/**
 * api-client.ts — Control Center server-side HTTP client.
 *
 * Wraps fetch() for use in Server Components, Server Actions, and Route
 * Handlers. Reads the platform_session JWT from the request cookie store
 * and forwards it as Authorization: Bearer on every outbound request.
 *
 * Base URL is controlled by CONTROL_CENTER_API_BASE (e.g. the API gateway).
 * Falls back to GATEWAY_URL → http://localhost:5010 if not set.
 *
 * ── Request tracing ──────────────────────────────────────────────────────────
 *
 *   Every request is assigned a UUID requestId via crypto.randomUUID().
 *   The ID is attached as:
 *     - X-Request-Id header → forwarded to the API gateway and downstream
 *       services so a single user-visible error can be traced end-to-end
 *     - all log entries for the request lifecycle
 *
 * ── Session pre-flight check ──────────────────────────────────────────────────
 *
 *   Before building headers or logging anything, apiFetch checks whether the
 *   platform_session cookie is present. If it is missing entirely, the request
 *   is aborted and the caller is redirected to /login?reason=session_expired.
 *
 *   This avoids an unnecessary gateway round-trip: without a token the gateway
 *   would return 401 anyway, which apiFetch would then redirect on. The
 *   pre-flight check short-circuits that path immediately and logs a WARN
 *   entry so operators can detect clients with stale sessions.
 *
 *   Note: the check is a cookie-presence guard only — it does NOT validate the
 *   JWT signature or expiry. Full validation happens on the Identity service
 *   when the token is forwarded. A token that is present but expired still
 *   results in a 401 response (and subsequent redirect) at step 6 below.
 *
 * ── Observability ────────────────────────────────────────────────────────────
 *
 *   apiFetch emits structured log entries at each lifecycle point:
 *     api.request.start       — method, endpoint, tenantId, impersonation
 *     api.request.success     — status, durationMs
 *     api.request.unauthorized_401 — downstream service auth failure, durationMs
 *     api.request.error       — status, message, durationMs (4xx/5xx)
 *     api.network_failure     — network-level error before HTTP response
 *     security.session.missing_token — pre-flight abort (no cookie at all)
 *
 * ── Caching (GET requests only) ──────────────────────────────────────────────
 *
 *   - Pass revalidateSeconds to opt into Next.js ISR-style fetch caching.
 *   - Pass tags[] to enable on-demand revalidation via revalidateTag().
 *   - Mutations (POST/PATCH/PUT/DELETE) always use cache: 'no-store'.
 *   - Reads with no revalidateSeconds default to cache: 'no-store' (safe default).
 *
 * ── Error handling ────────────────────────────────────────────────────────────
 *
 *   HTTP 401 → throws ApiError(401) — session guards redirect before any call
 *   HTTP 403 → throws ApiError (Forbidden)
 *   Other non-2xx → throws ApiError with status + message
 *   Network error → throws the original fetch error
 *
 * TODO: add retry/backoff
 * TODO: add Redis or edge caching
 * TODO: add stale-while-revalidate strategy
 * TODO: add request deduplication
 */

import { redirect }                          from 'next/navigation';
import { cookies }                           from 'next/headers';
import { logInfo, logWarn, logError }        from '@/lib/logger';
import { getTenantContext, getImpersonation } from '@/lib/auth';
import { CONTROL_CENTER_API_BASE }           from '@/lib/env';

// ── Config ────────────────────────────────────────────────────────────────────

// All env var resolution is centralised in env.ts; no process.env reads here.
const API_BASE: string = CONTROL_CENTER_API_BASE;

// ── Error ─────────────────────────────────────────────────────────────────────

/**
 * ApiError — thrown for any non-2xx response except 401 (which redirects).
 *
 * Carries the HTTP status code so callers can distinguish 404 (not found)
 * from 500 (server error) and render appropriate UI.
 */
export class ApiError extends Error {
  constructor(
    public readonly status:  number,
    message: string,
  ) {
    super(message);
    this.name = 'ApiError';
  }

  /** true when the admin does not have permission for the requested resource */
  get isForbidden(): boolean { return this.status === 403; }

  /** true when the requested resource does not exist */
  get isNotFound(): boolean { return this.status === 404; }

  /** true when the upstream service is unavailable */
  get isServerError(): boolean { return this.status >= 500; }
}

// ── Options ───────────────────────────────────────────────────────────────────

/**
 * ApiFetchOptions — extended options for apiFetch.
 *
 * method            — HTTP verb (default "GET")
 * body              — JSON-serialisable request body
 * revalidateSeconds — seconds until the Next.js Data Cache entry expires.
 *                     Only applied on GET requests; ignored on mutations.
 *                     Pass 0 to force re-fetch on every request (same as
 *                     cache: 'no-store' but keeps the cache entry warm for
 *                     on-demand revalidation via tags).
 * tags              — cache tags for on-demand revalidation via revalidateTag().
 *                     Only applied on GET requests.
 *
 * TODO: add Redis or edge caching
 * TODO: add stale-while-revalidate strategy
 * TODO: add request deduplication
 */
export interface ApiFetchOptions {
  method?:            string;
  body?:              unknown;
  revalidateSeconds?: number;
  tags?:              string[];
}

// ── Core ──────────────────────────────────────────────────────────────────────

/**
 * apiFetch<T>(path, options?) — send a typed HTTP request to the API gateway.
 *
 * ── Request lifecycle ─────────────────────────────────────────────────────────
 *
 *   1. Generate requestId (UUID) + read tenant/impersonation context
 *   2. Build headers: Authorization, Content-Type, X-Request-Id
 *   3. Build Next.js cache config based on method + revalidateSeconds
 *   4. Log api.request.start
 *   5. Call fetch() — network errors are caught and logged separately
 *   6. Log api.request.unauthorized_401 + throw ApiError(401) on 401
 *   7. Log api.request.error + throw ApiError on non-2xx
 *   8. Log api.request.success
 *   9. Return parsed JSON body (or undefined for 204)
 *
 * ── Caching behaviour ─────────────────────────────────────────────────────────
 *
 *   GET + revalidateSeconds set → next: { revalidate, tags }
 *   GET + no revalidateSeconds  → cache: 'no-store'
 *   Non-GET                     → cache: 'no-store' (never cache mutations)
 *
 * TODO: add retry/backoff
 * TODO: add Redis or edge caching
 * TODO: add stale-while-revalidate strategy
 * TODO: add request deduplication
 */
export async function apiFetch<T>(
  path:    string,
  options: ApiFetchOptions = {},
): Promise<T> {
  // ── 1. Request identity + context ────────────────────────────────────────

  // Unique ID for this request — forwarded to the gateway via X-Request-Id
  // and included in every log entry so the full lifecycle is searchable by ID.
  // TODO: integrate with Datadog / OpenTelemetry trace context
  const requestId = crypto.randomUUID();

  // Read active tenant + impersonation context from cookies.
  // getTenantContext() and getImpersonation() both call cookies() internally;
  // Next.js memoises cookies() per-request so there is no extra overhead.
  const tenantCtx      = await getTenantContext();
  const impersonation  = await getImpersonation();

  // Build a shared meta object for all log entries in this request's lifecycle
  const method  = options.method ?? 'GET';
  const logMeta = {
    requestId,
    method,
    endpoint: path,
    ...(tenantCtx     ? { tenantId: tenantCtx.tenantId, tenantCode: tenantCtx.tenantCode } : {}),
    ...(impersonation ? {
      impersonatedUserId:    impersonation.impersonatedUserId,
      impersonatedUserEmail: impersonation.impersonatedUserEmail,
    } : {}),
  };

  // ── 2. Auth header ────────────────────────────────────────────────────────

  const cookieStore = await cookies();
  const token = cookieStore.get('platform_session')?.value;

  // ── Session pre-flight check ───────────────────────────────────────────
  // If the session cookie is completely absent, short-circuit immediately.
  // This avoids a gateway round-trip that would result in a 401 → redirect
  // anyway. A WARN log is emitted so operators can detect stale sessions.
  // Note: this is a presence check only. JWT expiry / signature validation
  // happens on the Identity service when the token is forwarded (step 5+).
  // TODO: add CSRF protection
  // TODO: add rate limiting on failed auth attempts
  if (!token) {
    logWarn('security.session.missing_token', { requestId, method, endpoint: path });
    redirect('/login?reason=session_expired');
  }

  const headers: Record<string, string> = {
    'Content-Type': 'application/json',
    'Accept':       'application/json',
    // Attach requestId so the API gateway and downstream services can correlate
    // log entries across the entire call chain.
    // TODO: integrate with Datadog / OpenTelemetry distributed tracing
    'X-Request-Id': requestId,
    'Authorization': `Bearer ${token}`,
  };

  // ── 3. Cache config ───────────────────────────────────────────────────────

  const isRead = method === 'GET' || method === 'HEAD';

  let fetchCache: RequestCache | undefined;
  let nextOptions: { revalidate?: number; tags?: string[] } | undefined;

  if (isRead && options.revalidateSeconds !== undefined) {
    nextOptions = {
      revalidate: options.revalidateSeconds,
      ...(options.tags && options.tags.length > 0 ? { tags: options.tags } : {}),
    };
  } else {
    fetchCache = 'no-store';
  }

  // ── 4. Log request start ──────────────────────────────────────────────────

  logInfo('api.request.start', logMeta);

  // ── 5. Execute fetch (network-error boundary) ─────────────────────────────

  const startMs = Date.now();
  let res: Response;

  try {
    res = await fetch(`${API_BASE}${path}`, {
      method,
      headers,
      body:    options.body !== undefined ? JSON.stringify(options.body) : undefined,
      ...(fetchCache  ? { cache: fetchCache } : {}),
      ...(nextOptions ? { next:  nextOptions } : {}),
    });
  } catch (networkErr: unknown) {
    // fetch() itself threw — DNS failure, connection refused, timeout, etc.
    const durationMs = Date.now() - startMs;
    logError('api.network_failure', networkErr, { ...logMeta, durationMs });
    throw networkErr;
  }

  const durationMs = Date.now() - startMs;

  // ── 6. Handle 401 ─────────────────────────────────────────────────────────
  //
  // Throw ApiError(401) rather than calling redirect() here.
  //
  // Rationale: requirePlatformAdmin() already validates the platform session
  // cookie via /identity/api/auth/me BEFORE any downstream API call is made.
  // If that guard passes, the session is valid. A 401 from a downstream service
  // (e.g. the Audit Event Service) therefore means the service has different
  // auth requirements — NOT that the session has expired. Redirecting to login
  // in that case drops the user out of an otherwise-authenticated session.
  //
  // Callers (synqaudit pages, etc.) catch ApiError(401) and show an error
  // banner with a descriptive message so the admin knows what failed.
  //
  // If the session truly expires, the NEXT page load's requirePlatformAdmin()
  // will call /identity/api/auth/me, receive a 401, return null, and redirect
  // to /login?reason=unauthenticated — which is the correct UX flow.

  if (res.status === 401) {
    logWarn('api.request.unauthorized_401', { ...logMeta, durationMs, status: 401 });
    throw new ApiError(401, 'Not authorised — the downstream service rejected the platform token.');
  }

  // ── 7. Handle non-2xx ─────────────────────────────────────────────────────

  if (!res.ok) {
    let message = `HTTP ${res.status} ${res.statusText}`;
    try {
      const errBody = await res.json() as Record<string, unknown>;
      // Top-level message (Identity/ProblemDetails format)
      if (typeof errBody.message === 'string') message = errBody.message;
      else if (typeof errBody.title === 'string') message = errBody.title;
      // Nested { error: { code, message } } format (Tenant/BuildingBlocks ExceptionHandlingMiddleware)
      else if (
        errBody.error !== null &&
        typeof errBody.error === 'object' &&
        typeof (errBody.error as Record<string, unknown>).message === 'string'
      ) {
        message = (errBody.error as Record<string, unknown>).message as string;
      }
    } catch { /* non-JSON error body — use status text */ }

    const apiErr = new ApiError(res.status, message);
    logError('api.request.error', apiErr, { ...logMeta, durationMs, status: res.status });
    throw apiErr;
  }

  // ── 8. Log success ────────────────────────────────────────────────────────

  logInfo('api.request.success', { ...logMeta, durationMs, status: res.status });

  // ── 9. Return body ────────────────────────────────────────────────────────

  // 204 No Content — return undefined without attempting JSON parse
  if (res.status === 204) return undefined as T;

  return res.json() as Promise<T>;
}

// ── Convenience helpers ───────────────────────────────────────────────────────

export const apiClient = {
  /**
   * GET with optional Next.js cache config.
   *
   * @param path              URL path (relative to API_BASE)
   * @param revalidateSeconds seconds until cache expires (omit → no-store)
   * @param tags              cache tags for revalidateTag() on-demand purge
   */
  get: <T>(
    path:               string,
    revalidateSeconds?: number,
    tags?:              string[],
  ) => apiFetch<T>(path, { revalidateSeconds, tags }),

  /** POST — always cache: 'no-store' */
  post:  <T>(path: string, body: unknown) => apiFetch<T>(path, { method: 'POST',  body }),

  /** PUT — always cache: 'no-store' */
  put:   <T>(path: string, body: unknown) => apiFetch<T>(path, { method: 'PUT',   body }),

  /** PATCH — always cache: 'no-store' */
  patch: <T>(path: string, body: unknown) => apiFetch<T>(path, { method: 'PATCH', body }),

  /** DELETE — always cache: 'no-store' */
  del:   <T>(path: string)               => apiFetch<T>(path, { method: 'DELETE' }),
};

// ── Cache tag constants ───────────────────────────────────────────────────────

/**
 * CACHE_TAGS — canonical tag strings used with revalidateTag().
 *
 * Import these in control-center-api.ts (for applying to fetch calls)
 * and in any Server Action that mutates data (for calling revalidateTag).
 *
 * TODO: add Redis or edge caching (tags would serve as Redis key prefixes)
 */
export const CACHE_TAGS = {
  tenants:              'cc:tenants',
  users:                'cc:users',
  roles:                'cc:roles',
  audit:                'cc:audit',
  settings:             'cc:settings',
  monitoring:           'cc:monitoring',
  support:              'cc:support',
  // Phase E — organization catalog & relationship graph
  orgTypes:             'cc:org-types',
  relTypes:             'cc:rel-types',
  orgRelationships:     'cc:org-relationships',
  productOrgTypeRules:  'cc:product-org-type-rules',
  productRelTypeRules:  'cc:product-rel-type-rules',
  legacyCoverage:       'cc:legacy-coverage',
  // Phase 8 — platform readiness summary
  platformReadiness:    'cc:platform-readiness',
  // CareConnect — integrity report
  ccIntegrity:          'cc:careconnect-integrity',
  // Step 24 — canonical audit (Platform Audit Event Service)
  auditCanonical:       'cc:audit-canonical',
  // LS-COR-AUT-005 — Access Groups
  accessGroups:         'cc:access-groups',
  // Step 28 — SynqAudit extensions
  auditExports:         'cc:audit-exports',
  auditIntegrity:       'cc:audit-integrity',
  auditLegalHolds:      'cc:audit-legal-holds',
  // NOTIF-UI-001 — Notifications service
  notifNotifications:   'notif:notifications',
  notifTemplates:       'notif:templates',
  notifProviders:       'notif:providers',
  notifBilling:         'notif:billing',
  notifContacts:        'notif:contacts',
  // LS-COR-AUT-011 — ABAC Policies
  policies:             'cc:policies',
  // E9.1 — Cross-product workflow operations list
  workflows:            'cc:workflows',
  // E19 — analytics & reporting layer
  analytics:            'cc:analytics',
} as const;

export type CacheTag = typeof CACHE_TAGS[keyof typeof CACHE_TAGS];
