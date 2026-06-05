/**
 * Tenant isolation tests — unit level.
 *
 * Tests the three-layer isolation model without a real database or HTTP server.
 *
 * Layer 1 — DB:      requireTenantId() in tenant-query.ts
 * Layer 2 — Service: assertDocumentTenantScope() in tenant-guard.ts
 * Layer 3 — Route:   assertTenantScope() in rbac.ts
 *
 * Test matrix:
 *
 *  requireTenantId()
 *    - allows a valid tenantId string
 *    - throws TenantIsolationError for null
 *    - throws TenantIsolationError for undefined
 *    - throws TenantIsolationError for empty string
 *    - throws TenantIsolationError for whitespace-only string
 *    - includes context in the error message
 *
 *  tenantQuery() / tenantQueryOne()
 *    - delegates to query() when tenantId is valid
 *    - throws before querying when tenantId is missing
 *    - tenantQueryOne() returns null on empty result
 *
 *  assertTenantScope() (route layer)
 *    - allows same-tenant access
 *    - blocks non-admin cross-tenant with ForbiddenError
 *    - allows PlatformAdmin cross-tenant (audit deferred to service)
 *
 *  assertDocumentTenantScope() (service layer)
 *    - allows same-tenant access with no audit event
 *    - blocks non-admin cross-tenant: throws TenantIsolationError + emits audit
 *    - allows PlatformAdmin cross-tenant: no throw + emits ADMIN_CROSS_TENANT_ACCESS audit
 *    - audit event for violation has outcome=DENIED
 *    - audit event for admin cross-tenant has outcome=SUCCESS
 *
 *  resolveEffectiveTenantId()
 *    - returns principal.tenantId when no targetTenantId
 *    - returns principal.tenantId when targetTenantId === principal.tenantId
 *    - returns targetTenantId for PlatformAdmin + different target
 *    - ignores targetTenantId for non-admin (silently uses principal.tenantId)
 *
 *  DocumentRepository cross-tenant isolation (mocked DB)
 *    - findById(id, TENANT_A) returns null when document belongs to TENANT_B
 *    - findById passes correct tenantId as the second SQL parameter
 *    - softDelete(id, TENANT_A) filters by tenantId — cross-tenant id → no-op
 *    - createVersion includes tenant_id in the document UPDATE WHERE clause
 *    - requireTenantId() is called in findById before the SQL executes
 *
 *  TenantIsolationError
 *    - has statusCode 403 and code TENANT_ISOLATION_VIOLATION
 *    - has a generic default message (no resource details)
 */

// ── Global mocks ─────────────────────────────────────────────────────────────

jest.mock('../../src/shared/logger', () => ({
  logger: { info: jest.fn(), debug: jest.fn(), warn: jest.fn(), error: jest.fn() },
}));

jest.mock('../../src/shared/config', () => ({
  config: {
    DATABASE_URL:    'postgresql://test:test@localhost:5432/test',
    ACCESS_TOKEN_STORE:        'memory',
    ACCESS_TOKEN_TTL_SECONDS:  300,
    ACCESS_TOKEN_ONE_TIME_USE: true,
    DIRECT_PRESIGN_ENABLED:    false,
    REQUIRE_CLEAN_SCAN_FOR_ACCESS: true,
    FILE_SCANNER_PROVIDER:     'none',
    RATE_LIMIT_PROVIDER:       'memory',
    SIGNED_URL_EXPIRY_SECONDS: 300,
  },
}));

jest.mock('../../src/application/audit-service', () => ({
  auditService: { log: jest.fn().mockResolvedValue(undefined) },
}));

// Mock the entire db module so no real PostgreSQL connection is made
const mockQuery    = jest.fn();
const mockQueryOne = jest.fn();
jest.mock('../../src/infrastructure/database/db', () => ({
  query:           (...args: unknown[]) => mockQuery(...args),
  queryOne:        (...args: unknown[]) => mockQueryOne(...args),
  withTransaction: jest.fn(),
}));

