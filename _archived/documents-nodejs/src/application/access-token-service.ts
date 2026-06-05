import crypto from 'crypto';
import { getAccessTokenStore }    from '@/infrastructure/access-token/access-token-store-factory';
import { DocumentRepository }     from '@/infrastructure/database/document-repository';
import { getStorageProvider }     from '@/infrastructure/storage/storage-factory';
import { auditService }           from './audit-service';
import { ScanService }            from './scan-service';
import { config }                 from '@/shared/config';
import { logger }                 from '@/shared/logger';
import { NotFoundError, TokenExpiredError, TokenInvalidError } from '@/shared/errors';
import { AuditEvent }             from '@/shared/constants';
import type { AccessToken, IssuedToken } from '@/domain/entities/access-token';
import type { AuthPrincipal }     from '@/domain/interfaces/auth-provider';

interface IssueContext {
  principal:     AuthPrincipal;
  correlationId: string;
  ipAddress?:    string;
  userAgent?:    string;
}

interface RedeemContext {
  correlationId: string;
  ipAddress?:    string;
  userAgent?:    string;
}

export interface RedeemedContent {
  /** Short-lived pre-signed URL (≤ 30s) for the actual file in storage */
  redirectUrl:      string;
  expiresInSeconds: number;
  mimeType:         string;
  type:             'view' | 'download';
}

/**
 * AccessTokenService — mediates all document file access.
 *
 * Issue flow (DIRECT_PRESIGN_ENABLED=false):
 *   1. Validate document exists and is accessible for this tenant
 *   2. Enforce scan gate (CLEAN / SKIPPED only)
 *   3. Generate opaque 32-byte hex token
 *   4. Store token with TTL in AccessTokenStore
 *   5. Return token + redeemUrl; client never receives storage key or bucket
 *
 * Redemption flow:
 *   1. Look up token in store → null if missing/expired
 *   2. Check expiry (double-check; store.get() also strips expired)
 *   3. If one-time-use: atomically markUsed() → reject if already used
 *   4. Re-fetch document and re-check scan status (defence in depth)
 *   5. Generate very short-lived (30s) pre-signed URL from storage
 *   6. Audit log DOCUMENT_ACCESSED
 *   7. Return redirect URL
 *
 * Authenticated direct access (GET /documents/:id/content):
 *   1. Verify principal.tenantId === document.tenantId
 *   2. Enforce RBAC (read permission) + scan gate
 *   3. Generate very short-lived (30s) pre-signed URL
 *   4. Audit log DOCUMENT_ACCESSED
 */
