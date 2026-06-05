import { query, queryOne, withTransaction } from './db';
import { requireTenantId }                  from './tenant-query';
import type { Document, CreateDocumentInput, UpdateDocumentInput } from '@/domain/entities/document';
import type { DocumentVersion, CreateVersionInput } from '@/domain/entities/document-version';
import { NotFoundError } from '@/shared/errors';
import { v4 as uuidv4 }  from 'uuid';

// ── Row → Entity mappers ───────────────────────────────────────────────────────

function rowToDocument(row: Record<string, unknown>): Document {
  return {
    id:               row['id'] as string,
    tenantId:         row['tenant_id'] as string,
    productId:        row['product_id'] as string,
    referenceId:      row['reference_id'] as string,
    referenceType:    row['reference_type'] as string,
    documentTypeId:   row['document_type_id'] as string,
    title:            row['title'] as string,
    description:      row['description'] as string | null,
    status:           row['status'] as Document['status'],
    storageKey:       row['storage_key'] as string,
    storageBucket:    row['storage_bucket'] as string,
    mimeType:         row['mime_type'] as string,
    fileSizeBytes:    Number(row['file_size_bytes']),
    checksum:         row['checksum'] as string,
    currentVersionId: row['current_version_id'] as string | null,
    versionCount:     Number(row['version_count']),
    scanStatus:       (row['scan_status'] as Document['scanStatus']) ?? 'PENDING',
    scanCompletedAt:  row['scan_completed_at'] ? new Date(row['scan_completed_at'] as string) : null,
    scanThreats:      (row['scan_threats'] as string[]) ?? [],
    isDeleted:        row['is_deleted'] as boolean,
    deletedAt:        row['deleted_at'] ? new Date(row['deleted_at'] as string) : null,
    deletedBy:        row['deleted_by'] as string | null,
    retainUntil:      row['retain_until'] ? new Date(row['retain_until'] as string) : null,
    legalHoldAt:      row['legal_hold_at'] ? new Date(row['legal_hold_at'] as string) : null,
    createdAt:        new Date(row['created_at'] as string),
    createdBy:        row['created_by'] as string,
    updatedAt:        new Date(row['updated_at'] as string),
    updatedBy:        row['updated_by'] as string,
  };
}

function rowToVersion(row: Record<string, unknown>): DocumentVersion {
  return {
    id:             row['id'] as string,
    documentId:     row['document_id'] as string,
    tenantId:       row['tenant_id'] as string,
    versionNumber:  Number(row['version_number']),
    storageKey:     row['storage_key'] as string,
    storageBucket:  row['storage_bucket'] as string,
    mimeType:       row['mime_type'] as string,
    fileSizeBytes:  Number(row['file_size_bytes']),
    checksum:       row['checksum'] as string,
    scanStatus:        (row['scan_status'] as DocumentVersion['scanStatus']) ?? 'PENDING',
    scanCompletedAt:   row['scan_completed_at'] ? new Date(row['scan_completed_at'] as string) : null,
    scanDurationMs:    row['scan_duration_ms'] != null ? Number(row['scan_duration_ms']) : null,
    scanThreats:       (row['scan_threats'] as string[]) ?? [],
    scanEngineVersion: row['scan_engine_version'] as string | null ?? null,
    uploadedAt:        new Date(row['uploaded_at'] as string),
    uploadedBy:        row['uploaded_by'] as string,
    label:             row['label'] as string | null,
    isDeleted:         row['is_deleted'] as boolean,
    deletedAt:         row['deleted_at'] ? new Date(row['deleted_at'] as string) : null,
    deletedBy:         row['deleted_by'] as string | null,
  };
}

// ── DocumentRepository ─────────────────────────────────────────────────────────

