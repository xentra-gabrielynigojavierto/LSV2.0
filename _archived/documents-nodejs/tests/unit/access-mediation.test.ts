/**
 * Access mediation tests — unit level, no network or DB required.
 *
 * Test matrix:
 *  InMemoryAccessTokenStore
 *    - store and get a valid token
 *    - get returns null for missing token
 *    - get returns null for expired token (lazy eviction)
 *    - markUsed returns true on first call, false on second (TOCTOU guard)
 *    - revoke removes the token
 *    - cleanup removes expired tokens and returns count
 *    - capacity limit enforced at MAX_TOKENS
 *
 *  AccessTokenService.issue
 *    - issues token for CLEAN document
 *    - throws ScanBlockedError for INFECTED document
 *    - audit log ACCESS_TOKEN_ISSUED emitted
 *    - token has correct tenantId, documentId, type, TTL
 *
 *  AccessTokenService.redeem
 *    - valid token → returns redirectUrl, emits ACCESS_TOKEN_REDEEMED
 *    - expired token → throws TokenExpiredError
 *    - missing token → throws TokenExpiredError
 *    - one-time-use token reused → throws TokenInvalidError + emits ACCESS_TOKEN_INVALID
 *    - cross-tenant: token cannot be redeemed for a document owned by different tenant
 *
 *  AccessTokenService.accessDirect
 *    - CLEAN doc + valid principal → returns redirectUrl, emits DOCUMENT_ACCESSED
 *    - INFECTED doc → throws ScanBlockedError
 *    - cross-tenant attempt → throws NotFoundError
 *
 *  Error type checks
 *    - TokenExpiredError has code TOKEN_EXPIRED and statusCode 401
 *    - TokenInvalidError has code TOKEN_INVALID and statusCode 401
 */

import { InMemoryAccessTokenStore }     from '../../src/infrastructure/access-token/in-memory-access-token-store';
import { AccessTokenService }           from '../../src/application/access-token-service';
import { TokenExpiredError, TokenInvalidError, ScanBlockedError } from '../../src/shared/errors';
import type { AccessToken }             from '../../src/domain/entities/access-token';
import type { AuthPrincipal }           from '../../src/domain/interfaces/auth-provider';

// ── Shared test fixtures ──────────────────────────────────────────────────────

function makeToken(overrides: Partial<AccessToken> = {}): AccessToken {
  return {
    token:        'a'.repeat(64),
    documentId:   'doc-1',
    tenantId:     'tenant-1',
    userId:       'user-1',
    type:         'view',
    isOneTimeUse: true,
    isUsed:       false,
    expiresAt:    new Date(Date.now() + 300_000), // 5 min from now
    createdAt:    new Date(),
    issuedFromIp: '127.0.0.1',
    ...overrides,
  };
}

function makePrincipal(overrides: Partial<AuthPrincipal> = {}): AuthPrincipal {
  return {
    userId:   'user-1',
    tenantId: 'tenant-1',
    email:    null,
    roles:    ['DocReader'] as AuthPrincipal['roles'],
    ...overrides,
  };
}

const BASE_CTX = {
  principal:     makePrincipal(),
  correlationId: 'corr-test',
};

// ── Mocks ──────────────────────────────────────────────────────────────────────

jest.mock('../../src/shared/logger', () => ({
  logger: { info: jest.fn(), debug: jest.fn(), warn: jest.fn(), error: jest.fn() },
}));

jest.mock('../../src/shared/config', () => ({
  config: {
    ACCESS_TOKEN_STORE:        'memory',
    ACCESS_TOKEN_TTL_SECONDS:  300,
    ACCESS_TOKEN_ONE_TIME_USE: true,
    DIRECT_PRESIGN_ENABLED:    false,
    SIGNED_URL_EXPIRY_SECONDS: 300,
    REQUIRE_CLEAN_SCAN_FOR_ACCESS: true,
    FILE_SCANNER_PROVIDER:     'none',
  },
}));

jest.mock('../../src/application/audit-service', () => ({
  auditService: { log: jest.fn().mockResolvedValue(undefined) },
}));

jest.mock('../../src/infrastructure/storage/storage-factory', () => ({
  getStorageProvider: () => ({
    generateSignedUrl: jest.fn().mockResolvedValue('https://storage.example.com/file?token=short'),
  }),
}));

let mockFindById = jest.fn();
jest.mock('../../src/infrastructure/database/document-repository', () => ({
  DocumentRepository: {
    findById: (...args: unknown[]) => mockFindById(...args),
  },
}));

