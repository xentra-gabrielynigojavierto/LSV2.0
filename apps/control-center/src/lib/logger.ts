/**
 * logger.ts — Control Center structured server-side logger.
 *
 * All logging in the Control Center flows through this module. It provides
 * three public functions — logInfo, logWarn, logError — that emit structured
 * log entries with a consistent shape in every environment.
 *
 * ── Environments ─────────────────────────────────────────────────────────────
 *
 *   development  →  human-readable prefix lines to stderr/stdout.
 *                   Formatted for easy reading in the Next.js dev console.
 *
 *   production   →  newline-delimited JSON (NDJSON) to stdout.
 *                   One JSON object per log entry, compatible with any
 *                   structured log aggregator (CloudWatch, Datadog, GCP
 *                   Logging, etc.).
 *
 * ── PII redaction ────────────────────────────────────────────────────────────
 *
 *   Sensitive values are automatically redacted before writing to any output:
 *
 *   Token redaction (ALL environments):
 *     - JWT tokens (three base64url segments): replaced with [REDACTED_JWT]
 *     - Bearer tokens in strings: replaced with Bearer [REDACTED_TOKEN]
 *     - Raw base64url strings that look like JWTs are caught by the same rule.
 *
 *   Email redaction (production only):
 *     - Email addresses are partially masked: user@domain.com → u***@domain.com
 *       This preserves enough of the address for debugging while preventing
 *       full PII leakage into log aggregators.
 *     - In development, email addresses are logged in full so developers
 *       can trace requests without decoding partial masks.
 *
 *   Scope:
 *     - errorMessage strings
 *     - impersonatedUserEmail field (the most sensitive PII in LogMeta)
 *     - endpoint fields (guards against accidentally passing query params
 *       that contain email addresses or tokens)
 *
 * ── Log entry shape ──────────────────────────────────────────────────────────
 *
 *   {
 *     level:      "INFO" | "WARN" | "ERROR"
 *     message:    string          — short event label, e.g. "api.request.start"
 *     timestamp:  ISO-8601        — UTC wall-clock time
 *     service:    "control-center"
 *     // optional fields from LogMeta:
 *     requestId?         string   — X-Request-Id propagated to API gateway
 *     endpoint?          string   — URL path (redacted if it contains tokens)
 *     method?            string   — HTTP verb
 *     durationMs?        number   — round-trip latency
 *     status?            number   — HTTP response status
 *     tenantId?          string   — active tenant context (if set)
 *     tenantCode?        string   — active tenant short code (if set)
 *     impersonatedUserId? string  — impersonated user (if active)
 *     impersonatedUserEmail? string — partially masked in prod
 *     // error fields (logError only):
 *     errorName?   string
 *     errorMessage? string        — tokens redacted; emails masked in prod
 *     errorStack?  string         — only in development
 *   }
 *
 * TODO: integrate with Datadog / OpenTelemetry
 * TODO: send logs to centralized logging service
 * TODO: add log sampling for high-volume INFO events
 * TODO: add correlation tracing (trace-id across microservices)
 * TODO: add slow-response warning threshold
 */

// ── Env ───────────────────────────────────────────────────────────────────────

const IS_DEV  = process.env.NODE_ENV !== 'production';
const SERVICE = 'control-center' as const;

// ── Types ─────────────────────────────────────────────────────────────────────

/** Level union */
export type LogLevel = 'INFO' | 'WARN' | 'ERROR';

/**
 * LogMeta — well-known structured fields accepted by all log functions.
 *
 * Every field is optional so callers can include only what they know.
 * Unknown extra fields are accepted via the index signature and are
 * forwarded verbatim to the log output.
 *
 * Sensitive values (passwords, tokens, raw cookie values) must NEVER
 * appear here. Include only opaque identifiers such as requestId, tenantId,
 * userId — never credential payloads.
 *
 * impersonatedUserEmail is automatically partially masked in production.
 */
export interface LogMeta {
  requestId?:             string;
  endpoint?:              string;
  method?:                string;
  durationMs?:            number;
  status?:                number;
  tenantId?:              string;
  tenantCode?:            string;
  impersonatedUserId?:    string;
  impersonatedUserEmail?: string;
  [key: string]:          unknown;
}

