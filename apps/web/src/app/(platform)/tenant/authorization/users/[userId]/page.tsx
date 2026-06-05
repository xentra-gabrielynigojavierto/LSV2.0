import { requireTenantAdmin } from '@/lib/tenant-auth-guard';
import { tenantServerApi, ServerApiError } from '@/lib/tenant-api';
import { UserDetailClient } from './UserDetailClient';
import type { TenantUserDetail, AccessDebugResponse, TenantGroup, AssignableRolesResponse } from '@/types/tenant';

interface Props {
  params: Promise<{ userId: string }>;
}

export default async function UserDetailPage({ params }: Props) {
  const session = await requireTenantAdmin();
  const { userId } = await params;

  let user: TenantUserDetail | null = null;
  let accessDebug: AccessDebugResponse | null = null;
  let groups: TenantGroup[] = [];
  let assignableRoles: AssignableRolesResponse | null = null;
  let fetchError: string | null = null;

  try {
    user = await tenantServerApi.getUserDetail(userId);
  } catch (err) {
    fetchError =
      err instanceof ServerApiError && err.isNotFound
        ? 'User not found.'
        : err instanceof ServerApiError
          ? `Failed to load user (${err.status}).`
          : 'Failed to load user details. Is the identity service running?';
  }

  if (user) {
    const results = await Promise.allSettled([
      tenantServerApi.getAccessDebug(userId),
      tenantServerApi.getGroups(session.tenantId),
      tenantServerApi.getAssignableRoles(userId),
    ]);

    accessDebug = results[0].status === 'fulfilled' ? results[0].value : null;
    groups = results[1].status === 'fulfilled' ? results[1].value : [];
    assignableRoles = results[2].status === 'fulfilled' ? results[2].value : null;
  }

  if (fetchError || !user) {
    return (
      <div className="space-y-4">
        <div className="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
          <div className="flex items-center gap-2">
            <i className="ri-error-warning-line text-base" />
            {fetchError ?? 'User not found.'}
          </div>
        </div>
        <a
          href="/tenant/authorization/users"
          className="inline-flex items-center gap-1.5 text-sm text-primary hover:text-primary/80 font-medium"
        >
          <i className="ri-arrow-left-line" />
          Back to Users
        </a>
      </div>
    );
  }

  return (
    <UserDetailClient
      user={user}
      accessDebug={accessDebug}
      groups={groups}
      assignableRoles={assignableRoles}
      tenantId={session.tenantId}
    />
  );
}
