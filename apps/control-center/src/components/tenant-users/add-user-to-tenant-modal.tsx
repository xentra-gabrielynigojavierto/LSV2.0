'use client';

import { useState }            from 'react';
import type { RoleSummary }    from '@/types/control-center';
import { addUserToTenantAction } from '@/app/tenants/[id]/users/actions';

interface Props {
  open:        boolean;
  tenantId:    string;
  tenantRoles: RoleSummary[];
  onClose:     () => void;
  onSuccess:   () => void;
}

export function AddUserToTenantModal({
  open,
  tenantId,
  tenantRoles,
  onClose,
  onSuccess,
}: Props) {
  const [userId,  setUserId]  = useState('');
  const [roleKey, setRoleKey] = useState('');
  const [loading, setLoading] = useState(false);
  const [error,   setError]   = useState<string | null>(null);
  const [isConflict, setIsConflict] = useState(false);

  if (!open) return null;

  function handleClose() {
    setUserId('');
    setRoleKey('');
    setError(null);
    setIsConflict(false);
    onClose();
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!userId.trim()) return;
    setLoading(true);
    setError(null);
    setIsConflict(false);
    const result = await addUserToTenantAction({
      tenantId,
      userId: userId.trim(),
      roleKey: roleKey || undefined,
    });
    setLoading(false);
    if (result.success) {
      handleClose();
      onSuccess();
    } else {
      setIsConflict(result.code === 'USER_IN_DIFFERENT_TENANT');
      setError(result.error ?? 'Failed to add user to tenant.');
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center">
      <div className="absolute inset-0 bg-black/40" onClick={handleClose} />
      <div className="relative bg-white rounded-xl shadow-xl w-full max-w-md mx-4 p-6 space-y-4">

        <div className="space-y-1">
          <h2 className="text-base font-semibold text-gray-900">Add Existing User to Tenant</h2>
          <p className="text-sm text-gray-500">
            Enter a user ID to confirm their membership in this tenant and optionally assign a role.
          </p>
        </div>

        {error && (
          <div className={`rounded-lg px-3 py-2 text-sm border ${isConflict ? 'bg-amber-50 border-amber-200 text-amber-800' : 'bg-red-50 border-red-200 text-red-700'}`}>
            {error}
            {isConflict && (
              <p className="mt-1 text-xs">
                Each user has exactly one home tenant. To add a user to this tenant, provision a new account directly in this tenant.
              </p>
            )}
          </div>
        )}

        <form onSubmit={handleSubmit} className="space-y-4">
          <div className="space-y-1.5">
            <label className="block text-xs font-medium text-gray-700">
              User ID <span className="text-red-500">*</span>
            </label>
            <input
              type="text"
              value={userId}
              onChange={e => setUserId(e.target.value)}
              required
              placeholder="xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
              className="w-full text-sm border border-gray-200 rounded-md px-3 py-2 focus:outline-none focus:ring-1 focus:ring-indigo-400 focus:border-indigo-400 font-mono text-gray-900 placeholder-gray-400"
            />
          </div>

          {tenantRoles.length > 0 && (
            <div className="space-y-1.5">
              <label className="block text-xs font-medium text-gray-700">
                Tenant Role <span className="text-gray-400">(optional)</span>
              </label>
              <select
                value={roleKey}
                onChange={e => setRoleKey(e.target.value)}
                className="w-full text-sm border border-gray-200 rounded-md px-3 py-2 focus:outline-none focus:ring-1 focus:ring-indigo-400 focus:border-indigo-400 text-gray-900"
              >
                <option value="">No role</option>
                {tenantRoles.map(r => (
                  <option key={r.id} value={r.name}>{r.name}</option>
                ))}
              </select>
            </div>
          )}

          <div className="flex justify-end gap-2 pt-1">
            <button
              type="button"
              onClick={handleClose}
              className="text-sm px-4 py-2 rounded-md border border-gray-200 text-gray-600 hover:bg-gray-50 transition-colors"
            >
              Cancel
            </button>
            <button
              type="submit"
              disabled={loading || !userId.trim()}
              className="text-sm px-4 py-2 rounded-md bg-indigo-600 hover:bg-indigo-700 text-white font-medium transition-colors disabled:opacity-60 disabled:cursor-not-allowed"
            >
              {loading ? 'Adding…' : 'Add to Tenant'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