export const DocumentRepository = {

  /**
   * Find a document by ID scoped to a specific tenant.
   * Returns null if the document does not exist OR belongs to a different tenant.
   * Callers MUST NOT expose which condition matched — always surface as 404.
   */
  async findById(id: string, tenantId: string): Promise<Document | null> {
    requireTenantId(tenantId, 'DocumentRepository.findById');
    const row = await queryOne<Record<string, unknown>>(
      `SELECT * FROM documents WHERE id = $1 AND tenant_id = $2 AND is_deleted = FALSE`,
      [id, tenantId],
    );
    return row ? rowToDocument(row) : null;
  },

  /**
   * List documents for a tenant with optional filters.
   * tenantId is always the first WHERE predicate — it cannot be omitted.
   */
  async list(tenantId: string, opts: {
    productId?:     string;
    referenceId?:   string;
    referenceType?: string;
    status?:        string;
    limit?:         number;
    offset?:        number;
  }): Promise<{ documents: Document[]; total: number }> {
    requireTenantId(tenantId, 'DocumentRepository.list');

    const conditions: string[] = ['d.tenant_id = $1', 'd.is_deleted = FALSE'];
    const params: unknown[] = [tenantId];
    let pi = 2;

    if (opts.productId)     { conditions.push(`d.product_id = $${pi++}`);     params.push(opts.productId); }
    if (opts.referenceId)   { conditions.push(`d.reference_id = $${pi++}`);   params.push(opts.referenceId); }
    if (opts.referenceType) { conditions.push(`d.reference_type = $${pi++}`); params.push(opts.referenceType); }
    if (opts.status)        { conditions.push(`d.status = $${pi++}`);         params.push(opts.status); }

    const where  = conditions.join(' AND ');
    const limit  = opts.limit  ?? 50;
    const offset = opts.offset ?? 0;

    const [rows, countRows] = await Promise.all([
      query<Record<string, unknown>>(
        `SELECT d.* FROM documents d WHERE ${where} ORDER BY d.created_at DESC LIMIT $${pi} OFFSET $${pi + 1}`,
        [...params, limit, offset],
      ),
      query<{ count: string }>(
        `SELECT COUNT(*) as count FROM documents d WHERE ${where}`,
        params,
      ),
    ]);

    return {
      documents: rows.map(rowToDocument),
      total:     parseInt(countRows[0]?.['count'] ?? '0'),
    };
  },

  /**
   * Insert a new document row.
   * tenantId is taken from the input and embedded in every column — never inferred.
   */
  async create(input: CreateDocumentInput & {
    id?:         string;
    storageKey: string; storageBucket: string;
    mimeType: string; fileSizeBytes: number; checksum: string;
    scanStatus?: string; scanCompletedAt?: Date | null; scanThreats?: string[];
  }): Promise<Document> {
    requireTenantId(input.tenantId, 'DocumentRepository.create');

    const id = input.id ?? uuidv4();
    const row = await queryOne<Record<string, unknown>>(
      `INSERT INTO documents (
        id, tenant_id, product_id, reference_id, reference_type, document_type_id,
        title, description, status,
        storage_key, storage_bucket, mime_type, file_size_bytes, checksum,
        scan_status, scan_completed_at, scan_threats,
        created_by, updated_by
      ) VALUES (
        $1,$2,$3,$4,$5,$6,$7,$8,'DRAFT',$9,$10,$11,$12,$13,$14,$15,$16,$17,$17
      ) RETURNING *`,
      [
        id, input.tenantId, input.productId, input.referenceId, input.referenceType,
        input.documentTypeId, input.title, input.description ?? null,
        input.storageKey, input.storageBucket, input.mimeType, input.fileSizeBytes,
        input.checksum,
        input.scanStatus ?? 'PENDING',
        input.scanCompletedAt ?? null,
        input.scanThreats ?? [],
        input.uploadedBy,
      ],
    );
    return rowToDocument(row!);
  },

  /**
   * Update mutable metadata fields.
   * WHERE clause always includes both id AND tenant_id — a cross-tenant id
   * will simply match 0 rows and throw NotFoundError.
   */
  async update(id: string, tenantId: string, input: UpdateDocumentInput): Promise<Document> {
    requireTenantId(tenantId, 'DocumentRepository.update');

    const sets: string[] = ['updated_at = NOW()', `updated_by = $3`];
    const params: unknown[] = [id, tenantId, input.updatedBy];
    let pi = 4;

    if (input.title !== undefined)          { sets.push(`title = $${pi++}`);           params.push(input.title); }
    if (input.description !== undefined)    { sets.push(`description = $${pi++}`);     params.push(input.description); }
    if (input.documentTypeId !== undefined) { sets.push(`document_type_id = $${pi++}`); params.push(input.documentTypeId); }
    if (input.status !== undefined)         { sets.push(`status = $${pi++}`);          params.push(input.status); }
    if (input.retainUntil !== undefined)    { sets.push(`retain_until = $${pi++}`);    params.push(input.retainUntil); }

    const row = await queryOne<Record<string, unknown>>(
      `UPDATE documents SET ${sets.join(', ')}
         WHERE id = $1 AND tenant_id = $2 AND is_deleted = FALSE
       RETURNING *`,
      params,
    );
    if (!row) throw new NotFoundError('Document', id);
    return rowToDocument(row);
  },

  /**
   * Soft-delete: sets is_deleted=TRUE and status='DELETED'.
   * Tenant filter in WHERE means cross-tenant id → 0 rows affected (silent).
   * Callers must call findById first to verify existence before relying on this.
   */
  async softDelete(id: string, tenantId: string, deletedBy: string): Promise<void> {
    requireTenantId(tenantId, 'DocumentRepository.softDelete');
    await query(
      `UPDATE documents
          SET is_deleted = TRUE, deleted_at = NOW(), deleted_by = $3, status = 'DELETED'
        WHERE id = $1 AND tenant_id = $2 AND is_deleted = FALSE`,
      [id, tenantId, deletedBy],
    );
  },

  // ── Versions ─────────────────────────────────────────────────────────────────

  /**
   * Create a new version inside a serialisable transaction.
   *
   * Security note: BOTH the SELECT (for-update lock) AND the final UPDATE on
   * the documents table include tenant_id in their WHERE clause.
   * This prevents a race condition where a version could be attached to a
   * document owned by a different tenant.
   */
  async createVersion(input: CreateVersionInput): Promise<DocumentVersion> {
    requireTenantId(input.tenantId, 'DocumentRepository.createVersion');

    return withTransaction(async (client) => {
      // Lock the document row — tenant filter prevents cross-tenant lock
      const docResult = await client.query(
        `SELECT version_count FROM documents
           WHERE id = $1 AND tenant_id = $2 AND is_deleted = FALSE
           FOR UPDATE`,
        [input.documentId, input.tenantId],
      );
      if (docResult.rowCount === 0) throw new NotFoundError('Document', input.documentId);

      const versionNumber = Number(docResult.rows[0]['version_count']) + 1;
      const versionId     = uuidv4();

      const vResult = await client.query<Record<string, unknown>>(
        `INSERT INTO document_versions (
          id, document_id, tenant_id, version_number,
          storage_key, storage_bucket, mime_type, file_size_bytes, checksum,
          uploaded_by, label,
          scan_status, scan_completed_at, scan_duration_ms, scan_threats, scan_engine_version
        ) VALUES ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15,$16) RETURNING *`,
        [
          versionId, input.documentId, input.tenantId, versionNumber,
          input.storageKey, input.storageBucket, input.mimeType,
          input.fileSizeBytes, input.checksum, input.uploadedBy, input.label ?? null,
          input.scanStatus ?? 'PENDING',
          input.scanCompletedAt ?? null,
          input.scanDurationMs ?? null,
          input.scanThreats ?? [],
          input.scanEngineVersion ?? null,
        ],
      );

      // ── BUG FIX: tenant_id added to WHERE clause ─────────────────────────
      // Without `AND tenant_id = $10`, a rogue documentId could update any
      // document row across tenants if the ID collision ever occurred.
      await client.query(
        `UPDATE documents
            SET current_version_id = $1,
                version_count      = $2,
                storage_key        = $3,
                storage_bucket     = $4,
                mime_type          = $5,
                file_size_bytes    = $6,
                checksum           = $7,
                updated_at         = NOW(),
                updated_by         = $8
          WHERE id = $9 AND tenant_id = $10`,
        [
          versionId, versionNumber,
          input.storageKey, input.storageBucket, input.mimeType,
          input.fileSizeBytes, input.checksum, input.uploadedBy,
          input.documentId, input.tenantId,   // $9, $10 — both required
        ],
      );

      return rowToVersion(vResult.rows[0]);
    });
  },

  /**
   * List all non-deleted versions for a document, scoped to a tenant.
   * documentId + tenantId both required — cross-tenant documentId → empty array.
   */
  async listVersions(documentId: string, tenantId: string): Promise<DocumentVersion[]> {
    requireTenantId(tenantId, 'DocumentRepository.listVersions');
    const rows = await query<Record<string, unknown>>(
      `SELECT * FROM document_versions
         WHERE document_id = $1 AND tenant_id = $2 AND is_deleted = FALSE
         ORDER BY version_number DESC`,
      [documentId, tenantId],
    );
    return rows.map(rowToVersion);
  },

  // ── Scan status updates ────────────────────────────────────────────────────

  async updateDocumentScanStatus(
    id:       string,
    tenantId: string,
    scan: { scanStatus: string; scanCompletedAt: Date; scanThreats: string[] },
  ): Promise<void> {
    requireTenantId(tenantId, 'DocumentRepository.updateDocumentScanStatus');
    await query(
      `UPDATE documents
          SET scan_status = $3, scan_completed_at = $4, scan_threats = $5, updated_at = NOW()
        WHERE id = $1 AND tenant_id = $2`,
      [id, tenantId, scan.scanStatus, scan.scanCompletedAt, scan.scanThreats],
    );
  },

  async updateVersionScanStatus(
    versionId: string,
    tenantId:  string,
    scan: {
      scanStatus:        string;
      scanCompletedAt:   Date;
      scanDurationMs:    number;
      scanThreats:       string[];
      scanEngineVersion: string | null;
    },
  ): Promise<void> {
    requireTenantId(tenantId, 'DocumentRepository.updateVersionScanStatus');
    await query(
      `UPDATE document_versions
          SET scan_status = $3, scan_completed_at = $4,
              scan_duration_ms = $5, scan_threats = $6, scan_engine_version = $7
        WHERE id = $1 AND tenant_id = $2`,
      [
        versionId, tenantId,
        scan.scanStatus, scan.scanCompletedAt,
        scan.scanDurationMs, scan.scanThreats, scan.scanEngineVersion,
      ],
    );
  },
};
