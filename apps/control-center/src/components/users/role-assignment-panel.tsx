'use client';

import { useState, useEffect, useCallback } from 'react';
import { useRouter } from 'next/navigation';
import type { UserRoleSummary, AssignableRole } from '@/types/control-center';

interface RoleAssignmentPanelProps {
  userId: string;
  currentRoles: UserRoleSummary[];
  assignableRoles?: AssignableRole[];
  userOrgType?: string;
}

const ERROR_LABELS: Record<string, string> = {
  PRODUCT_NOT_ENABLED_FOR_TENANT: 'Product not enabled for this tenant. Enable it in tenant settings first.',
  INVALID_ORG_TYPE_FOR_ROLE: 'This user\'s organization type is not eligible for this role.',
  NO_ORGANIZATION_MEMBERSHIP: 'User must belong to an organization before product roles can be assigned.',
};

function translateRoleError(raw: string): string {
  for (const [code, label] of Object.entries(ERROR_LABELS)) {
    if (raw.includes(code)) return label;
  }
  const lower = raw.toLowerCase();
  if (lower.includes('already') || lower.includes('conflict') || lower.includes('duplicate'))
    return 'This role is already assigned to the user.';
  if (lower.includes('not found') || lower.includes('invalid'))
    return 'Role not found — it may have been removed. Refresh and try again.';
  return raw;
}

