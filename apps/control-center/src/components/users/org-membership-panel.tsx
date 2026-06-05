'use client';

/**
 * OrgMembershipPanel — UIX-003 / UIX-003-02
 *
 * Shows current organization memberships for a user and allows
 * adding / removing / setting primary via live BFF endpoints.
 *
 * BFF routes:
 *   POST   /api/identity/admin/users/{id}/memberships                            → assign
 *   POST   /api/identity/admin/users/{id}/memberships/{membershipId}/set-primary → set primary
 *   DELETE /api/identity/admin/users/{id}/memberships/{membershipId}             → remove
 *
 * Safety rules enforced by backend:
 *   - Cannot remove the last active membership (409 LAST_MEMBERSHIP)
 *   - Cannot remove primary membership while others exist (409 PRIMARY_MEMBERSHIP)
 */

import { useState, useEffect }      from 'react';
import { useRouter }                from 'next/navigation';
import type { OrgMembershipSummary, OrgSummary } from '@/types/control-center';

const MEMBER_ROLES = ['Member', 'Admin', 'Billing', 'ReadOnly'];

interface OrgMembershipPanelProps {
  userId:         string;
  memberships:    OrgMembershipSummary[];
  availableOrgs:  OrgSummary[];
}

/** Translate backend membership conflict codes into admin-friendly messages. */
function translateMembershipError(raw: string): string {
  const lower = raw.toLowerCase();
  if (lower.includes('last_membership') || lower.includes('last membership'))
    return 'Cannot remove the last membership. Add the user to another organization first.';
  if (lower.includes('primary_membership') || lower.includes('primary membership'))
    return 'This is the primary organization. Set another org as primary before removing this one.';
  if (lower.includes('already') || lower.includes('duplicate'))
    return 'User is already a member of this organization.';
  return raw;
}