/** Full log entry shape (internal) */
interface LogEntry extends LogMeta {
  level:        LogLevel;
  message:      string;
  timestamp:    string;
  service:      typeof SERVICE;
  errorName?:    string;
  errorMessage?: string;
  errorStack?:   string;
}

// ── PII redaction ─────────────────────────────────────────────────────────────

/**
 * JWT_PATTERN — matches a three-segment base64url string (JSON Web Token).
 *
 * A JWT looks like: eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiJ4eXoifQ.SIG
 * Each segment is base64url-encoded (A-Z, a-z, 0-9, -, _, with optional =
 * padding). The regex requires at least 10 chars per segment to avoid false
 * positives on short base64 strings.
 */
const JWT_PATTERN   = /eyJ[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]{8,}/g;

/**
 * BEARER_PATTERN — matches a Bearer token in Authorization / error strings.
 * Replaces the entire token value, keeping the "Bearer" keyword for context.
 */
const BEARER_PATTERN = /Bearer\s+[A-Za-z0-9_\-.+/=]{8,}/g;

/**
 * EMAIL_PATTERN — matches RFC-5321 email addresses for partial masking.
 * Applied only in production.
 */
const EMAIL_PATTERN  = /[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}/g;

/**
 * redactTokens — removes JWT and Bearer tokens from a string.
 *
 * Applied in ALL environments (dev and prod). Tokens have no debugging
 * value in log output — their presence indicates a code path that
 * accidentally serialised a credential, which is a security defect.
 *
 * @param text  Input string that may contain JWT/Bearer tokens
 * @returns     String with tokens replaced by redaction markers
 */
function redactTokens(text: string): string {
  return text
    .replace(JWT_PATTERN,    '[REDACTED_JWT]')
    .replace(BEARER_PATTERN, 'Bearer [REDACTED_TOKEN]');
}

/**
 * redactEmail — partially masks an email address.
 *
 * Examples:
 *   margaret@hartwell.law   →  m*******@hartwell.law
 *   admin@legalsynq.com     →  a****@legalsynq.com
 *   a@b.com                 →  *@b.com
 *
 * The local part (before @) is reduced to first character + asterisks.
 * The domain is preserved so operators can identify tenant-specific issues.
 *
 * Only applied in production.
 */
function redactEmail(email: string): string {
  const atIndex = email.indexOf('@');
  if (atIndex <= 0) return email;
  const local  = email.slice(0, atIndex);
  const domain = email.slice(atIndex);
  return `${local[0]}${'*'.repeat(local.length - 1)}${domain}`;
}

/**
 * redactEmails — replaces all email addresses in a string with masked versions.
 *
 * Only applied in production. In development, full addresses are preserved
 * so developers can trace requests without decoding partial masks.
 */
function redactEmails(text: string): string {
  if (IS_DEV) return text;
  return text.replace(EMAIL_PATTERN, redactEmail);
}

/**
 * sanitiseString — applies all redaction rules to a string value.
 * Called before writing any string field to the log output.
 *
 * Order: tokens first (may contain @-like sequences), then emails.
 */
function sanitiseString(text: string): string {
  return redactEmails(redactTokens(text));
}

/**
 * sanitiseMeta — returns a copy of LogMeta with sensitive fields redacted.
 *
 * Fields processed:
 *   - impersonatedUserEmail  → partial mask in prod, full in dev
 *   - endpoint               → token redaction (catches accidental query params)
 *   - all other string fields via the index signature → token redaction
 *
 * The original LogMeta object is NOT mutated.
 */
function sanitiseMeta(meta: LogMeta): LogMeta {
  const out: LogMeta = { ...meta };

  // impersonatedUserEmail is the highest-risk PII field in LogMeta
  if (typeof out.impersonatedUserEmail === 'string') {
    out.impersonatedUserEmail = IS_DEV
      ? redactTokens(out.impersonatedUserEmail)
      : redactEmail(redactTokens(out.impersonatedUserEmail));
  }

  // endpoint — guard against query strings that accidentally contain tokens
  if (typeof out.endpoint === 'string') {
    out.endpoint = redactTokens(out.endpoint);
  }

  // Generic pass over remaining string fields from the index signature
  for (const key of Object.keys(out)) {
    const val = out[key];
    if (
      typeof val === 'string' &&
      key !== 'impersonatedUserEmail' &&
      key !== 'endpoint'
    ) {
      out[key] = sanitiseString(val);
    }
  }

  return out;
}

