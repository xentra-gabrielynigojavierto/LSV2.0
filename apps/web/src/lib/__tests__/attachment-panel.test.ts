import { test } from 'node:test';
import assert from 'node:assert/strict';

type FakeFetch = (url: string, init?: RequestInit) => Promise<Response>;

function makeOk(body: unknown, status = 200): FakeFetch {
  return (_url, _init) =>
    Promise.resolve({
      ok:      true,
      status,
      headers: { get: (_h: string) => 'test-corr-id' },
      json:    () => Promise.resolve(body),
    } as unknown as Response);
}

function makeErr(status: number, body: unknown): FakeFetch {
  return (_url, _init) =>
    Promise.resolve({
      ok:      false,
      status,
      headers: { get: (_h: string) => 'test-corr-id' },
      json:    () => Promise.resolve(body),
    } as unknown as Response);
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

async function getApiError() {
  const { ApiError } = await import('../api-client.js');
  return ApiError;
}

async function getCareConnectApi() {
  const { careConnectApi } = await import('../careconnect-api.js');
  return careConnectApi;
}

const ATT_1 = {
  id: 'att-1', fileName: 'referral-notes.pdf', contentType: 'application/pdf',
  fileSizeBytes: 204800, status: 'available', createdAtUtc: '2026-01-15T10:00:00Z',
};

const ATT_2 = {
  id: 'att-2', fileName: 'lab-results.png', contentType: 'image/png',
  fileSizeBytes: 512000, status: 'available', createdAtUtc: '2026-01-16T09:30:00Z',
};

const SIGNED_URL = { url: 'https://storage.example.com/signed?token=abc', expiresInSeconds: 300 };

// ApiError class

test('ApiError: 403 → isForbidden true', async () => {
  const ApiError = await getApiError();
  const err = new ApiError(403, 'Forbidden', 'c1');
  assert.ok(err.isForbidden);
  assert.equal(err.isServerError, false);
});

test('ApiError: 401 → isUnauthorized true', async () => {
  const ApiError = await getApiError();
  assert.ok(new ApiError(401, 'Unauthorized', 'c2').isUnauthorized);
});

test('ApiError: 503 → isServerError true', async () => {
  const ApiError = await getApiError();
  assert.ok(new ApiError(503, 'Unavailable', 'c3').isServerError);
});

test('ApiError: 500 → isServerError true', async () => {
  const ApiError = await getApiError();
  assert.ok(new ApiError(500, 'ISE', 'c4').isServerError);
});

test('ApiError: instanceof ApiError and Error', async () => {
  const ApiError = await getApiError();
  const err = new ApiError(503, 'x', 'c5');
  assert.ok(err instanceof ApiError);
  assert.ok(err instanceof Error);
  assert.equal(err.name, 'ApiError');
});

// referralAttachments.list

test('referralAttachments.list: returns the attachment list', async () => {
  const api = await getCareConnectApi();
  const { data } = await withFetch(makeOk([ATT_1, ATT_2]), () =>
    api.referralAttachments.list('ref-1'),
  );
  assert.equal(data.length, 2);
  assert.equal(data[0].id, 'att-1');
});

test('referralAttachments.list: calls the correct endpoint', async () => {
  const api = await getCareConnectApi();
  let url = '';
  await withFetch((u) => { url = u; return makeOk([ATT_1])(u); }, () =>
    api.referralAttachments.list('ref-99'),
  );
  assert.ok(url.includes('/careconnect/api/referrals/ref-99/attachments'), `bad URL: ${url}`);
});

test('referralAttachments.list: throws ApiError on 500', async () => {
  const [api, ApiError] = await Promise.all([getCareConnectApi(), getApiError()]);
  await assert.rejects(
    () => withFetch(makeErr(500, { message: 'Server error' }), () =>
      api.referralAttachments.list('ref-1'),
    ),
    (err: unknown) => { assert.ok(err instanceof ApiError); assert.equal(err.status, 500); return true; },
  );
});

// referralAttachments.upload

test('referralAttachments.upload: returns the created summary', async () => {
  const api = await getCareConnectApi();
  const { data } = await withFetch(makeOk(ATT_1, 201), () => {
    return api.referralAttachments.upload('ref-1', new File(['x'], 'doc.pdf', { type: 'application/pdf' }));
  });
  assert.equal(data.id, 'att-1');
});

test('referralAttachments.upload: POST to the correct endpoint', async () => {
  const api = await getCareConnectApi();
  let url = ''; let method = '';
  await withFetch((u, i) => { url = u; method = i?.method ?? ''; return makeOk(ATT_1, 201)(u, i); }, () =>
    api.referralAttachments.upload('ref-7', new File(['x'], 'f.pdf', { type: 'application/pdf' })),
  );
  assert.ok(url.includes('/careconnect/api/referrals/ref-7/attachments/upload'), `bad URL: ${url}`);
  assert.equal(method, 'POST');
});

test('referralAttachments.upload: throws ApiError with server message on 413', async () => {
  const [api, ApiError] = await Promise.all([getCareConnectApi(), getApiError()]);
  await assert.rejects(
    () => withFetch(makeErr(413, { message: 'File too large.' }), () =>
      api.referralAttachments.upload('ref-1', new File(['x'], 'big.pdf', { type: 'application/pdf' })),
    ),
    (err: unknown) => {
      assert.ok(err instanceof ApiError);
      assert.equal(err.status, 413);
      assert.equal(err.message, 'File too large.');
      return true;
    },
  );
});

// referralAttachments.getSignedUrl

test('referralAttachments.getSignedUrl: returns the signed URL', async () => {
  const api = await getCareConnectApi();
  const { data } = await withFetch(makeOk(SIGNED_URL), () =>
    api.referralAttachments.getSignedUrl('ref-1', 'att-1'),
  );
  assert.equal(data.url, SIGNED_URL.url);
  assert.equal(data.expiresInSeconds, 300);
});

test('referralAttachments.getSignedUrl: calls the correct endpoint', async () => {
  const api = await getCareConnectApi();
  let url = '';
  await withFetch((u) => { url = u; return makeOk(SIGNED_URL)(u); }, () =>
    api.referralAttachments.getSignedUrl('ref-1', 'att-1'),
  );
  assert.ok(url.includes('/careconnect/api/referrals/ref-1/attachments/att-1/url'), `bad URL: ${url}`);
});

test('referralAttachments.getSignedUrl: each call hits the network independently', async () => {
  const api = await getCareConnectApi();
  let calls = 0;
  await withFetch(async (u) => { calls++; return makeOk({ ...SIGNED_URL, url: `https://x.com?n=${calls}` })(u); }, async () => {
    const r1 = await api.referralAttachments.getSignedUrl('ref-1', 'att-1');
    const r2 = await api.referralAttachments.getSignedUrl('ref-1', 'att-1');
    assert.equal(calls, 2);
    assert.notEqual(r1.data.url, r2.data.url);
  });
});

test('referralAttachments.getSignedUrl: 403 → ApiError with isForbidden', async () => {
  const [api, ApiError] = await Promise.all([getCareConnectApi(), getApiError()]);
  await assert.rejects(
    () => withFetch(makeErr(403, { message: 'Forbidden' }), () =>
      api.referralAttachments.getSignedUrl('ref-1', 'att-x'),
    ),
    (err: unknown) => { assert.ok(err instanceof ApiError); assert.ok(err.isForbidden); return true; },
  );
});

test('referralAttachments.getSignedUrl: 503 → ApiError with isServerError', async () => {
  const [api, ApiError] = await Promise.all([getCareConnectApi(), getApiError()]);
  await assert.rejects(
    () => withFetch(makeErr(503, { message: 'Unavailable' }), () =>
      api.referralAttachments.getSignedUrl('ref-1', 'att-1'),
    ),
    (err: unknown) => { assert.ok(err instanceof ApiError); assert.ok(err.isServerError); return true; },
  );
});

// appointmentAttachments

test('appointmentAttachments.list: calls the correct endpoint', async () => {
  const api = await getCareConnectApi();
  let url = '';
  await withFetch((u) => { url = u; return makeOk([ATT_1])(u); }, () =>
    api.appointmentAttachments.list('appt-55'),
  );
  assert.ok(url.includes('/careconnect/api/appointments/appt-55/attachments'), `bad URL: ${url}`);
});

test('appointmentAttachments.upload: returns the created summary', async () => {
  const api = await getCareConnectApi();
  const { data } = await withFetch(makeOk(ATT_2, 201), () =>
    api.appointmentAttachments.upload('appt-55', new File(['x'], 'img.png', { type: 'image/png' })),
  );
  assert.equal(data.id, 'att-2');
});

test('appointmentAttachments.getSignedUrl: 403 → ApiError with isForbidden', async () => {
  const [api, ApiError] = await Promise.all([getCareConnectApi(), getApiError()]);
  await assert.rejects(
    () => withFetch(makeErr(403, { message: 'Access denied' }), () =>
      api.appointmentAttachments.getSignedUrl('appt-55', 'att-x'),
    ),
    (err: unknown) => { assert.ok(err instanceof ApiError); assert.ok(err.isForbidden); return true; },
  );
});

test('appointmentAttachments.getSignedUrl: 503 → ApiError with isServerError', async () => {
  const [api, ApiError] = await Promise.all([getCareConnectApi(), getApiError()]);
  await assert.rejects(
    () => withFetch(makeErr(503, { title: 'Service Unavailable' }), () =>
      api.appointmentAttachments.getSignedUrl('appt-55', 'att-2'),
    ),
    (err: unknown) => { assert.ok(err instanceof ApiError); assert.ok(err.isServerError); return true; },
  );
});
