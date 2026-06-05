import type { ScanResult }    from '@/domain/interfaces/file-scanner-provider';
import { getFileScannerProvider } from '@/infrastructure/scanner/scanner-factory';
import { DocumentRepository } from '@/infrastructure/database/document-repository';
import { auditService }       from './audit-service';
import { config }             from '@/shared/config';
import { logger }             from '@/shared/logger';
import { AuditEvent, ScanStatus } from '@/shared/constants';
import { ScanBlockedError, InfectedFileError } from '@/shared/errors';
import type { ScanStatusValue } from '@/shared/constants';

interface ScanContext {
  documentId:        string;
  documentVersionId?: string;
  tenantId:          string;
  actorId:           string;
  actorRoles:        string[];
  correlationId:     string;
  filename:          string;
}

/**
 * ScanService — orchestrates file scanning lifecycle.
 *
 * Synchronous implementation: scan runs inline during upload.
 *
 * Async extension path (future):
 *  1. Upload to quarantine bucket, set scanStatus=PENDING
 *  2. Enqueue scan job (SQS/Pub-Sub/BullMQ)
 *  3. Worker calls scanService.scanAndUpdateVersion(documentId, versionId, buffer)
 *  4. Move file to clean bucket or quarantine; update scanStatus
 *
 * Access gating rules (enforced via enforceCleanScan):
 *  - CLEAN   → always allowed
 *  - SKIPPED → always allowed (no scanner configured)
 *  - PENDING → blocked when REQUIRE_CLEAN_SCAN_FOR_ACCESS=true
 *  - FAILED  → blocked when REQUIRE_CLEAN_SCAN_FOR_ACCESS=true
 *  - INFECTED → ALWAYS blocked regardless of config
 */
