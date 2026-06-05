/**
 * Centralised error hierarchy for the Docs Service.
 * All errors carry an HTTP status code and a machine-readable code for the client.
 */

export class DocsError extends Error {
  constructor(
    message: string,
    public readonly statusCode: number,
    public readonly code: string,
    public readonly details?: unknown,
  ) {
    super(message);
    this.name = this.constructor.name;
    Error.captureStackTrace(this, this.constructor);
  }
}

// ── 400 Bad Request ────────────────────────────────────────────────────────────
export class ValidationError extends DocsError {
  constructor(message: string, details?: unknown) {
    super(message, 400, 'VALIDATION_ERROR', details);
  }
}

export class FileValidationError extends DocsError {
  constructor(message: string) {
    super(message, 400, 'FILE_VALIDATION_ERROR');
  }
}

// ── 401 Unauthorized ──────────────────────────────────────────────────────────
export class AuthenticationError extends DocsError {
  constructor(message = 'Authentication required') {
    super(message, 401, 'AUTHENTICATION_REQUIRED');
  }
}

// ── 403 Forbidden ─────────────────────────────────────────────────────────────
export class ForbiddenError extends DocsError {
  constructor(message = 'Access denied') {
    super(message, 403, 'ACCESS_DENIED');
  }
}

// ── 404 Not Found ─────────────────────────────────────────────────────────────
export class NotFoundError extends DocsError {
  constructor(resource: string, id: string) {
    super(`${resource} not found: ${id}`, 404, 'NOT_FOUND');
  }
}

// ── 409 Conflict ──────────────────────────────────────────────────────────────
export class ConflictError extends DocsError {
  constructor(message: string) {
    super(message, 409, 'CONFLICT');
  }
}

// ── 413 Payload Too Large ─────────────────────────────────────────────────────
export class FileTooLargeError extends DocsError {
  constructor(maxMb: number) {
    super(`File exceeds maximum allowed size of ${maxMb} MB`, 413, 'FILE_TOO_LARGE');
  }
}

// ── 422 Unprocessable Entity ──────────────────────────────────────────────────
export class UnsupportedFileTypeError extends DocsError {
  constructor(mimeType: string) {
    super(`File type not permitted: ${mimeType}`, 422, 'UNSUPPORTED_FILE_TYPE');
  }
}

// ── 403 Scan-blocked ─────────────────────────────────────────────────────────
export class ScanBlockedError extends DocsError {
  constructor(scanStatus: string) {
    super(
      `Access denied: file scan status is ${scanStatus}. Only CLEAN files may be accessed.`,
      403,
      'SCAN_BLOCKED',
    );
  }
}

export class InfectedFileError extends DocsError {
  constructor(threats: string[]) {
    super(
      `File rejected: malware detected (${threats.join(', ')})`,
      422,
      'INFECTED_FILE',
    );
  }
}

// ── 401 Token errors ──────────────────────────────────────────────────────────
export class TokenExpiredError extends DocsError {
  constructor() {
    super('Access token has expired', 401, 'TOKEN_EXPIRED');
  }
}

export class TokenInvalidError extends DocsError {
  constructor(reason = 'Access token is invalid or has already been used') {
    super(reason, 401, 'TOKEN_INVALID');
  }
}

// ── 429 Too Many Requests ─────────────────────────────────────────────────────
export class RateLimitError extends DocsError {
  constructor(
    public readonly retryAfterSeconds: number,
    public readonly limitDimension: 'ip' | 'user' | 'tenant',
  ) {
    super(
      `Rate limit exceeded. Retry after ${retryAfterSeconds} second(s).`,
      429,
      'RATE_LIMIT_EXCEEDED',
    );
  }
}

// ── 403 Tenant isolation ──────────────────────────────────────────────────────
/**
 * Thrown when a tenant boundary is violated at the application layer.
 * Distinct from generic ForbiddenError so monitoring can alert on cross-tenant
 * attempts separately from ordinary permission denials.
 *
 * NEVER reveal which tenant owns the resource in the message — return 403 with
 * a generic string to avoid tenantId enumeration.
 */
export class TenantIsolationError extends DocsError {
  constructor(context = 'Cross-tenant access denied') {
    super(context, 403, 'TENANT_ISOLATION_VIOLATION');
  }
}

// ── 503 Service Unavailable ───────────────────────────────────────────────────
/**
 * Thrown when a Redis command fails because the connection is down.
 * The API middleware maps 503 → Retry-After header, and the factory
 * layers catch this to decide whether to fall back to the memory provider.
 */
export class RedisUnavailableError extends DocsError {
  constructor(operation: string, cause?: string) {
    super(
      `Redis unavailable during ${operation}${cause ? `: ${cause}` : ''}`,
      503,
      'REDIS_UNAVAILABLE',
    );
  }
}

// ── 500 Internal ──────────────────────────────────────────────────────────────
export class StorageError extends DocsError {
  constructor(message: string) {
    super(message, 500, 'STORAGE_ERROR');
  }
}

export class DatabaseError extends DocsError {
  constructor(message: string) {
    super(message, 500, 'DATABASE_ERROR');
  }
}
