/**
 * Service-layer tenant isolation guard (ABAC).
 *
 * This is Layer 2 of the three-layer isolation strategy:
 *
 *   Layer 1 (DB)     : requireTenantId() + WHERE tenant_id = ?  (data never returned cross-tenant)
 *   Layer 2 (Service): assertDocumentTenantScope()              (explicit ABAC verification)
 *   Layer 3 (Route)  : assertTenantScope()                      (request-scoped pre-flight)
 *
 * PURPOSE:
 *   Even when the repository correctly filters by tenant_id, a future code
 *   change could introduce a code path that bypasses the tenant parameter.
 *   The service-layer assertion provides defence-in-depth: if a document is
 *   somehow returned for the wrong tenant, we block access before any data
 *   is returned and emit a high-severity audit event.
 *
 * ADMIN CROSS-TENANT:
 *   PlatformAdmin may supply a targetTenantId in the request context to
 *   explicitly access a different tenant's documents.  This path is:
 *     - Only available to PlatformAdmin (checked here, not trusted from caller)
 *     - Always audited with ADMIN_CROSS_TENANT_ACCESS event
 *     - Never the default code path
 *
 * SECURITY INVARIANT:
 *   A non-admin principal MUST NEVER receive data owned by a different tenant,
 *   regardless of which layer's check fires last.
 */

import type { AuthPrincipal }  from '@/domain/interfaces/auth-provider';
import { auditService }        from './audit-service';
import { Role, AuditEvent }    from '@/shared/constants';
import { TenantIsolationError } from '@/shared/errors';
import { logger }              from '@/shared/logger';

interface TenantGuardContext {
  correlationId: string;
  ipAddress?:    string;
  userAgent?:    string;
}

/**
 * Assert that the calling principal is permitted to access a resource
 * owned by resourceTenantId.
 *
 * Standard path:
 *   principal.tenantId === resourceTenantId  → allowed (no log)
 *
 * Admin cross-tenant path:
 *   principal.tenantId !== resourceTenantId
 *   AND principal is PlatformAdmin            → allowed + AUDIT log
 *
 * Blocked:
 *   principal.tenantId !== resourceTenantId
 *   AND principal is NOT PlatformAdmin        → TenantIsolationError + AUDIT log
 *
 * @param principal        The authenticated caller.
 * @param resource         The loaded resource — must carry tenantId and id.
 * @param ctx              Correlation data for audit log.
 */
export async function assertDocumentTenantScope(
  principal: AuthPrincipal,
  resource:  { id: string; tenantId: string },
  ctx:       TenantGuardContext,
): Promise<void> {
  if (principal.tenantId === resource.tenantId) {
    return; // same-tenant — fast path, no DB round-trip
  }

  const isPlatformAdmin = principal.roles.includes(Role.PLATFORM_ADMIN as AuthPrincipal['roles'][number]);

  if (isPlatformAdmin) {
    // Explicit, auditable admin cross-tenant access
    logger.warn(
      {
        actorId:          principal.userId,
        actorTenantId:    principal.tenantId,
        resourceTenantId: resource.tenantId,
        resourceId:       resource.id,
        correlationId:    ctx.correlationId,
      },
      'SECURITY: PlatformAdmin cross-tenant access — auditing',
    );

    await auditService.log({
      tenantId:      resource.tenantId,     // log against the accessed resource's tenant
      documentId:    resource.id,
      event:         AuditEvent.ADMIN_CROSS_TENANT_ACCESS,
      actorId:       principal.userId,
      actorRoles:    principal.roles,
      correlationId: ctx.correlationId,
      ipAddress:     ctx.ipAddress,
      userAgent:     ctx.userAgent,
      outcome:       'SUCCESS',
      detail: {
        actorTenantId:    principal.tenantId,
        resourceTenantId: resource.tenantId,
        resourceId:       resource.id,
      },
    });

    return; // admin access granted
  }

  // Non-admin cross-tenant attempt — always block and alert
  logger.error(
    {
      actorId:          principal.userId,
      actorTenantId:    principal.tenantId,
      resourceTenantId: resource.tenantId,
      resourceId:       resource.id,
      correlationId:    ctx.correlationId,
    },
    'SECURITY ALERT: cross-tenant access attempt blocked',
  );

  await auditService.log({
    tenantId:      principal.tenantId,    // log against the ACTOR's tenant for their audit trail
    documentId:    resource.id,
    event:         AuditEvent.TENANT_ISOLATION_VIOLATION,
    actorId:       principal.userId,
    actorRoles:    principal.roles,
    correlationId: ctx.correlationId,
    ipAddress:     ctx.ipAddress,
    userAgent:     ctx.userAgent,
    outcome:       'DENIED',
    detail: {
      actorTenantId:    principal.tenantId,
      resourceTenantId: resource.tenantId,  // included in audit only — never sent to client
    },
  });

  // Generic message — do NOT leak which tenant owns the resource
  throw new TenantIsolationError('Access denied');
}

/**
 * Resolve the effective tenantId for a request.
 *
 * Standard path: use principal.tenantId from the verified JWT.
 *
 * PlatformAdmin explicit cross-tenant path:
 *   If the request includes a targetTenantId (from X-Admin-Target-Tenant header),
 *   AND the principal is PlatformAdmin, the targetTenantId is used.
 *   Any other role supplying targetTenantId is silently ignored — the principal's
 *   own tenantId is used and the deviation is logged.
 *
 * This function NEVER trusts a tenantId supplied by a non-admin caller.
 */
export function resolveEffectiveTenantId(
  principal:      AuthPrincipal,
  targetTenantId: string | undefined,
): string {
  if (!targetTenantId || targetTenantId === principal.tenantId) {
    return principal.tenantId;
  }

  const isPlatformAdmin = principal.roles.includes(Role.PLATFORM_ADMIN as AuthPrincipal['roles'][number]);

  if (!isPlatformAdmin) {
    logger.warn(
      { actorId: principal.userId, actorRoles: principal.roles, targetTenantId },
      'SECURITY: Non-admin supplied targetTenantId — silently ignored, using principal tenantId',
    );
    return principal.tenantId;
  }

  // PlatformAdmin — explicit cross-tenant allowed; caller must call assertDocumentTenantScope
  logger.info(
    { actorId: principal.userId, targetTenantId },
    'PlatformAdmin cross-tenant query resolved',
  );
  return targetTenantId;
}
