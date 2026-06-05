import { isRedirectError }              from 'next/dist/client/components/redirect-error';
import { requirePlatformAdmin }        from '@/lib/auth-guards';
import { getTenantContext }            from '@/lib/auth';
import { controlCenterServerApi }      from '@/lib/control-center-api';
import { CCShell }                     from '@/components/shell/cc-shell';
import { PermissionAuditWorkspace }    from '@/components/synqaudit/permission-audit-workspace';

export const dynamic = 'force-dynamic';

interface Props {
  searchParams: Promise<{
    scope?:   string;
    actorId?: string;
    tenantId?: string;
    dateFrom?: string;
    dateTo?:   string;
    search?:   string;
    page?:     string;
  }>;
}

const PAGE_SIZE = 15;

/**
 * /synqaudit/permissions — Permission-change audit viewer.
 *
 * Focused sub-view of the canonical audit stream scoped to identity
 * permission-change event types:
 *   identity.user.role.*
 *   identity.user.product.*
 *   identity.group.member.*
 *   identity.group.role.*
 *   identity.group.product.*
 *   identity.tenant.product.*
 *
 * Access: PlatformAdmin only (requirePlatformAdmin).
 * Tenant isolation: enforced by the audit service query authorizer —
 *   non-PlatformAdmin callers are restricted to their own tenant's records.
 *   When a tenant context cookie is active, queries are scoped to that tenant.
 *
 * Data: server-side fetch via auditCanonical.list(), then passed to the
 *   PermissionAuditWorkspace client component for interactive filters,
 *   row-click detail panel, and pagination.
 */
export default async function PermissionsAuditPage({ searchParams }: Props) {
  const searchParamsData = await searchParams;
  const session   = await requirePlatformAdmin();
  const tenantCtx = await getTenantContext();

  const scope    = searchParamsData.scope    ?? '';
  const actorId  = searchParamsData.actorId  ?? '';
  const tenantId = searchParamsData.tenantId ?? '';
  const dateFrom = searchParamsData.dateFrom ?? '';
  const dateTo   = searchParamsData.dateTo   ?? '';
  const search   = searchParamsData.search   ?? '';
  const page     = Math.max(1, parseInt(searchParamsData.page ?? '1', 10));

  // Map the scope preset to an eventType filter string for the API.
  const eventType = SCOPE_TO_EVENT_TYPE[scope] ?? '';

  let items:      Awaited<ReturnType<typeof controlCenterServerApi.auditCanonical.list>>['items'] = [];
  let totalCount  = 0;
  let fetchError: string | null = null;

  try {
    const result = await controlCenterServerApi.auditCanonical.list({
      page,
      pageSize:  PAGE_SIZE,
      tenantId:  tenantId  || tenantCtx?.tenantId,
      actorId:   actorId   || undefined,
      eventType: eventType || undefined,
      dateFrom:  dateFrom  || undefined,
      dateTo:    dateTo    || undefined,
      search:    search    || undefined,
    });
    items      = result.items;
    totalCount = result.totalCount;
  } catch (err) {
    if (isRedirectError(err)) throw err;
    fetchError = err instanceof Error ? err.message : 'Failed to load permission audit events.';
  }

  const totalPages = Math.max(1, Math.ceil(totalCount / PAGE_SIZE));

  return (
    <CCShell userEmail={session.email}>
      <div className="space-y-4">

        {/* Header */}
        <div className="flex items-start justify-between gap-4">
          <div>
            <div className="flex items-center gap-3">
              <h1 className="text-xl font-semibold text-gray-900">Permission Changes</h1>
              <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full bg-green-50 border border-green-300 text-[11px] font-semibold text-green-700">
                <span className="h-1.5 w-1.5 rounded-full bg-green-500 animate-pulse" />
                LIVE
              </span>
            </div>
            <p className="text-sm text-gray-500 mt-0.5">
              {tenantCtx
                ? `Scoped to ${tenantCtx.tenantName} — identity permission-change events`
                : 'Platform-wide identity permission-change audit trail'}
            </p>
          </div>
          <a
            href="/synqaudit/exports"
            className="inline-flex items-center gap-1.5 h-9 px-3 text-sm font-medium text-gray-600 hover:text-gray-900 bg-white border border-gray-300 hover:border-gray-400 rounded-md transition-colors whitespace-nowrap"
          >
            <i className="ri-download-cloud-line text-sm" />
            Export
          </a>
        </div>

        {/* Error banner */}
        {fetchError && (
          <div className="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
            {fetchError}
          </div>
        )}

        {/* Interactive workspace (client component) */}
        {!fetchError && (
          <PermissionAuditWorkspace
            entries={items}
            totalCount={totalCount}
            page={page}
            totalPages={totalPages}
            filters={{ scope, actorId, tenantId, dateFrom, dateTo, search }}
            tenantCtxName={tenantCtx?.tenantName}
          />
        )}
      </div>
    </CCShell>
  );
}

// ── Scope → event type mapping ────────────────────────────────────────────────

/**
 * Maps the URL `scope` param to the exact `eventType` string sent to the
 * audit query API.  When scope is empty or unrecognised, no eventType filter
 * is applied (all events are returned, subject to tenant isolation).
 */
const SCOPE_TO_EVENT_TYPE: Record<string, string> = {
  'user-role-assigned':       'identity.user.role.assigned',
  'user-role-revoked':        'identity.user.role.revoked',
  'user-product-assigned':    'identity.user.product.assigned',
  'user-product-revoked':     'identity.user.product.revoked',
  'user-product-reactivated': 'identity.user.product.reactivated',
  'group-member-added':       'identity.group.member.added',
  'group-member-removed':     'identity.group.member.removed',
  'group-member-reactivated': 'identity.group.member.reactivated',
  'group-role-assigned':      'identity.group.role.assigned',
  'group-role-revoked':       'identity.group.role.revoked',
  'group-product-assigned':   'identity.group.product.assigned',
  'group-product-revoked':    'identity.group.product.revoked',
  'tenant-product-assigned':  'identity.tenant.product.assigned',
};
