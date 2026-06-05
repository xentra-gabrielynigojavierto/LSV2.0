import { assertPermission, assertTenantScope } from '../../src/application/rbac';
import { ForbiddenError } from '../../src/shared/errors';
import { Role } from '../../src/shared/constants';
import type { AuthPrincipal } from '../../src/domain/interfaces/auth-provider';

const makePrincipal = (roles: string[], tenantId = 'tenant-a'): AuthPrincipal => ({
  userId:   'user-1',
  tenantId,
  email:    null,
  roles:    roles as AuthPrincipal['roles'],
});

describe('RBAC — assertPermission', () => {
  it('grants read to DocReader', () => {
    expect(() => assertPermission(makePrincipal([Role.DOC_READER]), 'read')).not.toThrow();
  });

  it('denies write to DocReader', () => {
    expect(() => assertPermission(makePrincipal([Role.DOC_READER]), 'write')).toThrow(ForbiddenError);
  });

  it('grants write to DocUploader', () => {
    expect(() => assertPermission(makePrincipal([Role.DOC_UPLOADER]), 'write')).not.toThrow();
  });

  it('denies delete to DocUploader', () => {
    expect(() => assertPermission(makePrincipal([Role.DOC_UPLOADER]), 'delete')).toThrow(ForbiddenError);
  });

  it('grants all permissions to PlatformAdmin', () => {
    const p = makePrincipal([Role.PLATFORM_ADMIN]);
    expect(() => assertPermission(p, 'read')).not.toThrow();
    expect(() => assertPermission(p, 'write')).not.toThrow();
    expect(() => assertPermission(p, 'delete')).not.toThrow();
    expect(() => assertPermission(p, 'admin')).not.toThrow();
  });

  it('denies all permissions when roles is empty (default deny)', () => {
    expect(() => assertPermission(makePrincipal([]), 'read')).toThrow(ForbiddenError);
  });
});

describe('ABAC — assertTenantScope', () => {
  it('allows access when tenantId matches', () => {
    expect(() =>
      assertTenantScope(makePrincipal([Role.DOC_READER], 'tenant-a'), 'tenant-a'),
    ).not.toThrow();
  });

  it('denies cross-tenant access for non-PlatformAdmin', () => {
    expect(() =>
      assertTenantScope(makePrincipal([Role.DOC_MANAGER], 'tenant-a'), 'tenant-b'),
    ).toThrow(ForbiddenError);
  });

  it('allows cross-tenant access for PlatformAdmin', () => {
    expect(() =>
      assertTenantScope(makePrincipal([Role.PLATFORM_ADMIN], 'tenant-a'), 'tenant-b'),
    ).not.toThrow();
  });
});