export const AccessTokenService = {
  // ── Issue ──────────────────────────────────────────────────────────────────

  async issue(
    documentId: string,
    type: 'view' | 'download',
    ctx: IssueContext,
  ): Promise<IssuedToken> {
    const doc = await DocumentRepository.findById(documentId, ctx.principal.tenantId);
    if (!doc) throw new NotFoundError('Document', documentId);

    // Scan gate — identical to the one in generateSignedUrl
    ScanService.enforceCleanScan(doc.scanStatus, {
      documentId,
      correlationId: ctx.correlationId,
    });

    const tokenString = crypto.randomBytes(32).toString('hex');
    const ttl         = config.ACCESS_TOKEN_TTL_SECONDS;
    const expiresAt   = new Date(Date.now() + ttl * 1000);

    const accessToken: AccessToken = {
      token:        tokenString,
      documentId,
      tenantId:     ctx.principal.tenantId,
      userId:       ctx.principal.userId,
      type,
      isOneTimeUse: config.ACCESS_TOKEN_ONE_TIME_USE,
      isUsed:       false,
      expiresAt,
      createdAt:    new Date(),
      issuedFromIp: ctx.ipAddress ?? null,
    };

    const store = getAccessTokenStore();
    await store.store(accessToken);

    logger.info(
      { documentId, type, tenantId: ctx.principal.tenantId, ttl },
      'Access token issued',
    );

    await auditService.log({
      tenantId:      ctx.principal.tenantId,
      documentId,
      event:         AuditEvent.ACCESS_TOKEN_ISSUED,
      actorId:       ctx.principal.userId,
      actorRoles:    ctx.principal.roles,
      correlationId: ctx.correlationId,
      ipAddress:     ctx.ipAddress,
      userAgent:     ctx.userAgent,
      outcome:       'SUCCESS',
      detail: {
        type,
        ttlSeconds:    ttl,
        isOneTimeUse:  config.ACCESS_TOKEN_ONE_TIME_USE,
        scanStatus:    doc.scanStatus,
      },
    });

    return {
      accessToken:      tokenString,
      redeemUrl:        `/access/${tokenString}`,
      expiresInSeconds: ttl,
      type,
    };
  },

  // ── Redeem (token-based, unauthenticated) ─────────────────────────────────

  async redeem(
    tokenString: string,
    ctx: RedeemContext,
  ): Promise<RedeemedContent> {
    const store  = getAccessTokenStore();
    const stored = await store.get(tokenString);

    // Token missing or already expired (store.get() strips expired tokens)
    if (!stored) {
      logger.warn({ correlationId: ctx.correlationId }, 'Token not found or expired');

      // We can't easily audit here without a tenantId, so log at application level only.
      throw new TokenExpiredError();
    }

    // Double-check expiry in case store.get() has clock skew
    if (stored.expiresAt <= new Date()) {
      await store.revoke(tokenString);
      throw new TokenExpiredError();
    }

    // One-time-use enforcement — atomic markUsed()
    if (stored.isOneTimeUse) {
      const marked = await store.markUsed(tokenString);
      if (!marked) {
        logger.warn(
          { documentId: stored.documentId, userId: stored.userId, correlationId: ctx.correlationId },
          'Token already used — possible replay attack',
        );
        await auditService.log({
          tenantId:      stored.tenantId,
          documentId:    stored.documentId,
          event:         AuditEvent.ACCESS_TOKEN_INVALID,
          actorId:       stored.userId,
          actorRoles:    [],
          correlationId: ctx.correlationId,
          ipAddress:     ctx.ipAddress,
          outcome:       'DENIED',
          detail:        { reason: 'token_already_used', type: stored.type },
        });
        throw new TokenInvalidError();
      }
    }

    // Re-fetch document (defence-in-depth: document may have been deleted/modified)
    const doc = await DocumentRepository.findById(stored.documentId, stored.tenantId);
    if (!doc) {
      throw new NotFoundError('Document', stored.documentId);
    }

    // Re-check scan status (document may have been re-scanned after token issuance)
    ScanService.enforceCleanScan(doc.scanStatus, {
      documentId: stored.documentId,
      correlationId: ctx.correlationId,
    });

    // Generate very short-lived redirect URL (30s — not the full TTL)
    const storage    = getStorageProvider();
    const redirectUrl = await storage.generateSignedUrl({
      bucket:           doc.storageBucket,
      key:              doc.storageKey,
      expiresInSeconds: 30,
      operation:        'GET',
    });

    await auditService.log({
      tenantId:      stored.tenantId,
      documentId:    stored.documentId,
      event:         AuditEvent.ACCESS_TOKEN_REDEEMED,
      actorId:       stored.userId,
      actorRoles:    [],
      correlationId: ctx.correlationId,
      ipAddress:     ctx.ipAddress,
      userAgent:     ctx.userAgent,
      outcome:       'SUCCESS',
      detail: {
        type:        stored.type,
        scanStatus:  doc.scanStatus,
        isOneTimeUse: stored.isOneTimeUse,
      },
    });

    logger.info(
      {
        documentId: stored.documentId,
        tenantId:   stored.tenantId,
        type:       stored.type,
        correlationId: ctx.correlationId,
      },
      'Access token redeemed',
    );

    return {
      redirectUrl,
      expiresInSeconds: 30,
      mimeType:         doc.mimeType,
      type:             stored.type,
    };
  },

  // ── Direct authenticated access (GET /documents/:id/content) ──────────────

  async accessDirect(
    documentId: string,
    type: 'view' | 'download',
    ctx: IssueContext,
  ): Promise<RedeemedContent> {
    const doc = await DocumentRepository.findById(documentId, ctx.principal.tenantId);
    if (!doc) throw new NotFoundError('Document', documentId);

    // Cross-tenant guard
    if (doc.tenantId !== ctx.principal.tenantId) {
      logger.warn(
        { documentId, principalTenantId: ctx.principal.tenantId, docTenantId: doc.tenantId },
        'Cross-tenant access attempt on direct access',
      );
      throw new NotFoundError('Document', documentId); // 404 to avoid tenantId disclosure
    }

    // Scan gate
    ScanService.enforceCleanScan(doc.scanStatus, {
      documentId,
      correlationId: ctx.correlationId,
    });

    const storage    = getStorageProvider();
    const redirectUrl = await storage.generateSignedUrl({
      bucket:           doc.storageBucket,
      key:              doc.storageKey,
      expiresInSeconds: 30,
      operation:        'GET',
    });

    await auditService.log({
      tenantId:      ctx.principal.tenantId,
      documentId,
      event:         AuditEvent.DOCUMENT_ACCESSED,
      actorId:       ctx.principal.userId,
      actorRoles:    ctx.principal.roles,
      correlationId: ctx.correlationId,
      ipAddress:     ctx.ipAddress,
      userAgent:     ctx.userAgent,
      outcome:       'SUCCESS',
      detail: {
        accessMode: 'direct',
        type,
        scanStatus: doc.scanStatus,
      },
    });

    return {
      redirectUrl,
      expiresInSeconds: 30,
      mimeType:         doc.mimeType,
      type,
    };
  },
};
