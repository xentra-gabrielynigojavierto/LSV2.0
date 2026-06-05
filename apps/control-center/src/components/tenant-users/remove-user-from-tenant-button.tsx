'use client';

import { useState } from 'react';
import { removeUserFromTenantAction } from '@/app/tenants/[id]/users/actions';

interface Props {
  tenantId:    string;
  userId:      string;
  displayName: string;
  onSuccess:   () => void;
}

export function RemoveUserFromTenantButton({
  tenantId,
  userId,
  displayName,
  onSuccess,
}: Props) {
  const [confirming, setConfirming] = useState(false);
  const [loading,    setLoading]    = useState(false);
  const [error,      setError]      = useState<string | null>(null);

  async function handleConfirm() {
    setLoading(true);
    setError(null);
    const result = await removeUserFromTenantAction({ tenantId, userId });
    setLoading(false);
    if (result.success) {
      setConfirming(false);
      onSuccess();
    } else {
      setError(result.error ?? 'Failed to remove tenant access.');
    }
  }

  if (confirming) {
    return (
      <div className="fixed inset-0 z-50 flex items-center justify-center">
        <div className="absolute inset-0 bg-black/40" onClick={() => setConfirming(false)} />
        <div className="relative bg-white rounded-xl shadow-xl w-full max-w-sm mx-4 p-6 space-y-4">
          <div className="space-y-1">
            <h2 className="text-base font-semibold text-gray-900">Remove Tenant Access</h2>
            <p className="text-sm text-gray-500">
              This will revoke all tenant-scoped roles for{' '}
              <span className="font-medium text-gray-800">{displayName}</span>.
              Their global account is not deleted.
            </p>
          </div>
          {error && (
            <div className="bg-red-50 border border-red-200 rounded-lg px-3 py-2 text-sm text-red-700">
              {error}
            </div>
          )}
          <div className="flex justify-end gap-2">
            <button
              type="button"
              onClick={() => setConfirming(false)}
              className="text-sm px-4 py-2 rounded-md border border-gray-200 text-gray-600 hover:bg-gray-50 transition-colors"
            >
              Cancel
            </button>
            <button
              type="button"
              onClick={handleConfirm}
              disabled={loading}
              className="text-sm px-4 py-2 rounded-md bg-red-600 hover:bg-red-700 text-white font-medium transition-colors disabled:opacity-60 disabled:cursor-not-allowed"
            >
              {loading ? 'Removing…' : 'Remove Access'}
            </button>
          </div>
        </div>
      </div>
    );
  }

  return (
    <button
      type="button"
      onClick={() => setConfirming(true)}
      className="text-xs px-2.5 py-1 rounded border border-red-200 bg-red-50 text-red-700 hover:bg-red-100 transition-colors whitespace-nowrap"
    >
      Remove Access
    </button>
  );
}
