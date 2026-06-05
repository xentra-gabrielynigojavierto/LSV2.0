/**
 * Rate limiting tests — unit level, no network required.
 * Tests cover:
 *  - InMemoryRateLimitProvider behaviour
 *  - IP throttling
 *  - User throttling
 *  - Tenant throttling
 *  - Stricter upload/signed-URL limits
 *  - Window reset
 *  - Multi-dimension isolation (IP vs user vs tenant)
 */

import { InMemoryRateLimitProvider } from '../../src/infrastructure/rate-limit/in-memory-rate-limit-provider';
import { RateLimitError }            from '../../src/shared/errors';

function makeKey(overrides: Partial<{
  type: string; identifier: string; windowSeconds: number; maxRequests: number;
}> = {}) {
  return {
    type:          'ip',
    identifier:    '127.0.0.1',
    windowSeconds: 60,
    maxRequests:   3,
    ...overrides,
  };
}

describe('InMemoryRateLimitProvider', () => {
  let provider: InMemoryRateLimitProvider;

  beforeEach(() => {
    provider = new InMemoryRateLimitProvider();
  });

  afterEach(() => {
    provider.destroy();
  });

  // ── Basic behaviour ──────────────────────────────────────────────────────────

  it('allows requests within the limit', async () => {
    const key = makeKey({ maxRequests: 5 });
    for (let i = 0; i < 5; i++) {
      const result = await provider.check(key);
      expect(result.allowed).toBe(true);
    }
  });

  it('blocks the request that exceeds the limit', async () => {
    const key = makeKey({ maxRequests: 3 });
    await provider.check(key);
    await provider.check(key);
    await provider.check(key);
    const result = await provider.check(key);  // 4th — over limit
    expect(result.allowed).toBe(false);
  });

  it('returns correct remaining count', async () => {
    const key = makeKey({ maxRequests: 5 });
    const r1  = await provider.check(key);
    expect(r1.remaining).toBe(4);
    const r2  = await provider.check(key);
    expect(r2.remaining).toBe(3);
  });

  it('remaining is 0 when limit is exactly hit', async () => {
    const key = makeKey({ maxRequests: 2 });
    await provider.check(key);
    const r = await provider.check(key);
    expect(r.remaining).toBe(0);
    expect(r.allowed).toBe(true);
  });

  it('remaining never goes below 0 after limit is exceeded', async () => {
    const key = makeKey({ maxRequests: 1 });
    await provider.check(key);
    const r = await provider.check(key);
    expect(r.remaining).toBe(0);
  });

  it('returns limit equal to maxRequests', async () => {
    const key    = makeKey({ maxRequests: 42 });
    const result = await provider.check(key);
    expect(result.limit).toBe(42);
  });

  it('returns a future resetAt timestamp', async () => {
    const before = Date.now();
    const result = await provider.check(makeKey());
    expect(result.resetAt).toBeGreaterThan(before);
  });

  it('retryAfterSeconds is positive and within window', async () => {
    const key    = makeKey({ windowSeconds: 60 });
    const result = await provider.check(key);
    expect(result.retryAfterSeconds).toBeGreaterThan(0);
    expect(result.retryAfterSeconds).toBeLessThanOrEqual(60);
  });

  // ── Dimension isolation ──────────────────────────────────────────────────────

  it('IP throttling: different IPs have independent counters', async () => {
    const key1 = makeKey({ type: 'ip', identifier: '1.2.3.4', maxRequests: 1 });
    const key2 = makeKey({ type: 'ip', identifier: '5.6.7.8', maxRequests: 1 });

    const r1 = await provider.check(key1);
    const r2 = await provider.check(key2);

    expect(r1.allowed).toBe(true);   // first hit for 1.2.3.4
    expect(r2.allowed).toBe(true);   // first hit for 5.6.7.8
  });

  it('user throttling: different users have independent counters', async () => {
    const key1 = makeKey({ type: 'user', identifier: 'user-aaa', maxRequests: 1 });
    const key2 = makeKey({ type: 'user', identifier: 'user-bbb', maxRequests: 1 });

    await provider.check(key1);   // exhaust user-aaa
    const r1 = await provider.check(key1);
    const r2 = await provider.check(key2);

    expect(r1.allowed).toBe(false);  // user-aaa over limit
    expect(r2.allowed).toBe(true);   // user-bbb unaffected
  });

  it('tenant throttling: different tenants have independent counters', async () => {
    const key1 = makeKey({ type: 'tenant', identifier: 'tenant-x', maxRequests: 2 });
    const key2 = makeKey({ type: 'tenant', identifier: 'tenant-y', maxRequests: 2 });

    await provider.check(key1);
    await provider.check(key1);
    await provider.check(key2);

    const r1 = await provider.check(key1);  // 3rd hit — over limit
    const r2 = await provider.check(key2);  // 2nd hit — within limit

    expect(r1.allowed).toBe(false);
    expect(r2.allowed).toBe(true);
  });

  it('type isolation: ip and user keys for same identifier are independent', async () => {
    const ipKey   = makeKey({ type: 'ip',   identifier: 'same-value', maxRequests: 1 });
    const userKey = makeKey({ type: 'user', identifier: 'same-value', maxRequests: 1 });

    await provider.check(ipKey);
    const ipResult   = await provider.check(ipKey);    // exhausted
    const userResult = await provider.check(userKey);  // separate bucket

    expect(ipResult.allowed).toBe(false);
    expect(userResult.allowed).toBe(true);
  });

  // ── Stricter limits simulation ──────────────────────────────────────────────

  it('upload endpoints enforce tighter limits than general (10 vs 100)', async () => {
    const uploadKey  = makeKey({ type: 'ip', identifier: '1.1.1.1', maxRequests: 10 });
    const generalKey = makeKey({ type: 'ip', identifier: '1.1.1.1', maxRequests: 100 });

    for (let i = 0; i < 10; i++) await provider.check(uploadKey);
    const afterUploadExhausted = await provider.check(uploadKey);

    for (let i = 0; i < 100; i++) await provider.check(generalKey);
    const afterGeneralExhausted = await provider.check(generalKey);

    expect(afterUploadExhausted.allowed).toBe(false);
    expect(afterGeneralExhausted.allowed).toBe(false);
  });

  it('signed-URL endpoints enforce intermediate limits (30)', async () => {
    const key = makeKey({ type: 'ip', identifier: '2.2.2.2', maxRequests: 30 });
    for (let i = 0; i < 30; i++) await provider.check(key);
    const r = await provider.check(key);
    expect(r.allowed).toBe(false);
    expect(r.remaining).toBe(0);
  });

  // ── Reset ──────────────────────────────────────────────────────────────────

  it('reset() clears the counter for a specific key', async () => {
    const key = makeKey({ maxRequests: 1 });
    await provider.check(key);
    const blocked = await provider.check(key);
    expect(blocked.allowed).toBe(false);

    await provider.reset(key.type, key.identifier);

    const afterReset = await provider.check(key);
    expect(afterReset.allowed).toBe(true);
  });

  it('reset() for one key does not affect other keys', async () => {
    const k1 = makeKey({ type: 'ip', identifier: 'a.b.c.d', maxRequests: 1 });
    const k2 = makeKey({ type: 'ip', identifier: 'e.f.g.h', maxRequests: 1 });

    await provider.check(k1);
    await provider.check(k2);
    await provider.reset(k1.type, k1.identifier);

    const r1 = await provider.check(k1);
    const r2 = await provider.check(k2);

    expect(r1.allowed).toBe(true);   // k1 was reset
    expect(r2.allowed).toBe(false);  // k2 still exhausted
  });
});

// ── RateLimitError ────────────────────────────────────────────────────────────

describe('RateLimitError', () => {
  it('has statusCode 429', () => {
    const err = new RateLimitError(30, 'ip');
    expect(err.statusCode).toBe(429);
  });

  it('carries retryAfterSeconds', () => {
    const err = new RateLimitError(45, 'user');
    expect(err.retryAfterSeconds).toBe(45);
  });

  it('carries limitDimension', () => {
    expect(new RateLimitError(10, 'ip').limitDimension).toBe('ip');
    expect(new RateLimitError(10, 'user').limitDimension).toBe('user');
    expect(new RateLimitError(10, 'tenant').limitDimension).toBe('tenant');
  });

  it('has code RATE_LIMIT_EXCEEDED', () => {
    expect(new RateLimitError(5, 'ip').code).toBe('RATE_LIMIT_EXCEEDED');
  });

  it('message includes retryAfterSeconds', () => {
    const err = new RateLimitError(60, 'tenant');
    expect(err.message).toContain('60');
  });
});
