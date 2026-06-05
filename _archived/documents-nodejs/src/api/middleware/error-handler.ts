import type { Request, Response, NextFunction } from 'express';
import { DocsError, RateLimitError } from '@/shared/errors';
import { logger }       from '@/shared/logger';
import { ZodError }     from 'zod';
import { MulterError }  from 'multer';

/**
 * Centralised error handler — MUST be the last middleware registered.
 * Maps all error types to clean, consistent JSON responses.
 * Sensitive internals are NOT exposed in production.
 */
export function errorHandler(
  err: unknown,
  req: Request,
  res: Response,
  _next: NextFunction,
): void {
  const correlationId = req.correlationId;

  // ── ZodError (validation) ──────────────────────────────────────────────────
  if (err instanceof ZodError) {
    res.status(400).json({
      error:         'VALIDATION_ERROR',
      message:       'Request validation failed',
      details:       err.flatten().fieldErrors,
      correlationId,
    });
    return;
  }

  // ── MulterError (file upload) ──────────────────────────────────────────────
  if (err instanceof MulterError) {
    res.status(413).json({
      error:         'FILE_TOO_LARGE',
      message:       `File upload error: ${err.message}`,
      correlationId,
    });
    return;
  }

  // ── RateLimitError (429) — set Retry-After header before body ────────────
  if (err instanceof RateLimitError) {
    res.setHeader('Retry-After', err.retryAfterSeconds);
    res.status(429).json({
      error:         err.code,
      message:       err.message,
      retryAfter:    err.retryAfterSeconds,
      limitDimension: err.limitDimension,
      correlationId,
    });
    return;
  }

  // ── Known DocsError ────────────────────────────────────────────────────────
  if (err instanceof DocsError) {
    if (err.statusCode >= 500) {
      logger.error({ err: err.message, correlationId, code: err.code }, 'Internal error');
    } else {
      logger.warn({ err: err.message, correlationId, code: err.code });
    }

    res.status(err.statusCode).json({
      error:         err.code,
      message:       err.message,
      ...(err.statusCode < 500 && err.details ? { details: err.details } : {}),
      correlationId,
    });
    return;
  }

  // ── Unknown error ──────────────────────────────────────────────────────────
  logger.error({ err, correlationId }, 'Unhandled error');
  res.status(500).json({
    error:         'INTERNAL_SERVER_ERROR',
    message:       'An unexpected error occurred',
    correlationId,
  });
}