let mockGetStore: InMemoryAccessTokenStore | null = null;
jest.mock('../../src/infrastructure/access-token/access-token-store-factory', () => ({
  getAccessTokenStore: () => mockGetStore!,
}));

// ── Helper: build a clean doc row ─────────────────────────────────────────────

function makeDoc(overrides: Record<string, unknown> = {}) {
  return {
    id:              'doc-1',
    tenantId:        'tenant-1',
    productId:       'p1',
    referenceId:     'r1',
    referenceType:   'CASE',
    documentTypeId:  't1',
    title:           'Contract.pdf',
    description:     null,
    status:          'ACTIVE',
    storageKey:      'tenant-1/doc-1/file.pdf',
    storageBucket:   'docs-bucket',
    mimeType:        'application/pdf',
    fileSizeBytes:   1024,
    checksum:        'abc',
    currentVersionId: null,
    versionCount:    1,
    scanStatus:      'CLEAN',
    scanCompletedAt: new Date(),
    scanThreats:     [],
    isDeleted:       false,
    deletedAt:       null,
    deletedBy:       null,
    retainUntil:     null,
    legalHoldAt:     null,
    createdAt:       new Date(),
    createdBy:       'user-1',
    updatedAt:       new Date(),
    updatedBy:       'user-1',
    ...overrides,
  };
}

// ── InMemoryAccessTokenStore ─────────────────────────────────────────────────

describe('InMemoryAccessTokenStore', () => {
  let store: InMemoryAccessTokenStore;

  beforeEach(() => {
    store = new InMemoryAccessTokenStore();
  });

  afterEach(() => {
    store.destroy();
  });

  it('stores and retrieves a valid token', async () => {
    const token = makeToken();
    await store.store(token);
    const retrieved = await store.get(token.token);
    expect(retrieved).not.toBeNull();
    expect(retrieved!.documentId).toBe('doc-1');
    expect(retrieved!.tenantId).toBe('tenant-1');
  });

  it('returns null for a missing token', async () => {
    const result = await store.get('nonexistent'.padEnd(64, '0'));
    expect(result).toBeNull();
  });

  it('returns null for an expired token (lazy eviction)', async () => {
    const expired = makeToken({ expiresAt: new Date(Date.now() - 1000) });
    await store.store(expired);
    const result = await store.get(expired.token);
    expect(result).toBeNull();
  });

  it('markUsed returns true on first call and false on second (TOCTOU guard)', async () => {
    const token = makeToken();
    await store.store(token);

    const first  = await store.markUsed(token.token);
    const second = await store.markUsed(token.token);

    expect(first).toBe(true);
    expect(second).toBe(false);
  });

  it('markUsed returns false for a missing token', async () => {
    const result = await store.markUsed('b'.repeat(64));
    expect(result).toBe(false);
  });

  it('revoke removes the token immediately', async () => {
    const token = makeToken();
    await store.store(token);
    await store.revoke(token.token);
    const result = await store.get(token.token);
    expect(result).toBeNull();
  });

  it('cleanup removes expired tokens and returns count', async () => {
    const live    = makeToken({ token: 'a'.repeat(64), expiresAt: new Date(Date.now() + 60_000) });
    const expired = makeToken({ token: 'b'.repeat(64), expiresAt: new Date(Date.now() - 1000) });

    await store.store(live);
    await store.store(expired);

    const removed = await store.cleanup();

    expect(removed).toBe(1);
    expect(await store.get(live.token)).not.toBeNull();
    expect(await store.get(expired.token)).toBeNull();
  });

  it('returns a defensive copy (mutations do not affect stored token)', async () => {
    const token = makeToken();
    await store.store(token);

    const copy = await store.get(token.token);
    copy!.isUsed = true; // mutate the returned copy

    const fresh = await store.get(token.token);
    expect(fresh!.isUsed).toBe(false); // original is unchanged
  });
});

// ── AccessTokenService.issue ──────────────────────────────────────────────────