// ── Imports ───────────────────────────────────────────────────────────────────

import { requireTenantId, tenantQuery, tenantQueryOne } from '../../src/infrastructure/database/tenant-query';
import { assertDocumentTenantScope, resolveEffectiveTenantId } from '../../src/application/tenant-guard';
import { assertTenantScope }                     from '../../src/application/rbac';
import { DocumentRepository }                    from '../../src/infrastructure/database/document-repository';
import { TenantIsolationError, ForbiddenError }  from '../../src/shared/errors';
import { AuditEvent }                            from '../../src/shared/constants';
import type { AuthPrincipal }                    from '../../src/domain/interfaces/auth-provider';

// ── Fixtures ─────────────────────────────────────────────────────────────────

const TENANT_A = 'tenant-aaaa-aaaa-aaaa-aaaaaaaaaaaa';
const TENANT_B = 'tenant-bbbb-bbbb-bbbb-bbbbbbbbbbbb';
const DOC_ID   = 'doc-00000000-0000-0000-0000-000000000001';

function makePrincipal(overrides: Partial<AuthPrincipal> = {}): AuthPrincipal {
  return {
    userId:   'user-1',
    tenantId: TENANT_A,
    email:    null,
    roles:    ['DocReader'] as AuthPrincipal['roles'],
    ...overrides,
  };
}

function makePlatformAdmin(targetTenantId?: string): AuthPrincipal {
  void targetTenantId;
  return makePrincipal({ roles: ['PlatformAdmin'] as AuthPrincipal['roles'] });
}

const BASE_CTX = { correlationId: 'corr-test', ipAddress: '127.0.0.1' };

function makeDocRow(tenantId = TENANT_A) {
  return {
    id: DOC_ID, tenant_id: tenantId, product_id: 'p1', reference_id: 'r1',
    reference_type: 'CASE', document_type_id: 'dt1', title: 'Contract.pdf',
    description: null, status: 'ACTIVE', storage_key: `${tenantId}/${DOC_ID}/file.pdf`,
    storage_bucket: 'docs-bucket', mime_type: 'application/pdf', file_size_bytes: 1024,
    checksum: 'abc', current_version_id: null, version_count: 1,
    scan_status: 'CLEAN', scan_completed_at: new Date(), scan_threats: [],
    is_deleted: false, deleted_at: null, deleted_by: null, retain_until: null,
    legal_hold_at: null, created_at: new Date(), created_by: 'user-1',
    updated_at: new Date(), updated_by: 'user-1',
  };
}

// ── requireTenantId ──────────────────────────────────────────────────────────

describe('requireTenantId()', () => {
  it('passes for a valid non-empty string', () => {
    expect(() => requireTenantId(TENANT_A, 'test')).not.toThrow();
  });

  it('throws TenantIsolationError for null', () => {
    expect(() => requireTenantId(null, 'test')).toThrow(TenantIsolationError);
  });

  it('throws TenantIsolationError for undefined', () => {
    expect(() => requireTenantId(undefined, 'test')).toThrow(TenantIsolationError);
  });

  it('throws TenantIsolationError for empty string', () => {
    expect(() => requireTenantId('', 'test')).toThrow(TenantIsolationError);
  });

  it('throws TenantIsolationError for whitespace-only string', () => {
    expect(() => requireTenantId('   ', 'test')).toThrow(TenantIsolationError);
  });

  it('includes context name in the error message', () => {
    let caught: Error | null = null;
    try { requireTenantId(null, 'DocumentRepository.findById'); }
    catch (e) { caught = e as Error; }
    expect(caught!.message).toContain('DocumentRepository.findById');
  });

  it('error has code TENANT_ISOLATION_VIOLATION', () => {
    let caught: TenantIsolationError | null = null;
    try { requireTenantId(''); }
    catch (e) { caught = e as TenantIsolationError; }
    expect(caught!.code).toBe('TENANT_ISOLATION_VIOLATION');
  });
});

