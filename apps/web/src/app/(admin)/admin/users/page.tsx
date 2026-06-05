import { requireAdmin } from '@/lib/auth-guards';
import { serverApi, ServerApiError } from '@/lib/server-api-client';
import { UserTable } from './UserTable';
import type { UserResponse } from '@/types/admin';

export const dynamic = 'force-dynamic';


/**
 * Admin — User management.
 *
 * Server component: fetches all users for the caller's tenant from the
 * identity service (GET /identity/api/users), then hands the list to the
 * UserTable client component for search / filter / pagination.
 *
 * TenantAdmin  → sees users scoped to their own tenant (identity handles JWT scoping).
 * PlatformAdmin → same endpoint; cross-tenant view requires a separate admin endpoint.
 */
export default async function AdminUsersPage() {
  const session = await requireAdmin();

  let users: UserResponse[] = [];
  let fetchError: string | null = null;

  try {
    users = await serverApi.get<UserResponse[]>('/identity/api/users');
  } catch (err) {
    fetchError =
      err instanceof ServerApiError
        ? `Failed to load users (${err.status}).`
        : 'Failed to load users. Is the identity service running?';
  }

  return (
    <div className="space-y-4">

      {/* ── Page header ── */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-semibold text-gray-900">Users</h1>
          <p className="text-sm text-gray-500 mt-0.5">
            {session.isPlatformAdmin ? 'All users in your tenant' : 'Users in your tenant'}
          </p>
        </div>
        <span className="text-xs bg-gray-100 border border-gray-200 text-gray-500 px-2 py-1 rounded">
          {session.isPlatformAdmin ? 'Platform Admin' : 'Tenant Admin'}
        </span>
      </div>

      {/* ── Error state ── */}
      {fetchError && (
        <div className="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
          {fetchError}
        </div>
      )}

      {/* ── User table ── */}
      {!fetchError && (
        <UserTable users={users} />
      )}
    </div>
  );
}
