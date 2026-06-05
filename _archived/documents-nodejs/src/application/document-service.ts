import crypto from 'crypto';
import { DocumentRepository }          from '@/infrastructure/database/document-repository';
import { getStorageProvider }          from '@/infrastructure/storage/storage-factory';
import { auditService }                from './audit-service';
import { ScanService }                 from './scan-service';
import { AccessTokenService }          from './access-token-service';
import { assertDocumentTenantScope, resolveEffectiveTenantId } from './tenant-guard';
import { config }                      from '@/shared/config';
import { NotFoundError, ForbiddenError, ScanBlockedError } from '@/shared/errors';
import { AuditEvent }                  from '@/shared/constants';
import type { AuthPrincipal }          from '@/domain/interfaces/auth-provider';
import type { CreateDocumentInput, UpdateDocumentInput } from '@/domain/entities/document';
import type { IssuedToken, PresignedUrlResult } from '@/domain/entities/access-token';


/**
 * Request context threaded through every service call.
 *
 * targetTenantId:
 *   PlatformAdmin only — explicit cross-tenant access.
 *   Non-admin: silently ignored (resolveEffectiveTenantId enforces this).
 *   When supplied by a PlatformAdmin, all queries use this tenantId and
 *   an ADMIN_CROSS_TENANT_ACCESS audit event is emitted.
 */
interface RequestContext {
  principal:      AuthPrincipal;
  correlationId:  string;
  ipAddress?:     string;
  userAgent?:     string;
  targetTenantId?: string;  // PlatformAdmin cross-tenant only
}

function buildStorageKey(tenantId: string, documentId: string, filename: string): string {
  const ext = filename.split('.').pop() ?? 'bin';
  return `${tenantId}/${documentId}/${Date.now()}.${ext}`;
}

function sha256(buffer: Buffer): string {
  return crypto.createHash('sha256').update(buffer).digest('hex');
}

