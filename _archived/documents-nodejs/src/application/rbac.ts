import type { AuthPrincipal } from '@/domain/interfaces/auth-provider';
import { Role }               from '@/shared/constants';
import { ForbiddenError }     from '@/shared/errors';
import { logger }             from '@/shared/logger';

type Action = 'read' | 'write' | 'delete' | 'admin';

/**
 * RBAC + ABAC enforcement.
 *
 * Default-DENY model: permission must be explicitly granted.
 * Tenant isolation: tenantId on the principal is always matched against the resource.
 *
 * Three-layer isolation model:
 *  Layer 1 — Route:   assertTenantScope() — pre-flight request scope check
 *  Layer 2 — Service: assertDocumentTenantScope() in tenant-guard.ts — post-load ABAC
 *  Layer 3 — DB:      requireTenantId() + WHERE tenant_id = ? in every query
 */
const ROLE_PERMISSIONS: Record<string, Action[]> = {
  [Role.PLATFORM_ADMIN]: ['read', 'write', 'delete', 'admin'],
  [Role.TENANT_ADMIN]:   ['read', 'write', 'delete'],
  [Role.DOC_MANAGER]:    ['read', 'write', 'delete'],
  [Role.DOC_UPLOADER]:   ['read', 'write'],
  [Role.DOC_READER]:     ['read'],
};

export function assertPermission(principal: AuthPrincipal, action: Action): void {
  const allowed = principal.roles.some(
    (role) => ROLE_PERMISSIONS[role]?.includes(action),
  );

  if (!allowed) {
    throw new ForbiddenError(
      `Role(s) [${principal.roles.join(', ')}] do not have '${action}' permission`,
    );
  }
}

/**
 * Route-level tenant scope pre-flight check.
 *
 * Validates that the principal's tenantId matches the tenantId declared in
 * the request body/path. Blocks obviously mis-scoped requests before they
 * reach the service layer.
 *
 * PlatformAdmin is allowed to operate across tenants — the cross-tenant audit
 * log is emitted later in assertDocumentTenantScope() (tenant-guard.ts) once
 * a specific document has been loaded and its tenantId is known.
 *
 * @param principal        Verified JWT principal.
 * @param resourceTenantId TenantId from the request body (e.g. POST /documents).
 */
export function assertTenantScope(principal: AuthPrincipal, resourceTenantId: string): void {
  if (principal.tenantId === resourceTenantId) {
    return; // same-tenant — fast path
  }

  const isPlatformAdmin = principal.roles.includes(Role.PLATFORM_ADMIN as AuthPrincipal['roles'][number]);

  if (isPlatformAdmin) {
    // Cross-tenant is permitted for PlatformAdmin at the route layer.
    // The per-document audit event is emitted by assertDocumentTenantScope().
    logger.debug(
      { actorId: principal.userId, resourceTenantId },
      'PlatformAdmin cross-tenant route scope — will be audited at document load',
    );
    return;
  }

  throw new ForbiddenError('Cross-tenant access denied');
}
