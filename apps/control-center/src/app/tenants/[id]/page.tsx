import { requirePlatformAdmin }           from '@/lib/auth-guards';
import { controlCenterServerApi }         from '@/lib/control-center-api';
import { getCachedTenantById }            from '@/lib/tenant-fetch';
import { TenantDetailCard }               from '@/components/tenants/tenant-detail-card';
import { ProductEntitlementsPanel }       from '@/components/tenants/product-entitlements-panel';
import { TenantSessionSettingsPanel }     from '@/components/tenants/tenant-session-settings-panel';
import { TenantLogoUpload }              from '@/components/tenants/TenantLogoUpload';
import { TenantOrganizationsPanel }      from '@/components/tenants/tenant-organizations-panel';

export const dynamic = 'force-dynamic';

interface TenantDetailPageProps {
  params: Promise<{ id: string }>;
}

/**
 * /tenants/[id] — Tenant detail body (Overview tab).
 *
 * The shared header (breadcrumb, tenant name/status/actions, sub-nav tabs)
 * is rendered by the parent layout.tsx — this page returns only body content.
 *
 * Access: PlatformAdmin only (enforced by layout + requirePlatformAdmin below).
 */
export default async function TenantDetailPage({ params }: TenantDetailPageProps) {
  await requirePlatformAdmin();
  const { id } = await params;

  let tenant = null;

  try {
    tenant = await getCachedTenantById(id);
  } catch {
    // The layout already renders the error banner for this tenant fetch.
    // Returning null here prevents a duplicate error box from appearing.
    return null;
  }

  if (!tenant) return null;

  let organizations: Awaited<ReturnType<typeof controlCenterServerApi.organizations.listByTenant>> = [];
  try {
    organizations = await controlCenterServerApi.organizations.listByTenant(id);
  } catch {
    // non-fatal
  }

  return (
    <div className="space-y-5">
      <TenantDetailCard tenant={tenant} />

      <TenantLogoUpload
        tenantId={tenant.id}
        logoDocumentId={tenant.logoDocumentId}
        logoWhiteDocumentId={tenant.logoWhiteDocumentId}
      />

      <TenantOrganizationsPanel organizations={organizations} />

      <TenantSessionSettingsPanel
        tenantId={tenant.id}
        sessionTimeoutMinutes={tenant.sessionTimeoutMinutes}
      />

      <ProductEntitlementsPanel
        tenantId={tenant.id}
        entitlements={tenant.productEntitlements}
      />
    </div>
  );
}
