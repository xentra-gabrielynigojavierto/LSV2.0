'use client';

import { useState, useEffect } from 'react';
import { useRouter } from 'next/navigation';
import Link from 'next/link';
import type { AccessGroupMember, UserSummary } from '@/types/control-center';
import { Routes } from '@/lib/routes';

interface AccessGroupMembersPanelProps {
  tenantId:       string;
  groupId:        string;
  members:        AccessGroupMember[];
  availableUsers: UserSummary[];
}

function fmtDate(iso: string): string {
  try { return new Date(iso).toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' }); }
  catch { return iso; }
}

export function AccessGroupMembersPanel({ tenantId, groupId, members, availableUsers }: AccessGroupMembersPanelProps) {
  const router = useRouter();

  const [addUserId, setAddUserId] = useState('');
  const [adding, setAdding]       = useState(false);
  const [addError, setAddError]   = useState<string | null>(null);
  const [addOk, setAddOk]         = useState(false);

  const [removeConfirm, setRemoveConfirm] = useState<string | null>(null);
  const [removing, setRemoving]           = useState<string | null>(null);
  const [removeError, setRemoveError]     = useState<string | null>(null);

  useEffect(() => {
    if (!addOk) return;
    const t = setTimeout(() => setAddOk(false), 3000);
    return () => clearTimeout(t);
  }, [addOk]);

  const memberUserIds = new Set(members.map(m => m.userId));
  const usersToAdd = availableUsers.filter(u => !memberUserIds.has(u.id) && u.status === 'Active');

  async function handleAdd() {
    if (!addUserId) return;
    setAdding(true);
    setAddError(null);
    setAddOk(false);
    try {
      const res = await fetch(
        `/api/access-groups/${encodeURIComponent(tenantId)}/${encodeURIComponent(groupId)}/members`,
        { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ userId: addUserId }) },
      );
      if (!res.ok) {
        const data = await res.json().catch(() => ({})) as { message?: string };
        throw new Error(data.message ?? 'Failed to add member.');
      }
      setAddOk(true);
      setAddUserId('');
      router.refresh();
    } catch (err) {
      setAddError(err instanceof Error ? err.message : 'An error occurred.');
    } finally {
      setAdding(false);
    }
  }

  async function handleRemove(userId: string) {
    setRemoving(userId);
    setRemoveError(null);
    try {
      const res = await fetch(
        `/api/access-groups/${encodeURIComponent(tenantId)}/${encodeURIComponent(groupId)}/members/${encodeURIComponent(userId)}`,
        { method: 'DELETE' },
      );
      if (!res.ok) {
        const data = await res.json().catch(() => ({})) as { message?: string };
        throw new Error(data.message ?? 'Failed to remove member.');
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
          <h2 className="text-xs font-semibold uppercase tracking-wide text-gray-500">Members</h2>
          <span className="inline-flex items-center px-1.5 py-0.5 rounded text-[10px] font-semibold border bg-indigo-50 text-indigo-600 border-indigo-200">
            {members.length}
          </span>
        </div>
      </div>

      {members.length > 0 ? (
        <ul className="divide-y divide-gray-100 max-h-64 overflow-y-auto">
          {members.map(m => {
            const user = availableUsers.find(u => u.id === m.userId);
            const displayName = user ? `${user.firstName} ${user.lastName}` : m.userId.slice(0, 8) + '…';
            const displayEmail = user?.email;

            return (
              <li key={m.id} className="flex items-center justify-between px-5 py-2.5 gap-3">
                <div className="flex items-center gap-3 min-w-0">
                  <Link
                    href={Routes.userDetail(m.userId)}
                    className="text-sm font-medium text-indigo-600 hover:text-indigo-800 hover:underline truncate"
                  >
                    {displayName}
                  </Link>
                  {displayEmail && (
                    <span className="text-[11px] text-gray-400 truncate hidden sm:block">{displayEmail}</span>
                  )}
                  <span className="text-[11px] text-gray-400 shrink-0">
                    Added {fmtDate(m.addedAtUtc)}
                  </span>
                </div>

                {removeConfirm === m.userId ? (
                  <span className="inline-flex items-center gap-1 text-xs shrink-0">
                    <span className="text-red-700 font-medium">Remove?</span>
                    <button
                      type="button"
                      disabled={removing === m.userId}
                      onClick={() => handleRemove(m.userId)}
                      className="px-2 py-0.5 rounded bg-red-600 text-white text-[11px] font-medium hover:bg-red-700 disabled:opacity-50 transition-colors"
                    >
                      {removing === m.userId ? '…' : 'Yes'}
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
                    onClick={() => setRemoveConfirm(m.userId)}
                    className="text-xs px-2 py-1 rounded border border-red-200 bg-white text-red-600 hover:bg-red-50 transition-colors disabled:opacity-40 disabled:cursor-not-allowed shrink-0"
                  >
                    Remove
                  </button>
                )}
              </li>
            );
          })}
        </ul>
      ) : (
        <div className="px-5 py-6 text-center">
          <p className="text-sm font-medium text-gray-500">No members</p>
          <p className="text-xs text-gray-400 mt-1">Add users to this group to grant inherited access.</p>
        </div>
      )}

      {removeError && (
        <div className="mx-5 my-2 rounded bg-red-50 border border-red-200 px-3 py-2 text-xs text-red-700">
          {removeError}
        </div>
      )}

      {usersToAdd.length > 0 && (
        <div className="px-5 py-3 border-t border-gray-100 bg-gray-50 flex items-end gap-3 flex-wrap">
          <div className="flex-1 min-w-48">
            <label className="block text-xs font-medium text-gray-600 mb-1">Add member</label>
            <select
              value={addUserId}
              onChange={e => { setAddUserId(e.target.value); setAddOk(false); setAddError(null); }}
              className="w-full h-8 rounded-md border border-gray-300 px-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500 bg-white"
            >
              <option value="">Select user…</option>
              {usersToAdd.map(u => (
                <option key={u.id} value={u.id}>{u.firstName} {u.lastName} ({u.email})</option>
              ))}
            </select>
          </div>
          <button
            type="button"
            disabled={!addUserId || adding}
            onClick={handleAdd}
            className="h-8 px-4 text-sm font-medium text-white bg-indigo-600 hover:bg-indigo-700 rounded-md transition-colors disabled:opacity-40 disabled:cursor-not-allowed"
          >
            {adding ? 'Adding…' : 'Add'}
          </button>
          {addOk && (
            <span className="text-xs text-green-700 font-medium flex items-center gap-1">
              <span className="w-1.5 h-1.5 rounded-full bg-green-500 inline-block" />
              Added.
            </span>
          )}
        </div>
      )}

      {addError && (
        <div className="mx-5 mb-3 rounded bg-red-50 border border-red-200 px-3 py-2 text-xs text-red-700">
          {addError}
        </div>
      )}
    </div>
  );
}