export function RoleAssignmentPanel({
  userId,
  currentRoles,
  assignableRoles: initialAssignableRoles,
  userOrgType,
}: RoleAssignmentPanelProps) {
  const router = useRouter();

  const [assignableRoles, setAssignableRoles] = useState<AssignableRole[]>(initialAssignableRoles ?? []);
  const [loading, setLoading] = useState(!initialAssignableRoles);

  const [assigningRoleId, setAssigningRoleId] = useState('');
  const [assigning, setAssigning] = useState(false);
  const [assignError, setAssignError] = useState<string | null>(null);
  const [assignOk, setAssignOk] = useState(false);

  const [revokingId, setRevokingId] = useState<string | null>(null);
  const [revokeConfirm, setRevokeConfirm] = useState<string | null>(null);
  const [revokeError, setRevokeError] = useState<string | null>(null);

  const [fetchError, setFetchError] = useState<string | null>(null);

  const fetchAssignableRoles = useCallback(async () => {
    try {
      setLoading(true);
      setFetchError(null);
      const res = await fetch(`/api/identity/admin/users/${encodeURIComponent(userId)}/assignable-roles`);
      if (!res.ok) {
        setFetchError('Failed to load assignable roles.');
        return;
      }
      const data = await res.json();
      if (Array.isArray(data.items)) setAssignableRoles(data.items);
    } catch {
      setFetchError('Failed to load assignable roles.');
    }
    finally { setLoading(false); }
  }, [userId]);

  useEffect(() => {
    if (!initialAssignableRoles) fetchAssignableRoles();
  }, [initialAssignableRoles, fetchAssignableRoles]);

  useEffect(() => {
    if (!assignOk) return;
    const t = setTimeout(() => setAssignOk(false), 3000);
    return () => clearTimeout(t);
  }, [assignOk]);

  const assignedRoleIds = new Set(currentRoles.map(r => r.roleId));

  const systemRoles = assignableRoles.filter(r => r.isSystemRole && !r.isAssigned);
  const productRolesEligible = assignableRoles.filter(r => r.isProductRole && r.assignable && !r.isAssigned);
  const productRolesIneligible = assignableRoles.filter(r => r.isProductRole && !r.assignable && !r.isAssigned);

  const selectableRoles = [...systemRoles, ...productRolesEligible];

  async function handleAssign() {
    if (!assigningRoleId) return;
    setAssigning(true);
    setAssignError(null);
    setAssignOk(false);
    try {
      const res = await fetch(
        `/api/identity/admin/users/${encodeURIComponent(userId)}/roles`,
        {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ roleId: assigningRoleId }),
        },
      );
      if (!res.ok) {
        const body = await res.json().catch(() => ({})) as { message?: string };
        throw new Error(body.message ?? 'Failed to assign role.');
      }
      setAssignOk(true);
      setAssigningRoleId('');
      await fetchAssignableRoles();
      router.refresh();
    } catch (err) {
      setAssignError(translateRoleError(err instanceof Error ? err.message : 'An error occurred.'));
    } finally {
      setAssigning(false);
    }
  }

  async function handleRevoke(roleId: string) {
    setRevokingId(roleId);
    setRevokeError(null);
    try {
      const res = await fetch(
        `/api/identity/admin/users/${encodeURIComponent(userId)}/roles/${encodeURIComponent(roleId)}`,
        { method: 'DELETE' },
      );
      if (!res.ok) {
        const body = await res.json().catch(() => ({})) as { message?: string };
        throw new Error(body.message ?? 'Failed to revoke role.');
      }
      await fetchAssignableRoles();
      router.refresh();
    } catch (err) {
      setRevokeError(translateRoleError(err instanceof Error ? err.message : 'An error occurred.'));
    } finally {
      setRevokingId(null);
      setRevokeConfirm(null);
    }
  }

  const roleCount = currentRoles.length;
  const selectedRole = assignableRoles.find(r => r.id === assigningRoleId);

  return (
    <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
      <div className="px-5 py-3 border-b border-gray-100 bg-gray-50 flex items-center justify-between">
        <div className="flex items-center gap-2">
          <h2 className="text-xs font-semibold uppercase tracking-wide text-gray-500">
            Role Management
          </h2>
          <span className="inline-flex items-center px-1.5 py-0.5 rounded text-[10px] font-semibold border bg-indigo-50 text-indigo-600 border-indigo-200">
            {roleCount} assigned
          </span>
        </div>
        <div className="flex items-center gap-2">
          {userOrgType && (
            <span className="text-[10px] font-mono text-gray-400 bg-gray-100 px-1.5 py-0.5 rounded border border-gray-200">
              {userOrgType}
            </span>
          )}
          <span className="text-[11px] text-gray-400 font-medium">GLOBAL scope</span>
        </div>
      </div>

      <div className="px-5 py-2 bg-gray-50 border-b border-gray-100 text-[11px] text-gray-400">
        System roles grant platform-wide access. Product roles require matching org type and product enablement.
      </div>

      {fetchError && (
        <div className="mx-5 my-2 rounded bg-amber-50 border border-amber-200 px-3 py-2 text-xs text-amber-700">
          {fetchError}
        </div>
      )}

      {loading ? (
        <div className="px-5 py-6 text-center text-sm text-gray-400">Loading roles...</div>
      ) : (
        <>
          {currentRoles.length > 0 ? (
            <ul className="divide-y divide-gray-100">
              {currentRoles.map(r => {
                const meta = assignableRoles.find(ar => ar.id === r.roleId);
                const isProductRole = meta?.isProductRole ?? false;
                return (
                  <li key={r.assignmentId} className="flex items-center justify-between px-5 py-2.5 gap-3">
                    <div className="flex items-center gap-2 min-w-0">
                      <span className={`inline-flex items-center gap-1.5 px-2 py-0.5 rounded text-[11px] font-semibold border ${
                        isProductRole
                          ? 'bg-purple-50 text-purple-700 border-purple-200'
                          : 'bg-indigo-50 text-indigo-700 border-indigo-200'
                      }`}>
                        <span className={`w-1.5 h-1.5 rounded-full inline-block ${isProductRole ? 'bg-purple-500' : 'bg-indigo-500'}`} />
                        {r.roleName}
                      </span>
                      {isProductRole && meta?.productName && (
                        <span className="text-[10px] text-purple-500 font-medium truncate">
                          {meta.productName}
                        </span>
                      )}
                      <span className="font-mono text-[10px] text-gray-400">{r.roleId.slice(0, 8)}…</span>
                    </div>

                    {revokeConfirm === r.roleId ? (
                      <span className="inline-flex items-center gap-1 text-xs shrink-0">
                        <span className="text-red-700 font-medium">Revoke?</span>
                        <button
                          type="button"
                          disabled={revokingId === r.roleId}
                          onClick={() => handleRevoke(r.roleId)}
                          className="px-2 py-0.5 rounded bg-red-600 text-white text-[11px] font-medium hover:bg-red-700 disabled:opacity-50 transition-colors"
                        >
                          {revokingId === r.roleId ? '…' : 'Yes'}
                        </button>
                        <button
                          type="button"
                          onClick={() => setRevokeConfirm(null)}
                          className="px-2 py-0.5 rounded border border-gray-200 bg-white text-gray-500 text-[11px] hover:bg-gray-50 transition-colors"
                        >
                          Cancel
                        </button>
                      </span>
                    ) : (
                      <button
                        type="button"
                        disabled={revokingId !== null}
                        onClick={() => setRevokeConfirm(r.roleId)}
                        className="text-xs px-2 py-1 rounded border border-red-200 bg-white text-red-600 hover:bg-red-50 transition-colors disabled:opacity-40 disabled:cursor-not-allowed shrink-0"
                      >
                        Revoke
                      </button>
                    )}
                  </li>
                );
              })}
            </ul>
          ) : (
            <div className="px-5 py-6 text-center">
              <p className="text-sm font-medium text-gray-500">No roles assigned</p>
              <p className="text-xs text-gray-400 mt-1">
                Assign a role below to grant this user access.
              </p>
            </div>
          )}

          {revokeError && (
            <div className="mx-5 my-2 rounded bg-red-50 border border-red-200 px-3 py-2 text-xs text-red-700">
              {revokeError}
            </div>
          )}

          {selectableRoles.length > 0 && (
            <div className="px-5 py-3 border-t border-gray-100 bg-gray-50 space-y-3">
              <div className="flex items-end gap-3 flex-wrap">
                <div className="flex-1 min-w-48">
                  <label className="block text-xs font-medium text-gray-600 mb-1">Assign role</label>
                  <select
                    value={assigningRoleId}
                    onChange={e => { setAssigningRoleId(e.target.value); setAssignOk(false); setAssignError(null); }}
                    className="w-full h-8 rounded-md border border-gray-300 px-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500 bg-white"
                  >
                    <option value="">Select role…</option>
                    {systemRoles.length > 0 && (
                      <optgroup label="System Roles">
                        {systemRoles.map(r => (
                          <option key={r.id} value={r.id}>{r.name}</option>
                        ))}
                      </optgroup>
                    )}
                    {productRolesEligible.length > 0 && (
                      <optgroup label="Product Roles (Eligible)">
                        {productRolesEligible.map(r => (
                          <option key={r.id} value={r.id}>
                            {r.name} — {r.productName}
                          </option>
                        ))}
                      </optgroup>
                    )}
                  </select>
                </div>
                <button
                  type="button"
                  disabled={!assigningRoleId || assigning}
                  onClick={handleAssign}
                  className="h-8 px-4 text-sm font-medium text-white bg-indigo-600 hover:bg-indigo-700 rounded-md transition-colors disabled:opacity-40 disabled:cursor-not-allowed"
                >
                  {assigning ? 'Assigning…' : 'Assign Role'}
                </button>
                {assignOk && (
                  <span className="text-xs text-green-700 font-medium flex items-center gap-1">
                    <span className="w-1.5 h-1.5 rounded-full bg-green-500 inline-block" />
                    Role assigned.
                  </span>
                )}
              </div>

              {selectedRole?.isProductRole && (
                <div className="rounded bg-purple-50 border border-purple-200 px-3 py-2 text-[11px] text-purple-700">
                  <span className="font-semibold">Product Role:</span>{' '}
                  {selectedRole.productName} — requires org type{' '}
                  {selectedRole.allowedOrgTypes?.join(', ') || 'any'}.
                </div>
              )}
            </div>
          )}

          {assignError && (
            <div className="mx-5 mb-3 rounded bg-red-50 border border-red-200 px-3 py-2 text-xs text-red-700">
              {assignError}
            </div>
          )}

          {productRolesIneligible.length > 0 && (
            <div className="px-5 py-3 border-t border-gray-100">
              <p className="text-[11px] font-semibold text-gray-400 uppercase tracking-wide mb-2">
                Ineligible Product Roles
              </p>
              <div className="space-y-1.5">
                {productRolesIneligible.map(r => (
                  <div key={r.id} className="flex items-center gap-2 text-[11px]">
                    <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded border bg-gray-100 text-gray-400 border-gray-200">
                      <span className="w-1.5 h-1.5 rounded-full bg-gray-300 inline-block" />
                      {r.name}
                    </span>
                    <span className="text-gray-400 truncate">
                      {r.disabledReason}
                    </span>
                  </div>
                ))}
              </div>
            </div>
          )}

          {selectableRoles.length === 0 && currentRoles.length > 0 && productRolesIneligible.length === 0 && (
            <p className="px-5 py-2 text-xs text-gray-400 italic border-t border-gray-100">
              All available roles are already assigned to this user.
            </p>
          )}
        </>
      )}
    </div>
  );
}
