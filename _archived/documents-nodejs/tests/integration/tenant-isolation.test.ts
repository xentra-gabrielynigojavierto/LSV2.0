/**
 * Integration — Tenant Isolation (CRITICAL)
 *
 * Validates application-layer multi-tenant data isolation.
 * MySQL has no row-level security; all isolation is enforced in code.
 * This suite is the primary security regression guard.
 *
 * Three-layer model under test:
 *  Layer 1 (Route)   — assertTenantScope() rejects cross-tenant create
 *  Layer 2 (Service) — assertDocumentTenantScope() ABAC post-load
 *  Layer 3 (DB)      — requireTenantId() + WHERE tenant_id = ?
 */

import request from 'supertest';
import { createApp } from '../../src/app';
import {
  managerToken, managerBToken, platformAdminToken, readerToken,
  TENANT_A, TENANT_B, USER_MANAGER_A, USER_MANAGER_B, TEST_DOC_TYPE_ID,
  USER_PLATFORM_ADMIN,
} from './helpers/token';
import {
  seedDocument, getAuditEvents, cleanTestDocuments, closeTestPool,
} from './helpers/db';

const app = createApp();

const TEXT_FILE = Buffer.from('Cross-tenant isolation test payload');

// ── Helper: upload a document as Tenant A manager ─────────────────────────────
async function uploadAsTenantA(): Promise<string> {
  const r = await request(app)
    .post('/documents')
    .set('Authorization', `Bearer ${managerToken(TENANT_A, USER_MANAGER_A)}`)
    .field('tenantId',       TENANT_A)
    .field('productId',      'int-test')
    .field('referenceId',    `ref-isolation-${Date.now()}`)
    .field('referenceType',  'CONTRACT')
    .field('documentTypeId', TEST_DOC_TYPE_ID)
    .field('title',          'Tenant A Isolation Test Doc')
    .attach('file', TEXT_FILE, { filename: 'isolation.txt', contentType: 'text/plain' });

  if (r.status !== 201) {
    throw new Error(`Upload failed: ${JSON.stringify(r.body)}`);
  }
  return r.body.data.id as string;
}

let tenantADocId: string;

beforeAll(async () => {
  tenantADocId = await uploadAsTenantA();
});

afterAll(async () => {
  await cleanTestDocuments();
  await closeTestPool();
});

// ── Layer 1: Route-level pre-flight — assertTenantScope() ─────────────────────

describe('Layer 1 — Route scope pre-flight (assertTenantScope)', () => {
  it('Tenant B manager CANNOT create a document with Tenant A tenantId → 403', async () => {
    const r = await request(app)
      .post('/documents')
      .set('Authorization', `Bearer ${managerBToken(USER_MANAGER_B)}`)
      .field('tenantId',       TENANT_A)  // ← cross-tenant claim
      .field('productId',      'int-test')
      .field('referenceId',    'ref-bad')
      .field('referenceType',  'CONTRACT')
      .field('documentTypeId', TEST_DOC_TYPE_ID)
      .field('title',          'Cross-tenant attempt')
      .attach('file', TEXT_FILE, { filename: 'bad.txt', contentType: 'text/plain' });

    expect(r.status).toBe(403);
    expect(r.body.error).toBe('ACCESS_DENIED');
  });

  it('Tenant A manager can create a document with their own tenantId → 201', async () => {
    const r = await request(app)
      .post('/documents')
      .set('Authorization', `Bearer ${managerToken(TENANT_A, USER_MANAGER_A)}`)
      .field('tenantId',       TENANT_A)  // ← same-tenant claim
      .field('productId',      'int-test')
      .field('referenceId',    `ref-same-${Date.now()}`)
      .field('referenceType',  'CONTRACT')
      .field('documentTypeId', TEST_DOC_TYPE_ID)
      .field('title',          'Same-tenant upload')
      .attach('file', TEXT_FILE, { filename: 'ok.txt', contentType: 'text/plain' });

    expect(r.status).toBe(201);
    expect(r.body.data.tenantId).toBe(TENANT_A);
  });
});

// ── Layer 2 & 3: Cross-tenant read blocked (404, never 200) ───────────────────

