import { requirePlatformAdmin } from '@/lib/auth-guards';
import { getTenantContext } from '@/lib/auth';
import { controlCenterServerApi } from '@/lib/control-center-api';
import { CCShell } from '@/components/shell/cc-shell';
import { AccessGroupListTable } from '@/components/access-groups/access-group-list-table';
import { CreateAccessGroupButton } from '@/components/access-groups/create-access-group-button';

export const dynamic = 'force-dynamic';

interface GroupsPageProps {
  searchParams: Promise<{
    tenantId?: string;
  }>;
}

export default async function GroupsPage({ searchParams }: GroupsPageProps) {
  const searchParamsData = await searchParams;
  const session   = await requirePlatformAdmin();
  const tenantCtx = await getTenantContext();

  const tenantId = searchParamsData.tenantId ?? tenantCtx?.tenantId;

  let groups: Awaited<ReturnType<typeof controlCenterServerApi.accessGroups.list>> = [];
  let fetchError: string | null = null;

  if (tenantId) {
    try {
      groups = await controlCenterServerApi.accessGroups.list(tenantId);
    } catch (err) {
      fetchError = err instanceof Error ? err.message : 'Failed to load access groups.';
    }
  }

  return (
    <CCShell userEmail={session.email}>
      <div className="space-y-4">

        <div className="flex items-center justify-between">
          <div>
            <div className="flex items-center gap-3">
              <h1 className="text-xl font-semibold text-gray-900">Access Groups</h1>
              {tenantCtx && (
                <span className="inline-flex items-center gap-1.5 px-2 py-0.5 rounded-full bg-amber-100 border border-amber-300 text-[11px] font-semibold text-amber-700">
                  <span className="h-1.5 w-1.5 rounded-full bg-amber-500" />
                  Scoped to {tenantCtx.tenantName}
                </span>
              )}
            </div>
            <p className="text-sm text-gray-500 mt-0.5">
              {tenantId
                ? 'Manage access groups with inherited product access and role assignments.'
                : 'Select a tenant context to manage access groups.'}
            </p>
          </div>

          {tenantId && (
            <CreateAccessGroupButton tenantId={tenantId} />
          )}
        </div>

        {fetchError && (
          <div className="bg-red-50 border border-red-200 rounded-lg px-4 py-3 text-sm text-red-700">
            {fetchError}
          </div>
        )}

        {!tenantId && !tenantCtx && (
          <div className="bg-amber-50 border border-amber-200 rounded-lg px-4 py-3 text-sm text-amber-800">
            <strong>No tenant context active.</strong> Set a tenant context using the tenant selector to manage access groups.
          </div>
        )}

        {tenantId && groups.length > 0 && (
          <>
            <p className="text-xs text-gray-400">
              {groups.length} access group{groups.length !== 1 ? 's' : ''}
            </p>
            <AccessGroupListTable
              groups={groups}
              tenantId={tenantId}
            />
          </>
        )}

        {tenantId && groups.length === 0 && !fetchError && (
          <div className="bg-white border border-gray-200 rounded-lg p-10 text-center space-y-2">
            <p className="text-sm font-medium text-gray-500">No access groups found</p>
            <p className="text-xs text-gray-400">
              Create an access group to define inherited product access and role assignments for members.
            </p>
          </div>
        )}
      </div>
    </CCShell>
  );
}
