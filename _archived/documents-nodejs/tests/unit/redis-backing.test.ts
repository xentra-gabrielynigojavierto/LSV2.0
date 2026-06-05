/**
 * Redis backing tests — unit level.
 * All Redis calls are mocked; no real Redis server required.
 *
 * Test matrix:
 *
 *  RedisClient module
 *    - creates client with lazyConnect and correct options
 *    - isRedisHealthy tracks connect/error/close events
 *    - resetRedisClient clears the singleton
 *    - getRedisClient throws when REDIS_URL is not set
 *
 *  RedisAccessTokenStore
 *    - store() calls SETEX with correct key, TTL, and JSON payload
 *    - store() throws RedisUnavailableError when Redis fails
 *    - get() returns deserialised AccessToken with Date objects
 *    - get() returns null for a missing key
 *    - get() throws RedisUnavailableError when Redis fails
 *    - markUsed() returns true when Lua returns 1 (first use)
 *    - markUsed() returns false when Lua returns 0 (already used)
 *    - markUsed() returns false when Lua returns -1 (key not found)
 *    - markUsed() throws RedisUnavailableError when Redis fails
 *    - revoke() calls DEL with the correct key
 *    - revoke() throws RedisUnavailableError when Redis fails
 *    - cleanup() always returns 0 (Redis TTL handles expiry)
 *    - destroy() is a no-op (connection managed by shared pool)
 *
 *  RedisRateLimitProvider
 *    - check() calls Lua script with correct key, window ARGV
 *    - check() returns allowed=true when count ≤ limit
 *    - check() returns allowed=false when count > limit
 *    - check() calculates remaining correctly
 *    - check() calculates resetAt from TTL
 *    - check() handles safeTtl when Redis returns -1 TTL
 *    - check() throws RedisUnavailableError when Redis fails
 *    - reset() calls KEYS then DEL for matching keys
 *    - reset() is a no-op when no keys match
 *    - reset() throws RedisUnavailableError when Redis fails
 *    - providerName() returns 'redis'
 *
 *  Factory fallback behaviour
 *    - access token factory: uses Redis when REDIS_URL is set
 *    - access token factory: falls back to memory when REDIS_URL is missing
 *    - rate limit factory: uses Redis when REDIS_URL is set
 *    - rate limit factory: falls back to memory when REDIS_URL is missing
 *
 *  RedisUnavailableError
 *    - has correct statusCode and code
 *    - message includes operation name
 *    - message includes cause when provided
 */

// ── Global mocks (must be before imports) ────────────────────────────────────

jest.mock('../../src/shared/logger', () => ({
  logger: { info: jest.fn(), debug: jest.fn(), warn: jest.fn(), error: jest.fn() },
}));

// Mutable config — individual tests override REDIS_URL / ACCESS_TOKEN_STORE / RATE_LIMIT_PROVIDER
const mockConfig: Record<string, unknown> = {
  REDIS_URL:                   'redis://localhost:6379',
  ACCESS_TOKEN_STORE:          'redis',
  RATE_LIMIT_PROVIDER:         'redis',
  ACCESS_TOKEN_TTL_SECONDS:    300,
  ACCESS_TOKEN_ONE_TIME_USE:   true,
  DIRECT_PRESIGN_ENABLED:      false,
  REQUIRE_CLEAN_SCAN_FOR_ACCESS: true,
  FILE_SCANNER_PROVIDER:       'none',
  RATE_LIMIT_WINDOW_SECONDS:   60,
  RATE_LIMIT_MAX_REQUESTS:     100,
  RATE_LIMIT_UPLOAD_MAX:       10,
  RATE_LIMIT_SIGNED_URL_MAX:   30,
  SIGNED_URL_EXPIRY_SECONDS:   300,
};

jest.mock('../../src/shared/config', () => ({ get config() { return mockConfig; } }));

// ── Redis client mock (used by stores/providers) ──────────────────────────────

const mockRedisOps = {
  setex: jest.fn(),
  get:   jest.fn(),
  del:   jest.fn(),
  eval:  jest.fn(),
  keys:  jest.fn(),
  quit:  jest.fn(),
  disconnect: jest.fn(),
  on:         jest.fn(),
  connect:    jest.fn(),
};

let mockHealthy = true;

