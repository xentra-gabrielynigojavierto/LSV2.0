/**
 * Integration — Role-Based Access Control (RBAC)
 *
 * Validates the ROLE_PERMISSIONS matrix against live HTTP endpoints.
 * Uses a pre-seeded document to test read/write/delete actions.
 *
 * DocReader      → read only
 * DocUploader    → read + write (no delete)
 * DocManager     → read + write + delete
 * TenantAdmin    → read + write + delete
 * PlatformAdmin  → all
 */

import request  from 'supertest';
import { createApp } from '../../src/app';
import {
  readerToken, uploaderToken, managerToken, tenantAdminToken, platformAdminToken,
  TENANT_A, TEST_DOC_TYPE_ID,
} from './helpers/token';
import { seedDocument, cleanTestDocuments, closeTestPool } from './helpers/db';

const app = createApp();

// ── Minimal valid text file (text/plain is whitelisted; file-type → undefined → pass)
const TEXT_FILE = Buffer.from('LegalSynq integration test document content');
const TEXT_FILE_MIME = 'text/plain';

// ── Upload a document via API ─────────────────────────────────────────────────

async function uploadDoc(token: string): Promise<request.Response> {
  return request(app)
    .post('/documents')
    .set('Authorization', `Bearer ${token}`)
    .field('tenantId',      TENANT_A)
    .field('productId',     'int-test')
    .field('referenceId',   'ref-rbac-test')
    .field('referenceType', 'CONTRACT')
    .field('documentTypeId', TEST_DOC_TYPE_ID)
    .field('title',         'RBAC Test Document')
    .attach('file', TEXT_FILE, { filename: 'rbac.txt', contentType: TEXT_FILE_MIME });
}

let seededDocId: string;

beforeAll(async () => {
  seededDocId = await seedDocument({ tenantId: TENANT_A, productId: 'int-test' });
});

afterAll(async () => {
  await cleanTestDocuments();
  await closeTestPool();
});

// ── DocReader — read only ──────────────────────────────────────────────────────

describe('DocReader', () => {
  it('can GET /documents (list)', async () => {
    const r = await request(app)
      .get('/documents')
      .set('Authorization', `Bearer ${readerToken()}`);
    expect(r.status).toBe(200);
  });

  it('can GET /documents/:id', async () => {
    const r = await request(app)
      .get(`/documents/${seededDocId}`)
      .set('Authorization', `Bearer ${readerToken()}`);
    expect(r.status).toBe(200);
    expect(r.body.data.id).toBe(seededDocId);
  });

  it('CANNOT upload a document → 403', async () => {
    const r = await uploadDoc(readerToken());
    expect(r.status).toBe(403);
    expect(r.body.error).toBe('ACCESS_DENIED');
  });

  it('CANNOT delete a document → 403', async () => {
    const r = await request(app)
      .delete(`/documents/${seededDocId}`)
      .set('Authorization', `Bearer ${readerToken()}`);
    expect(r.status).toBe(403);
    expect(r.body.error).toBe('ACCESS_DENIED');
  });

  it('CANNOT PATCH a document → 403', async () => {
    const r = await request(app)
      .patch(`/documents/${seededDocId}`)
      .set('Authorization', `Bearer ${readerToken()}`)
      .send({ title: 'Hacked title' });
    expect(r.status).toBe(403);
  });
});

// ── DocUploader — read + write, no delete ─────────────────────────────────────

describe('DocUploader', () => {
  let uploadedDocId: string;

  it('can upload a document → 201', async () => {
    const r = await uploadDoc(uploaderToken());
    expect(r.status).toBe(201);
    uploadedDocId = r.body.data.id;
  });

  it('can GET /documents', async () => {
    const r = await request(app)
      .get('/documents')
      .set('Authorization', `Bearer ${uploaderToken()}`);
    expect(r.status).toBe(200);
  });

  it('CANNOT delete a document → 403', async () => {
    const docId = uploadedDocId ?? seededDocId;
    const r = await request(app)
      .delete(`/documents/${docId}`)
      .set('Authorization', `Bearer ${uploaderToken()}`);
    expect(r.status).toBe(403);
    expect(r.body.error).toBe('ACCESS_DENIED');
  });
});

