'use client';

import { useState, useTransition } from 'react';
import { useRouter } from 'next/navigation';
import type { PermissionPolicyMapping } from '@/types/control-center';

interface PolicyPermissionMappingsProps {
  policyId: string;
  mappings: PermissionPolicyMapping[];
}

export function PolicyPermissionMappings({ policyId, mappings }: PolicyPermissionMappingsProps) {
  const router = useRouter();
  const [isPending, startTransition] = useTransition();
  const [showForm, setShowForm] = useState(false);
  const [permissionCode, setPermissionCode] = useState('');
  const [error, setError] = useState<string | null>(null);

  async function handleAdd(e: React.FormEvent) {
    e.preventDefault();
    setError(null);

    try {
      const res = await fetch('/api/permission-policies', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ permissionCode, policyId }),
      });

      if (!res.ok) {
        const data = await res.json().catch(() => ({}));
        setError(data.error || `Failed (${res.status})`);
        return;
      }

      startTransition(() => {
        setShowForm(false);
        setPermissionCode('');
        router.refresh();
      });
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Unknown error');
    }
  }

  async function handleRemove(mappingId: string) {
    try {
      const res = await fetch(`/api/permission-policies/${mappingId}`, { method: 'DELETE' });
      if (!res.ok) return;
      startTransition(() => router.refresh());
    } catch { /* ignore */ }
  }

  return (
    <div className="space-y-3">
      {mappings.length === 0 && !showForm && (
        <div className="text-center py-8 text-gray-500 text-sm">
          No permissions linked to this policy yet.
        </div>
      )}

      {mappings.length > 0 && (
        <div className="space-y-2">
          {mappings.map(m => (
            <div key={m.id} className="flex items-center gap-3 bg-gray-50 border border-gray-200 rounded-lg px-4 py-2.5 text-sm">
              <span className="font-mono text-indigo-700">{m.permissionCode}</span>
              <span className={`inline-flex items-center px-2 py-0.5 rounded text-xs font-medium ${
                m.isActive ? 'bg-green-50 text-green-700' : 'bg-gray-100 text-gray-500'
              }`}>
                {m.isActive ? 'Active' : 'Inactive'}
              </span>
              <span className="flex-1" />
              <span className="text-xs text-gray-400">{new Date(m.createdAtUtc).toLocaleDateString()}</span>
              <button
                onClick={() => handleRemove(m.id)}
                className="text-red-400 hover:text-red-600 text-xs transition-colors"
                disabled={isPending}
              >
                <i className="ri-close-circle-line" />
              </button>
            </div>
          ))}
        </div>
      )}

      {!showForm ? (
        <button
          onClick={() => setShowForm(true)}
          className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-md border border-gray-300 text-gray-700 text-xs font-medium hover:bg-gray-50 transition-colors"
        >
          <i className="ri-link text-sm" />
          Link Permission
        </button>
      ) : (
        <form onSubmit={handleAdd} className="bg-white border border-gray-200 rounded-lg p-4 space-y-3">
          <div>
            <label className="block text-xs font-medium text-gray-700 mb-1">Permission Code</label>
            <input
              type="text"
              value={permissionCode}
              onChange={e => setPermissionCode(e.target.value)}
              placeholder="SYNQ_FUND.application:approve"
              className="w-full px-2.5 py-1.5 border border-gray-300 rounded-md text-sm font-mono"
              required
            />
          </div>

          {error && (
            <div className="bg-red-50 border border-red-200 rounded px-3 py-2 text-xs text-red-700">{error}</div>
          )}

          <div className="flex justify-end gap-2">
            <button type="button" onClick={() => { setShowForm(false); setError(null); }} className="px-3 py-1.5 text-xs text-gray-600">
              Cancel
            </button>
            <button type="submit" disabled={isPending} className="px-4 py-1.5 bg-indigo-600 text-white text-xs font-medium rounded-md hover:bg-indigo-700 disabled:opacity-50">
              {isPending ? 'Linking...' : 'Link'}
            </button>
          </div>
        </form>
      )}
    </div>
  );
}
