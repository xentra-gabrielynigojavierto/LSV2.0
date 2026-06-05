import type {
  RateLimitProvider,
  RateLimitKey,
  RateLimitResult,
} from '@/domain/interfaces/rate-limit-provider';

interface WindowEntry {
  count:       number;
  windowStart: number;   // epoch ms — start of the current fixed window
}

/**
 * InMemoryRateLimitProvider — fixed-window rate limiting backed by a JS Map.
 *
 * Suitable for:
 *  - Single-instance deployments
 *  - Local development
 *  - Test environments
 *
 * NOT suitable for:
 *  - Multi-instance / horizontally scaled deployments (use RedisRateLimitProvider)
 *
 * Memory management:
 *  - Expired entries are swept every `sweepIntervalMs` (default 5 min)
 *  - Memory bounded: one entry per unique (type, identifier, window) tuple
 */
export class InMemoryRateLimitProvider implements RateLimitProvider {
  private readonly store = new Map<string, WindowEntry>();
  private readonly sweepTimer: ReturnType<typeof setInterval>;

  constructor(sweepIntervalMs = 5 * 60 * 1000) {
    this.sweepTimer = setInterval(() => this.sweep(), sweepIntervalMs);
    // Allow Node to exit even if timer is pending
    if (this.sweepTimer.unref) this.sweepTimer.unref();
  }

  async check(key: RateLimitKey): Promise<RateLimitResult> {
    const windowMs    = key.windowSeconds * 1000;
    const now         = Date.now();
    const windowStart = Math.floor(now / windowMs) * windowMs;
    const storeKey    = `${key.type}:${key.identifier}:${windowStart}`;

    let entry = this.store.get(storeKey);

    if (!entry || entry.windowStart !== windowStart) {
      // New window — reset counter
      entry = { count: 0, windowStart };
    }

    entry.count += 1;
    this.store.set(storeKey, entry);

    const resetAt           = windowStart + windowMs;
    const remaining         = Math.max(0, key.maxRequests - entry.count);
    const retryAfterSeconds = Math.ceil((resetAt - now) / 1000);

    return {
      allowed:           entry.count <= key.maxRequests,
      remaining,
      limit:             key.maxRequests,
      resetAt,
      retryAfterSeconds,
    };
  }

  async reset(type: string, identifier: string): Promise<void> {
    // Delete all windows for this key prefix
    for (const k of this.store.keys()) {
      if (k.startsWith(`${type}:${identifier}:`)) {
        this.store.delete(k);
      }
    }
  }

  /** Remove entries whose window has expired */
  private sweep(): void {
    const now = Date.now();
    for (const [k, entry] of this.store.entries()) {
      // Extract windowMs from the key — approximate: drop any entry older than 2 hours
      if (now - entry.windowStart > 2 * 60 * 60 * 1000) {
        this.store.delete(k);
      }
    }
  }

  /** Expose store size for testing */
  storeSize(): number {
    return this.store.size;
  }

  /** Clean up timer on shutdown */
  destroy(): void {
    clearInterval(this.sweepTimer);
  }

  providerName(): string {
    return 'memory';
  }
}