// ── DocManager — full read/write/delete ───────────────────────────────────────

describe('DocManager', () => {
  let managedDocId: string;

  beforeAll(async () => {
    const r = await uploadDoc(managerToken());
    expect(r.status).toBe(201);
    managedDocId = r.body.data.id;
  });

  it('can GET /documents', async () => {
    const r = await request(app)
      .get('/documents')
      .set('Authorization', `Bearer ${managerToken()}`);
    expect(r.status).toBe(200);
  });

  it('can PATCH a document title', async () => {
    const r = await request(app)
      .patch(`/documents/${managedDocId}`)
      .set('Authorization', `Bearer ${managerToken()}`)
      .send({ title: 'Updated by Manager' });
    expect(r.status).toBe(200);
    expect(r.body.data.title).toBe('Updated by Manager');
  });

  it('can DELETE a document → 204', async () => {
    const r = await request(app)
      .delete(`/documents/${managedDocId}`)
      .set('Authorization', `Bearer ${managerToken()}`);
    expect(r.status).toBe(204);
  });

  it('deleted document returns 404', async () => {
    const r = await request(app)
      .get(`/documents/${managedDocId}`)
      .set('Authorization', `Bearer ${managerToken()}`);
    expect(r.status).toBe(404);
  });
});

// ── TenantAdmin — same as DocManager ─────────────────────────────────────────

describe('TenantAdmin', () => {
  it('can upload, read, and delete documents', async () => {
    const upload = await uploadDoc(tenantAdminToken());
    expect(upload.status).toBe(201);
    const docId = upload.body.data.id;

    const get = await request(app)
      .get(`/documents/${docId}`)
      .set('Authorization', `Bearer ${tenantAdminToken()}`);
    expect(get.status).toBe(200);

    const del = await request(app)
      .delete(`/documents/${docId}`)
      .set('Authorization', `Bearer ${tenantAdminToken()}`);
    expect(del.status).toBe(204);
  });
});

// ── PlatformAdmin — all permissions ──────────────────────────────────────────

describe('PlatformAdmin', () => {
  it('can upload documents in their own tenant', async () => {
    const r = await uploadDoc(platformAdminToken());
    expect(r.status).toBe(201);
  });

  it('can list, read, and delete documents', async () => {
    const upload = await uploadDoc(platformAdminToken());
    const docId  = upload.body.data.id;

    const list = await request(app)
      .get('/documents')
      .set('Authorization', `Bearer ${platformAdminToken()}`);
    expect(list.status).toBe(200);

    const del = await request(app)
      .delete(`/documents/${docId}`)
      .set('Authorization', `Bearer ${platformAdminToken()}`);
    expect(del.status).toBe(204);
  });
});

// ── Response structure invariants ─────────────────────────────────────────────

describe('Response structure', () => {
  it('successful list response has data/total/limit/offset', async () => {
    const r = await request(app)
      .get('/documents')
      .set('Authorization', `Bearer ${readerToken()}`);
    expect(r.status).toBe(200);
    expect(r.body).toHaveProperty('data');
    expect(r.body).toHaveProperty('total');
    expect(r.body).toHaveProperty('limit');
    expect(r.body).toHaveProperty('offset');
  });

  it('response does NOT contain storageKey or storageBucket', async () => {
    const r = await request(app)
      .get(`/documents/${seededDocId}`)
      .set('Authorization', `Bearer ${readerToken()}`);
    expect(r.status).toBe(200);
    expect(r.body.data.storageKey).toBeUndefined();
    expect(r.body.data.storageBucket).toBeUndefined();
    expect(r.body.data.checksum).toBeUndefined();
  });
});