describe('Tenant B CANNOT read Tenant A documents', () => {
  it('GET /documents/:id returns 404 for another tenant\'s document', async () => {
    const r = await request(app)
      .get(`/documents/${tenantADocId}`)
      .set('Authorization', `Bearer ${managerBToken(USER_MANAGER_B)}`);

    // MUST be 404, never 200 — do not reveal the document exists
    expect(r.status).toBe(404);
    expect(r.body.error).toBe('NOT_FOUND');
  });

  it('DocReader from Tenant B also gets 404 for Tenant A document', async () => {
    const r = await request(app)
      .get(`/documents/${tenantADocId}`)
      .set('Authorization', `Bearer ${readerToken(TENANT_B)}`);

    expect(r.status).toBe(404);
  });

  it('GET /documents (list) does NOT return Tenant A documents to Tenant B', async () => {
    const r = await request(app)
      .get('/documents')
      .set('Authorization', `Bearer ${managerBToken(USER_MANAGER_B)}`);

    expect(r.status).toBe(200);
    const ids = (r.body.data as Array<{ id: string }>).map((d) => d.id);
    expect(ids).not.toContain(tenantADocId);
  });
});

// ── Cross-tenant delete blocked ───────────────────────────────────────────────

describe('Tenant B CANNOT delete Tenant A documents', () => {
  it('DELETE /documents/:id returns 404 for another tenant\'s document', async () => {
    const r = await request(app)
      .delete(`/documents/${tenantADocId}`)
      .set('Authorization', `Bearer ${managerBToken(USER_MANAGER_B)}`);

    // 404 not 403 — never disclose ownership
    expect(r.status).toBe(404);
  });

  it('document is NOT deleted after cross-tenant delete attempt', async () => {
    // Verify Tenant A can still retrieve it
    const r = await request(app)
      .get(`/documents/${tenantADocId}`)
      .set('Authorization', `Bearer ${managerToken(TENANT_A, USER_MANAGER_A)}`);

    expect(r.status).toBe(200);
    expect(r.body.data.id).toBe(tenantADocId);
    expect(r.body.data.isDeleted).toBe(false);
  });
});

// ── Cross-tenant update blocked ───────────────────────────────────────────────

describe('Tenant B CANNOT update Tenant A documents', () => {
  it('PATCH /documents/:id returns 404 for another tenant\'s document', async () => {
    const r = await request(app)
      .patch(`/documents/${tenantADocId}`)
      .set('Authorization', `Bearer ${managerBToken(USER_MANAGER_B)}`)
      .send({ title: 'Hijacked title' });

    expect(r.status).toBe(404);
  });

  it('document title is unchanged after cross-tenant update attempt', async () => {
    const r = await request(app)
      .get(`/documents/${tenantADocId}`)
      .set('Authorization', `Bearer ${managerToken(TENANT_A, USER_MANAGER_A)}`);

    expect(r.body.data.title).not.toBe('Hijacked title');
  });
});

// ── Access token / view-url: cross-tenant blocked ────────────────────────────

describe('Tenant B CANNOT request access token for Tenant A document', () => {
  it('POST /documents/:id/view-url returns 404 for cross-tenant', async () => {
    const r = await request(app)
      .post(`/documents/${tenantADocId}/view-url`)
      .set('Authorization', `Bearer ${managerBToken(USER_MANAGER_B)}`);

    expect(r.status).toBe(404);
  });
});

// ── Version upload: cross-tenant blocked ─────────────────────────────────────

describe('Tenant B CANNOT upload a version to Tenant A document', () => {
  it('POST /documents/:id/versions returns 404 for cross-tenant', async () => {
    const r = await request(app)
      .post(`/documents/${tenantADocId}/versions`)
      .set('Authorization', `Bearer ${managerBToken(USER_MANAGER_B)}`)
      .attach('file', TEXT_FILE, { filename: 'version.txt', contentType: 'text/plain' });

    expect(r.status).toBe(404);
  });
});

// ── DB-layer guard: requireTenantId prevents empty tenantId queries ───────────

describe('DB-layer guard — requireTenantId', () => {
  it('documents seeded for Tenant A are not visible in Tenant B list', async () => {
    // Seed extra docs directly in DB under TENANT_A
    const extraId = await seedDocument({ tenantId: TENANT_A, productId: 'int-test' });

    const rB = await request(app)
      .get('/documents')
      .set('Authorization', `Bearer ${managerBToken(USER_MANAGER_B)}`);

    expect(rB.status).toBe(200);
    const bIds = (rB.body.data as Array<{ id: string }>).map((d) => d.id);
    expect(bIds).not.toContain(extraId);

    // Verify TENANT_A can see it
    const rA = await request(app)
      .get(`/documents/${extraId}`)
      .set('Authorization', `Bearer ${managerToken(TENANT_A, USER_MANAGER_A)}`);
    expect(rA.status).toBe(200);
  });
});

// ── PlatformAdmin cross-tenant access ────────────────────────────────────────

