import { requireTenantAdmin } from '@/lib/tenant-auth-guard';
import { tenantServerApi } from '@/lib/tenant-api';
import { GroupTable } from './GroupTable';
import type { TenantGroup, TenantUser } from '@/types/tenant';

export const dynamic = 'force-dynamic';


export default async function GroupsPage() {
  const session = await requireTenantAdmin();

  let groups: TenantGroup[] = [];
  let users: TenantUser[] = [];
  let fetchError: string | null = null;

  try {
    const results = await Promise.allSettled([
      tenantServerApi.getGroups(session.tenantId),
      tenantServerApi.getUsers(),
    ]);
    groups = results[0].status === 'fulfilled' ? results[0].value : [];
    users = results[1].status === 'fulfilled' ? results[1].value : [];
    if (results[0].status === 'rejected') {
      fetchError = 'Failed to load groups. Is the identity service running?';
    }
  } catch {
    fetchError = 'Failed to load groups.';
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
      <GroupTable groups={groups} users={users} tenantId={session.tenantId} />
    </div>
  );
}