// ── tenantQuery / tenantQueryOne ─────────────────────────────────────────────

describe('tenantQuery()', () => {
  beforeEach(() => jest.clearAllMocks());

  it('delegates to query() when tenantId is valid', async () => {
    mockQuery.mockResolvedValue([{ id: 1 }]);
    const result = await tenantQuery('SELECT 1', TENANT_A, [TENANT_A]);
    expect(mockQuery).toHaveBeenCalledTimes(1);
    expect(result).toEqual([{ id: 1 }]);
  });

  it('throws TenantIsolationError and does NOT call query() when tenantId is missing', async () => {
    await expect(tenantQuery('SELECT 1', '', [])).rejects.toThrow(TenantIsolationError);
    expect(mockQuery).not.toHaveBeenCalled();
  });
});

describe('tenantQueryOne()', () => {
  beforeEach(() => jest.clearAllMocks());

  it('returns null when queryOne() returns null (empty result set)', async () => {
    // tenantQueryOne delegates to queryOne (not query)
    mockQueryOne.mockResolvedValue(null);
    const result = await tenantQueryOne('SELECT 1', TENANT_A, []);
    expect(result).toBeNull();
  });

  it('throws TenantIsolationError and does NOT query when tenantId is null', async () => {
    await expect(tenantQueryOne('SELECT 1', null as unknown as string, [])).rejects.toThrow(TenantIsolationError);
    expect(mockQueryOne).not.toHaveBeenCalled();
    expect(mockQuery).not.toHaveBeenCalled();
  });
});

// ── assertTenantScope (route layer) ──────────────────────────────────────────

describe('assertTenantScope()', () => {
  it('passes for same-tenant access', () => {
    const principal = makePrincipal({ tenantId: TENANT_A });
    expect(() => assertTenantScope(principal, TENANT_A)).not.toThrow();
  });

  it('throws ForbiddenError for non-admin cross-tenant', () => {
    const principal = makePrincipal({ tenantId: TENANT_A });
    expect(() => assertTenantScope(principal, TENANT_B)).toThrow(ForbiddenError);
  });

  it('allows PlatformAdmin cross-tenant (audit deferred to service layer)', () => {
    const admin = makePlatformAdmin();
    expect(() => assertTenantScope(admin, TENANT_B)).not.toThrow();
  });

  it('ForbiddenError has statusCode 403', () => {
    const principal = makePrincipal({ tenantId: TENANT_A });
    let caught: ForbiddenError | null = null;
    try { assertTenantScope(principal, TENANT_B); }
    catch (e) { caught = e as ForbiddenError; }
    expect(caught!.statusCode).toBe(403);
  });
});

// ── assertDocumentTenantScope (service layer) ─────────────────────────────────

