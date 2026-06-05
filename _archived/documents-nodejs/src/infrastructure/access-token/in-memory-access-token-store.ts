import type { AccessTokenStore }  from '@/domain/interfaces/access-token-store';
import type { AccessToken }       from '@/domain/entities/access-token';
import { logger }                 from '@/shared/logger';

/**
 * InMemoryAccessTokenStore — single-process implementation.
 *
 * Suitable for: development, test, single-replica deployments.
 * Not suitable for: multi-replica deployments (tokens issued by replica A
 * will not be found on replica B). Use RedisAccessTokenStore for HA.
 *
 * Concurrency: Node.js is single-threaded, so Map operations are atomic.
 * The markUsed() method reads + writes in the same tick, making it TOCTOU-safe
 * within a single process. Redis SETNX is required for true distributed atomicity.
 *
 * Memory safety:
 *  - Background cleanup runs every CLEANUP_INTERVAL_MS (default: 60s)
 *  - Expired tokens are removed on cleanup OR lazily on get()
 *  - Maximum of MAX_TOKENS entries enforced on store() to prevent unbounded growth
 */

const CLEANUP_INTERVAL_MS = 60_000;
const MAX_TOKENS = 50_000;

export class InMemoryAccessTokenStore implements AccessTokenStore {
  private readonly store_: Map<string, AccessToken> = new Map();
  private readonly cleanupTimer: ReturnType<typeof setInterval>;

  constructor() {
    this.cleanupTimer = setInterval(() => {
      this.cleanup().catch((err: unknown) => {
        logger.error({ err }, 'InMemoryAccessTokenStore: background cleanup error');
      });
    }, CLEANUP_INTERVAL_MS);

    // Allow Node.js to exit even if this timer is active
    if (this.cleanupTimer.unref) this.cleanupTimer.unref();
  }

  async store(token: AccessToken): Promise<void> {
    if (this.store_.size >= MAX_TOKENS) {
      // Emergency: evict all expired tokens before refusing
      await this.cleanup();
      if (this.store_.size >= MAX_TOKENS) {
        logger.warn('InMemoryAccessTokenStore: capacity limit reached, refusing new token');
        throw new Error('Access token store at capacity');
      }
    }
    this.store_.set(token.token, { ...token });
  }

  async get(tokenString: string): Promise<AccessToken | null> {
    const token = this.store_.get(tokenString);
    if (!token) return null;

    // Lazy expiry eviction
    if (token.expiresAt <= new Date()) {
      this.store_.delete(tokenString);
      return null;
    }

    return { ...token }; // return a defensive copy
  }

  async markUsed(tokenString: string): Promise<boolean> {
    const token = this.store_.get(tokenString);
    if (!token) return false;
    if (token.isUsed) return false;  // already used — TOCTOU guard
    token.isUsed = true;
    return true;
  }

  async revoke(tokenString: string): Promise<void> {
    this.store_.delete(tokenString);
  }

  async cleanup(): Promise<number> {
    const now = new Date();
    let removed = 0;
    for (const [key, token] of this.store_) {
      if (token.expiresAt <= now) {
        this.store_.delete(key);
        removed++;
      }
    }
    if (removed > 0) {
      logger.debug({ removed }, 'InMemoryAccessTokenStore: cleaned up expired tokens');
    }
    return removed;
  }

  destroy(): void {
    clearInterval(this.cleanupTimer);
    this.store_.clear();
  }
}