export const ScanService = {
  /**
   * Scan a buffer and update the document's scan status atomically.
   * Called during initial document creation.
   */
  async scanDocument(
    buffer: Buffer,
    ctx: ScanContext,
  ): Promise<ScanResult> {
    const scanner = getFileScannerProvider();

    // Audit: scan requested
    await auditService.log({
      tenantId:      ctx.tenantId,
      documentId:    ctx.documentId,
      event:         AuditEvent.SCAN_REQUESTED,
      actorId:       ctx.actorId,
      actorRoles:    ctx.actorRoles,
      correlationId: ctx.correlationId,
      outcome:       'SUCCESS',
      detail:        { provider: scanner.providerName(), filename: ctx.filename },
    });

    const result = await scanner.scan(buffer, ctx.filename);

    logger.info(
      {
        documentId:    ctx.documentId,
        scanStatus:    result.status,
        scanDurationMs: result.scanDurationMs,
        provider:      scanner.providerName(),
      },
      'File scan completed',
    );

    // Audit the outcome
    await auditScanResult(result, ctx);

    // Update the document's scan status
    await DocumentRepository.updateDocumentScanStatus(ctx.documentId, ctx.tenantId, {
      scanStatus:      result.status,
      scanCompletedAt: result.scannedAt,
      scanThreats:     result.threats ?? [],
    });

    return result;
  },

  /**
   * Scan a buffer and update the version's scan status atomically.
   * Called during version upload.
   */
  async scanVersion(
    buffer: Buffer,
    ctx: ScanContext & { documentVersionId: string },
  ): Promise<ScanResult> {
    const scanner = getFileScannerProvider();

    await auditService.log({
      tenantId:          ctx.tenantId,
      documentId:        ctx.documentId,
      documentVersionId: ctx.documentVersionId,
      event:             AuditEvent.SCAN_REQUESTED,
      actorId:           ctx.actorId,
      actorRoles:        ctx.actorRoles,
      correlationId:     ctx.correlationId,
      outcome:           'SUCCESS',
      detail:            { provider: scanner.providerName(), filename: ctx.filename },
    });

    const result = await scanner.scan(buffer, ctx.filename);

    logger.info(
      {
        documentId:        ctx.documentId,
        documentVersionId: ctx.documentVersionId,
        scanStatus:        result.status,
        scanDurationMs:    result.scanDurationMs,
        provider:          scanner.providerName(),
      },
      'Version file scan completed',
    );

    await auditScanResult(result, ctx);

    // Update version scan status + mirror to document
    await DocumentRepository.updateVersionScanStatus(
      ctx.documentVersionId,
      ctx.tenantId,
      {
        scanStatus:        result.status,
        scanCompletedAt:   result.scannedAt,
        scanDurationMs:    result.scanDurationMs,
        scanThreats:       result.threats ?? [],
        scanEngineVersion: result.engineVersion ?? null,
      },
    );

    // Mirror current scan status to the parent document
    await DocumentRepository.updateDocumentScanStatus(ctx.documentId, ctx.tenantId, {
      scanStatus:      result.status,
      scanCompletedAt: result.scannedAt,
      scanThreats:     result.threats ?? [],
    });

    return result;
  },

  /**
   * Enforce scan gate before allowing file access.
   * Call this before generating any signed URL.
   *
   * Rules:
   *  CLEAN   → pass
   *  SKIPPED → pass (scanner not configured)
   *  INFECTED → ALWAYS reject (throw ScanBlockedError)
   *  PENDING  → reject if REQUIRE_CLEAN_SCAN_FOR_ACCESS
   *  FAILED   → reject if REQUIRE_CLEAN_SCAN_FOR_ACCESS
   */
  enforceCleanScan(
    scanStatus: ScanStatusValue,
    ctx: { documentId: string; correlationId: string },
  ): void {
    if (scanStatus === ScanStatus.CLEAN || scanStatus === ScanStatus.SKIPPED) {
      return;
    }

    const strictMode = config.REQUIRE_CLEAN_SCAN_FOR_ACCESS;

    if (scanStatus === ScanStatus.INFECTED) {
      logger.warn(
        { documentId: ctx.documentId, correlationId: ctx.correlationId, scanStatus },
        'Access denied — INFECTED file',
      );
      throw new ScanBlockedError(scanStatus);
    }

    if (strictMode && (scanStatus === ScanStatus.PENDING || scanStatus === ScanStatus.FAILED)) {
      logger.warn(
        { documentId: ctx.documentId, correlationId: ctx.correlationId, scanStatus, strictMode },
        'Access denied — scan incomplete',
      );
      throw new ScanBlockedError(scanStatus);
    }
  },

  /**
   * If a scan result shows INFECTED, throw immediately (reject upload).
   * Used inline during the upload flow to prevent storing infected files.
   */
  assertNotInfected(result: ScanResult): void {
    if (result.status === ScanStatus.INFECTED) {
      throw new InfectedFileError(result.threats ?? ['unknown threat']);
    }
  },
};

// ── Helpers ───────────────────────────────────────────────────────────────────

async function auditScanResult(result: ScanResult, ctx: ScanContext): Promise<void> {
  const event = result.status === ScanStatus.INFECTED
    ? AuditEvent.SCAN_INFECTED
    : result.status === ScanStatus.FAILED
      ? AuditEvent.SCAN_FAILED
      : AuditEvent.SCAN_COMPLETED;

  await auditService.log({
    tenantId:          ctx.tenantId,
    documentId:        ctx.documentId,
    documentVersionId: ctx.documentVersionId,
    event,
    actorId:           ctx.actorId,
    actorRoles:        ctx.actorRoles,
    correlationId:     ctx.correlationId,
    outcome:           result.status === ScanStatus.INFECTED ? 'DENIED' :
                       result.status === ScanStatus.FAILED   ? 'ERROR'  : 'SUCCESS',
    detail: {
      scanStatus:     result.status,
      scanDurationMs: result.scanDurationMs,
      engineVersion:  result.engineVersion,
      // NEVER log threat names that could contain PHI/PII
      threatCount:    result.threats?.length ?? 0,
    },
  });
}