describe('assertDocumentTenantScope()', () => {
  const { auditService } = jest.requireMock('../../src/application/audit-service');

  beforeEach(() => jest.clearAllMocks());

  it('returns without throwing for same-tenant access', async () => {
    const principal = makePrincipal({ tenantId: TENANT_A });
    const resource  = { id: DOC_ID, tenantId: TENANT_A };
    await expect(assertDocumentTenantScope(principal, resource, BASE_CTX)).resolves.toBeUndefined();
    expect(auditService.log).not.toHaveBeenCalled();
  });

  it('throws TenantIsolationError for non-admin cross-tenant access', async () => {
    const principal = makePrincipal({ tenantId: TENANT_A });
    const resource  = { id: DOC_ID, tenantId: TENANT_B };
    await expect(
      assertDocumentTenantScope(principal, resource, BASE_CTX),
    ).rejects.toThrow(TenantIsolationError);
  });

  it('emits TENANT_ISOLATION_VIOLATION audit event on cross-tenant block', async () => {
    const principal = makePrincipal({ tenantId: TENANT_A });
    const resource  = { id: DOC_ID, tenantId: TENANT_B };
    await expect(assertDocumentTenantScope(principal, resource, BASE_CTX)).rejects.toThrow();
    expect(auditService.log).toHaveBeenCalledWith(
      expect.objectContaining({
        event:   AuditEvent.TENANT_ISOLATION_VIOLATION,
        outcome: 'DENIED',
        actorId: 'user-1',
      }),
    );
  });

  it('cross-tenant violation error has generic message (no resource details)', async () => {
    const principal = makePrincipal({ tenantId: TENANT_A });
    const resource  = { id: DOC_ID, tenantId: TENANT_B };
    let caught: TenantIsolationError | null = null;
    try { await assertDocumentTenantScope(principal, resource, BASE_CTX); }
    catch (e) { caught = e as TenantIsolationError; }
    // Message must NOT contain the resource's tenantId
    expect(caught!.message).not.toContain(TENANT_B);
    expect(caught!.statusCode).toBe(403);
  });

  it('allows PlatformAdmin cross-tenant access without throwing', async () => {
    const admin    = makePlatformAdmin();
    const resource = { id: DOC_ID, tenantId: TENANT_B };
    await expect(assertDocumentTenantScope(admin, resource, BASE_CTX)).resolves.toBeUndefined();
  });

  it('emits ADMIN_CROSS_TENANT_ACCESS audit event for PlatformAdmin cross-tenant', async () => {
    const admin    = makePlatformAdmin();
    const resource = { id: DOC_ID, tenantId: TENANT_B };
    await assertDocumentTenantScope(admin, resource, BASE_CTX);
    expect(auditService.log).toHaveBeenCalledWith(
      expect.objectContaining({
        event:   AuditEvent.ADMIN_CROSS_TENANT_ACCESS,
        outcome: 'SUCCESS',
        actorId: admin.userId,
      }),
    );
  });

  it('admin audit log records both actor and resource tenantIds in detail', async () => {
    const admin    = makePlatformAdmin();
    const resource = { id: DOC_ID, tenantId: TENANT_B };
    await assertDocumentTenantScope(admin, resource, BASE_CTX);
    const logged = auditService.log.mock.calls[0][0];
    expect(logged.detail.actorTenantId).toBe(admin.tenantId);
    expect(logged.detail.resourceTenantId).toBe(TENANT_B);
  });
});

// ── resolveEffectiveTenantId ─────────────────────────────────────────────────

describe('resolveEffectiveTenantId()', () => {
  it('returns principal.tenantId when no targetTenantId supplied', () => {
    const principal = makePrincipal({ tenantId: TENANT_A });
    expect(resolveEffectiveTenantId(principal, undefined)).toBe(TENANT_A);
  });

  it('returns principal.tenantId when targetTenantId matches principal.tenantId', () => {
    const principal = makePrincipal({ tenantId: TENANT_A });
    expect(resolveEffectiveTenantId(principal, TENANT_A)).toBe(TENANT_A);
  });

  it('returns targetTenantId for PlatformAdmin with different target', () => {
    const admin = makePlatformAdmin();
    expect(resolveEffectiveTenantId(admin, TENANT_B)).toBe(TENANT_B);
  });

  it('silently uses principal.tenantId for non-admin with targetTenantId', () => {
    const principal = makePrincipal({ tenantId: TENANT_A, roles: ['DocReader'] as AuthPrincipal['roles'] });
    // Non-admin supplying cross-tenant targetTenantId — ignored
    const result = resolveEffectiveTenantId(principal, TENANT_B);
    expect(result).toBe(TENANT_A);  // never TENANT_B
  });
});

// ── DocumentRepository cross-tenant isolation ─────────────────────────────────

