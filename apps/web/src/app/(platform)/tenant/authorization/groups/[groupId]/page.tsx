import { requireTenantAdmin } from '@/lib/tenant-auth-guard';
import { tenantServerApi, ServerApiError } from '@/lib/tenant-api';
import { GroupDetailClient } from './GroupDetailClient';
import type { TenantGroup, TenantUser, GroupMember, GroupProductAccess, GroupRoleAssignment } from '@/types/tenant';

interface Props {
  params: Promise<{ groupId: string }>;
}

export default async function GroupDetailPage({ params }: Props) {
  const session = await requireTenantAdmin();
  const { groupId } = await params;
  const tid = session.tenantId;

  let group: TenantGroup | null = null;
  let fetchError: string | null = null;

  try {
    group = await tenantServerApi.getGroup(tid, groupId);
  } catch (err) {
    fetchError =
      err instanceof ServerApiError && err.isNotFound
        ? 'Group not found.'
        : err instanceof ServerApiError
          ? `Failed to load group (${err.status}).`
          : 'Failed to load group. Is the identity service running?';
  }

  let members: GroupMember[] = [];
  let products: GroupProductAccess[] = [];
  let roles: GroupRoleAssignment[] = [];
  let allUsers: TenantUser[] = [];
  let allRoles: { id: string; name: string }[] = [];

  if (group) {
    const results = await Promise.allSettled([
      tenantServerApi.getGroupMembers(tid, groupId),
      tenantServerApi.getGroupProducts(tid, groupId),
      tenantServerApi.getGroupRoles(tid, groupId),
      tenantServerApi.getUsers(),
      tenantServerApi.getRoles(),
    ]);

    members = results[0].status === 'fulfilled' ? results[0].value : [];
    products = results[1].status === 'fulfilled' ? results[1].value : [];
    roles = results[2].status === 'fulfilled' ? results[2].value : [];
    allUsers = results[3].status === 'fulfilled' ? results[3].value : [];
    allRoles = results[4].status === 'fulfilled' ? (results[4].value?.items ?? []) : [];
  }

  if (fetchError || !group) {
    return (
      <div className="space-y-4">
        <div className="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
          <div className="flex items-center gap-2">
            <i className="ri-error-warning-line text-base" />
            {fetchError ?? 'Group not found.'}
          </div>
        </div>
        <a
          href="/tenant/authorization/groups"
          className="inline-flex items-center gap-1.5 text-sm text-primary hover:text-primary/80 font-medium"
        >
          <i className="ri-arrow-left-line" />
          Back to Groups
        </a>
      </div>
    );
  }

  return (
    <GroupDetailClient
      group={group}
      members={members}
      products={products}
      roles={roles}
      allUsers={allUsers}
      allRoles={allRoles}
      tenantId={tid}
    />
  );
}
