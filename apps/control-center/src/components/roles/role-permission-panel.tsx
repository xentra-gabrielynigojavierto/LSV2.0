'use client';

import { useState, useTransition } from 'react';
import { useRouter }               from 'next/navigation';
import type { RoleCapabilityItem, PermissionCatalogItem } from '@/types/control-center';

interface RolePermissionPanelProps {
  roleId:           string;
  isSystemRole:     boolean;
  /** True when the role is a product-defined role (not a system or custom tenant role). */
  isProductRole?:   boolean;
  /**
   * When set (product role), the Assign picker is scoped to this product's permissions only.
   * Governance boundary: product roles should only receive permissions from their own product.
   */
  productCode?:     string | null;
  productName?:     string | null;
  assignedItems:    RoleCapabilityItem[];
  catalog:          PermissionCatalogItem[];
}

const PRODUCT_BADGE_COLORS: Record<string, string> = {
  'CareConnect': 'bg-teal-50 text-teal-700 border-teal-100',
  'SynqLien':    'bg-amber-50 text-amber-700 border-amber-100',
  'SynqFund':    'bg-violet-50 text-violet-700 border-violet-100',
};

function ProductBadge({ name }: { name: string }) {
  const cls = PRODUCT_BADGE_COLORS[name] ?? 'bg-gray-50 text-gray-600 border-gray-200';
  return (
    <span className={`inline-flex items-center px-2 py-0.5 rounded text-[10px] font-semibold border ${cls}`}>
      {name}
    </span>
  );
}