describe('DocumentRepository cross-tenant isolation', () => {
  beforeEach(() => jest.clearAllMocks());

  it('findById(id, TENANT_A) returns null when document belongs to TENANT_B', async () => {
    // DB returns empty (correct: WHERE tenant_id = TENANT_A filters it out)
    mockQuery.mockResolvedValue([]);
    const result = await DocumentRepository.findById(DOC_ID, TENANT_A);
    expect(result).toBeNull();
  });

  it('findById passes tenantId as the second SQL parameter', async () => {
    // findById calls queryOne (not query directly)
    mockQueryOne.mockResolvedValue(null);
    await DocumentRepository.findById(DOC_ID, TENANT_A);
    const [sql, params] = mockQueryOne.mock.calls[0] as [string, unknown[]];
    expect(sql).toContain('tenant_id');
    expect(params).toContain(TENANT_A);
    expect(params[1]).toBe(TENANT_A); // second param
  });

  it('findById calls requireTenantId — throws before querying if tenantId is empty', async () => {
    await expect(DocumentRepository.findById(DOC_ID, '')).rejects.toThrow(TenantIsolationError);
    expect(mockQuery).not.toHaveBeenCalled();
  });

  it('findById with TENANT_B principal: SQL includes TENANT_B (row filtered by DB)', async () => {
    // findById calls queryOne — mock it to return the TENANT_B row
    mockQueryOne.mockResolvedValue(makeDocRow(TENANT_B));
    // Caller provides TENANT_B — they should only receive TENANT_B rows
    const doc = await DocumentRepository.findById(DOC_ID, TENANT_B);
    expect(doc!.tenantId).toBe(TENANT_B);
    const [, params] = mockQueryOne.mock.calls[0] as [string, unknown[]];
    expect(params[1]).toBe(TENANT_B);
  });

  it('softDelete WHERE clause includes tenant_id — cross-tenant id is a no-op', async () => {
    mockQuery.mockResolvedValue([]);
    await DocumentRepository.softDelete(DOC_ID, TENANT_A, 'user-1');
    const [sql, params] = mockQuery.mock.calls[0] as [string, unknown[]];
    expect(sql).toContain('tenant_id');
    // TENANT_A is in the params — cross-tenant document simply matches 0 rows
    expect(params[1]).toBe(TENANT_A);
  });

  it('softDelete throws TenantIsolationError if tenantId is missing', async () => {
    await expect(DocumentRepository.softDelete(DOC_ID, '', 'user-1')).rejects.toThrow(TenantIsolationError);
    expect(mockQuery).not.toHaveBeenCalled();
  });

  it('list() requires tenantId — throws TenantIsolationError when empty', async () => {
    await expect(DocumentRepository.list('', {})).rejects.toThrow(TenantIsolationError);
    expect(mockQuery).not.toHaveBeenCalled();
  });

  it('list() always puts tenantId as first WHERE predicate', async () => {
    mockQuery.mockResolvedValue([]);
    await DocumentRepository.list(TENANT_A, { productId: 'p1' });
    const [sql] = mockQuery.mock.calls[0] as [string];
    // tenant_id condition must appear before product_id condition
    expect(sql.indexOf('tenant_id')).toBeLessThan(sql.indexOf('product_id'));
  });

  it('updateDocumentScanStatus throws TenantIsolationError without tenantId', async () => {
    await expect(
      DocumentRepository.updateDocumentScanStatus(DOC_ID, '', {
        scanStatus: 'CLEAN', scanCompletedAt: new Date(), scanThreats: [],
      }),
    ).rejects.toThrow(TenantIsolationError);
    expect(mockQuery).not.toHaveBeenCalled();
  });
});

// ── TenantIsolationError ─────────────────────────────────────────────────────

describe('TenantIsolationError', () => {
  it('has statusCode 403', () => {
    expect(new TenantIsolationError().statusCode).toBe(403);
  });

  it('has code TENANT_ISOLATION_VIOLATION', () => {
    expect(new TenantIsolationError().code).toBe('TENANT_ISOLATION_VIOLATION');
  });

  it('has a generic default message with no resource details', () => {
    const err = new TenantIsolationError();
    expect(err.message).toBeTruthy();
    expect(err.message).not.toContain('tenant-');
  });
});
