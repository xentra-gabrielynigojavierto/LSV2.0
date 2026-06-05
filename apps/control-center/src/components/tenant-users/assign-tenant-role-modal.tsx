'use client';

import { useState }            from 'react';
import type { TenantUserSummary, RoleSummary } from '@/types/control-center';
import { assignTenantRoleAction }  from '@/app/tenants/[id]/users/actions';

interface Props {
  open:        boolean;
  tenantId:    string;
  user:        TenantUserSummary;
  tenantRoles: RoleSummary[];
  onClose:     () => void;
  onSuccess:   () => void;
}

export function AssignTenantRoleModal({
  open,
  tenantId,
  user,
  tenantRoles,
  onClose,
  onSuccess,
}: Props) {
  const [roleId,   setRoleId]   = useState('');
  const [loading,  setLoading]  = useState(false);
  const [error,    setError]    = useState<string | null>(null);

  if (!open) return null;

  const assignedIds = new Set(user.roles.map(r => r.roleId));
  const available   = tenantRoles.filter(r => !assignedIds.has(r.id));

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!roleId) return;
    setLoading(true);
    setError(null);
    const result = await assignTenantRoleAction({ tenantId, userId: user.userId, roleId });
    setLoading(false);
    if (result.success) {
      onSuccess();
    } else {
      setError(result.error ?? 'Failed to assign role.');
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center">
      <div className="absolute inset-0 bg-black/40" onClick={onClose} />
      <div className="relative bg-white rounded-xl shadow-xl w-full max-w-md mx-4 p-6 space-y-4">

        <div className="space-y-1">
          <h2 className="text-base font-semibold text-gray-900">Assign Tenant Role</h2>
          <p className="text-sm text-gray-500">
            Assign a tenant-scoped role to{' '}
            <span className="font-medium text-gray-800">{user.displayName}</span>
          </p>
        </div>

        {error && (
          <div className="bg-red-50 border border-red-200 rounded-lg px-3 py-2 text-sm text-red-700">
            {error}
          </div>
        )}

        {available.length === 0 ? (
          <div className="bg-amber-50 border border-amber-200 rounded-lg px-3 py-2 text-sm text-amber-700">
            All available tenant roles are already assigned to this user.
          </div>
        ) : (
          <form onSubmit={handleSubmit} className="space-y-4">
            <div className="space-y-1.5">
              <label className="block text-xs font-medium text-gray-700">
                Tenant Role
              </label>
              <select
                value={roleId}
                onChange={e => setRoleId(e.target.value)}
                required
                className="w-full text-sm border border-gray-200 rounded-md px-3 py-2 focus:outline-none focus:ring-1 focus:ring-indigo-400 focus:border-indigo-400 text-gray-900"
              >
                <option value="">Select a role…</option>
                {available.map(r => (
                  <option key={r.id} value={r.id}>
                    {r.name}{r.description ? ` — ${r.description}` : ''}
                  </option>
                ))}
              </select>
            </div>

            <div className="flex justify-end gap-2 pt-1">
              <button
                type="button"
                onClick={onClose}
                className="text-sm px-4 py-2 rounded-md border border-gray-200 text-gray-600 hover:bg-gray-50 transition-colors"
              >
                Cancel
              </button>
              <button
                type="submit"
                disabled={loading || !roleId}
                className="text-sm px-4 py-2 rounded-md bg-indigo-600 hover:bg-indigo-700 text-white font-medium transition-colors disabled:opacity-60 disabled:cursor-not-allowed"
              >
                {loading ? 'Assigning…' : 'Assign Role'}
              </button>
            </div>
          </form>
        )}

        {available.length === 0 && (
          <div className="flex justify-end">
            <button
              type="button"
              onClick={onClose}
              className="text-sm px-4 py-2 rounded-md border border-gray-200 text-gray-600 hover:bg-gray-50 transition-colors"
            >
              Close
            </button>
          </div>
        )}
      </div>
    </div>
  );
}