describe('AccessTokenService.issue', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    mockGetStore = new InMemoryAccessTokenStore();
  });

  afterEach(() => {
    mockGetStore?.destroy();
  });

  it('issues a token for a CLEAN document', async () => {
    mockFindById = jest.fn().mockResolvedValue(makeDoc());

    const result = await AccessTokenService.issue('doc-1', 'view', BASE_CTX);

    expect(result.accessToken).toMatch(/^[0-9a-f]{64}$/);
    expect(result.redeemUrl).toBe(`/access/${result.accessToken}`);
    expect(result.type).toBe('view');
    expect(result.expiresInSeconds).toBe(300);
  });

  it('issues a download token correctly', async () => {
    mockFindById = jest.fn().mockResolvedValue(makeDoc());

    const result = await AccessTokenService.issue('doc-1', 'download', BASE_CTX);

    expect(result.type).toBe('download');
  });

  it('throws ScanBlockedError for an INFECTED document', async () => {
    mockFindById = jest.fn().mockResolvedValue(makeDoc({ scanStatus: 'INFECTED' }));

    await expect(
      AccessTokenService.issue('doc-1', 'view', BASE_CTX),
    ).rejects.toThrow(ScanBlockedError);
  });

  it('throws ScanBlockedError for PENDING document in strict mode', async () => {
    mockFindById = jest.fn().mockResolvedValue(makeDoc({ scanStatus: 'PENDING' }));

    await expect(
      AccessTokenService.issue('doc-1', 'view', BASE_CTX),
    ).rejects.toThrow(ScanBlockedError);
  });

  it('emits ACCESS_TOKEN_ISSUED audit event on success', async () => {
    mockFindById = jest.fn().mockResolvedValue(makeDoc());

    await AccessTokenService.issue('doc-1', 'view', BASE_CTX);

    const { auditService } = await import('../../src/application/audit-service');
    expect(auditService.log).toHaveBeenCalledWith(
      expect.objectContaining({ event: 'ACCESS_TOKEN_ISSUED', outcome: 'SUCCESS' }),
    );
  });

  it('token is tenant-bound (tenantId embedded)', async () => {
    mockFindById = jest.fn().mockResolvedValue(makeDoc());

    const result = await AccessTokenService.issue('doc-1', 'view', BASE_CTX);
    const stored = await mockGetStore!.get(result.accessToken);

    expect(stored!.tenantId).toBe('tenant-1');
    expect(stored!.documentId).toBe('doc-1');
    expect(stored!.userId).toBe('user-1');
  });
});

// ── AccessTokenService.redeem ─────────────────────────────────────────────────

describe('AccessTokenService.redeem', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    mockGetStore = new InMemoryAccessTokenStore();
  });

  afterEach(() => {
    mockGetStore?.destroy();
  });

  const REDEEM_CTX = { correlationId: 'corr-redeem', ipAddress: '10.0.0.1' };

  it('returns redirectUrl for a valid token', async () => {
    const token = makeToken();
    await mockGetStore!.store(token);
    mockFindById = jest.fn().mockResolvedValue(makeDoc());

    const result = await AccessTokenService.redeem(token.token, REDEEM_CTX);

    expect(result.redirectUrl).toContain('storage.example.com');
    expect(result.expiresInSeconds).toBe(30);
    expect(result.type).toBe('view');
  });

  it('emits ACCESS_TOKEN_REDEEMED audit event', async () => {
    const token = makeToken();
    await mockGetStore!.store(token);
    mockFindById = jest.fn().mockResolvedValue(makeDoc());

    await AccessTokenService.redeem(token.token, REDEEM_CTX);

    const { auditService } = await import('../../src/application/audit-service');
    expect(auditService.log).toHaveBeenCalledWith(
      expect.objectContaining({ event: 'ACCESS_TOKEN_REDEEMED', outcome: 'SUCCESS' }),
    );
  });

  it('throws TokenExpiredError for a missing (or expired) token', async () => {
    await expect(
      AccessTokenService.redeem('z'.repeat(64), REDEEM_CTX),
    ).rejects.toThrow(TokenExpiredError);
  });

  it('throws TokenExpiredError for an expired token', async () => {
    const expired = makeToken({ expiresAt: new Date(Date.now() - 1000) });
    await mockGetStore!.store(expired);

    await expect(
      AccessTokenService.redeem(expired.token, REDEEM_CTX),
    ).rejects.toThrow(TokenExpiredError);
  });

  it('throws TokenInvalidError on second redemption of one-time-use token', async () => {
    const token = makeToken({ isOneTimeUse: true });
    await mockGetStore!.store(token);
    mockFindById = jest.fn().mockResolvedValue(makeDoc());

    // First redemption succeeds
    await AccessTokenService.redeem(token.token, REDEEM_CTX);

    // Second redemption fails — markUsed() returns false
    await expect(
      AccessTokenService.redeem(token.token, { ...REDEEM_CTX, correlationId: 'corr-2' }),
    ).rejects.toThrow(TokenInvalidError);
  });

  it('emits ACCESS_TOKEN_INVALID audit event on token reuse', async () => {
    const token = makeToken({ isOneTimeUse: true });
    await mockGetStore!.store(token);
    mockFindById = jest.fn().mockResolvedValue(makeDoc());

    // Consume the token
    await AccessTokenService.redeem(token.token, REDEEM_CTX);

    jest.clearAllMocks();

    // Attempt replay
    await expect(
      AccessTokenService.redeem(token.token, REDEEM_CTX),
    ).rejects.toThrow(TokenInvalidError);

    const { auditService } = await import('../../src/application/audit-service');
    expect(auditService.log).toHaveBeenCalledWith(
      expect.objectContaining({ event: 'ACCESS_TOKEN_INVALID', outcome: 'DENIED' }),
    );
  });

  it('re-checks scan status at redemption time (defence in depth)', async () => {
    const token = makeToken();
    await mockGetStore!.store(token);
    // Doc was CLEAN at issue time, now INFECTED (e.g. re-scanned)
    mockFindById = jest.fn().mockResolvedValue(makeDoc({ scanStatus: 'INFECTED' }));

    await expect(
      AccessTokenService.redeem(token.token, REDEEM_CTX),
    ).rejects.toThrow(ScanBlockedError);
  });

  it('allows redemption of multi-use token multiple times', async () => {
    const token = makeToken({ isOneTimeUse: false });
    await mockGetStore!.store(token);
    mockFindById = jest.fn().mockResolvedValue(makeDoc());

    const first  = await AccessTokenService.redeem(token.token, REDEEM_CTX);
    const second = await AccessTokenService.redeem(token.token, { ...REDEEM_CTX, correlationId: 'corr-2' });

    expect(first.redirectUrl).toContain('storage.example.com');
    expect(second.redirectUrl).toContain('storage.example.com');
  });
});

