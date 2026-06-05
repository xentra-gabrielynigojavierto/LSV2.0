import { requireTenantAdmin } from '@/lib/tenant-auth-guard';
import { tenantServerApi } from '@/lib/tenant-api';
import { AccessExplainabilityClient } from './AccessExplainabilityClient';
import type {

  TenantGroup,
  AdminUserItem,
  PermissionItem,
  GroupMember,
  GroupProductAccess,
  GroupRoleAssignment,
} from '@/types/tenant';

export const dynamic = 'force-dynamic';


export default async function AccessPage() {
  const session = await requireTenantAdmin();
  const tid = session.tenantId;

  let users: AdminUserItem[] = [];
  let groups: TenantGroup[] = [];
  let permissions: PermissionItem[] = [];
  let roles: { id: string; name: string }[] = [];
  let fetchError: string | null = null;

  const groupMembers: Record<string, GroupMember[]> = {};
  const groupProducts: Record<string, GroupProductAccess[]> = {};
  const groupRoles: Record<string, GroupRoleAssignment[]> = {};

  try {
    const results = await Promise.allSettled([
      tenantServerApi.getAdminUsers(1, 500),
      tenantServerApi.getGroups(tid),
      tenantServerApi.getPermissions(),
      tenantServerApi.getRoles(),
    ]);

    users = results[0].status === 'fulfilled' ? results[0].value.items : [];
    groups = results[1].status === 'fulfilled' ? results[1].value : [];
    permissions = results[2].status === 'fulfilled' ? results[2].value.items : [];
    roles = results[3].status === 'fulfilled' ? (results[3].value?.items ?? []) : [];

    if (results[0].status === 'rejected' && results[1].status === 'rejected') {
      fetchError = 'Failed to load access data. Is the identity service running?';
    }

    if (groups.length > 0) {
      const activeGroups = groups.filter((g) => g.status === 'Active');
      const memberResults = await Promise.allSettled(
        activeGroups.map((g) => tenantServerApi.getGroupMembers(tid, g.id))
      );
      const productResults = await Promise.allSettled(
        activeGroups.map((g) => tenantServerApi.getGroupProducts(tid, g.id))
      );
      const roleResults = await Promise.allSettled(
        activeGroups.map((g) => tenantServerApi.getGroupRoles(tid, g.id))
      );

      activeGroups.forEach((g, i) => {
        groupMembers[g.id] = memberResults[i].status === 'fulfilled' ? memberResults[i].value : [];
        groupProducts[g.id] = productResults[i].status === 'fulfilled' ? productResults[i].value : [];
        groupRoles[g.id] = roleResults[i].status === 'fulfilled' ? roleResults[i].value : [];
      });
    }
  } catch {
    fetchError = 'Failed to load access data.';
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
      <AccessExplainabilityClient
        users={users}
        groups={groups}
        permissions={permissions}
        roles={roles}
        groupMembers={groupMembers}
        groupProducts={groupProducts}
        groupRoles={groupRoles}
        tenantId={tid}
      />
    </div>
  );
}
