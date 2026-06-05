/**
 * Integration test environment bootstrap.
 *
 * This file is listed under `setupFiles` in jest.integration.config.js.
 * It runs in each Jest worker BEFORE any test module is imported, so every
 * env var set here is visible when the app's `config.ts` first parses
 * `process.env`.
 *
 * IMPORTANT: Do NOT import any app modules here.
 */

// ── Service
process.env['NODE_ENV']      = 'test';
process.env['LOG_LEVEL']     = 'error';  // suppress noise during test runs
process.env['PORT']          = '0';       // not used directly; supertest binds to 0

// ── Database — Replit PostgreSQL (local)
process.env['DATABASE_URL'] =
  process.env['DATABASE_URL'] ??
  'postgresql://postgres:password@helium/heliumdb?sslmode=disable';

// ── Auth — JWT with a symmetric test secret; no external JWKS needed
process.env['AUTH_PROVIDER'] = 'jwt';
process.env['JWT_SECRET']    = 'integration-test-secret-do-not-use-in-prod';
// No issuer/audience → jwt.verify skips those claims

// ── Storage — local filesystem; created by LocalStorageProvider constructor
process.env['STORAGE_PROVIDER']   = 'local';
process.env['LOCAL_STORAGE_PATH'] = '/tmp/docs-integration-test-storage';

// ── File scanning — NullFileScannerProvider (scan_status = SKIPPED, access allowed)
process.env['FILE_SCANNER_PROVIDER']          = 'none';
process.env['REQUIRE_CLEAN_SCAN_FOR_ACCESS']  = 'true';

// ── File limits — 1 MB cap (small, easier to test oversized rejection)
process.env['MAX_FILE_SIZE_MB'] = '1';

// ── Rate limiting — in-memory, generous limits so other tests aren't impacted.
// The rate-limiting.test.ts file resets the module cache and sets lower limits.
process.env['RATE_LIMIT_PROVIDER']         = 'memory';
process.env['RATE_LIMIT_MAX_REQUESTS']     = '200';
process.env['RATE_LIMIT_UPLOAD_MAX']       = '50';
process.env['RATE_LIMIT_SIGNED_URL_MAX']   = '50';
process.env['RATE_LIMIT_WINDOW_SECONDS']   = '3600';

// ── Access tokens — in-memory store, 5-minute TTL
process.env['ACCESS_TOKEN_STORE']        = 'memory';
process.env['ACCESS_TOKEN_TTL_SECONDS']  = '300';
process.env['ACCESS_TOKEN_ONE_TIME_USE'] = 'true';
process.env['DIRECT_PRESIGN_ENABLED']    = 'false';

// ── CORS — allow any origin in tests (supertest doesn't send Origin header anyway)
process.env['CORS_ORIGINS'] = 'http://localhost:5000';

// ── Redis — not used; no REDIS_URL means Redis providers fall back gracefully
delete process.env['REDIS_URL'];