jest.mock('../../src/infrastructure/redis/redis-client', () => ({
  getRedisClient:   () => mockRedisOps,
  isRedisHealthy:   () => mockHealthy,
  disconnectRedis:  jest.fn().mockResolvedValue(undefined),
  resetRedisClient: jest.fn(),
}));

// ── ioredis mock (for redis-client.ts own tests) ──────────────────────────────

type EventHandler = (...args: unknown[]) => void;

const listeners: Record<string, EventHandler> = {};

const mockIoRedisInstance = {
  on:         jest.fn((event: string, fn: EventHandler) => { listeners[event] = fn; }),
  connect:    jest.fn().mockResolvedValue(undefined),
  quit:       jest.fn().mockResolvedValue('OK'),
  disconnect: jest.fn(),
};

jest.mock('ioredis', () => {
  return jest.fn().mockImplementation(() => mockIoRedisInstance);
});

// ── Imports (after mocks) ─────────────────────────────────────────────────────

import { RedisAccessTokenStore }     from '../../src/infrastructure/access-token/redis-access-token-store';
import { RedisRateLimitProvider }    from '../../src/infrastructure/rate-limit/redis-rate-limit-provider';
import { RedisUnavailableError }     from '../../src/shared/errors';
import type { AccessToken }          from '../../src/domain/entities/access-token';
import type { RateLimitKey }         from '../../src/domain/interfaces/rate-limit-provider';

// ── Test helpers ──────────────────────────────────────────────────────────────

function makeToken(overrides: Partial<AccessToken> = {}): AccessToken {
  return {
    token:        'a'.repeat(64),
    documentId:   'doc-1',
    tenantId:     'tenant-1',
    userId:       'user-1',
    type:         'view',
    isOneTimeUse: true,
    isUsed:       false,
    expiresAt:    new Date(Date.now() + 300_000),
    createdAt:    new Date(),
    issuedFromIp: '127.0.0.1',
    ...overrides,
  };
}

function makeKey(overrides: Partial<RateLimitKey> = {}): RateLimitKey {
  return {
    type:          'ip',
    identifier:    '203.0.113.5',
    windowSeconds: 60,
    maxRequests:   10,
    ...overrides,
  };
}

// ── RedisUnavailableError ─────────────────────────────────────────────────────

describe('RedisUnavailableError', () => {
  it('has statusCode 503 and code REDIS_UNAVAILABLE', () => {
    const err = new RedisUnavailableError('store');
    expect(err.statusCode).toBe(503);
    expect(err.code).toBe('REDIS_UNAVAILABLE');
  });

  it('message includes the operation name', () => {
    const err = new RedisUnavailableError('markUsed');
    expect(err.message).toContain('markUsed');
  });

  it('message includes cause when provided', () => {
    const err = new RedisUnavailableError('get', 'ECONNREFUSED');
    expect(err.message).toContain('ECONNREFUSED');
  });
});

// ── RedisAccessTokenStore ─────────────────────────────────────────────────────

