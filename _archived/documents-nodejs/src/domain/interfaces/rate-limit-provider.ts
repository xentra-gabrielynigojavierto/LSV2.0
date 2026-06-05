/**
 * RateLimitProvider — cloud-agnostic rate limiting abstraction.
 *
 * Concrete implementations:
 *  - InMemoryRateLimitProvider (default, no deps)
 *  - RedisRateLimitProvider    (distributed, production-grade)
 *
 * Uses a fixed-window algorithm:
 *  - Fast: O(1) per check
 *  - Predictable: window resets at a known time
 *  - Memory-efficient: one record per (key, window)
 */

export interface RateLimitResult {
  /** Whether this request is within the allowed limit */
  allowed:            boolean;
  /** Remaining requests in the current window */
  remaining:          number;
  /** Absolute total limit for this key/window */
  limit:              number;
  /** When the current window resets (epoch ms) */
  resetAt:            number;
  /** Seconds until the window resets (for Retry-After header) */
  retryAfterSeconds:  number;
}

export interface RateLimitKey {
  /** Dimension: 'ip' | 'user' | 'tenant' */
  type:        string;
  /** The identifier for this dimension (IP string, userId, tenantId) */
  identifier:  string;
  /** Window size in seconds */
  windowSeconds: number;
  /** Maximum requests allowed in the window */
  maxRequests: number;
}

export interface RateLimitProvider {
  /**
   * Increment the counter for a key and return the result.
   * Atomically increments and returns allowed/remaining/reset info.
   */
  check(key: RateLimitKey): Promise<RateLimitResult>;

  /**
   * Explicitly reset a key (for testing or admin operations).
   */
  reset(type: string, identifier: string): Promise<void>;

  /** Return the name of the active provider for observability. */
  providerName(): string;
}
