import Link from 'next/link';
import { requirePlatformAdmin }            from '@/lib/auth-guards';
import { controlCenterServerApi }          from '@/lib/control-center-api';
import { Routes }                          from '@/lib/routes';
import { CCShell }                         from '@/components/shell/cc-shell';
import { UserDetailCard }                  from '@/components/users/user-detail-card';
import { UserActions }                     from '@/components/users/user-actions';
import { UserSecurityPanel }              from '@/components/users/user-security-panel';
import { UserActivityPanel }              from '@/components/users/user-activity-panel';
import { EffectivePermissionsPanel }      from '@/components/users/effective-permissions-panel';
import { RoleAssignmentPanel }             from '@/components/users/role-assignment-panel';
import { AccessExplanationPanel }         from '@/components/users/access-explanation-panel';
import { startImpersonationAction }        from '@/app/actions/impersonation';
import type { UserStatus }                 from '@/types/control-center';

export const dynamic = 'force-dynamic';

interface PlatformUserDetailPageProps {
  params: Promise<{ userId: string }>;
}

/**
 * /platform-users/[userId] — PlatformInternal user detail page.
 *
 * Access: PlatformAdmin only (enforced by requirePlatformAdmin).
 *
 * Reuses the same informational and action panels as /tenant-users/[id].
 * The only differences are:
 *   - Auth guard: requirePlatformAdmin (stricter than requireAdmin)
 *   - Back-link goes to /platform-users
 *   - Skips org membership + access group panels (not relevant for platform staff)
 */
