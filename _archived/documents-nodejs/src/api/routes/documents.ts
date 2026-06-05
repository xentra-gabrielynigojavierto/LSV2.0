import { Router, Request, Response } from 'express';
import { z }                     from 'zod';
import { requireAuth, getPrincipal } from '@/api/middleware/auth';
import { upload, validateFileContent } from '@/api/middleware/file-validator';
import { generalLimiter, uploadLimiter, signedUrlLimiter } from '@/api/middleware/rate-limiter';
import { DocumentService }       from '@/application/document-service';
import { AccessTokenService }    from '@/application/access-token-service';
import { assertPermission, assertTenantScope } from '@/application/rbac';
import { ValidationError }       from '@/shared/errors';

const router = Router();

// All document routes require authentication, then general rate limiting
router.use(requireAuth);
router.use(generalLimiter);

// ── Schema helpers ────────────────────────────────────────────────────────────
const CreateDocumentBody = z.object({
  tenantId:      z.string().uuid(),
  productId:     z.string().min(1),
  referenceId:   z.string().min(1),
  referenceType: z.string().min(1),
  documentTypeId: z.string().uuid(),
  title:         z.string().min(1).max(500),
  description:   z.string().max(2000).optional(),
});

const UpdateDocumentBody = z.object({
  title:          z.string().min(1).max(500).optional(),
  description:    z.string().max(2000).optional(),
  documentTypeId: z.string().uuid().optional(),
  status:         z.enum(['DRAFT', 'ACTIVE', 'ARCHIVED', 'LEGAL_HOLD']).optional(),
  retainUntil:    z.string().datetime().optional().transform((v) => v ? new Date(v) : undefined),
});

const ListQuery = z.object({
  productId:     z.string().optional(),
  referenceId:   z.string().optional(),
  referenceType: z.string().optional(),
  status:        z.string().optional(),
  limit:         z.coerce.number().int().min(1).max(200).default(50),
  offset:        z.coerce.number().int().min(0).default(0),
});

function ctx(req: Request) {
  return {
    principal:      getPrincipal(req),
    correlationId:  req.correlationId,
    ipAddress:      req.ip,
    userAgent:      req.headers['user-agent'],
    // PlatformAdmin explicit cross-tenant: header is extracted here but only
    // honoured if the principal has the PlatformAdmin role (enforced in tenant-guard.ts).
    // Non-admin callers supplying this header are silently ignored.
    targetTenantId: req.headers['x-admin-target-tenant'] as string | undefined,
  };
}

// ── POST /documents — uploadLimiter enforced before file processing ───────────
router.post('/', uploadLimiter, (req, res, next) => {
  upload(req, res, async (multerErr) => {
    if (multerErr) return next(multerErr);

    try {
      const principal = getPrincipal(req);
      const body      = CreateDocumentBody.parse(
        typeof req.body === 'string' ? JSON.parse(req.body) : req.body,
      );

      assertPermission(principal, 'write');
      assertTenantScope(principal, body.tenantId);

      if (!req.file) throw new ValidationError('File is required');

      await validateFileContent(req.file.buffer, req.file.mimetype);

      const doc = await DocumentService.create(
        { ...body, mimeType: req.file.mimetype },
        req.file.buffer,
        req.file.originalname,
        ctx(req),
      );

      res.status(201).json({ data: sanitizeDocument(doc) });
    } catch (err) {
      next(err);
    }
  });
});

// ── GET /documents ────────────────────────────────────────────────────────────
router.get('/', async (req: Request, res: Response, next) => {
  try {
    const principal = getPrincipal(req);
    assertPermission(principal, 'read');

    const query  = ListQuery.parse(req.query);
    const result = await DocumentService.list({ tenantId: principal.tenantId, ...query });

    res.json({
      data:   result.documents.map(sanitizeDocument),
      total:  result.total,
      limit:  query.limit,
      offset: query.offset,
    });
  } catch (err) {
    next(err);
  }
});

// ── GET /documents/:id ────────────────────────────────────────────────────────
router.get('/:id', async (req: Request, res: Response, next) => {
  try {
    const principal = getPrincipal(req);
    assertPermission(principal, 'read');

    const doc = await DocumentService.getById(req.params['id']!, ctx(req));

    res.json({ data: sanitizeDocument(doc) });
  } catch (err) {
    next(err);
  }
});

