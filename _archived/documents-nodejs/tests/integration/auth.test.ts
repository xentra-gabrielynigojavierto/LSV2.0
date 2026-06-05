/**
 * Integration — Authentication
 *
 * Validates that every protected endpoint enforces Bearer JWT:
 *  - missing token  → 401
 *  - invalid format → 401
 *  - expired token  → 401
 *  - valid token    → request processed (may 404 on unknown ID, but NOT 401)
 */

import request from 'supertest';
import { createApp } from '../../src/app';
import { makeToken, makeExpiredToken, readerToken, TENANT_A, USER_READER_A } from './helpers/token';
import { cleanTestDocuments, closeTestPool } from './helpers/db';
import type { AuthPrincipal } from '../../src/domain/interfaces/auth-provider';
import { Role } from '../../src/shared/constants';

const app = createApp();

const READER_PRINCIPAL: AuthPrincipal = {
  userId:   USER_READER_A,
  tenantId: TENANT_A,
  email:    null,
  roles:    [Role.DOC_READER],
};

const PROTECTED_ENDPOINTS = [
  { method: 'get',    path: '/documents' },
  { method: 'get',    path: '/documents/00000000-0000-0000-0000-000000000001' },
  { method: 'delete', path: '/documents/00000000-0000-0000-0000-000000000001' },
  { method: 'patch',  path: '/documents/00000000-0000-0000-0000-000000000001' },
];

afterAll(async () => {
  await cleanTestDocuments();
  await closeTestPool();
});

// ── 1. Missing Bearer token ───────────────────────────────────────────────────

describe('Authentication — missing token', () => {
  test.each(PROTECTED_ENDPOINTS)(
    '$method $path → 401 when no Authorization header',
    async ({ method, path }) => {
      const r = await (request(app) as any)[method](path);
      expect(r.status).toBe(401);
      expect(r.body.error).toBe('AUTHENTICATION_REQUIRED');
    },
  );

  it('returns 401 with wrong scheme (Basic, not Bearer)', async () => {
    const r = await request(app)
      .get('/documents')
      .set('Authorization', 'Basic dXNlcjpwYXNz');
    expect(r.status).toBe(401);
    expect(r.body.error).toBe('AUTHENTICATION_REQUIRED');
  });

  it('returns a correlationId in the 401 body', async () => {
    const r = await request(app).get('/documents');
    expect(r.status).toBe(401);
    expect(typeof r.body.correlationId).toBe('string');
    expect(r.body.correlationId.length).toBeGreaterThan(0);
  });
});

// ── 2. Invalid token format ───────────────────────────────────────────────────

describe('Authentication — invalid token', () => {
  it('rejects a completely random string', async () => {
    const r = await request(app)
      .get('/documents')
      .set('Authorization', 'Bearer not-a-valid-jwt-at-all');
    expect(r.status).toBe(401);
  });

  it('rejects a token signed with the wrong secret', async () => {
    const wrongSecret = 'totally-wrong-secret';
    const { default: jwt } = await import('jsonwebtoken');
    const badToken = jwt.sign(
      { sub: USER_READER_A, tenantId: TENANT_A, roles: [Role.DOC_READER] },
      wrongSecret,
      { algorithm: 'HS256', expiresIn: '1h' },
    );
    const r = await request(app)
      .get('/documents')
      .set('Authorization', `Bearer ${badToken}`);
    expect(r.status).toBe(401);
  });

  it('rejects a token missing required claims (no sub/tenantId)', async () => {
    const { default: jwt } = await import('jsonwebtoken');
    const noClaimsToken = jwt.sign(
      { email: 'user@test.com' }, // no sub, no tenantId
      'integration-test-secret-do-not-use-in-prod',
      { algorithm: 'HS256', expiresIn: '1h' },
    );
    const r = await request(app)
      .get('/documents')
      .set('Authorization', `Bearer ${noClaimsToken}`);
    expect(r.status).toBe(401);
  });

  it('rejects an empty Bearer value', async () => {
    const r = await request(app)
      .get('/documents')
      .set('Authorization', 'Bearer ');
    expect(r.status).toBe(401);
  });
});

// ── 3. Expired token ──────────────────────────────────────────────────────────

describe('Authentication — expired token', () => {
  it('rejects an expired JWT', async () => {
    const expiredToken = makeExpiredToken(READER_PRINCIPAL);
    const r = await request(app)
      .get('/documents')
      .set('Authorization', `Bearer ${expiredToken}`);
    expect(r.status).toBe(401);
  });
});

// ── 4. Valid token — request passes auth gate ─────────────────────────────────

describe('Authentication — valid token', () => {
  it('passes auth and reaches the handler (GET /documents → 200)', async () => {
    const r = await request(app)
      .get('/documents')
      .set('Authorization', `Bearer ${readerToken()}`);
    // Successfully authenticated — 200 with empty data
    expect(r.status).toBe(200);
    expect(Array.isArray(r.body.data)).toBe(true);
  });

  it('passes auth and reaches the handler (GET /documents/:id → 404 not 401)', async () => {
    const r = await request(app)
      .get('/documents/00000000-0000-0000-0000-000000000099')
      .set('Authorization', `Bearer ${readerToken()}`);
    // Authenticated — resource not found, but NOT an auth failure
    expect(r.status).toBe(404);
    expect(r.body.error).not.toBe('AUTHENTICATION_REQUIRED');
  });

  it('X-Correlation-Id header is echoed in the response', async () => {
    const correlId = 'test-correlation-abc123';
    const r = await request(app)
      .get('/documents')
      .set('Authorization', `Bearer ${makeToken(READER_PRINCIPAL)}`)
      .set('X-Correlation-Id', correlId);
    expect(r.status).toBe(200);
    expect(r.headers['x-correlation-id']).toBe(correlId);
  });
});

// ── 5. Health endpoint is public ─────────────────────────────────────────────

describe('Authentication — public endpoints', () => {
  it('GET /health requires no token', async () => {
    const r = await request(app).get('/health');
    expect(r.status).toBe(200);
  });
});
