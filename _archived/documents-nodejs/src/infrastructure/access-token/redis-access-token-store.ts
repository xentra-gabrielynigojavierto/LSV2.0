/**
 * RedisAccessTokenStore — distributed access-token storage.
 *
 * Key schema:
 *   access_token:{tokenString}  → JSON(AccessToken), TTL = remaining seconds
 *
 * Atomic one-time-use via Lua MARK_USED_LUA:
 *   Reads isUsed, guards against concurrent replay, flips flag in a single
 *   Redis command — prevents TOCTOU races across horizontally-scaled replicas.
 *   Returns: 1 = marked successfully, 0 = already used, -1 = key not found.
 *
 * Graceful degradation:
 *   All methods wrap Redis errors in RedisUnavailableError so callers
 *   can decide to fall back to the memory provider or surface a 503.
 */

import type { AccessTokenStore } from '@/domain/interfaces/access-token-store';
import type { AccessToken }      from '@/domain/entities/access-token';
import { getRedisClient }        from '@/infrastructure/redis/redis-client';
import { RedisUnavailableError } from '@/shared/errors';
import { logger }                from '@/shared/logger';

const KEY_PREFIX = 'access_token:';

const MARK_USED_LUA = `
local key  = KEYS[1]
local raw  = redis.call('GET', key)
if not raw then return -1 end
local tok  = cjson.decode(raw)
if tok.isUsed then return 0 end
tok.isUsed = true
local ttl  = redis.call('TTL', key)
if ttl < 1 then return -1 end
redis.call('SET', key, cjson.encode(tok), 'EX', ttl)
return 1
`;

function key(tokenString: string): string {
  return `${KEY_PREFIX}${tokenString}`;
}

function deserialise(raw: string): AccessToken {
  const t = JSON.parse(raw) as AccessToken;
  t.expiresAt = new Date(t.expiresAt);
  t.createdAt = new Date(t.createdAt);
  return t;
}

function ttlSeconds(expiresAt: Date): number {
  return Math.max(1, Math.ceil((expiresAt.getTime() - Date.now()) / 1_000));
}

export class RedisAccessTokenStore implements AccessTokenStore {
  constructor() {
    logger.info({ store: 'redis' }, 'RedisAccessTokenStore initialising');
  }

  async store(token: AccessToken): Promise<void> {
    try {
      const ttl = ttlSeconds(token.expiresAt);
      await getRedisClient().setex(key(token.token), ttl, JSON.stringify(token));
      logger.debug({ documentId: token.documentId, ttl }, 'Access token stored in Redis');
    } catch (err) {
      const msg = err instanceof Error ? err.message : String(err);
      logger.error({ err: msg, operation: 'store' }, 'Redis access token store error');
      throw new RedisUnavailableError('store', msg);
    }
  }

  async get(tokenString: string): Promise<AccessToken | null> {
    try {
      const raw = await getRedisClient().get(key(tokenString));
      if (!raw) return null;
      return deserialise(raw);
    } catch (err) {
      const msg = err instanceof Error ? err.message : String(err);
      logger.error({ err: msg, operation: 'get' }, 'Redis access token get error');
      throw new RedisUnavailableError('get', msg);
    }
  }

  /**
   * Atomically mark a token as used.
   * Returns true if the token was successfully marked (first use).
   * Returns false if the token was already used OR does not exist.
   *
   * The Lua script ensures no two concurrent requests can both succeed,
   * even on a Redis cluster with multiple app replicas.
   */
  async markUsed(tokenString: string): Promise<boolean> {
    try {
      const result = await getRedisClient().eval(MARK_USED_LUA, 1, key(tokenString)) as number;
      // 1 = marked, 0 = already used, -1 = not found / expired
      return result === 1;
    } catch (err) {
      const msg = err instanceof Error ? err.message : String(err);
      logger.error({ err: msg, operation: 'markUsed' }, 'Redis markUsed error');
      throw new RedisUnavailableError('markUsed', msg);
    }
  }

  async revoke(tokenString: string): Promise<void> {
    try {
      await getRedisClient().del(key(tokenString));
      logger.debug({ operation: 'revoke' }, 'Access token revoked in Redis');
    } catch (err) {
      const msg = err instanceof Error ? err.message : String(err);
      logger.error({ err: msg, operation: 'revoke' }, 'Redis revoke error');
      throw new RedisUnavailableError('revoke', msg);
    }
  }

  /**
   * Redis TTL handles expiry automatically — no manual sweep needed.
   * Always returns 0 (nothing manually removed).
   */
  async cleanup(): Promise<number> {
    return 0;
  }

  destroy(): void {
    // Connection lifecycle managed by shared redis-client singleton.
    // Individual stores do not close the connection.
    logger.info({ store: 'redis' }, 'RedisAccessTokenStore destroyed (connection managed by pool)');
  }
}