export function OrgMembershipPanel({
  userId,
  memberships,
  availableOrgs,
}: OrgMembershipPanelProps) {
  const router = useRouter();

  const [addOrgId,    setAddOrgId]    = useState('');
  const [addRole,     setAddRole]     = useState('Member');
  const [adding,      setAdding]      = useState(false);
  const [addError,    setAddError]    = useState<string | null>(null);
  const [addOk,       setAddOk]       = useState(false);

  const [settingPrimary, setSettingPrimary] = useState<string | null>(null);
  const [primaryError,   setPrimaryError]   = useState<string | null>(null);

  const [removeConfirm, setRemoveConfirm] = useState<string | null>(null);
  const [removing,      setRemoving]      = useState<string | null>(null);
  const [removeError,   setRemoveError]   = useState<string | null>(null);

  /* Auto-dismiss add success after 3 s */
  useEffect(() => {
    if (!addOk) return;
    const t = setTimeout(() => setAddOk(false), 3000);
    return () => clearTimeout(t);
  }, [addOk]);

  const memberOrgIds  = new Set(memberships.map(m => m.organizationId));
  const availableToAdd = availableOrgs.filter(o => !memberOrgIds.has(o.id) && o.isActive);

  async function handleAdd() {
    if (!addOrgId) return;
    setAdding(true);
    setAddError(null);
    setAddOk(false);
    try {
      const res = await fetch(
        `/api/identity/admin/users/${encodeURIComponent(userId)}/memberships`,
        {
          method:  'POST',
          headers: { 'Content-Type': 'application/json' },
          body:    JSON.stringify({ organizationId: addOrgId, memberRole: addRole }),
        },
      );
      if (!res.ok) {
        const body = await res.json().catch(() => ({})) as { message?: string };
        throw new Error(body.message ?? 'Failed to add membership.');
      }
      setAddOk(true);
      setAddOrgId('');
      setAddRole('Member');
      router.refresh();
    } catch (err) {
      setAddError(translateMembershipError(err instanceof Error ? err.message : 'An error occurred.'));
    } finally {
      setAdding(false);
    }
  }

  async function handleSetPrimary(membershipId: string) {
    setSettingPrimary(membershipId);
    setPrimaryError(null);
    try {
      const res = await fetch(
        `/api/identity/admin/users/${encodeURIComponent(userId)}/memberships/${encodeURIComponent(membershipId)}/set-primary`,
        { method: 'POST' },
      );
      if (!res.ok) {
        const body = await res.json().catch(() => ({})) as { message?: string };
        throw new Error(body.message ?? 'Failed to set primary.');
      }
      router.refresh();
    } catch (err) {
      setPrimaryError(translateMembershipError(err instanceof Error ? err.message : 'An error occurred.'));
    } finally {
      setSettingPrimary(null);
    }
  }

  async function handleRemove(membershipId: string) {
    setRemoving(membershipId);
    setRemoveError(null);
    try {
      const res = await fetch(
        `/api/identity/admin/users/${encodeURIComponent(userId)}/memberships/${encodeURIComponent(membershipId)}`,
        { method: 'DELETE' },
      );
      if (!res.ok) {
        const body = await res.json().catch(() => ({})) as { message?: string };
        throw new Error(body.message ?? 'Failed to remove membership.');
      }
      router.refresh();
    } catch (err) {
      setRemoveError(translateMembershipError(err instanceof Error ? err.message : 'An error occurred.'));
    } finally {
      setRemoving(null);
      setRemoveConfirm(null);
    }
  }

  /* Lookup org type for display in "add" dropdown */
  const orgById = new Map(availableOrgs.map(o => [o.id, o]));

  const membershipCount = memberships.length;

  return (
    <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
      {/* Header */}
      <div className="px-5 py-3 border-b border-gray-100 bg-gray-50 flex items-center justify-between">
        <div className="flex items-center gap-2">
          <h2 className="text-xs font-semibold uppercase tracking-wide text-gray-500">
            Organization Memberships
          </h2>
          <span className="inline-flex items-center px-1.5 py-0.5 rounded text-[10px] font-semibold border bg-indigo-50 text-indigo-600 border-indigo-200">
            {membershipCount} {membershipCount === 1 ? 'org' : 'orgs'}
          </span>
        </div>
        <span className="text-[11px] text-gray-400">Primary org controls billing &amp; defaults</span>
      </div>

      {/* Current memberships */}
      {memberships.length > 0 ? (
        <ul className="divide-y divide-gray-100">
          {memberships.map(m => (
            <li key={m.membershipId} className="px-5 py-3 flex items-start justify-between gap-4 flex-wrap">
              <div className="space-y-0.5">
                <div className="flex items-center gap-2 flex-wrap">
                  <span className="text-sm font-medium text-gray-800">{m.orgName}</span>
                  {m.isPrimary && (
                    <span className="inline-flex items-center gap-1 px-1.5 py-0.5 rounded text-[10px] font-bold border bg-amber-50 text-amber-700 border-amber-200 uppercase tracking-wide">
                      <span className="w-1 h-1 rounded-full bg-amber-500 inline-block" />
                      Primary
                    </span>
                  )}
                  <span className="inline-flex items-center px-1.5 py-0.5 rounded text-[10px] font-medium border bg-gray-50 text-gray-600 border-gray-200">
                    {m.memberRole}
                  </span>
                </div>
                <p className="font-mono text-[10px] text-gray-400">{m.organizationId.slice(0, 16)}…</p>
              </div>

              <div className="flex items-center gap-2 flex-wrap">
                {!m.isPrimary && (
                  <button
                    type="button"
                    disabled={settingPrimary !== null}
                    onClick={() => handleSetPrimary(m.membershipId)}
                    className="text-xs px-2 py-1 rounded border border-amber-200 bg-white text-amber-700 hover:bg-amber-50 transition-colors disabled:opacity-40 disabled:cursor-not-allowed"
                  >
                    {settingPrimary === m.membershipId ? 'Setting…' : 'Set Primary'}
                  </button>
                )}

                {removeConfirm === m.membershipId ? (
                  <span className="inline-flex items-center gap-1 text-xs">
                    <span className="text-red-700 font-medium">Remove membership?</span>
                    <button
                      type="button"
                      disabled={removing === m.membershipId}
                      onClick={() => handleRemove(m.membershipId)}
                      className="px-2 py-0.5 rounded bg-red-600 text-white text-[11px] font-medium hover:bg-red-700 disabled:opacity-50 transition-colors"
                    >
                      {removing === m.membershipId ? '…' : 'Yes, remove'}
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
                    disabled={removing !== null || settingPrimary !== null}
                    onClick={() => setRemoveConfirm(m.membershipId)}
                    className="text-xs px-2 py-1 rounded border border-red-200 bg-white text-red-600 hover:bg-red-50 transition-colors disabled:opacity-40 disabled:cursor-not-allowed"
                  >
                    Remove
                  </button>
                )}
              </div>
            </li>
          ))}
        </ul>
      ) : (
        <div className="px-5 py-6 text-center">
          <p className="text-sm font-medium text-gray-500">No organization memberships</p>
          <p className="text-xs text-gray-400 mt-1">
            Add the user to an organization below to grant access to its resources.
          </p>
        </div>
      )}

      {(primaryError || removeError) && (
        <div className="mx-5 my-2 rounded bg-red-50 border border-red-200 px-3 py-2 text-xs text-red-700">
          {primaryError ?? removeError}
        </div>
      )}

      {/* Add membership */}
      {availableToAdd.length > 0 && (
        <div className="px-5 py-3 border-t border-gray-100 bg-gray-50 space-y-2">
          <p className="text-xs font-medium text-gray-600">Add to organization</p>
          <div className="flex items-end gap-3 flex-wrap">
            <div className="flex-1 min-w-48">
              <label className="block text-[11px] text-gray-500 mb-1">Organization</label>
              <select
                value={addOrgId}
                onChange={e => { setAddOrgId(e.target.value); setAddOk(false); setAddError(null); }}
                className="w-full h-8 rounded-md border border-gray-300 px-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500 bg-white"
              >
                <option value="">Select organization…</option>
                {availableToAdd.map(o => (
                  <option key={o.id} value={o.id}>
                    {o.displayName}{o.orgType ? ` · ${o.orgType}` : ''}
                  </option>
                ))}
              </select>
            </div>
            <div className="w-32">
              <label className="block text-[11px] text-gray-500 mb-1">Member role</label>
              <select
                value={addRole}
                onChange={e => setAddRole(e.target.value)}
                className="w-full h-8 rounded-md border border-gray-300 px-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500 bg-white"
              >
                {MEMBER_ROLES.map(r => (
                  <option key={r} value={r}>{r}</option>
                ))}
              </select>
            </div>
            <button
              type="button"
              disabled={!addOrgId || adding}
              onClick={handleAdd}
              className="h-8 px-4 text-sm font-medium text-white bg-indigo-600 hover:bg-indigo-700 rounded-md transition-colors disabled:opacity-40 disabled:cursor-not-allowed"
            >
              {adding ? 'Adding…' : 'Add'}
            </button>
            {addOk && (
              <span className="text-xs text-green-700 font-medium flex items-center gap-1">
                <span className="w-1.5 h-1.5 rounded-full bg-green-500 inline-block" />
                Membership added.
              </span>
            )}
          </div>
        </div>
      )}

      {addError && (
        <div className="mx-5 mb-3 rounded bg-red-50 border border-red-200 px-3 py-2 text-xs text-red-700">
          {addError}
        </div>
      )}

      {availableToAdd.length === 0 && memberships.length > 0 && (
        <p className="px-5 py-2 text-xs text-gray-400 italic border-t border-gray-100">
          User is already a member of all active organizations in this tenant.
        </p>
      )}

      {/* Org type key — shown only when orgs are listed */}
      {addOrgId && orgById.get(addOrgId)?.orgType && (
        <p className="px-5 pb-2 text-[11px] text-gray-400">
          Type: <span className="font-medium text-gray-600">{orgById.get(addOrgId)?.orgType}</span>
        </p>
      )}
    </div>
  );
}
