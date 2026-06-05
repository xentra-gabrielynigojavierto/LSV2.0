/**
 * RedisRateLimitProvider — distributed fixed-window rate limiting.
 *
 * Algorithm: atomic Lua INCR + conditional EXPIRE
 *  1. INCR the window key → new count
 *  2. If count == 1 (first hit), EXPIRE key for windowSeconds
 *     (Lua atomicity prevents the race between INCR and EXPIRE)
 *  3. Remaining TTL → resetAt / retryAfterSeconds
 *
 * Key schema:
 *   rl:{type}:{identifier}:{windowBucket}
 *   e.g.  rl:ip:203.0.113.5:28531    (windowBucket = floor(unixSec / windowSec))
 *
 * Graceful degradation:
 *   All methods wrap Redis errors in RedisUnavailableError so the factory
 *   can catch and fall back to the memory provider.
 */

import type {
  RateLimitProvider,
  RateLimitKey,
  RateLimitResult,
} from '@/domain/interfaces/rate-limit-provider';
import { getRedisClient }        from '@/infrastructure/redis/redis-client';
import { RedisUnavailableError } from '@/shared/errors';
import { logger }                from '@/shared/logger';

// Atomic INCR + conditional EXPIRE in a single round-trip.
// Returns a two-element array: [count, ttl].
const RATE_LIMIT_LUA = `
local key    = KEYS[1]
local window = tonumber(ARGV[1])
local count  = redis.call('INCR', key)
if count == 1 then
  redis.call('EXPIRE', key, window)
end
local ttl = redis.call('TTL', key)
return {count, ttl}
`;

function windowBucket(windowSeconds: number): number {
  return Math.floor(Date.now() / 1_000 / windowSeconds);
}

function redisKey(key: RateLimitKey): string {
  return `rl:${key.type}:${key.identifier}:${windowBucket(key.windowSeconds)}`;
}

export class RedisRateLimitProvider implements RateLimitProvider {
  constructor() {
    logger.info({ provider: 'redis' }, 'RedisRateLimitProvider initialising');
  }

  async check(key: RateLimitKey): Promise<RateLimitResult> {
    const rKey = redisKey(key);
    try {
      const result = await getRedisClient().eval(
        RATE_LIMIT_LUA,
        1,
        rKey,
        String(key.windowSeconds),
      ) as [number, number];

      const [count, ttl] = result;
      const now          = Date.now();
      // ttl is the remaining seconds in the window; -1 means PERSIST (shouldn't happen), -2 = missing key
      const safeTtl      = ttl > 0 ? ttl : key.windowSeconds;
      const resetAt      = now + safeTtl * 1_000;
      const remaining    = Math.max(0, key.maxRequests - count);

      return {
        allowed:           count <= key.maxRequests,
        remaining,
        limit:             key.maxRequests,
        resetAt,
        retryAfterSeconds: safeTtl,
      };
    } catch (err) {
      const msg = err instanceof Error ? err.message : String(err);
      logger.error({ err: msg, key: rKey, operation: 'check' }, 'Redis rate-limit check error');
      throw new RedisUnavailableError('rate-limit check', msg);
    }
  }

  async reset(type: string, identifier: string): Promise<void> {
    const pattern = `rl:${type}:${identifier}:*`;
    try {
      const keys = await getRedisClient().keys(pattern);
      if (keys.length > 0) {
        await getRedisClient().del(...keys);
      }
      logger.debug({ type, identifier, removed: keys.length }, 'Rate limit reset in Redis');
    } catch (err) {
      const msg = err instanceof Error ? err.message : String(err);
      logger.error({ err: msg, pattern, operation: 'reset' }, 'Redis rate-limit reset error');
      throw new RedisUnavailableError('rate-limit reset', msg);
    }
  }

  providerName(): string {
    return 'redis';
  }
}
