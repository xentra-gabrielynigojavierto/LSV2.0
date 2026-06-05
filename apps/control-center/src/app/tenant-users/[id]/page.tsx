import Link from 'next/link';
import { requireAdmin }                   from '@/lib/auth-guards';
import { controlCenterServerApi }         from '@/lib/control-center-api';
import { Routes }                         from '@/lib/routes';
import { CCShell }                        from '@/components/shell/cc-shell';
import { UserDetailCard }                 from '@/components/users/user-detail-card';
import { UserActions }                    from '@/components/users/user-actions';
import { UserSecurityPanel }             from '@/components/users/user-security-panel';
import { UserActivityPanel }             from '@/components/users/user-activity-panel';
import { EffectivePermissionsPanel }     from '@/components/users/effective-permissions-panel';
import { RoleAssignmentPanel }            from '@/components/users/role-assignment-panel';
import { OrgMembershipPanel }             from '@/components/users/org-membership-panel';
import { AccessGroupMembershipPanel }    from '@/components/access-groups/access-group-membership-panel';
import { AccessExplanationPanel }        from '@/components/users/access-explanation-panel';
import { startImpersonationAction }       from '@/app/actions/impersonation';
import type { UserStatus }                from '@/types/control-center';

export const dynamic = 'force-dynamic';

interface UserDetailPageProps {
  params: Promise<{ id: string }>;
}

/**
 * /tenant-users/[id] — User detail page.
 *
 * Access: PlatformAdmin or TenantAdmin (UIX-003-01).
 *   - PlatformAdmin: full cross-tenant access.
 *   - TenantAdmin: scoped to their own tenant (enforced by BFF + backend).
 *
 * Layout:
 *   1. Informational summary (UserDetailCard) — User Information, Account Status,
 *      Effective Access Summary. Read-only.
 *   2. Access Control Management panels (UIX-003) — interactive role assignment,
 *      org membership, group membership. Editable.
 */