// ── PATCH /documents/:id ──────────────────────────────────────────────────────
router.patch('/:id', async (req: Request, res: Response, next) => {
  try {
    const principal = getPrincipal(req);
    assertPermission(principal, 'write');

    const body    = UpdateDocumentBody.parse(req.body);
    const updated = await DocumentService.update(req.params['id']!, body, ctx(req));

    res.json({ data: sanitizeDocument(updated) });
  } catch (err) {
    next(err);
  }
});

// ── DELETE /documents/:id ─────────────────────────────────────────────────────
router.delete('/:id', async (req: Request, res: Response, next) => {
  try {
    const principal = getPrincipal(req);
    assertPermission(principal, 'delete');

    await DocumentService.delete(req.params['id']!, ctx(req));

    res.status(204).send();
  } catch (err) {
    next(err);
  }
});

// ── POST /documents/:id/versions — uploadLimiter enforced before file processing
router.post('/:id/versions', uploadLimiter, (req, res, next) => {
  upload(req, res, async (multerErr) => {
    if (multerErr) return next(multerErr);

    try {
      const principal = getPrincipal(req);
      assertPermission(principal, 'write');

      if (!req.file) throw new ValidationError('File is required');

      await validateFileContent(req.file.buffer, req.file.mimetype);

      const label   = typeof req.body?.['label'] === 'string' ? req.body['label'] : undefined;
      const version = await DocumentService.uploadVersion(
        req.params['id']!,
        req.file.buffer,
        req.file.originalname,
        label,
        ctx(req),
      );

      res.status(201).json({ data: sanitizeVersion(version) });
    } catch (err) {
      next(err);
    }
  });
});

// ── GET /documents/:id/versions ───────────────────────────────────────────────
router.get('/:id/versions', async (req: Request, res: Response, next) => {
  try {
    const principal = getPrincipal(req);
    assertPermission(principal, 'read');

    const versions = await DocumentService.listVersions(req.params['id']!, ctx(req));

    res.json({ data: versions.map(sanitizeVersion) });
  } catch (err) {
    next(err);
  }
});

// ── POST /documents/:id/view-url ─────────────────────────────────────────────
// Returns an access token (DIRECT_PRESIGN_ENABLED=false, default) or a
// pre-signed URL (DIRECT_PRESIGN_ENABLED=true, legacy compat).
router.post('/:id/view-url', signedUrlLimiter, async (req: Request, res: Response, next) => {
  try {
    const principal = getPrincipal(req);
    assertPermission(principal, 'read');

    const result = await DocumentService.requestAccess(req.params['id']!, 'view', ctx(req));

    res.json({ data: result });
  } catch (err) {
    next(err);
  }
});

// ── POST /documents/:id/download-url ─────────────────────────────────────────
router.post('/:id/download-url', signedUrlLimiter, async (req: Request, res: Response, next) => {
  try {
    const principal = getPrincipal(req);
    assertPermission(principal, 'read');

    const result = await DocumentService.requestAccess(req.params['id']!, 'download', ctx(req));

    res.json({ data: result });
  } catch (err) {
    next(err);
  }
});

// ── GET /documents/:id/content ────────────────────────────────────────────────
// Authenticated direct access — RBAC validated, then 302 redirect to a
// 30-second storage URL. No token required; session JWT is the credential.
// Storage keys are never exposed.
router.get('/:id/content', async (req: Request, res: Response, next) => {
  try {
    const principal = getPrincipal(req);
    assertPermission(principal, 'read');

    const typeParam = req.query['type'];
    const type: 'view' | 'download' = typeParam === 'download' ? 'download' : 'view';

    const result = await AccessTokenService.accessDirect(req.params['id']!, type, ctx(req));

    // 302 redirect → short-lived storage URL
    res.redirect(302, result.redirectUrl);
  } catch (err) {
    next(err);
  }
});

// ── Sanitisers — strip internal storage fields from responses ─────────────────
// eslint-disable-next-line @typescript-eslint/no-explicit-any
function sanitizeDocument(doc: any) {
  const { storageKey: _sk, storageBucket: _sb, checksum: _cs, ...safe } = doc;
  return safe;
}

// eslint-disable-next-line @typescript-eslint/no-explicit-any
function sanitizeVersion(v: any) {
  const { storageKey: _sk, storageBucket: _sb, checksum: _cs, ...safe } = v;
  return safe;
}

export default router;