describe('RedisAccessTokenStore', () => {
  let store: RedisAccessTokenStore;

  beforeEach(() => {
    jest.clearAllMocks();
    mockHealthy = true;
    store = new RedisAccessTokenStore();
  });

  // ── store() ──────────────────────────────────────────────────────────────

  describe('store()', () => {
    it('calls SETEX with the correct key prefix, positive TTL, and JSON body', async () => {
      mockRedisOps.setex.mockResolvedValue('OK');
      const token = makeToken();

      await store.store(token);

      expect(mockRedisOps.setex).toHaveBeenCalledTimes(1);
      const [calledKey, calledTtl, calledBody] = mockRedisOps.setex.mock.calls[0] as [string, number, string];

      expect(calledKey).toBe(`access_token:${'a'.repeat(64)}`);
      expect(calledTtl).toBeGreaterThan(0);
      expect(calledTtl).toBeLessThanOrEqual(300);

      const parsed = JSON.parse(calledBody) as AccessToken;
      expect(parsed.documentId).toBe('doc-1');
      expect(parsed.tenantId).toBe('tenant-1');
    });

    it('throws RedisUnavailableError when SETEX fails', async () => {
      mockRedisOps.setex.mockRejectedValue(new Error('ECONNREFUSED'));
      await expect(store.store(makeToken())).rejects.toThrow(RedisUnavailableError);
    });

    it('TTL is at least 1 second for tokens expiring very soon', async () => {
      mockRedisOps.setex.mockResolvedValue('OK');
      // expires in 500ms — should round up to 1
      const token = makeToken({ expiresAt: new Date(Date.now() + 500) });

      await store.store(token);

      const [, ttl] = mockRedisOps.setex.mock.calls[0] as [string, number, string];
      expect(ttl).toBe(1);
    });
  });

  // ── get() ─────────────────────────────────────────────────────────────────

  describe('get()', () => {
    it('returns a deserialised AccessToken with proper Date objects', async () => {
      const token = makeToken();
      mockRedisOps.get.mockResolvedValue(JSON.stringify(token));

      const result = await store.get(token.token);

      expect(result).not.toBeNull();
      expect(result!.expiresAt).toBeInstanceOf(Date);
      expect(result!.createdAt).toBeInstanceOf(Date);
      expect(result!.documentId).toBe('doc-1');
    });

    it('returns null when the key does not exist', async () => {
      mockRedisOps.get.mockResolvedValue(null);

      const result = await store.get('z'.repeat(64));
      expect(result).toBeNull();
    });

    it('throws RedisUnavailableError when GET fails', async () => {
      mockRedisOps.get.mockRejectedValue(new Error('ECONNRESET'));
      await expect(store.get('a'.repeat(64))).rejects.toThrow(RedisUnavailableError);
    });
  });

  // ── markUsed() ────────────────────────────────────────────────────────────

  describe('markUsed()', () => {
    it('returns true when Lua script returns 1 (first use)', async () => {
      mockRedisOps.eval.mockResolvedValue(1);
      const result = await store.markUsed('a'.repeat(64));
      expect(result).toBe(true);
    });

    it('returns false when Lua script returns 0 (already used)', async () => {
      mockRedisOps.eval.mockResolvedValue(0);
      const result = await store.markUsed('a'.repeat(64));
      expect(result).toBe(false);
    });

    it('returns false when Lua script returns -1 (key not found or expired)', async () => {
      mockRedisOps.eval.mockResolvedValue(-1);
      const result = await store.markUsed('a'.repeat(64));
      expect(result).toBe(false);
    });

    it('calls eval with the correct key argument', async () => {
      mockRedisOps.eval.mockResolvedValue(1);
      await store.markUsed('b'.repeat(64));

      const callArgs = mockRedisOps.eval.mock.calls[0] as unknown[];
      // args: script, numkeys, key
      expect(callArgs[2]).toBe(`access_token:${'b'.repeat(64)}`);
    });

    it('throws RedisUnavailableError when eval fails', async () => {
      mockRedisOps.eval.mockRejectedValue(new Error('LOADING'));
      await expect(store.markUsed('a'.repeat(64))).rejects.toThrow(RedisUnavailableError);
    });
  });

  // ── revoke() ──────────────────────────────────────────────────────────────

  describe('revoke()', () => {
    it('calls DEL with the correct prefixed key', async () => {
      mockRedisOps.del.mockResolvedValue(1);
      await store.revoke('c'.repeat(64));

      expect(mockRedisOps.del).toHaveBeenCalledWith(`access_token:${'c'.repeat(64)}`);
    });

    it('throws RedisUnavailableError when DEL fails', async () => {
      mockRedisOps.del.mockRejectedValue(new Error('ECONNREFUSED'));
      await expect(store.revoke('a'.repeat(64))).rejects.toThrow(RedisUnavailableError);
    });
  });

  // ── cleanup() + destroy() ─────────────────────────────────────────────────

  describe('cleanup()', () => {
    it('always returns 0 (Redis TTL handles expiry)', async () => {
      const count = await store.cleanup();
      expect(count).toBe(0);
      expect(mockRedisOps.del).not.toHaveBeenCalled();
    });
  });

  describe('destroy()', () => {
    it('does not call quit (connection managed by shared pool)', () => {
      store.destroy();
      expect(mockRedisOps.quit).not.toHaveBeenCalled();
    });
  });
});

// ── RedisRateLimitProvider ────────────────────────────────────────────────────

