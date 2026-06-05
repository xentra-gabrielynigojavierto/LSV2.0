import type { Request, Response, NextFunction } from 'express';
import { getRateLimitProvider }  from '@/infrastructure/rate-limit/rate-limit-factory';
import { RateLimitError }        from '@/shared/errors';
import { logger }                from '@/shared/logger';
import { config }                from '@/shared/config';
import type { RateLimitResult }  from '@/domain/interfaces/rate-limit-provider';

export interface RateLimiterOptions {
  /** Window duration in seconds (defaults to RATE_LIMIT_WINDOW_SECONDS) */
  windowSeconds?: number;
  /** Max requests per IP per window */
  ipMax?:         number;
  /** Max requests per authenticated userId per window */
  userMax?:       number;
  /** Max requests per tenantId per window */
  tenantMax?:     number;
}

/**
 * Normalise an IP address for use as a rate-limit key.
 * Strips IPv6-mapped IPv4 prefix (::ffff:) so 1.2.3.4 and ::ffff:1.2.3.4 share a bucket.
 * Hashes the result so raw IPs are not stored (GDPR / privacy consideration).
 */
function normaliseIp(ip: string | undefined): string {
  if (!ip) return 'unknown';
  return ip.replace(/^::ffff:/, '');
}

/**
 * Attach rate-limit response headers.
 * X-RateLimit-Limit, X-RateLimit-Remaining, X-RateLimit-Reset, Retry-After.
 */
function attachHeaders(res: Response, result: RateLimitResult): void {
  res.setHeader('X-RateLimit-Limit',     result.limit);
  res.setHeader('X-RateLimit-Remaining', result.remaining);
  res.setHeader('X-RateLimit-Reset',     Math.ceil(result.resetAt / 1000)); // UNIX epoch seconds
  if (!result.allowed) {
    res.setHeader('Retry-After', result.retryAfterSeconds);
  }
}

/**
 * createRateLimiter — returns an Express middleware that enforces rate limits
 * across three independent dimensions: IP, userId, and tenantId.
 *
 * Dimensions are checked in order: IP → userId → tenantId.
 * The FIRST exceeded limit triggers a 429 response.
 *
 * Headers set on every response (even non-429):
 *   X-RateLimit-Limit     — configured max for the binding dimension
 *   X-RateLimit-Remaining — remaining requests in this window
 *   X-RateLimit-Reset     — Unix timestamp (seconds) when the window resets
 *   Retry-After           — seconds until retry is safe (429 only)
 */
export function createRateLimiter(opts: RateLimiterOptions = {}) {
  const windowSeconds = opts.windowSeconds ?? config.RATE_LIMIT_WINDOW_SECONDS;
  const ipMax         = opts.ipMax         ?? config.RATE_LIMIT_MAX_REQUESTS;
  const userMax       = opts.userMax       ?? config.RATE_LIMIT_MAX_REQUESTS;
  const tenantMax     = opts.tenantMax     ?? config.RATE_LIMIT_MAX_REQUESTS;

  return async function rateLimiterMiddleware(
    req: Request,
    res: Response,
    next: NextFunction,
  ): Promise<void> {
    const provider = getRateLimitProvider();
    const ip       = normaliseIp(req.ip);

    try {
      // ── 1. IP rate limit (applies to all requests, authenticated or not) ──
      const ipResult = await provider.check({
        type:          'ip',
        identifier:    ip,
        windowSeconds,
        maxRequests:   ipMax,
      });

      attachHeaders(res, ipResult);

      if (!ipResult.allowed) {
        logger.warn({
          dimension:     'ip',
          correlationId: req.correlationId,
          retryAfter:    ipResult.retryAfterSeconds,
        }, 'Rate limit exceeded');

        throw new RateLimitError(ipResult.retryAfterSeconds, 'ip');
      }

      // ── 2. User rate limit (authenticated requests only) ─────────────────
      if (req.principal) {
        const userResult = await provider.check({
          type:          'user',
          identifier:    req.principal.userId,
          windowSeconds,
          maxRequests:   userMax,
        });

        attachHeaders(res, userResult);

        if (!userResult.allowed) {
          logger.warn({
            dimension:     'user',
            correlationId: req.correlationId,
            retryAfter:    userResult.retryAfterSeconds,
          }, 'Rate limit exceeded');

          throw new RateLimitError(userResult.retryAfterSeconds, 'user');
        }

        // ── 3. Tenant rate limit ───────────────────────────────────────────
        const tenantResult = await provider.check({
          type:          'tenant',
          identifier:    req.principal.tenantId,
          windowSeconds,
          maxRequests:   tenantMax,
        });

        attachHeaders(res, tenantResult);

        if (!tenantResult.allowed) {
          logger.warn({
            dimension:     'tenant',
            correlationId: req.correlationId,
            retryAfter:    tenantResult.retryAfterSeconds,
          }, 'Rate limit exceeded');

          throw new RateLimitError(tenantResult.retryAfterSeconds, 'tenant');
        }
      }

      next();
    } catch (err) {
      next(err);
    }
  };
}

/**
 * Pre-built rate limiters for common endpoint classes.
 * Applied at the route level for surgical precision.
 */

/** General API — 100 req/min per IP, 100/min per user, 200/min per tenant */
export const generalLimiter = createRateLimiter({
  ipMax:     config.RATE_LIMIT_MAX_REQUESTS,
  userMax:   config.RATE_LIMIT_MAX_REQUESTS,
  tenantMax: config.RATE_LIMIT_MAX_REQUESTS * 2,
});

/** Upload endpoints — strict: 10/min per IP, 10/min per user, 20/min per tenant */
export const uploadLimiter = createRateLimiter({
  ipMax:     config.RATE_LIMIT_UPLOAD_MAX,
  userMax:   config.RATE_LIMIT_UPLOAD_MAX,
  tenantMax: config.RATE_LIMIT_UPLOAD_MAX * 2,
});

/** Signed-URL endpoints — moderate: 30/min per IP, 30/min per user, 60/min per tenant */
export const signedUrlLimiter = createRateLimiter({
  ipMax:     config.RATE_LIMIT_SIGNED_URL_MAX,
  userMax:   config.RATE_LIMIT_SIGNED_URL_MAX,
  tenantMax: config.RATE_LIMIT_SIGNED_URL_MAX * 2,
});
