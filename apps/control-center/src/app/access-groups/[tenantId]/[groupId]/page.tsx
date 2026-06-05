import Link from 'next/link';
import { requireAdmin }              from '@/lib/auth-guards';
import { controlCenterServerApi }    from '@/lib/control-center-api';
import { Routes }                    from '@/lib/routes';
import { CCShell }                   from '@/components/shell/cc-shell';
import { AccessGroupInfoCard }       from '@/components/access-groups/access-group-info-card';
import { AccessGroupMembersPanel }   from '@/components/access-groups/access-group-members-panel';
import { GroupProductAccessPanel }   from '@/components/access-groups/group-product-access-panel';
import { GroupRoleAssignmentPanel }  from '@/components/access-groups/group-role-assignment-panel';
import { AccessGroupActions }        from '@/components/access-groups/access-group-actions';

export const dynamic = 'force-dynamic';

interface AccessGroupDetailPageProps {
  params: Promise<{ tenantId: string; groupId: string }>;
}

export default async function AccessGroupDetailPage({ params }: AccessGroupDetailPageProps) {
  const session = await requireAdmin();
  const { tenantId, groupId } = await params;

  let group = null;
  let fetchError: string | null = null;

  try {
    group = await controlCenterServerApi.accessGroups.getById(tenantId, groupId);
  } catch (err) {
    fetchError = err instanceof Error ? err.message : 'Failed to load access group.';
  }

  const [membersResult, productsResult, rolesResult, usersResult] = await Promise.allSettled([
    group ? controlCenterServerApi.accessGroups.listMembers(tenantId, groupId) : Promise.resolve([]),
    group ? controlCenterServerApi.accessGroups.listProducts(tenantId, groupId) : Promise.resolve([]),
    group ? controlCenterServerApi.accessGroups.listRoles(tenantId, groupId) : Promise.resolve([]),
    group ? controlCenterServerApi.users.list({ tenantId, pageSize: 200 }) : Promise.resolve({ items: [], totalCount: 0, page: 1, pageSize: 200, totalPages: 0 }),
  ]);

  const members  = membersResult.status  === 'fulfilled' ? membersResult.value  : [];
  const products = productsResult.status === 'fulfilled' ? productsResult.value : [];
  const roles    = rolesResult.status    === 'fulfilled' ? rolesResult.value    : [];
  const users    = usersResult.status    === 'fulfilled' ? usersResult.value    : { items: [] };

  const activeMembers = members.filter(m => m.membershipStatus === 'Active');
  const activeProducts = products.filter(p => p.accessStatus === 'Granted');
  const activeRoles = roles.filter(r => r.assignmentStatus === 'Active');

  return (
    <CCShell userEmail={session.email}>
      <div className="space-y-5">

        <nav className="flex items-center gap-1.5 text-sm text-gray-500">
          <Link href={Routes.groups} className="hover:text-gray-900 transition-colors">
            Access Groups
          </Link>
          <span className="text-gray-300">›</span>
          <span className="text-gray-900 font-medium">
            {group ? group.name : groupId}
          </span>
        </nav>

        {fetchError && (
          <div className="bg-red-50 border border-red-200 rounded-lg px-4 py-3 text-sm text-red-700">
            {fetchError}
          </div>
        )}

        {!fetchError && !group && (
          <div className="bg-white border border-gray-200 rounded-lg p-10 text-center space-y-3">
            <p className="text-sm font-medium text-gray-700">Access group not found</p>
            <p className="text-xs text-gray-400">
              No access group with ID <code className="font-mono bg-gray-100 px-1 rounded">{groupId}</code> exists in this tenant.
            </p>
            <Link href={Routes.groups} className="text-xs text-indigo-600 hover:underline">
              ← Back to Groups
            </Link>
          </div>
        )}

        {group && (
          <>
            <div className="flex items-start justify-between gap-4 flex-wrap">
              <div>
                <h1 className="text-xl font-semibold text-gray-900">{group.name}</h1>
                {group.description && (
                  <p className="text-sm text-gray-500 mt-0.5">{group.description}</p>
                )}
              </div>
              <div className="flex items-center gap-2">
                <span className={`inline-flex items-center px-2 py-0.5 rounded text-[11px] font-semibold border ${group.status === 'Active' ? 'bg-green-50 text-green-700 border-green-200' : 'bg-gray-100 text-gray-500 border-gray-200'}`}>
                  {group.status}
                </span>
                <span className={`inline-flex items-center px-2 py-0.5 rounded text-[11px] font-semibold border ${
                  group.scopeType === 'Product' ? 'bg-purple-50 text-purple-700 border-purple-200' :
                  group.scopeType === 'Organization' ? 'bg-teal-50 text-teal-700 border-teal-200' :
                  'bg-blue-50 text-blue-700 border-blue-200'
                }`}>
                  {group.scopeType}{group.productCode ? `: ${group.productCode}` : ''}
                </span>
                <AccessGroupActions tenantId={tenantId} groupId={groupId} groupName={group.name} status={group.status} />
              </div>
            </div>

            <AccessGroupInfoCard group={group} />

            <div className="grid grid-cols-1 lg:grid-cols-2 gap-5">
              <AccessGroupMembersPanel
                tenantId={tenantId}
                groupId={groupId}
                members={activeMembers}
                availableUsers={users.items}
              />
              <div className="space-y-5">
                <GroupProductAccessPanel
                  tenantId={tenantId}
                  groupId={groupId}
                  products={activeProducts}
                />
                <GroupRoleAssignmentPanel
                  tenantId={tenantId}
                  groupId={groupId}
                  roles={activeRoles}
                />
              </div>
            </div>
          </>
        )}
      </div>
    </CCShell>
  );
}