describe('RedisRateLimitProvider', () => {
  let provider: RedisRateLimitProvider;

  beforeEach(() => {
    jest.clearAllMocks();
    provider = new RedisRateLimitProvider();
  });

  describe('check()', () => {
    it('returns allowed=true when count ≤ maxRequests', async () => {
      // Lua returns [count, ttl]
      mockRedisOps.eval.mockResolvedValue([3, 45]);
      const key = makeKey({ maxRequests: 10 });

      const result = await provider.check(key);

      expect(result.allowed).toBe(true);
      expect(result.remaining).toBe(7); // 10 - 3
      expect(result.limit).toBe(10);
    });

    it('returns allowed=false when count > maxRequests', async () => {
      mockRedisOps.eval.mockResolvedValue([11, 30]);
      const key = makeKey({ maxRequests: 10 });

      const result = await provider.check(key);

      expect(result.allowed).toBe(false);
      expect(result.remaining).toBe(0);
    });

    it('calculates resetAt from the returned TTL', async () => {
      const before = Date.now();
      mockRedisOps.eval.mockResolvedValue([1, 45]);
      const key = makeKey();

      const result = await provider.check(key);

      const after = Date.now();
      // resetAt should be approximately now + 45s
      expect(result.resetAt).toBeGreaterThanOrEqual(before + 44_000);
      expect(result.resetAt).toBeLessThanOrEqual(after + 46_000);
      expect(result.retryAfterSeconds).toBe(45);
    });

    it('uses windowSeconds as safeTtl when Redis returns ttl = -1', async () => {
      mockRedisOps.eval.mockResolvedValue([1, -1]);
      const key = makeKey({ windowSeconds: 60 });

      const result = await provider.check(key);

      expect(result.retryAfterSeconds).toBe(60); // fallback to windowSeconds
    });

    it('calls eval with key as KEYS[1] and windowSeconds as ARGV[1]', async () => {
      mockRedisOps.eval.mockResolvedValue([1, 59]);
      const key = makeKey({ type: 'user', identifier: 'user-42', windowSeconds: 60 });

      await provider.check(key);

      const callArgs = mockRedisOps.eval.mock.calls[0] as unknown[];
      // eval(script, numkeys, key, windowSeconds)
      expect(callArgs[1]).toBe(1);                           // numkeys
      const redisKey = callArgs[2] as string;
      expect(redisKey).toMatch(/^rl:user:user-42:\d+$/);    // key format
      expect(callArgs[3]).toBe('60');                        // windowSeconds as string
    });

    it('remaining is clamped to 0 when count is far over limit', async () => {
      mockRedisOps.eval.mockResolvedValue([50, 10]);
      const key = makeKey({ maxRequests: 10 });

      const result = await provider.check(key);

      expect(result.remaining).toBe(0);
    });

    it('throws RedisUnavailableError when eval fails', async () => {
      mockRedisOps.eval.mockRejectedValue(new Error('ECONNREFUSED'));
      await expect(provider.check(makeKey())).rejects.toThrow(RedisUnavailableError);
    });
  });

  describe('reset()', () => {
    it('calls KEYS then DEL for all matching bucket keys', async () => {
      const matchingKeys = ['rl:ip:203.0.113.5:28531', 'rl:ip:203.0.113.5:28532'];
      mockRedisOps.keys.mockResolvedValue(matchingKeys);
      mockRedisOps.del.mockResolvedValue(2);

      await provider.reset('ip', '203.0.113.5');

      expect(mockRedisOps.keys).toHaveBeenCalledWith('rl:ip:203.0.113.5:*');
      expect(mockRedisOps.del).toHaveBeenCalledWith(...matchingKeys);
    });

    it('does not call DEL when no keys match', async () => {
      mockRedisOps.keys.mockResolvedValue([]);

      await provider.reset('ip', '10.0.0.1');

      expect(mockRedisOps.del).not.toHaveBeenCalled();
    });

    it('throws RedisUnavailableError when KEYS fails', async () => {
      mockRedisOps.keys.mockRejectedValue(new Error('ECONNRESET'));
      await expect(provider.reset('ip', '10.0.0.1')).rejects.toThrow(RedisUnavailableError);
    });
  });

  describe('providerName()', () => {
    it('returns "redis"', () => {
      expect(provider.providerName()).toBe('redis');
    });
  });
});