export const DocumentService = {

  // ── Create ─────────────────────────────────────────────────────────────────
  async create(
    input: Omit<CreateDocumentInput, 'uploadedBy'>,
    fileBuffer: Buffer,
    originalName: string,
    ctx: RequestContext,
  ) {
    // tenantId on the input is already validated against the principal at the
    // route layer via assertTenantScope(principal, body.tenantId).
    // requireTenantId() is called inside DocumentRepository.create() as a
    // second line of defence.
    const storage   = getStorageProvider();
    const bucket    = config.AWS_BUCKET_NAME ?? 'docs-local';
    const docId     = crypto.randomUUID();
    const key       = buildStorageKey(input.tenantId, docId, originalName);
    const checksum  = sha256(fileBuffer);

    // ── Phase 1: scan before upload ─────────────────────────────────────────
    const scanResult = await ScanService.scanDocument(fileBuffer, {
      documentId:    docId,
      tenantId:      input.tenantId,
      actorId:       ctx.principal.userId,
      actorRoles:    ctx.principal.roles,
      correlationId: ctx.correlationId,
      filename:      originalName,
    });

    ScanService.assertNotInfected(scanResult);

    // ── Phase 2: upload to storage ──────────────────────────────────────────
    await storage.upload({
      bucket,
      key,
      body:     fileBuffer,
      mimeType: input.mimeType ?? 'application/octet-stream',
    });

    // ── Phase 3: persist document with scan result ──────────────────────────
    // Pass the pre-generated docId so scan audit events and the document row
    // share the same document_id in document_audits.
    const doc = await DocumentRepository.create({
      ...input,
      id:              docId,
      uploadedBy:      ctx.principal.userId,
      storageKey:      key,
      storageBucket:   bucket,
      mimeType:        input.mimeType ?? 'application/octet-stream',
      fileSizeBytes:   fileBuffer.byteLength,
      checksum,
      scanStatus:      scanResult.status,
      scanCompletedAt: scanResult.scannedAt,
      scanThreats:     scanResult.threats ?? [],
    });

    await auditService.log({
      tenantId:      ctx.principal.tenantId,
      documentId:    doc.id,
      event:         AuditEvent.DOCUMENT_CREATED,
      actorId:       ctx.principal.userId,
      actorRoles:    ctx.principal.roles,
      correlationId: ctx.correlationId,
      ipAddress:     ctx.ipAddress,
      userAgent:     ctx.userAgent,
      outcome:       'SUCCESS',
      detail: {
        title:         doc.title,
        mimeType:      doc.mimeType,
        fileSizeBytes: doc.fileSizeBytes,
        scanStatus:    doc.scanStatus,
      },
    });

    return doc;
  },

  // ── List ───────────────────────────────────────────────────────────────────
  async list(opts: {
    tenantId:      string;
    productId?:    string;
    referenceId?:  string;
    referenceType?: string;
    status?:       string;
    limit?:        number;
    offset?:       number;
  }) {
    // tenantId comes from principal at the route layer — not from user input
    return DocumentRepository.list(opts.tenantId, opts);
  },

  // ── Get by ID ──────────────────────────────────────────────────────────────
  async getById(id: string, ctx: RequestContext) {
    const effectiveTenantId = resolveEffectiveTenantId(ctx.principal, ctx.targetTenantId);
    const doc = await DocumentRepository.findById(id, effectiveTenantId);
    if (!doc) throw new NotFoundError('Document', id);

    // Layer 2 ABAC — defence-in-depth; also handles admin cross-tenant audit
    await assertDocumentTenantScope(ctx.principal, doc, ctx);

    return doc;
  },

  // ── Update metadata ────────────────────────────────────────────────────────
  async update(id: string, input: Omit<UpdateDocumentInput, 'updatedBy'>, ctx: RequestContext) {
    const effectiveTenantId = resolveEffectiveTenantId(ctx.principal, ctx.targetTenantId);
    const doc = await DocumentRepository.findById(id, effectiveTenantId);
    if (!doc) throw new NotFoundError('Document', id);

    // Layer 2 ABAC
    await assertDocumentTenantScope(ctx.principal, doc, ctx);

    const updated = await DocumentRepository.update(id, effectiveTenantId, {
      ...input,
      updatedBy: ctx.principal.userId,
    });

    await auditService.log({
      tenantId:      ctx.principal.tenantId,
      documentId:    id,
      event:         input.status ? AuditEvent.DOCUMENT_STATUS_CHANGED : AuditEvent.DOCUMENT_UPDATED,
      actorId:       ctx.principal.userId,
      actorRoles:    ctx.principal.roles,
      correlationId: ctx.correlationId,
      outcome:       'SUCCESS',
      detail:        { changes: input },
    });

    return updated;
  },

  // ── Soft delete ────────────────────────────────────────────────────────────
  async delete(id: string, ctx: RequestContext) {
    const effectiveTenantId = resolveEffectiveTenantId(ctx.principal, ctx.targetTenantId);
    const doc = await DocumentRepository.findById(id, effectiveTenantId);
    if (!doc) throw new NotFoundError('Document', id);

    // Layer 2 ABAC
    await assertDocumentTenantScope(ctx.principal, doc, ctx);

    // Prevent deletion of documents on legal hold
    if (doc.legalHoldAt) {
      throw new ForbiddenError('Document is on legal hold and cannot be deleted');
    }

    await DocumentRepository.softDelete(id, effectiveTenantId, ctx.principal.userId);

    await auditService.log({
      tenantId:      ctx.principal.tenantId,
      documentId:    id,
      event:         AuditEvent.DOCUMENT_DELETED,
      actorId:       ctx.principal.userId,
      actorRoles:    ctx.principal.roles,
      correlationId: ctx.correlationId,
      outcome:       'SUCCESS',
      detail:        { title: doc.title },
    });
  },

  // ── Upload new version ─────────────────────────────────────────────────────
  async uploadVersion(
    documentId:  string,
    fileBuffer:  Buffer,
    originalName: string,
    label:       string | undefined,
    ctx:         RequestContext,
  ) {
    const effectiveTenantId = resolveEffectiveTenantId(ctx.principal, ctx.targetTenantId);
    const doc = await DocumentRepository.findById(documentId, effectiveTenantId);
    if (!doc) throw new NotFoundError('Document', documentId);

    // Layer 2 ABAC
    await assertDocumentTenantScope(ctx.principal, doc, ctx);

    const storage  = getStorageProvider();
    const bucket   = doc.storageBucket;
    const key      = buildStorageKey(effectiveTenantId, documentId, originalName);
    const checksum = sha256(fileBuffer);

    // ── Scan before upload ──────────────────────────────────────────────────
    const scanResult = await ScanService.scanDocument(fileBuffer, {
      documentId,
      tenantId:      effectiveTenantId,
      actorId:       ctx.principal.userId,
      actorRoles:    ctx.principal.roles,
      correlationId: ctx.correlationId,
      filename:      originalName,
    });

    ScanService.assertNotInfected(scanResult);

    await storage.upload({
      bucket,
      key,
      body:     fileBuffer,
      mimeType: doc.mimeType,
    });

    const version = await DocumentRepository.createVersion({
      documentId,
      tenantId:          effectiveTenantId,
      uploadedBy:        ctx.principal.userId,
      label,
      storageKey:        key,
      storageBucket:     bucket,
      mimeType:          doc.mimeType,
      fileSizeBytes:     fileBuffer.byteLength,
      checksum,
      scanStatus:        scanResult.status,
      scanCompletedAt:   scanResult.scannedAt,
      scanDurationMs:    scanResult.scanDurationMs,
      scanThreats:       scanResult.threats ?? [],
      scanEngineVersion: scanResult.engineVersion,
    });

    // Mirror scan status to parent document
    await DocumentRepository.updateDocumentScanStatus(documentId, effectiveTenantId, {
      scanStatus:      scanResult.status,
      scanCompletedAt: scanResult.scannedAt,
      scanThreats:     scanResult.threats ?? [],
    });

    await auditService.log({
      tenantId:          ctx.principal.tenantId,
      documentId,
      documentVersionId: version.id,
      event:             AuditEvent.VERSION_UPLOADED,
      actorId:           ctx.principal.userId,
      actorRoles:        ctx.principal.roles,
      correlationId:     ctx.correlationId,
      outcome:           'SUCCESS',
      detail: {
        versionNumber:  version.versionNumber,
        fileSizeBytes:  version.fileSizeBytes,
        scanStatus:     version.scanStatus,
      },
    });

    return version;
  },

  // ── Request access (replaces generateSignedUrl in new flow) ───────────────
  /**
   * Primary access entry point.
   *
   * When DIRECT_PRESIGN_ENABLED=false (default — secure mode):
   *   Issues a short-lived opaque access token.
   *   Client redeems it at GET /access/:token.
   *   Storage key is never exposed.
   *
   * When DIRECT_PRESIGN_ENABLED=true (legacy / compat mode):
   *   Generates a pre-signed storage URL directly (old behaviour).
   */
  async requestAccess(
    documentId: string,
    type: 'view' | 'download',
    ctx: RequestContext,
  ): Promise<IssuedToken | PresignedUrlResult> {
    if (config.DIRECT_PRESIGN_ENABLED) {
      return this.generateSignedUrl(documentId, type, ctx);
    }
    return AccessTokenService.issue(documentId, type, ctx);
  },

  // ── Signed URL (internal / legacy) ────────────────────────────────────────
  /**
   * Generates a direct pre-signed URL from storage.
   * Used internally by token redemption (short TTL = 30s) and
   * as a fallback when DIRECT_PRESIGN_ENABLED=true.
   *
   * NEVER call this from a route handler unless DIRECT_PRESIGN_ENABLED=true.
   */
  async generateSignedUrl(
    documentId: string,
    type: 'view' | 'download',
    ctx: RequestContext,
  ): Promise<PresignedUrlResult> {
    const effectiveTenantId = resolveEffectiveTenantId(ctx.principal, ctx.targetTenantId);
    const doc = await DocumentRepository.findById(documentId, effectiveTenantId);
    if (!doc) throw new NotFoundError('Document', documentId);

    // Layer 2 ABAC
    await assertDocumentTenantScope(ctx.principal, doc, ctx);

    // ── Scan gate ────────────────────────────────────────────────────────────
    try {
      ScanService.enforceCleanScan(doc.scanStatus, {
        documentId,
        correlationId: ctx.correlationId,
      });
    } catch (err) {
      if (err instanceof ScanBlockedError) {
        await auditService.log({
          tenantId:      ctx.principal.tenantId,
          documentId,
          event:         AuditEvent.SCAN_ACCESS_DENIED,
          actorId:       ctx.principal.userId,
          actorRoles:    ctx.principal.roles,
          correlationId: ctx.correlationId,
          outcome:       'DENIED',
          detail: {
            reason:     'scan_blocked',
            scanStatus: doc.scanStatus,
            urlType:    type,
          },
        });
      }
      throw err;
    }

    const storage    = getStorageProvider();
    const expiresIn  = config.SIGNED_URL_EXPIRY_SECONDS;

    const url = await storage.generateSignedUrl({
      bucket:           doc.storageBucket,
      key:              doc.storageKey,
      expiresInSeconds: expiresIn,
      operation:        'GET',
    });

    await auditService.log({
      tenantId:      ctx.principal.tenantId,
      documentId,
      event:         type === 'view' ? AuditEvent.VIEW_URL_GENERATED : AuditEvent.DOWNLOAD_URL_GENERATED,
      actorId:       ctx.principal.userId,
      actorRoles:    ctx.principal.roles,
      correlationId: ctx.correlationId,
      outcome:       'SUCCESS',
      detail:        { type, expiresInSeconds: expiresIn, scanStatus: doc.scanStatus },
    });

    return { url, expiresInSeconds: expiresIn };
  },

  // ── Versions list ──────────────────────────────────────────────────────────
  async listVersions(documentId: string, ctx: RequestContext) {
    const effectiveTenantId = resolveEffectiveTenantId(ctx.principal, ctx.targetTenantId);
    const doc = await DocumentRepository.findById(documentId, effectiveTenantId);
    if (!doc) throw new NotFoundError('Document', documentId);

    // Layer 2 ABAC
    await assertDocumentTenantScope(ctx.principal, doc, ctx);

    return DocumentRepository.listVersions(documentId, effectiveTenantId);
  },
};