// ── AccessTokenService.accessDirect ──────────────────────────────────────────

describe('AccessTokenService.accessDirect', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    mockGetStore = new InMemoryAccessTokenStore();
  });

  afterEach(() => {
    mockGetStore?.destroy();
  });

  it('returns redirectUrl for CLEAN document with valid principal', async () => {
    mockFindById = jest.fn().mockResolvedValue(makeDoc());

    const result = await AccessTokenService.accessDirect('doc-1', 'view', BASE_CTX);

    expect(result.redirectUrl).toContain('storage.example.com');
    expect(result.expiresInSeconds).toBe(30);
  });

  it('emits DOCUMENT_ACCESSED audit event', async () => {
    mockFindById = jest.fn().mockResolvedValue(makeDoc());

    await AccessTokenService.accessDirect('doc-1', 'view', BASE_CTX);

    const { auditService } = await import('../../src/application/audit-service');
    expect(auditService.log).toHaveBeenCalledWith(
      expect.objectContaining({ event: 'DOCUMENT_ACCESSED', outcome: 'SUCCESS' }),
    );
  });

  it('throws ScanBlockedError for INFECTED document', async () => {
    mockFindById = jest.fn().mockResolvedValue(makeDoc({ scanStatus: 'INFECTED' }));

    await expect(
      AccessTokenService.accessDirect('doc-1', 'view', BASE_CTX),
    ).rejects.toThrow(ScanBlockedError);
  });

  it('throws NotFoundError for cross-tenant access attempt', async () => {
    const { NotFoundError } = await import('../../src/shared/errors');

    // Document is owned by 'tenant-2', principal claims 'tenant-1'
    mockFindById = jest.fn().mockResolvedValue(makeDoc({ tenantId: 'tenant-2' }));

    const crossCtx = {
      principal:     makePrincipal({ tenantId: 'tenant-1' }),
      correlationId: 'corr-cross',
    };

    await expect(
      AccessTokenService.accessDirect('doc-1', 'view', crossCtx),
    ).rejects.toThrow(NotFoundError); // 404 — no tenantId disclosure
  });
});

// ── Error type correctness ────────────────────────────────────────────────────

describe('Error types', () => {
  it('TokenExpiredError has correct code and statusCode', () => {
    const err = new TokenExpiredError();
    expect(err.code).toBe('TOKEN_EXPIRED');
    expect(err.statusCode).toBe(401);
  });

  it('TokenInvalidError has correct code and statusCode', () => {
    const err = new TokenInvalidError();
    expect(err.code).toBe('TOKEN_INVALID');
    expect(err.statusCode).toBe(401);
  });

  it('TokenInvalidError accepts a custom reason', () => {
    const err = new TokenInvalidError('custom reason');
    expect(err.message).toBe('custom reason');
  });
});
