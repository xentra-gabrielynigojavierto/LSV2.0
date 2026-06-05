import type { AccessToken } from '@/domain/entities/access-token';

/**
 * AccessTokenStore — pluggable short-lived token persistence.
 *
 * Implementations:
 *  - InMemoryAccessTokenStore  (single-process, dev/test)
 *  - RedisAccessTokenStore     (distributed, production)
 *
 * Design constraints:
 *  - store() must be atomic: race-safe on concurrent requests
 *  - get() returns null for missing, expired, or revoked tokens
 *  - markUsed() must be atomic: prevents TOCTOU race on one-time-use tokens
 *  - revoke() is idempotent
 */
export interface AccessTokenStore {
  /** Persist a new access token. TTL is implicit: expiresAt is stored and checked on get(). */
  store(token: AccessToken): Promise<void>;

  /**
   * Retrieve a token by its opaque string.
   * Returns null if: missing, expired, revoked.
   * Does NOT check isUsed — caller decides what to do with used tokens.
   */
  get(tokenString: string): Promise<AccessToken | null>;

  /**
   * Atomically mark a token as used.
   * Must be idempotent and race-safe (use Redis SETNX or conditional update).
   * Returns false if the token was already used (caller should reject).
   */
  markUsed(tokenString: string): Promise<boolean>;

  /** Immediately invalidate a token (e.g. on logout or permission revocation). */
  revoke(tokenString: string): Promise<void>;

  /** Prune expired tokens — called by background cleanup. */
  cleanup(): Promise<number>;

  /** Tear down background timers (for graceful shutdown / test cleanup). */
  destroy(): void;
}