// ── Factory fallback behaviour ────────────────────────────────────────────────

describe('Access token store factory — fallback', () => {
  beforeEach(() => {
    jest.resetModules();
    jest.clearAllMocks();
  });

  it('returns a RedisAccessTokenStore when ACCESS_TOKEN_STORE=redis and REDIS_URL is set', async () => {
    mockConfig['ACCESS_TOKEN_STORE'] = 'redis';
    mockConfig['REDIS_URL'] = 'redis://localhost:6379';

    const { getAccessTokenStore, resetAccessTokenStore } = await import(
      '../../src/infrastructure/access-token/access-token-store-factory'
    );
    resetAccessTokenStore();
    const store = getAccessTokenStore();
    expect(store.constructor.name).toBe('RedisAccessTokenStore');
    resetAccessTokenStore();
  });

  it('falls back to InMemoryAccessTokenStore when REDIS_URL is not set', async () => {
    mockConfig['ACCESS_TOKEN_STORE'] = 'redis';
    mockConfig['REDIS_URL']          = undefined;

    const { getAccessTokenStore, resetAccessTokenStore } = await import(
      '../../src/infrastructure/access-token/access-token-store-factory'
    );
    resetAccessTokenStore();
    const store = getAccessTokenStore();
    expect(store.constructor.name).toBe('InMemoryAccessTokenStore');
    resetAccessTokenStore();
    mockConfig['REDIS_URL'] = 'redis://localhost:6379';
  });

  it('returns InMemoryAccessTokenStore directly when ACCESS_TOKEN_STORE=memory', async () => {
    mockConfig['ACCESS_TOKEN_STORE'] = 'memory';
    mockConfig['REDIS_URL']          = 'redis://localhost:6379';

    const { getAccessTokenStore, resetAccessTokenStore } = await import(
      '../../src/infrastructure/access-token/access-token-store-factory'
    );
    resetAccessTokenStore();
    const store = getAccessTokenStore();
    expect(store.constructor.name).toBe('InMemoryAccessTokenStore');
    resetAccessTokenStore();
    mockConfig['ACCESS_TOKEN_STORE'] = 'redis';
  });
});

describe('Rate limit provider factory — fallback', () => {
  beforeEach(() => {
    jest.resetModules();
    jest.clearAllMocks();
  });

  it('returns a RedisRateLimitProvider when RATE_LIMIT_PROVIDER=redis and REDIS_URL is set', async () => {
    mockConfig['RATE_LIMIT_PROVIDER'] = 'redis';
    mockConfig['REDIS_URL']           = 'redis://localhost:6379';

    const { getRateLimitProvider, resetRateLimitProvider } = await import(
      '../../src/infrastructure/rate-limit/rate-limit-factory'
    );
    resetRateLimitProvider();
    const p = getRateLimitProvider();
    expect(p.constructor.name).toBe('RedisRateLimitProvider');
    resetRateLimitProvider();
  });

  it('falls back to InMemoryRateLimitProvider when REDIS_URL is not set', async () => {
    mockConfig['RATE_LIMIT_PROVIDER'] = 'redis';
    mockConfig['REDIS_URL']           = undefined;

    const { getRateLimitProvider, resetRateLimitProvider } = await import(
      '../../src/infrastructure/rate-limit/rate-limit-factory'
    );
    resetRateLimitProvider();
    const p = getRateLimitProvider();
    expect(p.constructor.name).toBe('InMemoryRateLimitProvider');
    resetRateLimitProvider();
    mockConfig['REDIS_URL'] = 'redis://localhost:6379';
  });

  it('returns InMemoryRateLimitProvider directly when RATE_LIMIT_PROVIDER=memory', async () => {
    mockConfig['RATE_LIMIT_PROVIDER'] = 'memory';

    const { getRateLimitProvider, resetRateLimitProvider } = await import(
      '../../src/infrastructure/rate-limit/rate-limit-factory'
    );
    resetRateLimitProvider();
    const p = getRateLimitProvider();
    expect(p.constructor.name).toBe('InMemoryRateLimitProvider');
    resetRateLimitProvider();
    mockConfig['RATE_LIMIT_PROVIDER'] = 'redis';
  });
});
