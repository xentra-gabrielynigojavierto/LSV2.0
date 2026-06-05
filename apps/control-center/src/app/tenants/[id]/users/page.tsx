import { requirePlatformAdmin }    from '@/lib/auth-guards';
import { controlCenterServerApi }  from '@/lib/control-center-api';
import { UserManagementTabs }      from '@/components/users/user-management-tabs';

export const dynamic = 'force-dynamic';

interface Props {
  params:       Promise<{ id: string }>;
  searchParams: Promise<{ page?: string; search?: string }>;
}

/**
 * /tenants/[id]/users — Tenant User Management hub (PUM-B07).
 *
 * Uses the PUM-B03 tenant-specific endpoint which:
 *   - Returns only users in this tenant
 *   - Includes inline tenant-scoped role assignments per user
 *   - Excludes PlatformInternal users (client-side filter in tenantAdminUsers.list)
 *
 * Fetches tenant roles (Tenant-scoped only) in parallel for the role assignment modal.
 */
export default async function TenantUsersPage({ params, searchParams }: Props) {
  await requirePlatformAdmin();

  const { id } = await params;
  const sp     = await searchParams;
  const page   = Math.max(1, parseInt(sp.page ?? '1') || 1);
  const search = sp.search ?? '';

  const [usersResult, rolesResult] = await Promise.allSettled([
    controlCenterServerApi.tenantAdminUsers.list(id, { page, pageSize: 20, search }),
    controlCenterServerApi.roles.list({ scope: 'Tenant' }),
  ]);

  const usersData  = usersResult.status  === 'fulfilled' ? usersResult.value  : null;
  const tenantRoles = rolesResult.status === 'fulfilled' ? rolesResult.value  : [];
  const hasError   = usersResult.status  === 'rejected';

  return (
    <UserManagementTabs
      tenantId={id}
      tenantUsers={usersData?.items ?? []}
      totalCount={usersData?.totalCount ?? 0}
      page={usersData?.page ?? page}
      pageSize={usersData?.pageSize ?? 20}
      search={search}
      hasError={hasError}
      tenantRoles={tenantRoles}
    />
  );
}
