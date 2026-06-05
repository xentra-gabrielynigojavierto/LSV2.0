/**
 * Access token store factory with graceful Redis fallback.
 *
 * Selection logic:
 *  1. If ACCESS_TOKEN_STORE=memory  → InMemoryAccessTokenStore (always works)
 *  2. If ACCESS_TOKEN_STORE=redis AND REDIS_URL is set → RedisAccessTokenStore
 *  3. If ACCESS_TOKEN_STORE=redis AND REDIS_URL is NOT set → warn + fall back to memory
 *  4. If RedisAccessTokenStore constructor throws → warn + fall back to memory
 *
 * The fallback ensures the service stays up even when Redis is unavailable.
 * Runtime Redis errors (connection drops after startup) surface as
 * RedisUnavailableError from the individual store methods — callers decide
 * whether to retry, serve stale data, or 503.
 */

import type { AccessTokenStore }       from '@/domain/interfaces/access-token-store';
import { InMemoryAccessTokenStore }    from './in-memory-access-token-store';
import { RedisAccessTokenStore }       from './redis-access-token-store';
import { config }                      from '@/shared/config';
import { logger }                      from '@/shared/logger';

let _instance: AccessTokenStore | null = null;

export function getAccessTokenStore(): AccessTokenStore {
  if (_instance) return _instance;

  if (config.ACCESS_TOKEN_STORE === 'redis') {
    if (!config.REDIS_URL) {
      logger.warn(
        { requested: 'redis', fallback: 'memory' },
        'ACCESS_TOKEN_STORE=redis but REDIS_URL is not set — falling back to memory store',
      );
      _instance = new InMemoryAccessTokenStore();
    } else {
      try {
        _instance = new RedisAccessTokenStore();
        logger.info({ store: 'redis' }, 'Access token store initialised (Redis)');
      } catch (err) {
        const msg = err instanceof Error ? err.message : String(err);
        logger.warn(
          { requested: 'redis', fallback: 'memory', err: msg },
          'RedisAccessTokenStore construction failed — falling back to memory store',
        );
        _instance = new InMemoryAccessTokenStore();
      }
    }
  } else {
    _instance = new InMemoryAccessTokenStore();
    logger.info({ store: 'memory' }, 'Access token store initialised (memory)');
  }

  return _instance;
}

/** For testing only — reset the singleton so tests get a fresh store. */
export function resetAccessTokenStore(): void {
  if (_instance) {
    _instance.destroy();
    _instance = null;
  }
}