// ── Serialise error ───────────────────────────────────────────────────────────

/**
 * serialiseError — extracts loggable fields from an unknown thrown value.
 *
 * Avoids leaking raw stack traces in production while still capturing
 * the essential name + message for alerting and search.
 *
 * errorMessage is sanitised (token + email redaction) before it is stored
 * so that error messages from the Identity service (which may include
 * "Invalid token: eyJ...") do not leak credential material.
 */
function serialiseError(err: unknown): {
  errorName:     string;
  errorMessage:  string;
  errorStack?:   string;
} {
  if (err instanceof Error) {
    return {
      errorName:    err.name    || 'Error',
      errorMessage: sanitiseString(err.message || String(err)),
      // Stack traces are valuable in dev; omit in prod to reduce log volume
      // and prevent accidental source-path leakage.
      errorStack:   IS_DEV ? (err.stack ?? undefined) : undefined,
    };
  }
  return {
    errorName:    'UnknownError',
    errorMessage: sanitiseString(String(err ?? 'unknown error')),
  };
}

// ── Output ────────────────────────────────────────────────────────────────────

/** ANSI colour codes — only used in dev */
const CLR = {
  reset:  '\x1b[0m',
  grey:   '\x1b[90m',
  cyan:   '\x1b[36m',
  yellow: '\x1b[33m',
  red:    '\x1b[31m',
  bold:   '\x1b[1m',
} as const;

/**
 * emit — the single write path for all log entries.
 *
 * dev  → human-readable single line to console
 * prod → JSON object to stdout (one line)
 *
 * TODO: integrate with Datadog / OpenTelemetry
 * TODO: send logs to centralized logging service
 */
function emit(entry: LogEntry): void {
  if (IS_DEV) {
    emitDev(entry);
  } else {
    emitProd(entry);
  }
}

/**
 * emitDev — pretty-prints a log entry to the developer console.
 *
 * Format:
 *   [CC] INFO  HH:MM:SS.mmm message  method endpoint  +NNNms  req=xxx  tenant=xxx
 *
 * Token redaction is applied to displayed strings even in dev (tokens have no
 * debug value). Email masking is NOT applied — full addresses remain visible
 * in dev to simplify tracing.
 */
function emitDev(entry: LogEntry): void {
  const time  = entry.timestamp.slice(11, 23); // HH:MM:SS.mmm
  const level = entry.level.padEnd(5);

  let levelColour: string;
  let logFn: typeof console.log;
  switch (entry.level) {
    case 'ERROR':
      levelColour = CLR.red    + CLR.bold;
      logFn       = console.error;
      break;
    case 'WARN':
      levelColour = CLR.yellow + CLR.bold;
      logFn       = console.warn;
      break;
    default:
      levelColour = CLR.cyan;
      logFn       = console.log;
  }

  // Build context suffix tokens
  const tokens: string[] = [];
  if (entry.method)               tokens.push(`${entry.method}`);
  if (entry.endpoint)             tokens.push(`${entry.endpoint}`);
  if (entry.status !== undefined) tokens.push(`HTTP ${entry.status}`);
  if (entry.durationMs !== undefined) tokens.push(`+${entry.durationMs}ms`);
  if (entry.requestId)            tokens.push(`${CLR.grey}req=${entry.requestId}${CLR.reset}`);
  if (entry.tenantId)             tokens.push(`${CLR.grey}tenant=${entry.tenantCode ?? entry.tenantId}${CLR.reset}`);
  if (entry.impersonatedUserId)   tokens.push(`${CLR.grey}impersonating=${entry.impersonatedUserEmail ?? entry.impersonatedUserId}${CLR.reset}`);
  if (entry.errorMessage)         tokens.push(`"${entry.errorMessage}"`);

  const suffix = tokens.length ? `  ${tokens.join('  ')}` : '';

  logFn(
    `${CLR.grey}[CC]${CLR.reset} ${levelColour}${level}${CLR.reset} ${CLR.grey}${time}${CLR.reset}  ${entry.message}${suffix}`,
  );

  // In dev, also log the stack if present
  if (entry.errorStack && entry.level === 'ERROR') {
    console.error(`${CLR.grey}${entry.errorStack}${CLR.reset}`);
  }
}