export function RolePermissionPanel({
  roleId,
  isSystemRole,
  isProductRole = false,
  productCode   = null,
  productName   = null,
  assignedItems,
  catalog,
}: RolePermissionPanelProps) {
  const router = useRouter();
  const [isPending, startTransition] = useTransition();

  const [showPicker,    setShowPicker]    = useState(false);
  const [pickerSearch,  setPickerSearch]  = useState('');
  const [assigningId,   setAssigningId]   = useState<string | null>(null);
  const [revokingId,    setRevokingId]    = useState<string | null>(null);
  const [actionError,   setActionError]   = useState<string | null>(null);
  const [successBanner, setSuccessBanner] = useState<string | null>(null);

  const assignedIds = new Set(assignedItems.map(i => i.id));

  // ── Governance scoping ────────────────────────────────────────────────────────
  // For product roles: only show permissions for the role's own product in the picker.
  // This prevents cross-product capability leakage (e.g., assigning CareConnect
  // permissions to a SynqLien role) and keeps TENANT.* permissions out of product roles.
  // For non-product roles: show full catalog (PlatformAdmin governance scope).
  const scopedCatalog = (isProductRole && productCode)
    ? catalog.filter(c => c.productCode === productCode)
    : catalog;

  const available = scopedCatalog.filter(c =>
    !assignedIds.has(c.id) &&
    (pickerSearch === '' ||
     c.code.toLowerCase().includes(pickerSearch.toLowerCase()) ||
     c.name.toLowerCase().includes(pickerSearch.toLowerCase()))
  );

  const availableByProduct = available.reduce<Record<string, PermissionCatalogItem[]>>((acc, c) => {
    if (!acc[c.productName]) acc[c.productName] = [];
    acc[c.productName].push(c);
    return acc;
  }, {});

  const assignedByProduct = assignedItems.reduce<Record<string, RoleCapabilityItem[]>>((acc, c) => {
    if (!acc[c.productName]) acc[c.productName] = [];
    acc[c.productName].push(c);
    return acc;
  }, {});

  const canEdit = !isSystemRole;

  function showSuccess(msg: string) {
    setSuccessBanner(msg);
    setTimeout(() => setSuccessBanner(null), 3500);
  }

  async function handleAssign(capabilityId: string) {
    setAssigningId(capabilityId);
    setActionError(null);
    try {
      const res = await fetch(
        `/api/identity/admin/roles/${roleId}/permissions`,
        {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ capabilityId }),
        },
      );
      if (!res.ok) {
        const err = await res.json().catch(() => ({}));
        throw new Error((err as { message?: string }).message ?? 'Failed to assign permission.');
      }
      setShowPicker(false);
      setPickerSearch('');
      showSuccess('Permission assigned successfully.');
      startTransition(() => router.refresh());
    } catch (err) {
      setActionError(err instanceof Error ? err.message : 'Failed to assign permission.');
    } finally {
      setAssigningId(null);
    }
  }

  async function handleRevoke(capabilityId: string) {
    setRevokingId(capabilityId);
    setActionError(null);
    try {
      const res = await fetch(
        `/api/identity/admin/roles/${roleId}/permissions/${capabilityId}`,
        { method: 'DELETE' },
      );
      if (!res.ok && res.status !== 204) {
        const err = await res.json().catch(() => ({}));
        throw new Error((err as { message?: string }).message ?? 'Failed to revoke permission.');
      }
      showSuccess('Permission removed.');
      startTransition(() => router.refresh());
    } catch (err) {
      setActionError(err instanceof Error ? err.message : 'Failed to revoke permission.');
    } finally {
      setRevokingId(null);
    }
  }

  const isBusy = isPending || assigningId !== null || revokingId !== null;

  return (
    <div className="space-y-4">
      {/* Panel header */}
      <div className="flex items-center justify-between">
        <div>
          <h2 className="text-base font-semibold text-gray-900">Assigned Permissions</h2>
          <p className="text-xs text-gray-500 mt-0.5">
            {assignedItems.length === 0
              ? 'No capabilities assigned. Add permissions to grant access.'
              : `${assignedItems.length} capability${assignedItems.length !== 1 ? 's' : ''} assigned`}
          </p>
        </div>
        {canEdit && (
          <button
            onClick={() => { setShowPicker(p => !p); setPickerSearch(''); setActionError(null); }}
            disabled={isBusy}
            className="inline-flex items-center gap-1.5 px-3 py-1.5 text-sm font-medium rounded-md border border-indigo-200 bg-indigo-50 text-indigo-700 hover:bg-indigo-100 transition-colors disabled:opacity-50"
          >
            <svg className="h-3.5 w-3.5" viewBox="0 0 16 16" fill="currentColor" aria-hidden="true">
              <path d="M8 2a.75.75 0 0 1 .75.75v4.5h4.5a.75.75 0 0 1 0 1.5h-4.5v4.5a.75.75 0 0 1-1.5 0v-4.5h-4.5a.75.75 0 0 1 0-1.5h4.5v-4.5A.75.75 0 0 1 8 2Z" />
            </svg>
            Assign Permission
          </button>
        )}
      </div>

      {/* System role notice */}
      {isSystemRole && (
        <div className="flex items-start gap-2 bg-amber-50 border border-amber-200 rounded-lg px-4 py-3 text-sm text-amber-800">
          <svg className="h-4 w-4 mt-0.5 shrink-0 text-amber-500" viewBox="0 0 20 20" fill="currentColor">
            <path fillRule="evenodd" d="M8.485 2.495c.673-1.167 2.357-1.167 3.03 0l6.28 10.875c.673 1.167-.17 2.625-1.516 2.625H3.72c-1.347 0-2.189-1.458-1.515-2.625L8.485 2.495ZM10 5a.75.75 0 0 1 .75.75v3.5a.75.75 0 0 1-1.5 0v-3.5A.75.75 0 0 1 10 5Zm0 9a1 1 0 1 0 0-2 1 1 0 0 0 0 2Z" clipRule="evenodd" />
          </svg>
          <span>
            System roles cannot be modified. Permissions for this role are managed by the platform engineering team.
          </span>
        </div>
      )}

      {/* Product governance notice — shown for product roles when picker is scoped */}
      {!isSystemRole && isProductRole && productName && (
        <div className="flex items-start gap-2 bg-blue-50 border border-blue-200 rounded-lg px-4 py-3 text-sm text-blue-800">
          <svg className="h-4 w-4 mt-0.5 shrink-0 text-blue-500" viewBox="0 0 20 20" fill="currentColor">
            <path fillRule="evenodd" d="M18 10a8 8 0 1 1-16 0 8 8 0 0 1 16 0Zm-7-4a1 1 0 1 1-2 0 1 1 0 0 1 2 0ZM9 9a.75.75 0 0 0 0 1.5h.253a.25.25 0 0 1 .244.304l-.459 2.066A1.75 1.75 0 0 0 10.747 15H11a.75.75 0 0 0 0-1.5h-.253a.25.25 0 0 1-.244-.304l.459-2.066A1.75 1.75 0 0 0 9.253 9H9Z" clipRule="evenodd" />
          </svg>
          <span>
            This is a <strong>{productName}</strong> product role. The permission picker is scoped to{' '}
            <strong>{productName}</strong> capabilities only, preserving product governance boundaries.
            Platform-level and tenant-level permissions are not available for product roles.
          </span>
        </div>
      )}

      {/* Success banner */}
      {successBanner && (
        <div className="flex items-center gap-2 bg-green-50 border border-green-200 rounded-lg px-4 py-3">
          <svg className="h-4 w-4 text-green-500 shrink-0" viewBox="0 0 16 16" fill="currentColor">
            <path fillRule="evenodd" d="M12.416 3.376a.75.75 0 0 1 .208 1.04l-5 7.5a.75.75 0 0 1-1.154.114l-3-3a.75.75 0 0 1 1.06-1.06l2.353 2.353 4.493-6.74a.75.75 0 0 1 1.04-.207Z" clipRule="evenodd" />
          </svg>
          <p className="text-sm text-green-700">{successBanner}</p>
        </div>
      )}

      {/* Error banner */}
      {actionError && (
        <div className="bg-red-50 border border-red-200 rounded-lg px-4 py-3 flex items-start gap-2">
          <p className="text-sm text-red-700 flex-1">{actionError}</p>
          <button onClick={() => setActionError(null)} className="text-red-400 hover:text-red-600">
            <svg className="h-4 w-4" viewBox="0 0 16 16" fill="currentColor">
              <path d="M3.72 3.72a.75.75 0 0 1 1.06 0L8 6.94l3.22-3.22a.75.75 0 1 1 1.06 1.06L9.06 8l3.22 3.22a.75.75 0 1 1-1.06 1.06L8 9.06l-3.22 3.22a.75.75 0 0 1-1.06-1.06L6.94 8 3.72 4.78a.75.75 0 0 1 0-1.06Z" />
            </svg>
          </button>
        </div>
      )}

      {/* Permission Picker */}
      {showPicker && (
        <div className="border border-indigo-200 rounded-lg bg-indigo-50/30 p-4 space-y-3">
          <div className="flex items-center gap-2">
            <input
              type="text"
              value={pickerSearch}
              onChange={e => setPickerSearch(e.target.value)}
              placeholder={isProductRole && productName
                ? `Search ${productName} permissions…`
                : 'Search permissions…'
              }
              className="flex-1 text-sm border border-gray-300 rounded-md px-3 py-1.5 focus:outline-none focus:ring-2 focus:ring-indigo-400 bg-white"
              autoFocus
            />
            <button
              onClick={() => { setShowPicker(false); setPickerSearch(''); }}
              className="text-gray-400 hover:text-gray-600"
            >
              <svg className="h-4 w-4" viewBox="0 0 16 16" fill="currentColor">
                <path d="M3.72 3.72a.75.75 0 0 1 1.06 0L8 6.94l3.22-3.22a.75.75 0 1 1 1.06 1.06L9.06 8l3.22 3.22a.75.75 0 1 1-1.06 1.06L8 9.06l-3.22 3.22a.75.75 0 0 1-1.06-1.06L6.94 8 3.72 4.78a.75.75 0 0 1 0-1.06Z" />
              </svg>
            </button>
          </div>
          {available.length === 0 ? (
            <p className="text-sm text-gray-500 text-center py-4">
              {pickerSearch
                ? 'No matching permissions found.'
                : isProductRole
                  ? `All ${productName ?? 'product'} permissions are already assigned.`
                  : 'All permissions are already assigned.'
              }
            </p>
          ) : (
            <div className="max-h-64 overflow-y-auto space-y-3">
              {Object.entries(availableByProduct).map(([product, caps]) => (
                <div key={product}>
                  <p className="text-[10px] font-semibold text-gray-400 uppercase tracking-wider mb-1.5">
                    {product}
                  </p>
                  <div className="space-y-1">
                    {caps.map(cap => (
                      <div
                        key={cap.id}
                        className="flex items-center justify-between gap-3 bg-white border border-gray-200 rounded-md px-3 py-2"
                      >
                        <div className="min-w-0 flex-1">
                          <code className="text-xs bg-gray-100 px-1.5 py-0.5 rounded font-mono text-gray-700">
                            {cap.code}
                          </code>
                          <p className="text-xs text-gray-600 mt-0.5">{cap.name}</p>
                        </div>
                        <button
                          onClick={() => handleAssign(cap.id)}
                          disabled={assigningId === cap.id || isBusy}
                          className="shrink-0 text-xs font-medium text-indigo-600 hover:text-indigo-800 disabled:opacity-50"
                        >
                          {assigningId === cap.id ? 'Adding…' : 'Add'}
                        </button>
                      </div>
                    ))}
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>
      )}

      {/* Assigned permissions list */}
      {assignedItems.length === 0 ? (
        <div className="bg-white border border-gray-200 rounded-lg p-8 text-center">
          <p className="text-sm text-gray-400">No permissions assigned yet.</p>
          {canEdit && (
            <p className="text-xs text-gray-400 mt-1">
              Use the &quot;Assign Permission&quot; button above to add capabilities.
            </p>
          )}
        </div>
      ) : (
        <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
          {Object.entries(assignedByProduct).map(([product, caps], idx) => (
            <div key={product} className={idx > 0 ? 'border-t border-gray-100' : ''}>
              <div className="px-4 py-2 bg-gray-50 border-b border-gray-100 flex items-center gap-2">
                <ProductBadge name={product} />
                <span className="text-xs text-gray-400">{caps.length} capability{caps.length !== 1 ? 's' : ''}</span>
              </div>
              <div className="divide-y divide-gray-50">
                {caps.map(cap => (
                  <div key={cap.id} className="flex items-center gap-3 px-4 py-3 hover:bg-gray-50 transition-colors">
                    <div className="flex-1 min-w-0">
                      <code className="text-xs bg-gray-100 px-1.5 py-0.5 rounded font-mono text-gray-700">
                        {cap.code}
                      </code>
                      <p className="text-sm font-medium text-gray-900 mt-0.5">{cap.name}</p>
                      {cap.description && (
                        <p className="text-xs text-gray-500">{cap.description}</p>
                      )}
                    </div>
                    {canEdit && (
                      <button
                        onClick={() => handleRevoke(cap.id)}
                        disabled={revokingId === cap.id || isBusy}
                        title="Revoke permission"
                        className="shrink-0 text-xs text-red-500 hover:text-red-700 disabled:opacity-40 transition-colors font-medium"
                      >
                        {revokingId === cap.id ? 'Removing…' : 'Remove'}
                      </button>
                    )}
                  </div>
                ))}
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
