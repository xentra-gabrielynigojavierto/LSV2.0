/**
 * Integration — Upload Validation
 *
 * Tests the three-layer file validation chain:
 *  1. Multer fileFilter  — MIME type whitelisting at declaration
 *  2. Multer limits      — file size enforcement
 *  3. validateFileContent — magic-byte verification and empty-file check
 */

import request  from 'supertest';
import { createApp } from '../../src/app';
import {
  managerToken, TENANT_A, TEST_DOC_TYPE_ID,
} from './helpers/token';
import { cleanTestDocuments, closeTestPool } from './helpers/db';

const app = createApp();

const MANAGER = managerToken(TENANT_A);

// ── File fixtures ─────────────────────────────────────────────────────────────

// Valid: text/plain content — file-type cannot detect it → falls through to whitelist check
const VALID_TEXT    = Buffer.from('This is valid plain-text content for LegalSynq.');
const EMPTY_FILE    = Buffer.alloc(0);

// Oversized: 1 MB + 1 byte — exceeds MAX_FILE_SIZE_MB=1 (set in env.ts)
const OVERSIZED     = Buffer.alloc(1 * 1024 * 1024 + 1, 0x61); // 1 048 577 bytes of 'a'

// MIME mismatch: JPEG magic bytes declared as application/pdf
// file-type will detect image/jpeg; declared MIME is application/pdf → mismatch
const JPEG_MAGIC    = Buffer.from([
  0xFF, 0xD8, 0xFF, 0xE0,             // JPEG SOI + APP0 marker
  0x00, 0x10,                          // Length
  0x4A, 0x46, 0x49, 0x46, 0x00,       // "JFIF\0"
  0x01, 0x01,                          // Version 1.1
  0x00,                                // Aspect ratio units
  0x00, 0x01, 0x00, 0x01,             // Aspect ratio
  0x00, 0x00,                          // No thumbnail
  0xFF, 0xD9,                          // JPEG EOI
]);

// Helper: build a base upload request
function uploadRequest(
  token: string,
  fileBuffer: Buffer,
  filename: string,
  contentType: string,
) {
  return request(app)
    .post('/documents')
    .set('Authorization', `Bearer ${token}`)
    .field('tenantId',       TENANT_A)
    .field('productId',      'int-test')
    .field('referenceId',    `ref-upload-${Date.now()}`)
    .field('referenceType',  'CONTRACT')
    .field('documentTypeId', TEST_DOC_TYPE_ID)
    .field('title',          'Upload Validation Test')
    .attach('file', fileBuffer, { filename, contentType });
}

afterAll(async () => {
  await cleanTestDocuments();
  await closeTestPool();
});

// ── Valid upload ───────────────────────────────────────────────────────────────

describe('Valid upload', () => {
  it('text/plain file accepted → 201', async () => {
    const r = await uploadRequest(MANAGER, VALID_TEXT, 'test.txt', 'text/plain');
    expect(r.status).toBe(201);
    expect(r.body.data).toMatchObject({
      mimeType:     'text/plain',
      fileSizeBytes: VALID_TEXT.byteLength,
    });
  });

  it('response does not expose internal storageKey', async () => {
    const r = await uploadRequest(MANAGER, VALID_TEXT, 'clean.txt', 'text/plain');
    expect(r.status).toBe(201);
    expect(r.body.data.storageKey).toBeUndefined();
    expect(r.body.data.storageBucket).toBeUndefined();
  });

  it('response has scanStatus (SKIPPED — no scanner configured)', async () => {
    const r = await uploadRequest(MANAGER, VALID_TEXT, 'scan.txt', 'text/plain');
    expect(r.status).toBe(201);
    // FILE_SCANNER_PROVIDER=none → NullFileScannerProvider → 'SKIPPED'
    expect(r.body.data.scanStatus).toBe('SKIPPED');
  });
});

// ── Empty file ────────────────────────────────────────────────────────────────

describe('Empty file rejection', () => {
  it('zero-byte file → 400 FILE_VALIDATION_ERROR', async () => {
    const r = await uploadRequest(MANAGER, EMPTY_FILE, 'empty.txt', 'text/plain');
    expect(r.status).toBe(400);
    expect(r.body.error).toBe('FILE_VALIDATION_ERROR');
  });
});

// ── Oversized file ────────────────────────────────────────────────────────────

describe('Oversized file rejection', () => {
  it('file exceeding MAX_FILE_SIZE_MB → 413 FILE_TOO_LARGE', async () => {
    const r = await uploadRequest(MANAGER, OVERSIZED, 'big.txt', 'text/plain');
    expect(r.status).toBe(413);
    expect(r.body.error).toBe('FILE_TOO_LARGE');
  });
});

// ── MIME type not on whitelist ────────────────────────────────────────────────

describe('Disallowed MIME type rejection', () => {
  it('application/javascript → 422 (filtered by Multer fileFilter)', async () => {
    // Multer's fileFilter checks the declared MIME type against the whitelist.
    // application/javascript is not in ALLOWED_MIME_TYPES → 422.
    const r = await uploadRequest(
      MANAGER,
      Buffer.from('alert("xss")'),
      'malicious.js',
      'application/javascript',
    );
    // UnsupportedFileTypeError → 422
    expect(r.status).toBe(422);
    expect(r.body.error).toBe('UNSUPPORTED_FILE_TYPE');
  });

  it('text/html → 422 (filtered by Multer fileFilter)', async () => {
    const r = await uploadRequest(
      MANAGER,
      Buffer.from('<script>alert("xss")</script>'),
      'evil.html',
      'text/html',
    );
    expect(r.status).toBe(422);
  });

  it('application/octet-stream → 422', async () => {
    const r = await uploadRequest(
      MANAGER,
      Buffer.from('raw binary'),
      'file.bin',
      'application/octet-stream',
    );
    expect(r.status).toBe(422);
  });
});

// ── MIME mismatch (magic-byte check) ─────────────────────────────────────────

describe('MIME mismatch detection', () => {
  it('JPEG bytes declared as application/pdf → 400 FILE_VALIDATION_ERROR', async () => {
    // Multer accepts the file (pdf is whitelisted).
    // validateFileContent detects image/jpeg via magic bytes and sees mismatch.
    const r = await uploadRequest(
      MANAGER,
      JPEG_MAGIC,
      'fake.pdf',
      'application/pdf',
    );
    expect(r.status).toBe(400);
    expect(r.body.error).toBe('FILE_VALIDATION_ERROR');
  });
});

// ── Missing file ──────────────────────────────────────────────────────────────

describe('Missing file rejection', () => {
  it('POST /documents without file → 400 VALIDATION_ERROR', async () => {
    const r = await request(app)
      .post('/documents')
      .set('Authorization', `Bearer ${MANAGER}`)
      .send({
        tenantId:       TENANT_A,
        productId:      'int-test',
        referenceId:    'ref-no-file',
        referenceType:  'CONTRACT',
        documentTypeId: TEST_DOC_TYPE_ID,
        title:          'No file',
      });
    // JSON body — not multipart; Multer won't find a file → ValidationError
    expect(r.status).toBe(400);
  });
});

// ── Whitelisted text types accepted ──────────────────────────────────────────

describe('Whitelisted MIME types accepted', () => {
  const CASES: Array<[string, string]> = [
    ['text/plain',  'sample.txt'],
    ['text/csv',    'data.csv'],
  ];

  test.each(CASES)('MIME %s accepted', async (contentType, filename) => {
    const r = await uploadRequest(
      MANAGER,
      Buffer.from('content,data\n1,2'),
      filename,
      contentType,
    );
    expect(r.status).toBe(201);
  });
});