export default async function PlatformUserDetailPage({ params }: PlatformUserDetailPageProps) {
  const session    = await requirePlatformAdmin();
  const { userId } = await params;

  let user        = null;
  let fetchError: string | null = null;

  try {
    user = await controlCenterServerApi.users.getById(userId);
  } catch (err) {
    fetchError = err instanceof Error ? err.message : 'Failed to load user.';
  }

  const [
    assignableRolesResult,
    securityResult,
    permissionsResult,
    accessDebugResult,
  ] = await Promise.allSettled([
    user ? controlCenterServerApi.users.getAssignableRoles(user.id) : Promise.resolve(null),
    user ? controlCenterServerApi.users.getSecurity(user.id)        : Promise.resolve(null),
    user ? controlCenterServerApi.users.getEffectivePermissions(user.id) : Promise.resolve(null),
    user ? controlCenterServerApi.users.getAccessDebug(user.id)     : Promise.resolve(null),
  ]);

  const assignableData = assignableRolesResult.status === 'fulfilled' ? assignableRolesResult.value : null;
  const security       = securityResult.status        === 'fulfilled' ? securityResult.value        : null;
  const effectivePerms = permissionsResult.status     === 'fulfilled' ? permissionsResult.value     : null;
  const permsError     = permissionsResult.status     === 'rejected'
    ? (permissionsResult.reason instanceof Error ? permissionsResult.reason.message : 'Failed to load permissions.')
    : null;
  const accessDebug    = accessDebugResult.status     === 'fulfilled' ? accessDebugResult.value     : null;
  const accessDebugError = accessDebugResult.status   === 'rejected'
    ? (accessDebugResult.reason instanceof Error ? accessDebugResult.reason.message : 'Failed to load access debug.')
    : null;

  return (
    <CCShell userEmail={session.email}>
      <div className="space-y-5">

        {/* Breadcrumb */}
        <nav className="flex items-center gap-1.5 text-sm text-gray-500">
          <Link href={Routes.platformUsers} className="hover:text-gray-900 transition-colors">
            Platform Staff
          </Link>
          <span className="text-gray-300">›</span>
          <span className="text-gray-900 font-medium">
            {user ? `${user.firstName} ${user.lastName}` : userId}
          </span>
        </nav>

        {/* Error state */}
        {fetchError && (
          <div className="bg-red-50 border border-red-200 rounded-lg px-4 py-3 text-sm text-red-700">
            {fetchError}
          </div>
        )}

        {/* Not found state */}
        {!fetchError && !user && (
          <div className="bg-white border border-gray-200 rounded-lg p-10 text-center space-y-3">
            <p className="text-sm font-medium text-gray-700">Platform user not found</p>
            <Link href={Routes.platformUsers} className="text-xs text-indigo-600 hover:underline">
              ← Back to Platform Staff
            </Link>
          </div>
        )}

        {user && (
          <>
            {/* Header */}
            <div className="flex items-start justify-between gap-4 flex-wrap">
              <div>
                <h1 className="text-xl font-semibold text-gray-900">
                  {user.firstName} {user.lastName}
                </h1>
                <p className="text-sm text-gray-500 mt-0.5">{user.email}</p>
                <div className="flex items-center gap-2 mt-1.5">
                  <StatusBadge status={user.status} />
                  <span className="inline-flex items-center px-2 py-0.5 rounded text-[11px] font-semibold border bg-violet-50 text-violet-700 border-violet-200">
                    PlatformInternal
                  </span>
                </div>
              </div>

              <div className="flex items-center gap-2 flex-wrap">
                {/* Impersonation */}
                {(() => {
                  const action = startImpersonationAction.bind(null, {
                    id:         user.id,
                    email:      user.email,
                    tenantId:   user.tenantId,
                    tenantName: user.tenantDisplayName,
                  });
                  return (
                    <form action={action}>
                      <button
                        type="submit"
                        disabled={user.status !== 'Active'}
                        className="inline-flex items-center gap-1.5 text-sm font-medium text-rose-700 hover:text-rose-900 bg-rose-50 hover:bg-rose-100 border border-rose-300 hover:border-rose-400 px-3 py-1.5 rounded-md transition-colors disabled:opacity-40 disabled:cursor-not-allowed"
                        title={
                          user.status !== 'Active'
                            ? 'Only Active users can be impersonated'
                            : `Impersonate ${user.email}`
                        }
                      >
                        <span aria-hidden="true">⚡</span>
                        Impersonate User
                      </button>
                    </form>
                  );
                })()}

                <UserActions
                  userId={user.id}
                  currentStatus={user.status}
                  isLocked={user.isLocked}
                />
              </div>
            </div>

            {/* Identity card */}
            <UserDetailCard user={user} />

            {/* Security & Sessions */}
            <UserSecurityPanel security={security} />

            {/* Activity */}
            <UserActivityPanel userId={user.id} tenantId={user.tenantId} />

            {/* Effective Permissions */}
            <EffectivePermissionsPanel result={effectivePerms} fetchError={permsError} />

            {/* Access explanation */}
            <AccessExplanationPanel data={accessDebug} fetchError={accessDebugError} />

            {/* Role assignment */}
            <div className="space-y-3">
              <div className="flex items-center gap-3">
                <h2 className="text-sm font-semibold text-gray-700 uppercase tracking-wide">
                  Platform Role Assignment
                </h2>
                <div className="flex-1 h-px bg-gray-200" />
                <span className="text-[11px] font-medium text-indigo-600 bg-indigo-50 border border-indigo-200 px-2 py-0.5 rounded uppercase tracking-wide">
                  Editable
                </span>
              </div>
              <RoleAssignmentPanel
                userId={user.id}
                currentRoles={user.roles ?? []}
                assignableRoles={assignableData?.items}
                userOrgType={assignableData?.userOrgType}
              />
            </div>
          </>
        )}
      </div>
    </CCShell>
  );
}

function StatusBadge({ status }: { status: UserStatus }) {
  const styles: Record<UserStatus, string> = {
    Active:   'bg-green-50 text-green-700 border-green-200',
    Inactive: 'bg-gray-100 text-gray-500 border-gray-200',
    Invited:  'bg-blue-50 text-blue-700 border-blue-200',
  };
  return (
    <span className={`inline-flex items-center px-2 py-0.5 rounded text-[11px] font-semibold border ${styles[status]}`}>
      {status}
    </span>
  );
}
