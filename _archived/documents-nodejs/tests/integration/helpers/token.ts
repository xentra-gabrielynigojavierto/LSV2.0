/**
 * JWT token helpers for integration tests.
 *
 * Auth provider is configured as 'jwt' with JWT_SECRET='integration-test-secret-do-not-use-in-prod'.
 * All tokens are HS256-signed — no external JWKS needed.
 */

import jwt from 'jsonwebtoken';
import type { AuthPrincipal } from '../../../src/domain/interfaces/auth-provider';
import { Role } from '../../../src/shared/constants';

// ── Fixtures ──────────────────────────────────────────────────────────────────

export const JWT_SECRET  = 'integration-test-secret-do-not-use-in-prod';
export const JWT_ALGO    = 'HS256' as const;

export const TENANT_A    = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';
export const TENANT_B    = 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb';

export const USER_READER_A    = 'aaaaaaaa-0000-0000-0000-000000000001';
export const USER_UPLOADER_A  = 'aaaaaaaa-0000-0000-0000-000000000002';
export const USER_MANAGER_A   = 'aaaaaaaa-0000-0000-0000-000000000003';
export const USER_ADMIN_A     = 'aaaaaaaa-0000-0000-0000-000000000004';
export const USER_PLATFORM_ADMIN = 'aaaaaaaa-0000-0000-0000-000000000005';

export const USER_READER_B    = 'bbbbbbbb-0000-0000-0000-000000000001';
export const USER_MANAGER_B   = 'bbbbbbbb-0000-0000-0000-000000000003';

export const TEST_DOC_TYPE_ID = '10000000-0000-0000-0000-000000000001';

// ── Token factory ─────────────────────────────────────────────────────────────

export function makeToken(
  principal: AuthPrincipal,
  opts: { expiresIn?: string | number } = {},
): string {
  const payload = {
    sub:      principal.userId,
    tenantId: principal.tenantId,
    roles:    principal.roles,
    email:    principal.email,
  };
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  return jwt.sign(payload, JWT_SECRET, {
    algorithm: JWT_ALGO,
    expiresIn: (opts.expiresIn ?? '1h') as any,
  });
}

export function makeExpiredToken(principal: AuthPrincipal): string {
  // expiresIn: 0 creates a token that is immediately expired
  const payload = {
    sub:      principal.userId,
    tenantId: principal.tenantId,
    roles:    principal.roles,
    email:    principal.email,
    exp:      Math.floor(Date.now() / 1000) - 60, // 60s in the past
  };
  return jwt.sign(payload, JWT_SECRET, { algorithm: JWT_ALGO });
}

// ── Role helpers ──────────────────────────────────────────────────────────────

export function readerToken(tenantId = TENANT_A, userId = USER_READER_A): string {
  return makeToken({ userId, tenantId, email: null, roles: [Role.DOC_READER] });
}

export function uploaderToken(tenantId = TENANT_A, userId = USER_UPLOADER_A): string {
  return makeToken({ userId, tenantId, email: null, roles: [Role.DOC_UPLOADER] });
}

export function managerToken(tenantId = TENANT_A, userId = USER_MANAGER_A): string {
  return makeToken({ userId, tenantId, email: null, roles: [Role.DOC_MANAGER] });
}

export function tenantAdminToken(tenantId = TENANT_A, userId = USER_ADMIN_A): string {
  return makeToken({ userId, tenantId, email: null, roles: [Role.TENANT_ADMIN] });
}

export function platformAdminToken(userId = USER_PLATFORM_ADMIN): string {
  return makeToken({
    userId,
    tenantId: TENANT_A,
    email:    null,
    roles:    [Role.PLATFORM_ADMIN],
  });
}

export function readerBToken(userId = USER_READER_B): string {
  return makeToken({ userId, tenantId: TENANT_B, email: null, roles: [Role.DOC_READER] });
}

export function managerBToken(userId = USER_MANAGER_B): string {
  return makeToken({ userId, tenantId: TENANT_B, email: null, roles: [Role.DOC_MANAGER] });
}
