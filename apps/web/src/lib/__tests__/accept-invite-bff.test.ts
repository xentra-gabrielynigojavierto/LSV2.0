import { test } from 'node:test';
import assert from 'node:assert/strict';

type FakeFetch = (url: string, init?: RequestInit) => Promise<Response>;

function makeFetchOk(body: unknown): FakeFetch {
  return (_url, _init) =>
    Promise.resolve({
      ok:     true,
      status: 200,
      json:   () => Promise.resolve(body),
    } as unknown as Response);
}

function makeFetchErr(status: number, body: unknown): FakeFetch {
  return (_url, _init) =>
    Promise.resolve({
      ok:     false,
      status,
      json:   () => Promise.resolve(body),
    } as unknown as Response);
}

function makeFetchThrow(err: Error): FakeFetch {
  return () => Promise.reject(err);
}

async function withFetch<T>(fake: FakeFetch, fn: () => Promise<T>): Promise<T> {
  const original = globalThis.fetch;
  try {
    (globalThis as Record<string, unknown>).fetch = fake;
    return await fn();
  } finally {
    (globalThis as Record<string, unknown>).fetch = original;
  }
}

async function invokePost(body: unknown): Promise<Response> {
  const { POST } = await import('../../app/api/auth/accept-invite/route.js');
  const req = new Request('http://localhost/api/auth/accept-invite', {
    method:  'POST',
    headers: { 'Content-Type': 'application/json' },
    body:    JSON.stringify(body),
  });
  return POST(req as import('next/server').NextRequest);
}

// ── tenantPortalUrl forwarding ─────────────────────────────────────────────

test('forwards tenantPortalUrl from identity response when present', async () => {
  const res = await withFetch(
    makeFetchOk({
      message:        'Invitation accepted. Your account is now active.',
      tenantPortalUrl: 'https://acme.portal.example.com',
    }),
    () => invokePost({ token: 'raw-token-abc', newPassword: 'Password123!' }),
  );

  assert.equal(res.status, 200);
  const body = await res.json();
  assert.equal(body.tenantPortalUrl, 'https://acme.portal.example.com');
  assert.equal(body.message, 'Invitation accepted. Your account is now active.');
});

test('tenantPortalUrl is null in response when identity omits it', async () => {
  const res = await withFetch(
    makeFetchOk({ message: 'Invitation accepted. Your account is now active.' }),
    () => invokePost({ token: 'raw-token-xyz', newPassword: 'Password123!' }),
  );

  assert.equal(res.status, 200);
  const body = await res.json();
  assert.equal(body.tenantPortalUrl, null);
});

test('tenantPortalUrl is null in response when identity returns null explicitly', async () => {
  const res = await withFetch(
    makeFetchOk({ message: 'Invitation accepted.', tenantPortalUrl: null }),
    () => invokePost({ token: 'raw-token-null', newPassword: 'Password123!' }),
  );

  assert.equal(res.status, 200);
  const body = await res.json();
  assert.equal(body.tenantPortalUrl, null);
});

// ── input validation ────────────────────────────────────────────────────────

test('returns 400 when token is missing', async () => {
  const res = await withFetch(
    makeFetchOk({}),
    () => invokePost({ newPassword: 'Password123!' }),
  );

  assert.equal(res.status, 400);
  const body = await res.json();
  assert.ok(typeof body.message === 'string');
});

test('returns 400 when newPassword is shorter than 8 characters', async () => {
  const res = await withFetch(
    makeFetchOk({}),
    () => invokePost({ token: 'tok', newPassword: 'short' }),
  );

  assert.equal(res.status, 400);
});

// ── upstream error handling ─────────────────────────────────────────────────

test('returns 503 when identity service is unreachable', async () => {
  const res = await withFetch(
    makeFetchThrow(new Error('ECONNREFUSED')),
    () => invokePost({ token: 'tok', newPassword: 'Password123!' }),
  );

  assert.equal(res.status, 503);
  const body = await res.json();
  assert.ok(body.message.includes('temporarily unavailable'));
});

test('maps identity 400 to 400 with the upstream message', async () => {
  const res = await withFetch(
    makeFetchErr(400, { error: 'Invalid or expired invitation token.' }),
    () => invokePost({ token: 'bad-tok', newPassword: 'Password123!' }),
  );

  assert.equal(res.status, 400);
  const body = await res.json();
  assert.equal(body.message, 'Invalid or expired invitation token.');
});

test('maps identity 500 to 503 with a user-friendly message', async () => {
  const res = await withFetch(
    makeFetchErr(500, { title: 'Internal Server Error' }),
    () => invokePost({ token: 'tok', newPassword: 'Password123!' }),
  );

  assert.equal(res.status, 503);
  const body = await res.json();
  assert.ok(body.message.includes('temporarily unavailable'));
});