/**
 * emitProd — emits a JSON log line to stdout.
 *
 * Produces NDJSON (newline-delimited JSON): one compact JSON object per line,
 * suitable for CloudWatch Logs, Datadog, GCP Logging, and similar services.
 *
 * All string fields are passed through sanitiseMeta() before writing so that
 * PII (email addresses, tokens) are redacted from persistent log storage.
 *
 * TODO: integrate with Datadog / OpenTelemetry
 * TODO: send logs to centralized logging service
 */
function emitProd(entry: LogEntry): void {
  // Build a clean object — strip undefined values so JSON is compact
  const out: Record<string, unknown> = {};
  for (const [k, v] of Object.entries(entry)) {
    if (v !== undefined) {
      // Sanitise string values before writing to persistent log storage
      out[k] = typeof v === 'string' ? sanitiseString(v) : v;
    }
  }
  // eslint-disable-next-line no-console
  process.stdout.write(JSON.stringify(out) + '\n');
}

// ── Build sanitised entry ─────────────────────────────────────────────────────

/**
 * buildEntry — constructs a LogEntry with PII redaction applied.
 *
 * Sanitisation is applied at entry construction time so that both
 * emitDev and emitProd receive clean data. emitDev applies token
 * redaction only; emitProd then applies a second pass in emitProd
 * for any residual strings (belt-and-suspenders).
 */
function buildEntry(
  level:   LogLevel,
  message: string,
  meta?:   LogMeta,
  errFields?: { errorName?: string; errorMessage?: string; errorStack?: string },
): LogEntry {
  const sanitised = meta ? sanitiseMeta(meta) : {};
  return {
    level,
    message,
    timestamp: new Date().toISOString(),
    service:   SERVICE,
    ...sanitised,
    ...errFields,
  };
}

// ── Public API ────────────────────────────────────────────────────────────────

/**
 * logInfo — emit an INFO-level log entry.
 *
 * Use for normal operational events:
 *   - request start / success
 *   - cache hit / miss
 *   - context switches
 *   - audit events (impersonation start/stop, tenant context switch)
 *
 * @param message  Short event label (e.g. "api.request.start")
 * @param meta     Optional structured fields (requestId, endpoint, …)
 */
export function logInfo(message: string, meta?: LogMeta): void {
  emit(buildEntry('INFO', message, meta));
}

/**
 * logWarn — emit a WARN-level log entry.
 *
 * Use for degraded-but-recoverable conditions:
 *   - unexpected field values from the API
 *   - missing optional context
 *   - slow responses (above threshold)
 *   - security anomalies that were handled (cookie shape mismatch,
 *     cross-tenant impersonation rejection)
 *
 * @param message  Short event label (e.g. "api.slow_response")
 * @param meta     Optional structured fields
 */
export function logWarn(message: string, meta?: LogMeta): void {
  emit(buildEntry('WARN', message, meta));
}

/**
 * logError — emit an ERROR-level log entry, optionally including a serialised
 * error object.
 *
 * Use for failures that require investigation:
 *   - non-2xx API responses (4xx client errors, 5xx server errors)
 *   - network-level failures (DNS, connection refused, timeout)
 *   - unexpected exceptions in Server Actions
 *
 * Does NOT re-throw the error — callers remain responsible for propagation.
 *
 * @param message  Short event label (e.g. "api.request.error")
 * @param error    The thrown value (Error, ApiError, or unknown)
 * @param meta     Optional structured fields
 *
 * TODO: integrate with Datadog / OpenTelemetry
 * TODO: send logs to centralized logging service
 */
export function logError(message: string, error?: unknown, meta?: LogMeta): void {
  const errFields = error !== undefined ? serialiseError(error) : {};
  emit(buildEntry('ERROR', message, meta, errFields));
}
