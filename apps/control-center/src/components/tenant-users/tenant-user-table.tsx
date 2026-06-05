'use client';

import { useState }             from 'react';
import { useRouter }            from 'next/navigation';
import type { TenantUserSummary, RoleSummary } from '@/types/control-center';
import { AssignTenantRoleModal }    from './assign-tenant-role-modal';
import { RemoveUserFromTenantButton } from './remove-user-from-tenant-button';
import { removeTenantUserRoleAction } from '@/app/tenants/[id]/users/actions';

interface Props {
  tenantId:    string;
  users:       TenantUserSummary[];
  totalCount:  number;
  page:        number;
  pageSize:    number;
  hasFilters:  boolean;
  tenantRoles: RoleSummary[];
}

export function TenantUserTable({
  tenantId,
  users,
  totalCount,
  page,
  pageSize,
  hasFilters,
  tenantRoles,
}: Props) {
  const router                        = useRouter();
  const [assignTarget, setAssignTarget] = useState<TenantUserSummary | null>(null);
  const [removingRole, setRemovingRole] = useState<string | null>(null);

  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));

  async function handleRemoveRole(userId: string, assignmentId: string) {
    setRemovingRole(assignmentId);
    try {
      await removeTenantUserRoleAction({ tenantId, userId, assignmentId });
      router.refresh();
    } finally {
      setRemovingRole(null);
    }
  }

  if (users.length === 0) {
    return (
      <div className="bg-white border border-gray-200 rounded-lg px-6 py-12 text-center space-y-2">
        <p className="text-sm font-medium text-gray-600">
          {hasFilters ? 'No users match your search.' : 'No users in this tenant yet.'}
        </p>
        {hasFilters && (
          <a href="?" className="text-xs text-indigo-600 hover:underline">Clear filters</a>
        )}
      </div>
    );
  }

  return (
    <>
      <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
        <table className="w-full text-sm min-w-[640px]">
          <thead>
            <tr className="border-b border-gray-100 bg-gray-50">
              <th className="text-left px-4 py-2.5 text-xs font-semibold text-gray-500 uppercase tracking-wider">User</th>
              <th className="text-left px-4 py-2.5 text-xs font-semibold text-gray-500 uppercase tracking-wider">Type</th>
              <th className="text-left px-4 py-2.5 text-xs font-semibold text-gray-500 uppercase tracking-wider">Status</th>
              <th className="text-left px-4 py-2.5 text-xs font-semibold text-gray-500 uppercase tracking-wider">Tenant Roles</th>
              <th className="text-left px-4 py-2.5 text-xs font-semibold text-gray-500 uppercase tracking-wider">Last Login</th>
              <th className="px-4 py-2.5" />
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-100">
            {users.map(u => (
              <tr key={u.userId} className="hover:bg-gray-50 transition-colors">

                {/* User name + email */}
                <td className="px-4 py-3">
                  <div className="font-medium text-gray-900 truncate max-w-[200px]">{u.displayName}</div>
                  <div className="text-xs text-gray-400 truncate max-w-[200px]">{u.email}</div>
                </td>

                {/* User type */}
                <td className="px-4 py-3">
                  <span className="inline-flex px-2 py-0.5 rounded text-[11px] font-medium bg-gray-100 text-gray-600">
                    {u.userType}
                  </span>
                </td>

                {/* Status */}
                <td className="px-4 py-3">
                  <StatusBadge isActive={u.isActive} />
                </td>

                {/* Tenant roles */}
                <td className="px-4 py-3">
                  <div className="flex flex-wrap gap-1.5 max-w-[260px]">
                    {u.roles.length === 0 ? (
                      <span className="text-xs text-gray-400 italic">No roles</span>
                    ) : (
                      u.roles.map(r => (
                        <span
                          key={r.assignmentId}
                          className="inline-flex items-center gap-1 px-2 py-0.5 rounded bg-indigo-50 text-indigo-700 text-[11px] font-medium border border-indigo-100"
                        >
                          {r.roleName}
                          <button
                            type="button"
                            disabled={removingRole === r.assignmentId}
                            onClick={() => handleRemoveRole(u.userId, r.assignmentId)}
                            className="ml-0.5 text-indigo-400 hover:text-red-500 transition-colors disabled:opacity-40"
                            title={`Remove role ${r.roleName}`}
                          >
                            {removingRole === r.assignmentId ? (
                              <span className="h-3 w-3 inline-block animate-spin border border-current border-t-transparent rounded-full" />
                            ) : (
                              <svg className="h-3 w-3" viewBox="0 0 20 20" fill="currentColor">
                                <path fillRule="evenodd" d="M4.293 4.293a1 1 0 011.414 0L10 8.586l4.293-4.293a1 1 0 111.414 1.414L11.414 10l4.293 4.293a1 1 0 01-1.414 1.414L10 11.414l-4.293 4.293a1 1 0 01-1.414-1.414L8.586 10 4.293 5.707a1 1 0 010-1.414z" clipRule="evenodd" />
                              </svg>
                            )}
                          </button>
                        </span>
                      ))
                    )}
                  </div>
                </td>

                {/* Last login */}
                <td className="px-4 py-3 text-xs text-gray-500 whitespace-nowrap">
                  {u.lastLoginAtUtc ? formatDate(u.lastLoginAtUtc) : '—'}
                </td>

                {/* Actions */}
                <td className="px-4 py-3">
                  <div className="flex items-center gap-2 justify-end">
                    <button
                      type="button"
                      onClick={() => setAssignTarget(u)}
                      className="text-xs px-2.5 py-1 rounded border border-indigo-200 bg-indigo-50 text-indigo-700 hover:bg-indigo-100 transition-colors whitespace-nowrap"
                    >
                      Assign Role
                    </button>
                    <RemoveUserFromTenantButton
                      tenantId={tenantId}
                      userId={u.userId}
                      displayName={u.displayName}
                      onSuccess={() => router.refresh()}
                    />
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {/* Pagination */}
      {totalPages > 1 && (
        <div className="flex items-center justify-between pt-1">
          <p className="text-xs text-gray-400">
            Page {page} of {totalPages} — {totalCount} user{totalCount !== 1 ? 's' : ''}
          </p>
          <div className="flex gap-1">
            {page > 1 && (
              <a
                href={`?page=${page - 1}`}
                className="text-xs px-3 py-1 rounded border border-gray-200 text-gray-600 hover:bg-gray-50"
              >
                ← Prev
              </a>
            )}
            {page < totalPages && (
              <a
                href={`?page=${page + 1}`}
                className="text-xs px-3 py-1 rounded border border-gray-200 text-gray-600 hover:bg-gray-50"
              >
                Next →
              </a>
            )}
          </div>
        </div>
      )}

      {/* Assign role modal */}
      {assignTarget && (
        <AssignTenantRoleModal
          open
          tenantId={tenantId}
          user={assignTarget}
          tenantRoles={tenantRoles}
          onClose={() => setAssignTarget(null)}
          onSuccess={() => { setAssignTarget(null); router.refresh(); }}
        />
      )}
    </>
  );
}

function StatusBadge({ isActive }: { isActive: boolean }) {
  return isActive ? (
    <span className="inline-flex px-2 py-0.5 rounded text-[11px] font-semibold bg-green-50 text-green-700 border border-green-200">
      Active
    </span>
  ) : (
    <span className="inline-flex px-2 py-0.5 rounded text-[11px] font-semibold bg-gray-100 text-gray-500 border border-gray-200">
      Inactive
    </span>
  );
}

function formatDate(iso: string): string {
  try {
    return new Date(iso).toLocaleDateString('en-US', {
      month: 'short', day: 'numeric', year: 'numeric',
    });
  } catch {
    return iso;
  }
}
