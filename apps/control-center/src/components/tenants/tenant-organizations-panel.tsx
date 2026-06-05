'use client';

import { useState, useTransition } from 'react';
import { updateOrganizationType } from '@/app/tenants/[id]/actions';
import type { OrgSummary } from '@/types/control-center';

const ORG_TYPE_OPTIONS = [
  { value: 'LAW_FIRM',   label: 'Law Firm' },
  { value: 'PROVIDER',   label: 'Provider' },
  { value: 'FUNDER',     label: 'Funder' },
  { value: 'LIEN_OWNER', label: 'Lien Owner' },
  { value: 'INTERNAL',   label: 'Internal' },
] as const;

function orgTypeLabel(code: string): string {
  return ORG_TYPE_OPTIONS.find(o => o.value === code)?.label ?? code;
}

interface TenantOrganizationsPanelProps {
  organizations: OrgSummary[];
}

export function TenantOrganizationsPanel({ organizations }: TenantOrganizationsPanelProps) {
  const [orgs, setOrgs] = useState(organizations);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [editType, setEditType] = useState('');
  const [errorMsg, setErrorMsg] = useState<string | null>(null);
  const [successMsg, setSuccessMsg] = useState<string | null>(null);
  const [isPending, startTransition] = useTransition();

  function handleEdit(org: OrgSummary) {
    setEditingId(org.id);
    setEditType(org.orgType);
    setErrorMsg(null);
    setSuccessMsg(null);
  }

  function handleCancel() {
    setEditingId(null);
    setEditType('');
    setErrorMsg(null);
  }

  function handleSave(orgId: string) {
    setErrorMsg(null);
    setSuccessMsg(null);
    startTransition(async () => {
      const result = await updateOrganizationType(orgId, editType);
      if (result.success && result.organization) {
        setOrgs(prev => prev.map(o => o.id === orgId ? result.organization! : o));
        setEditingId(null);
        setSuccessMsg(`Organization type updated to ${orgTypeLabel(editType)}.`);
        setTimeout(() => setSuccessMsg(null), 4000);
      } else {
        setErrorMsg(result.error ?? 'Failed to update organization type.');
      }
    });
  }

  return (
    <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
      <div className="px-5 py-3 border-b border-gray-100 bg-gray-50 flex items-center justify-between">
        <h2 className="text-xs font-semibold uppercase tracking-wide text-gray-500">
          Organizations ({orgs.length})
        </h2>
      </div>

      {errorMsg && (
        <div className="mx-5 mt-3 bg-red-50 border border-red-200 rounded px-3 py-2 text-sm text-red-700">
          {errorMsg}
        </div>
      )}
      {successMsg && (
        <div className="mx-5 mt-3 bg-green-50 border border-green-200 rounded px-3 py-2 text-sm text-green-700">
          {successMsg}
        </div>
      )}

      {orgs.length === 0 ? (
        <div className="px-5 py-8 text-center text-sm text-gray-400">
          No organizations linked to this tenant.
        </div>
      ) : (
        <div className="divide-y divide-gray-100">
          {orgs.map(org => (
            <div key={org.id} className="px-5 py-3 flex items-center justify-between gap-4">
              <div className="min-w-0 flex-1">
                <p className="text-sm font-medium text-gray-900 truncate">
                  {org.displayName}
                </p>
                {org.displayName !== org.name && (
                  <p className="text-xs text-gray-400 truncate">{org.name}</p>
                )}
              </div>

              {editingId === org.id ? (
                <div className="flex items-center gap-2">
                  <select
                    value={editType}
                    onChange={e => setEditType(e.target.value)}
                    disabled={isPending}
                    className="text-sm border border-gray-300 rounded px-2 py-1 bg-white focus:outline-none focus:ring-2 focus:ring-blue-500"
                  >
                    {ORG_TYPE_OPTIONS.map(opt => (
                      <option key={opt.value} value={opt.value}>{opt.label}</option>
                    ))}
                  </select>
                  <button
                    onClick={() => handleSave(org.id)}
                    disabled={isPending || editType === org.orgType}
                    className="text-xs font-medium px-3 py-1 rounded bg-blue-600 text-white hover:bg-blue-700 disabled:opacity-50 disabled:cursor-not-allowed"
                  >
                    {isPending ? 'Saving…' : 'Save'}
                  </button>
                  <button
                    onClick={handleCancel}
                    disabled={isPending}
                    className="text-xs font-medium px-3 py-1 rounded border border-gray-300 text-gray-600 hover:bg-gray-50 disabled:opacity-50"
                  >
                    Cancel
                  </button>
                </div>
              ) : (
                <div className="flex items-center gap-3">
                  <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium bg-gray-100 text-gray-700">
                    {orgTypeLabel(org.orgType)}
                  </span>
                  <button
                    onClick={() => handleEdit(org)}
                    className="text-xs font-medium text-blue-600 hover:text-blue-800"
                  >
                    Edit
                  </button>
                </div>
              )}
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
