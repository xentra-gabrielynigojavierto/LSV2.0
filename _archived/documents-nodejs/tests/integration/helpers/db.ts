/**
 * Database helpers for integration tests.
 *
 * Uses the raw `pg` Pool directly (bypasses app's singleton pool)
 * so each test file gets isolated DB connections.
 */

import { Pool } from 'pg';
import { v4 as uuidv4 } from 'uuid';
import { TENANT_A, TEST_DOC_TYPE_ID } from './token';

const DB_URL =
  process.env['DATABASE_URL'] ??
  'postgresql://postgres:password@helium/heliumdb?sslmode=disable';

let _pool: Pool | null = null;

export function getTestPool(): Pool {
  if (!_pool) {
    _pool = new Pool({ connectionString: DB_URL, max: 3 });
  }
  return _pool;
}

export async function closeTestPool(): Promise<void> {
  if (_pool) {
    await _pool.end();
    _pool = null;
  }
}

// ── Document seeding ──────────────────────────────────────────────────────────

export interface SeedDocOptions {
  tenantId?:       string;
  scanStatus?:     string;
  legalHoldAt?:    Date | null;
  isDeleted?:      boolean;
  status?:         string;
  productId?:      string;
}

/**
 * Insert a document row directly into the database (bypasses service layer).
 * Useful for seeding edge-case states (infected, on legal hold, deleted, etc.)
 * that are hard to produce through the API alone.
 */
export async function seedDocument(opts: SeedDocOptions = {}): Promise<string> {
  const pool      = getTestPool();
  const id        = uuidv4();
  const tenantId  = opts.tenantId    ?? TENANT_A;
  const scanSt    = opts.scanStatus  ?? 'SKIPPED';
  const status    = opts.status      ?? 'DRAFT';
  const productId = opts.productId   ?? 'int-test';
  const isDeleted = opts.isDeleted   ?? false;
  const actorId   = uuidv4();
  const storageKey = `${tenantId}/${id}/seed.txt`;

  await pool.query(
    `INSERT INTO documents (
       id, tenant_id, product_id, reference_id, reference_type,
       document_type_id, title,
       storage_key, storage_bucket, mime_type, file_size_bytes, checksum,
       scan_status, legal_hold_at,
       status, is_deleted, deleted_at,
       created_by, updated_by
     ) VALUES (
       $1,$2,$3,$4,'CONTRACT',
       $5,'Seeded Test Document',
       $6,'docs-local','text/plain',10,'abc123',
       $7,$8,
       $9,$10,$11,
       $12,$12
     )`,
    [
      id, tenantId, productId, uuidv4(),
      TEST_DOC_TYPE_ID,
      storageKey,
      scanSt,
      opts.legalHoldAt ?? null,
      status,
      isDeleted,
      isDeleted ? new Date() : null,
      actorId,
    ],
  );

  return id;
}

/**
 * Hard-delete all integration test documents.
 * Versions must be deleted before documents (FK constraint).
 */
export async function cleanTestDocuments(): Promise<void> {
  const pool = getTestPool();
  await pool.query(`
    DELETE FROM document_versions
    WHERE document_id IN (SELECT id FROM documents WHERE product_id = 'int-test')
  `);
  await pool.query("DELETE FROM documents WHERE product_id = 'int-test'");
}

/**
 * Fetch audit events for a document, newest first.
 */
export async function getAuditEvents(documentId: string): Promise<Array<{
  event: string;
  actor_id: string;
  outcome: string;
  occurred_at: Date;
  detail: Record<string, unknown>;
}>> {
  const pool = getTestPool();
  const r = await pool.query(
    `SELECT event, actor_id, outcome, occurred_at, detail
       FROM document_audits
      WHERE document_id = $1
      ORDER BY occurred_at DESC`,
    [documentId],
  );
  return r.rows as Array<{
    event: string;
    actor_id: string;
    outcome: string;
    occurred_at: Date;
    detail: Record<string, unknown>;
  }>;
}

/**
 * Update scan status directly — simulates a scanner completing asynchronously.
 */
export async function updateScanStatus(
  documentId: string,
  tenantId: string,
  scanStatus: string,
): Promise<void> {
  const pool = getTestPool();
  await pool.query(
    `UPDATE documents
        SET scan_status = $3, scan_completed_at = NOW()
      WHERE id = $1 AND tenant_id = $2`,
    [documentId, tenantId, scanStatus],
  );
}