describe('PlatformAdmin explicit cross-tenant access', () => {
  // PlatformAdmin lives in TENANT_A; we need a TENANT_B doc for true cross-tenant
  let tenantBDocForAdmin: string;

  beforeAll(async () => {
    tenantBDocForAdmin = await seedDocument({ tenantId: TENANT_B, productId: 'int-test' });
  });

  it('admin WITH X-Admin-Target-Tenant header can read their own tenant A document', async () => {
    const r = await request(app)
      .get(`/documents/${tenantADocId}`)
      .set('Authorization', `Bearer ${platformAdminToken(USER_PLATFORM_ADMIN)}`);

    // No X-Admin-Target-Tenant needed — admin is in TENANT_A, doc is in TENANT_A
    expect(r.status).toBe(200);
    expect(r.body.data.id).toBe(tenantADocId);
  });

  it('admin reads TENANT_B doc with X-Admin-Target-Tenant: TENANT_B → 200', async () => {
    const r = await request(app)
      .get(`/documents/${tenantBDocForAdmin}`)
      .set('Authorization', `Bearer ${platformAdminToken(USER_PLATFORM_ADMIN)}`)
      .set('X-Admin-Target-Tenant', TENANT_B);  // true cross-tenant

    expect(r.status).toBe(200);
    expect(r.body.data.id).toBe(tenantBDocForAdmin);
  });

  it('admin cross-tenant access emits ADMIN_CROSS_TENANT_ACCESS audit event', async () => {
    // Trigger TRUE cross-tenant access: admin (TENANT_A) → doc (TENANT_B)
    await request(app)
      .get(`/documents/${tenantBDocForAdmin}`)
      .set('Authorization', `Bearer ${platformAdminToken(USER_PLATFORM_ADMIN)}`)
      .set('X-Admin-Target-Tenant', TENANT_B);

    const events = await getAuditEvents(tenantBDocForAdmin);
    const crossTenantEvents = events.filter(
      (e) => e.event === 'ADMIN_CROSS_TENANT_ACCESS',
    );

    // At least one ADMIN_CROSS_TENANT_ACCESS event must be present
    expect(crossTenantEvents.length).toBeGreaterThan(0);
    expect(crossTenantEvents[0]!.actor_id).toBe(USER_PLATFORM_ADMIN);
    expect(crossTenantEvents[0]!.outcome).toBe('SUCCESS');
  });

  it('non-admin ignoring X-Admin-Target-Tenant still sees only their own tenant', async () => {
    // Tenant B manager sends the header but is not PlatformAdmin — silently ignored
    const r = await request(app)
      .get(`/documents/${tenantADocId}`)
      .set('Authorization', `Bearer ${managerBToken(USER_MANAGER_B)}`)
      .set('X-Admin-Target-Tenant', TENANT_A);  // should be ignored

    // Must be 404 — header has no effect for non-admin
    expect(r.status).toBe(404);
  });

  it('error message does NOT reveal which tenant owns the resource', async () => {
    const r = await request(app)
      .get(`/documents/${tenantADocId}`)
      .set('Authorization', `Bearer ${managerBToken(USER_MANAGER_B)}`);

    expect(r.status).toBe(404);
    const bodyStr = JSON.stringify(r.body).toLowerCase();
    // Must not mention TENANT_A UUID in the response
    expect(bodyStr).not.toContain(TENANT_A.toLowerCase());
    expect(bodyStr).not.toContain(TENANT_B.toLowerCase());
  });
});

// ── Tenant A cannot see Tenant B documents (symmetry check) ──────────────────

describe('Tenant A CANNOT read Tenant B documents (symmetry)', () => {
  let tenantBDocId: string;

  beforeAll(async () => {
    tenantBDocId = await seedDocument({ tenantId: TENANT_B, productId: 'int-test' });
  });

  it('Tenant A GET /documents/:tenantBDocId → 404', async () => {
    const r = await request(app)
      .get(`/documents/${tenantBDocId}`)
      .set('Authorization', `Bearer ${managerToken(TENANT_A, USER_MANAGER_A)}`);

    expect(r.status).toBe(404);
  });

  it('Tenant A list does NOT include Tenant B documents', async () => {
    const r = await request(app)
      .get('/documents')
      .set('Authorization', `Bearer ${managerToken(TENANT_A, USER_MANAGER_A)}`);

    expect(r.status).toBe(200);
    const ids = (r.body.data as Array<{ id: string }>).map((d) => d.id);
    expect(ids).not.toContain(tenantBDocId);
  });
});