export default async function UserDetailPage({ params }: UserDetailPageProps) {
  const session = await requireAdmin();
  const { id }  = await params;

  let user        = null;
  let fetchError: string | null = null;

  try {
    user = await controlCenterServerApi.users.getById(id);
  } catch (err) {
    fetchError = err instanceof Error ? err.message : 'Failed to load user.';
  }

  const [assignableRolesResult, orgsResult, securityResult, permissionsResult, accessGroupsResult, userAccessGroupsResult, accessDebugResult] = await Promise.allSettled([
    user
      ? controlCenterServerApi.users.getAssignableRoles(user.id)
      : Promise.resolve(null),
    user
      ? controlCenterServerApi.organizations.listByTenant(user.tenantId)
      : Promise.resolve([]),
    user
      ? controlCenterServerApi.users.getSecurity(user.id)
      : Promise.resolve(null),
    user
      ? controlCenterServerApi.users.getEffectivePermissions(user.id)
      : Promise.resolve(null),
    user
      ? controlCenterServerApi.accessGroups.list(user.tenantId)
      : Promise.resolve([]),
    user
      ? controlCenterServerApi.accessGroups.listUserGroups(user.tenantId, user.id)
      : Promise.resolve([]),
    user
      ? controlCenterServerApi.users.getAccessDebug(user.id)
      : Promise.resolve(null),
  ]);

  const assignableData    = assignableRolesResult.status  === 'fulfilled' ? assignableRolesResult.value   : null;
  const availableOrgs     = orgsResult.status            === 'fulfilled' ? orgsResult.value               : [];
  const security          = securityResult.status        === 'fulfilled' ? securityResult.value           : null;
  const effectivePerms    = permissionsResult.status     === 'fulfilled' ? permissionsResult.value       : null;
  const permsError        = permissionsResult.status     === 'rejected'
    ? (permissionsResult.reason instanceof Error ? permissionsResult.reason.message : 'Failed to load permissions.')
    : null;
  const accessGroupsList  = accessGroupsResult.status    === 'fulfilled' ? accessGroupsResult.value      : [];
  const userAccessGroups  = userAccessGroupsResult.status === 'fulfilled' ? userAccessGroupsResult.value : [];
  const accessDebug       = accessDebugResult.status      === 'fulfilled' ? accessDebugResult.value      : null;
  const accessDebugError  = accessDebugResult.status      === 'rejected'
    ? (accessDebugResult.reason instanceof Error ? accessDebugResult.reason.message : 'Failed to load access debug.')
    : null;

  return (
    <CCShell userEmail={session.email}>
      <div className="space-y-5">

        {/* Breadcrumb */}
        <nav className="flex items-center gap-1.5 text-sm text-gray-500">
          <Link href={Routes.tenantUsers} className="hover:text-gray-900 transition-colors">
            Tenant Users
          </Link>
          <span className="text-gray-300">›</span>
          <span className="text-gray-900 font-medium">
            {user ? `${user.firstName} ${user.lastName}` : id}
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
            <p className="text-sm font-medium text-gray-700">User not found</p>
            <p className="text-xs text-gray-400">
              No user with ID <code className="font-mono bg-gray-100 px-1 rounded">{id}</code> exists.
            </p>
            <Link href={Routes.tenantUsers} className="text-xs text-indigo-600 hover:underline">
              ← Back to Tenant Users
            </Link>
          </div>
        )}

        {/* Detail content */}
        {user && (
          <>
            {/* Page header */}
            <div className="flex items-start justify-between gap-4 flex-wrap">
              <div className="space-y-2">
                {/* Name + email */}
                <div>
                  <h1 className="text-xl font-semibold text-gray-900">
                    {user.firstName} {user.lastName}
                  </h1>
                  <p className="text-sm text-gray-500 mt-0.5">{user.email}</p>
                </div>
                {/* Badge row */}
                <div className="flex items-center gap-2 flex-wrap">
                  <StatusBadge status={user.status} />
                  <RoleBadge role={user.role} />
                  <TenantBadge
                    tenantId={user.tenantId}
                    tenantCode={user.tenantCode}
                    tenantDisplayName={user.tenantDisplayName}
                  />
                  {(user.isLocked ?? false) && <LockedBadge />}
                </div>
              </div>

              {/* Action buttons */}
              <div className="flex items-center gap-2 flex-wrap">

                {/* Impersonate User — only for Active users */}
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
                        className="inline-flex items-center gap-1.5 text-sm font-medium text-rose-700 hover:text-rose-900 bg-rose-50 hover:bg-rose-100 border border-rose-300 hover:border-rose-400 px-3 py-1.5 rounded-md transition-colors disabled:opacity-40 disabled:cursor-not-allowed disabled:hover:bg-rose-50 disabled:hover:border-rose-300"
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

            {/* Read-only profile / account info */}
            <UserDetailCard user={user} />

            {/* ── Security & Sessions (UIX-003-03) ─────────────────────────── */}
            <UserSecurityPanel security={security} />

            {/* ── Activity Timeline (UIX-004) ───────────────────────────────── */}
            <UserActivityPanel userId={user.id} tenantId={user.tenantId} />

            {/* ── Effective Permissions (UIX-005) ────────────────────────────── */}
            <EffectivePermissionsPanel
              result={effectivePerms}
              fetchError={permsError}
            />

            {/* ── Access Explanation (LS-COR-AUT-008) ──────────────────────── */}
            <AccessExplanationPanel
              data={accessDebug}
              fetchError={accessDebugError}
            />

            {/* ── Access Control Management (UIX-003) ──────────────────────── */}
            <div className="space-y-3">
              <div className="flex items-center gap-3">
                <h2 className="text-sm font-semibold text-gray-700 uppercase tracking-wide">
                  Access Control Management
                </h2>
                <div className="flex-1 h-px bg-gray-200" />
                <span className="text-[11px] font-medium text-indigo-600 bg-indigo-50 border border-indigo-200 px-2 py-0.5 rounded uppercase tracking-wide">
                  Editable
                </span>
              </div>
              <p className="text-xs text-gray-500">
                Use the panels below to manage this user's system roles, organization memberships, and group assignments.
                Changes take effect immediately.
              </p>

              <RoleAssignmentPanel
                userId={user.id}
                currentRoles={user.roles ?? []}
                assignableRoles={assignableData?.items}
                userOrgType={assignableData?.userOrgType}
              />

              <OrgMembershipPanel
                userId={user.id}
                memberships={user.memberships ?? []}
                availableOrgs={availableOrgs}
              />

              <AccessGroupMembershipPanel
                tenantId={user.tenantId}
                userId={user.id}
                userMemberships={userAccessGroups}
                allAccessGroups={accessGroupsList}
              />
            </div>
          </>
        )}
      </div>
    </CCShell>
  );
}

// ── Local badge helpers ───────────────────────────────────────────────────────

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

function RoleBadge({ role }: { role: string }) {
  return (
    <span className="inline-flex items-center px-2 py-0.5 rounded text-[11px] font-semibold border bg-indigo-50 text-indigo-700 border-indigo-200">
      {role}
    </span>
  );
}

function TenantBadge({
  tenantId,
  tenantCode,
  tenantDisplayName,
}: {
  tenantId:          string;
  tenantCode:        string;
  tenantDisplayName: string;
}) {
  return (
    <Link
      href={Routes.tenantDetail(tenantId)}
      className="inline-flex items-center gap-1 px-2 py-0.5 rounded text-[11px] font-semibold border bg-gray-50 text-gray-700 border-gray-200 hover:bg-gray-100 transition-colors"
    >
      {tenantDisplayName}
      <span className="font-mono text-[10px] text-gray-400">{tenantCode}</span>
    </Link>
  );
}

function LockedBadge() {
  return (
    <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded text-[11px] font-semibold border bg-red-50 text-red-700 border-red-200">
      <span className="w-1.5 h-1.5 rounded-full bg-red-500 inline-block" />
      Locked
    </span>
  );
}
