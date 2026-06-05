/**
 * Shared Redis connection layer for the Docs Service.
 *
 * Design goals:
 *  - Single ioredis instance shared across all Redis-backed providers
 *  - Lazy connect: service starts even if Redis is unavailable at boot
 *  - enableOfflineQueue: false — commands fail immediately when disconnected
 *    (callers catch and handle; no unbounded queue building up in memory)
 *  - Structured error logging via Pino — never crashes the process
 *  - isRedisHealthy() for health-check / factory fallback decisions
 *  - resetRedisClient() for clean test isolation (no singleton bleed)
 *
 * Key schema conventions (enforced by callers, not here):
 *  access_token:{hex64}          → JSON, SETEX TTL seconds
 *  rl:{type}:{identifier}:{bucket} → integer counter, EXPIRE seconds
 */

import Redis from 'ioredis';
import { logger } from '@/shared/logger';
import { config }  from '@/shared/config';

let _client: Redis | null    = null;
let _healthy:  boolean        = false;

/**
 * Returns the shared Redis client, creating it on first call.
 *
 * Throws `Error` immediately if `REDIS_URL` is not configured so
 * factory code can catch it and fall back to the memory provider.
 */
export function getRedisClient(): Redis {
  if (_client) return _client;

  if (!config.REDIS_URL) {
    throw new Error('REDIS_URL is not configured');
  }

  _client = new Redis(config.REDIS_URL, {
    lazyConnect:              true,   // don't connect synchronously in the constructor
    enableOfflineQueue:       false,  // reject commands immediately when disconnected
    maxRetriesPerRequest:     3,      // retry transient errors before throwing
    connectTimeout:           5_000,  // 5 s TCP connection timeout
    commandTimeout:           3_000,  // 3 s per-command timeout
    reconnectOnError(err) {
      // Reconnect on read/write errors (e.g. ECONNRESET), not auth errors
      return err.message.includes('ECONNRESET') || err.message.includes('ETIMEDOUT');
    },
  });

  _client.on('connect', () => {
    _healthy = true;
    logger.info({ provider: 'redis' }, 'Redis connected');
  });

  _client.on('ready', () => {
    _healthy = true;
    logger.debug({ provider: 'redis' }, 'Redis ready');
  });

  _client.on('error', (err: Error) => {
    _healthy = false;
    logger.error(
      { provider: 'redis', err: err.message, code: (err as NodeJS.ErrnoException).code },
      'Redis connection error',
    );
  });

  _client.on('close', () => {
    _healthy = false;
    logger.warn({ provider: 'redis' }, 'Redis connection closed');
  });

  _client.on('reconnecting', (delay: number) => {
    logger.info({ provider: 'redis', delayMs: delay }, 'Redis reconnecting');
  });

  // Initiate the lazy connection — errors are handled by the 'error' event above
  _client.connect().catch((err: Error) => {
    logger.warn(
      { provider: 'redis', err: err.message },
      'Redis initial connect failed — will retry in background',
    );
  });

  return _client;
}

/** True when the shared client has an active, ready connection. */
export function isRedisHealthy(): boolean {
  return _healthy;
}

/** Gracefully close the Redis connection and reset the singleton. */
export async function disconnectRedis(): Promise<void> {
  if (_client) {
    try {
      await _client.quit();
    } catch {
      _client.disconnect();
    }
    _client  = null;
    _healthy = false;
    logger.info({ provider: 'redis' }, 'Redis disconnected');
  }
}

/** Reset the singleton WITHOUT disconnecting. Used in unit tests only. */
export function resetRedisClient(): void {
  _client  = null;
  _healthy = false;
}
