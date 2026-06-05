import { requireTenantAdmin } from '@/lib/tenant-auth-guard';
import { tenantServerApi } from '@/lib/tenant-api';
import { PermissionsClient } from './PermissionsClient';
import type { TenantPermissionCatalogItem, TenantRoleItem } from '@/types/tenant';

export const dynamic = 'force-dynamic';


export default async function PermissionsPage() {
  const session = await requireTenantAdmin();
  const tid = session.tenantId;

  let tenantPermissions: TenantPermissionCatalogItem[] = [];
  let roles: TenantRoleItem[] = [];
  let fetchError: string | null = null;

  try {
    const [catalogResult, rolesResult] = await Promise.allSettled([
      tenantServerApi.getTenantPermissionCatalog(tid),
      tenantServerApi.getRoles(),
    ]);

    if (catalogResult.status === 'fulfilled') {
      tenantPermissions = catalogResult.value.permissions;
    }

    if (rolesResult.status === 'fulfilled') {
      roles = rolesResult.value?.items ?? [];
    }

    if (catalogResult.status === 'rejected' && rolesResult.status === 'rejected') {
      fetchError = 'Failed to load permission data. Is the identity service running?';
    }
  } catch {
    fetchError = 'Failed to load permission data.';
  }

  return (
    <div className="space-y-4">
      {fetchError && (
        <div className="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
          <div className="flex items-center gap-2">
            <i className="ri-error-warning-line text-base" />
            {fetchError}
          </div>
        </div>
      )}
      <PermissionsClient
        tenantPermissions={tenantPermissions}
        roles={roles}
        isTenantAdmin={session.isTenantAdmin}
      />
    </div>
  );
}
