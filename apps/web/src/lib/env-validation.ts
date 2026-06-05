/**
 * BLK-OPS-01: Server-side environment validation for the Next.js BFF.
 *
 * Called at module load time (imported in next.config.mjs) so that
 * missing or placeholder values cause an explicit startup error rather
 * than silent runtime failures.
 *
 * Rules:
 *   - Only runs on the server (Node.js process). Safe to import in Server
 *     Components, API routes, and next.config.mjs.
 *   - Never import this from Client Components — it would be dead code in the
 *     browser build, but the import itself would be a lint/bundle error.
 *   - NEXT_PUBLIC_* variables are intentionally excluded: they are inlined at
 *     build time and cannot be injected via runtime environment variables.
 */

/** Known placeholder values that must never appear in production config. */
const PLACEHOLDERS = [
  'REPLACE_VIA_SECRET',
  'CHANGE_ME',
  'YOUR_SECRET_HERE',
  'INSERT_SECRET_HERE',
  'TODO',
  'FIXME',
];

function requireNonEmpty(key: string, value: string | undefined): void {
  if (!value || value.trim() === '') {
    throw new Error(
      `[web-bff] Environment variable '${key}' is missing or empty. ` +
      `Set it via the deployment environment or .env.local for local development.`,
    );
  }
}

function requireNotPlaceholder(key: string, value: string | undefined): void {
  if (!value) return; // defer to requireNonEmpty
  const upper = value.toUpperCase();
  for (const ph of PLACEHOLDERS) {
    if (upper.includes(ph.toUpperCase())) {
      throw new Error(
        `[web-bff] Environment variable '${key}' contains placeholder value '${ph}'. ` +
        `Replace with a real secret before deploying to production.`,
      );
    }
  }
}

function requireAbsoluteUrl(key: string, value: string | undefined): void {
  if (!value || value.trim() === '') {
    throw new Error(
      `[web-bff] Environment variable '${key}' is missing or empty. ` +
      `Set an absolute HTTP/HTTPS URL.`,
    );
  }
  try {
    const url = new URL(value);
    if (url.protocol !== 'http:' && url.protocol !== 'https:') {
      throw new Error('bad protocol');
    }
  } catch {
    throw new Error(
      `[web-bff] Environment variable '${key}' is not a valid absolute HTTP/HTTPS URL. ` +
      `Current value: '${value}'.`,
    );
  }
}

/**
 * Validates all server-side environment variables required for production.
 * Throws with a descriptive message on the first failure.
 *
 * Called automatically when this module is imported. Validation is skipped
 * in Development (NODE_ENV=development) so local dev remains usable without
 * real production secrets.
 */
export function validateServerEnv(): void {
  const isDev =
    process.env.NODE_ENV === 'development' ||
    process.env.NEXT_PUBLIC_ENV === 'development';

  if (isDev) return;

  // GATEWAY_URL — server-side fetch to the .NET API gateway
  requireAbsoluteUrl('GATEWAY_URL', process.env.GATEWAY_URL);

  // Trust boundary shared secret — must match PublicTrustBoundary:InternalRequestSecret
  // in Gateway and CareConnect.  Accept either the canonical .NET-style env key
  // (PublicTrustBoundary__InternalRequestSecret, preferred in deployed environments)
  // or the legacy alias (INTERNAL_REQUEST_SECRET, used in .env.local for local dev).
  const trustSecret =
    process.env['PublicTrustBoundary__InternalRequestSecret'] ??
    process.env.INTERNAL_REQUEST_SECRET;
  requireNonEmpty('PublicTrustBoundary__InternalRequestSecret (or INTERNAL_REQUEST_SECRET)', trustSecret);
  requireNotPlaceholder('PublicTrustBoundary__InternalRequestSecret', trustSecret);
}

// Run validation immediately on module load (server-side only).
// next.config.mjs runs in Node.js, so this executes during `next build` and
// `next start`, making misconfiguration visible before the server accepts traffic.
if (typeof window === 'undefined') {
  validateServerEnv();
}
