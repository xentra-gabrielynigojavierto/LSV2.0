import { requireTenantAdmin } from '@/lib/tenant-auth-guard';
import { tenantServerApi, ServerApiError } from '@/lib/tenant-api';
import { AuthUserTable } from './AuthUserTable';
import type { TenantUser } from '@/types/tenant';

export const dynamic = 'force-dynamic';


export default async function AuthorizationUsersPage() {
  const session = await requireTenantAdmin();

  let users: TenantUser[] = [];
  let fetchError: string | null = null;

  try {
    users = await tenantServerApi.getUsers();
  } catch (err) {
    fetchError = err instanceof ServerApiError && err.isForbidden
      ? 'You do not have permission to view users.'
      : 'Unable to load users right now.';
  }

  if (fetchError) {
    return (
      <div className="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
        <div className="flex items-center gap-2">
          <i className="ri-error-warning-line text-base" />
          {fetchError}
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-4">
      <AuthUserTable users={users} tenantId={session.tenantId} />
    </div>
  );
}
