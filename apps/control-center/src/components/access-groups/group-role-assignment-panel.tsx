'use client';

import { useState, useEffect } from 'react';
import { useRouter } from 'next/navigation';
import type { GroupRoleAssignment } from '@/types/control-center';

interface GroupRoleAssignmentPanelProps {
  tenantId: string;
  groupId:  string;
  roles:    GroupRoleAssignment[];
}

function fmtDate(iso: string): string {
  try { return new Date(iso).toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' }); }
  catch { return iso; }
}

export function GroupRoleAssignmentPanel({ tenantId, groupId, roles }: GroupRoleAssignmentPanelProps) {
  const router = useRouter();

  const [roleCode, setRoleCode]         = useState('');
  const [roleProdCode, setRoleProdCode] = useState('');
  const [assigning, setAssigning]       = useState(false);
  const [assignError, setAssignError]   = useState<string | null>(null);
  const [assignOk, setAssignOk]         = useState(false);

  const [removeConfirm, setRemoveConfirm] = useState<string | null>(null);
  const [removing, setRemoving]           = useState<string | null>(null);
  const [removeError, setRemoveError]     = useState<string | null>(null);

  useEffect(() => {
    if (!assignOk) return;
    const t = setTimeout(() => setAssignOk(false), 3000);
    return () => clearTimeout(t);
  }, [assignOk]);

  async function handleAssign() {
    if (!roleCode.trim()) return;
    setAssigning(true);
    setAssignError(null);
    setAssignOk(false);
    try {
      const body: Record<string, string> = { roleCode: roleCode.trim() };
      if (roleProdCode.trim()) body.productCode = roleProdCode.trim();

      const res = await fetch(
        `/api/access-groups/${encodeURIComponent(tenantId)}/${encodeURIComponent(groupId)}/roles`,
        { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) },
      );
      if (!res.ok) {
        const data = await res.json().catch(() => ({})) as { message?: string };
        throw new Error(data.message ?? 'Failed to assign role.');
      }
      setAssignOk(true);
      setRoleCode('');
      setRoleProdCode('');
      router.refresh();
    } catch (err) {
      setAssignError(err instanceof Error ? err.message : 'An error occurred.');
    } finally {
      setAssigning(false);
    }
  }

  async function handleRemove(assignmentId: string) {
    setRemoving(assignmentId);
    setRemoveError(null);
    try {
      const res = await fetch(
        `/api/access-groups/${encodeURIComponent(tenantId)}/${encodeURIComponent(groupId)}/roles/${encodeURIComponent(assignmentId)}`,
        { method: 'DELETE' },
      );
      if (!res.ok) {
        const data = await res.json().catch(() => ({})) as { message?: string };
        throw new Error(data.message ?? 'Failed to remove role.');
      }
      router.refresh();
    } catch (err) {
      setRemoveError(err instanceof Error ? err.message : 'An error occurred.');
    } finally {
      setRemoving(null);
      setRemoveConfirm(null);
    }
  }

  return (
    <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
      <div className="px-5 py-3 border-b border-gray-100 bg-gray-50 flex items-center justify-between">
        <div className="flex items-center gap-2">
          <h2 className="text-xs font-semibold uppercase tracking-wide text-gray-500">Role Assignments</h2>
          <span className="inline-flex items-center px-1.5 py-0.5 rounded text-[10px] font-semibold border bg-orange-50 text-orange-600 border-orange-200">
            {roles.length}
          </span>
        </div>
        <span className="text-[11px] text-gray-400">Inherited by all members</span>
      </div>

      {roles.length > 0 ? (
        <ul className="divide-y divide-gray-100">
          {roles.map(r => (
            <li key={r.id} className="flex items-center justify-between px-5 py-2.5 gap-3">
              <div className="flex items-center gap-3 flex-wrap">
                <span className="font-mono text-sm font-medium text-gray-800">{r.roleCode}</span>
                {r.productCode && (
                  <span className="inline-flex items-center px-1.5 py-0.5 rounded text-[10px] font-medium border bg-purple-50 text-purple-600 border-purple-200">
                    {r.productCode}
                  </span>
                )}
                <span className="text-[11px] text-gray-400">
                  Assigned {fmtDate(r.assignedAtUtc)}
                </span>
              </div>

              {removeConfirm === r.id ? (
                <span className="inline-flex items-center gap-1 text-xs shrink-0">
                  <span className="text-red-700 font-medium">Remove?</span>
                  <button
                    type="button"
                    disabled={removing === r.id}
                    onClick={() => handleRemove(r.id)}
                    className="px-2 py-0.5 rounded bg-red-600 text-white text-[11px] font-medium hover:bg-red-700 disabled:opacity-50 transition-colors"
                  >
                    {removing === r.id ? '…' : 'Yes'}
                  </button>
                  <button
                    type="button"
                    onClick={() => setRemoveConfirm(null)}
                    className="px-2 py-0.5 rounded border border-gray-200 bg-white text-gray-500 text-[11px] hover:bg-gray-50 transition-colors"
                  >
                    Cancel
                  </button>
                </span>
              ) : (
                <button
                  type="button"
                  disabled={removing !== null}
                  onClick={() => setRemoveConfirm(r.id)}
                  className="text-xs px-2 py-1 rounded border border-red-200 bg-white text-red-600 hover:bg-red-50 transition-colors disabled:opacity-40 disabled:cursor-not-allowed shrink-0"
                >
                  Remove
                </button>
              )}
            </li>
          ))}
        </ul>
      ) : (
        <div className="px-5 py-6 text-center">
          <p className="text-sm font-medium text-gray-500">No role assignments</p>
          <p className="text-xs text-gray-400 mt-1">Assign a role to give all members inherited role access.</p>
        </div>
      )}

      {removeError && (
        <div className="mx-5 my-2 rounded bg-red-50 border border-red-200 px-3 py-2 text-xs text-red-700">
          {removeError}
        </div>
      )}

      <div className="px-5 py-3 border-t border-gray-100 bg-gray-50 space-y-2">
        <p className="text-xs font-medium text-gray-600">Assign role</p>
        <div className="flex items-end gap-3 flex-wrap">
          <div className="flex-1 min-w-36">
            <label className="block text-[11px] text-gray-500 mb-0.5">Role Code *</label>
            <input
              type="text"
              value={roleCode}
              onChange={e => { setRoleCode(e.target.value); setAssignOk(false); setAssignError(null); }}
              placeholder="e.g. ClaimsReviewer"
              className="w-full h-8 rounded-md border border-gray-300 px-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
            />
          </div>
          <div className="w-32">
            <label className="block text-[11px] text-gray-500 mb-0.5">Product (opt.)</label>
            <input
              type="text"
              value={roleProdCode}
              onChange={e => setRoleProdCode(e.target.value)}
              placeholder="e.g. FUND"
              className="w-full h-8 rounded-md border border-gray-300 px-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
            />
          </div>
          <button
            type="button"
            disabled={!roleCode.trim() || assigning}
            onClick={handleAssign}
            className="h-8 px-4 text-sm font-medium text-white bg-orange-600 hover:bg-orange-700 rounded-md transition-colors disabled:opacity-40 disabled:cursor-not-allowed"
          >
            {assigning ? 'Assigning…' : 'Assign'}
          </button>
          {assignOk && (
            <span className="text-xs text-green-700 font-medium flex items-center gap-1">
              <span className="w-1.5 h-1.5 rounded-full bg-green-500 inline-block" />
              Assigned.
            </span>
          )}
        </div>
      </div>

      {assignError && (
        <div className="mx-5 mb-3 rounded bg-red-50 border border-red-200 px-3 py-2 text-xs text-red-700">
          {assignError}
        </div>
      )}
    </div>
  );
}
