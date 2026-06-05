/**
 * Rate limit provider factory with graceful Redis fallback.
 *
 * Selection logic:
 *  1. If RATE_LIMIT_PROVIDER=memory → InMemoryRateLimitProvider (always works)
 *  2. If RATE_LIMIT_PROVIDER=redis AND REDIS_URL is set → RedisRateLimitProvider
 *  3. If RATE_LIMIT_PROVIDER=redis AND REDIS_URL NOT set → warn + fall back to memory
 *  4. If RedisRateLimitProvider constructor throws → warn + fall back to memory
 *
 * The fallback keeps the service running even if Redis is unavailable.
 * In a multi-replica deployment without Redis, rate limits are per-instance
 * (less accurate but still functional). The log warning makes the degradation
 * observable.
 */

import type { RateLimitProvider }      from '@/domain/interfaces/rate-limit-provider';
import { InMemoryRateLimitProvider }   from './in-memory-rate-limit-provider';
import { RedisRateLimitProvider }      from './redis-rate-limit-provider';
import { config }                      from '@/shared/config';
import { logger }                      from '@/shared/logger';

let _instance: RateLimitProvider | null = null;

export function getRateLimitProvider(): RateLimitProvider {
  if (_instance) return _instance;

  if (config.RATE_LIMIT_PROVIDER === 'redis') {
    if (!config.REDIS_URL) {
      logger.warn(
        { requested: 'redis', fallback: 'memory' },
        'RATE_LIMIT_PROVIDER=redis but REDIS_URL is not set — falling back to memory provider',
      );
      _instance = new InMemoryRateLimitProvider();
    } else {
      try {
        _instance = new RedisRateLimitProvider();
        logger.info({ provider: 'redis' }, 'Rate limit provider initialised (Redis)');
      } catch (err) {
        const msg = err instanceof Error ? err.message : String(err);
        logger.warn(
          { requested: 'redis', fallback: 'memory', err: msg },
          'RedisRateLimitProvider construction failed — falling back to memory provider',
        );
        _instance = new InMemoryRateLimitProvider();
      }
    }
  } else {
    _instance = new InMemoryRateLimitProvider();
    logger.info({ provider: 'memory' }, 'Rate limit provider initialised (memory)');
  }

  return _instance;
}

/** For testing only — reset the singleton. */
export function resetRateLimitProvider(): void {
  if (_instance && 'destroy' in _instance) {
    (_instance as { destroy(): void }).destroy();
  }
  _instance = null;
}
