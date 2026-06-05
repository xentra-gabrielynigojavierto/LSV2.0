'use client';

import { useState, useEffect, useCallback } from 'react';
import { tenantClientApi, ApiError } from '@/lib/tenant-client-api';
import { clsx } from 'clsx';
import type { TenantPermissionCatalogItem, TenantRoleItem } from '@/types/tenant';

interface Props {
  tenantPermissions: TenantPermissionCatalogItem[];
  roles:             TenantRoleItem[];
  isTenantAdmin:     boolean;
}

type View = 'roles' | 'catalog';

interface LoadedRolePermission {
  id:          string;
  code:        string;
  name:        string;
  productCode: string;
}

function groupByCategory(
  items: TenantPermissionCatalogItem[],
): Record<string, TenantPermissionCatalogItem[]> {
  return items.reduce<Record<string, TenantPermissionCatalogItem[]>>((acc, p) => {
    const cat = p.category ?? 'General';
    if (!acc[cat]) acc[cat] = [];
    acc[cat].push(p);
    return acc;
  }, {});
}

export function PermissionsClient({ tenantPermissions, roles, isTenantAdmin }: Props) {
  const [view, setView]                 = useState<View>('roles');
  const [selectedRoleId, setSelectedRoleId] = useState<string | null>(null);
  const [rolePermissions, setRolePermissions] = useState<LoadedRolePermission[]>([]);
  const [pendingAdd, setPendingAdd]     = useState<Set<string>>(new Set());
  const [pendingRemove, setPendingRemove] = useState<Set<string>>(new Set());
  const [loading, setLoading]           = useState(false);
  const [saving, setSaving]             = useState(false);
  const [loadError, setLoadError]       = useState<string | null>(null);
  const [toast, setToast]               = useState<{ type: 'success' | 'error'; message: string } | null>(null);

  const selectedRole   = roles.find(r => r.id === selectedRoleId) ?? null;
  const isEditable     = isTenantAdmin && !!selectedRole && !selectedRole.isSystemRole && !selectedRole.isProductRole;
  const catalogByCategory = groupByCategory(tenantPermissions);
  const hasPending     = pendingAdd.size > 0 || pendingRemove.size > 0;

  const tenantRoles    = roles.filter(r => !r.isProductRole);

  const loadRolePermissions = useCallback(async (roleId: string) => {
    setLoading(true);
    setLoadError(null);
    setPendingAdd(new Set());
    setPendingRemove(new Set());
    try {
      const { data } = await tenantClientApi.getRolePermissions(roleId);
      setRolePermissions(
        data.permissions.filter(p => p.productCode === 'SYNQ_PLATFORM'),
      );
    } catch {
      setLoadError('Failed to load permissions for this role.');
      setRolePermissions([]);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    if (!selectedRoleId) return;
    loadRolePermissions(selectedRoleId);
  }, [selectedRoleId, loadRolePermissions]);

  function selectRole(id: string) {
    if (id === selectedRoleId) return;
    setSelectedRoleId(id);
  }

  function isChecked(permId: string): boolean {
    if (pendingAdd.has(permId))    return true;
    if (pendingRemove.has(permId)) return false;
    return rolePermissions.some(p => p.id === permId);
  }

  function toggle(permId: string) {
    if (!isEditable || saving) return;
    const checked = isChecked(permId);
    if (checked) {
      setPendingRemove(prev => { const n = new Set(prev); n.add(permId); return n; });
      setPendingAdd(prev    => { const n = new Set(prev); n.delete(permId); return n; });
    } else {
      setPendingAdd(prev    => { const n = new Set(prev); n.add(permId); return n; });
      setPendingRemove(prev => { const n = new Set(prev); n.delete(permId); return n; });
    }
  }

  function cancel() {
    setPendingAdd(new Set());
    setPendingRemove(new Set());
  }

  function showToast(type: 'success' | 'error', message: string) {
    setToast({ type, message });
    setTimeout(() => setToast(null), 4000);
  }

  async function save() {
    if (!selectedRoleId || !hasPending) return;
    setSaving(true);
    try {
      const addOps    = [...pendingAdd].map(pid => tenantClientApi.assignRolePermission(selectedRoleId, pid));
      const removeOps = [...pendingRemove].map(pid => tenantClientApi.revokeRolePermission(selectedRoleId, pid));
      await Promise.all([...addOps, ...removeOps]);
      await loadRolePermissions(selectedRoleId);
      showToast('success', 'Permissions updated successfully.');
    } catch (e: unknown) {
      const msg =
        e instanceof ApiError
          ? e.message
          : e instanceof Error
          ? e.message
          : 'Failed to save permissions.';
      if (e instanceof ApiError && e.isForbidden) {
        showToast('error', 'You do not have permission to modify this role.');
      } else {
        showToast('error', msg);
      }
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="space-y-4">
      {/* View switcher */}
      <div className="flex items-center gap-1">
        <button
          onClick={() => setView('roles')}
          className={clsx(
            'flex items-center gap-1.5 px-3 py-1.5 text-sm font-medium rounded-lg transition-colors',
            view === 'roles'
              ? 'bg-primary/10 text-primary'
              : 'text-gray-600 hover:text-gray-900 hover:bg-gray-50',
          )}
        >
          <i className="ri-shield-check-line text-base" />
          Role Permissions
        </button>
        <button
          onClick={() => setView('catalog')}
          className={clsx(
            'flex items-center gap-1.5 px-3 py-1.5 text-sm font-medium rounded-lg transition-colors',
            view === 'catalog'
              ? 'bg-primary/10 text-primary'
              : 'text-gray-600 hover:text-gray-900 hover:bg-gray-50',
          )}
        >
          <i className="ri-book-2-line text-base" />
          Permission Catalog
        </button>
      </div>

      {/* Toast */}
      {toast && (
        <div
          className={clsx(
            'flex items-center gap-2 rounded-lg px-4 py-2.5 text-sm',
            toast.type === 'success'
              ? 'bg-green-50 border border-green-200 text-green-700'
              : 'bg-red-50 border border-red-200 text-red-700',
          )}
        >
          <i className={toast.type === 'success' ? 'ri-checkbox-circle-line' : 'ri-error-warning-line'} />
          {toast.message}
        </div>
      )}

      {/* ── Role Permissions View ─────────────────────────────────────────── */}
      {view === 'roles' && (
        <div className="grid grid-cols-1 gap-4 lg:grid-cols-3">
          {/* Roles list */}
          <div className="rounded-xl border border-gray-200 bg-white overflow-hidden">
            <div className="px-4 py-3 border-b border-gray-100 bg-gray-50">
              <p className="text-xs font-semibold uppercase tracking-wide text-gray-500">
                Roles
              </p>
              <p className="text-xs text-gray-400 mt-0.5">
                {tenantRoles.length} role{tenantRoles.length !== 1 ? 's' : ''}
              </p>
            </div>
            <div className="divide-y divide-gray-100">
              {tenantRoles.length === 0 && (
                <p className="px-4 py-8 text-sm text-gray-400 text-center">
                  No roles found.
                </p>
              )}
              {tenantRoles.map(role => (
                <button
                  key={role.id}
                  onClick={() => selectRole(role.id)}
                  className={clsx(
                    'w-full px-4 py-3 flex items-center justify-between text-left transition-colors',
                    'hover:bg-gray-50',
                    selectedRoleId === role.id && 'bg-primary/5 border-l-2 border-l-primary',
                  )}
                >
                  <div className="min-w-0">
                    <p className="text-sm font-medium text-gray-900 truncate">{role.name}</p>
                    <p className="text-xs text-gray-400 mt-0.5">
                      {role.isSystemRole ? 'System role — read only' : 'Tenant role'}
                    </p>
                  </div>
                  <i className="ri-arrow-right-s-line text-gray-300 flex-shrink-0 ml-2" />
                </button>
              ))}
            </div>
          </div>

          {/* Role permissions detail */}
          <div className="lg:col-span-2 rounded-xl border border-gray-200 bg-white overflow-hidden">
            {!selectedRole ? (
              <div className="flex flex-col items-center justify-center h-64 text-center px-8">
                <i className="ri-key-2-line text-4xl text-gray-200 mb-3" />
                <p className="text-sm text-gray-400">
                  Select a role from the list to view and manage its tenant-level permissions.
                </p>
              </div>
            ) : (
              <>
                {/* Role header */}
                <div className="px-5 py-4 border-b border-gray-100 bg-gray-50 flex items-start justify-between gap-4">
                  <div>
                    <div className="flex items-center gap-2">
                      <h2 className="text-sm font-semibold text-gray-900">{selectedRole.name}</h2>
                      {selectedRole.isSystemRole && (
                        <span className="text-xs px-2 py-0.5 rounded-full bg-gray-100 text-gray-500 border border-gray-200">
                          System
                        </span>
                      )}
                    </div>
                    <p className="text-xs text-gray-500 mt-1">
                      {isEditable
                        ? 'Check or uncheck permissions below, then save your changes.'
                        : selectedRole.isSystemRole
                        ? 'System roles have platform-managed permissions and cannot be edited here.'
                        : 'Only tenant admins can edit tenant role permissions.'}
                    </p>
                  </div>
                  {isEditable && hasPending && (
                    <div className="flex items-center gap-2 flex-shrink-0">
                      <button
                        onClick={cancel}
                        disabled={saving}
                        className="px-3 py-1.5 text-xs font-medium text-gray-600 hover:text-gray-900 hover:bg-gray-100 rounded-lg transition-colors disabled:opacity-50"
                      >
                        Cancel
                      </button>
                      <button
                        onClick={save}
                        disabled={saving}
                        className="px-3 py-1.5 text-xs font-medium text-white bg-primary hover:bg-primary/90 rounded-lg transition-colors disabled:opacity-50 flex items-center gap-1.5"
                      >
                        {saving && <i className="ri-loader-4-line animate-spin" />}
                        {saving ? 'Saving…' : `Save (${pendingAdd.size + pendingRemove.size} change${pendingAdd.size + pendingRemove.size !== 1 ? 's' : ''})`}
                      </button>
                    </div>
                  )}
                </div>

                {/* Governance notice for system roles */}
                {selectedRole.isSystemRole && (
                  <div className="mx-5 mt-4 flex items-start gap-2 rounded-lg bg-amber-50 border border-amber-200 px-3 py-2.5 text-xs text-amber-700">
                    <i className="ri-information-line mt-0.5 flex-shrink-0" />
                    <span>
                      System role permissions are managed by the platform. Contact your platform
                      administrator to change permissions for <strong>{selectedRole.name}</strong>.
                    </span>
                  </div>
                )}

                {/* Permission checklist */}
                {loading ? (
                  <div className="flex items-center justify-center h-32">
                    <i className="ri-loader-4-line animate-spin text-gray-400 text-xl" />
                  </div>
                ) : loadError ? (
                  <div className="m-5 rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700 flex items-center gap-2">
                    <i className="ri-error-warning-line" />
                    {loadError}
                  </div>
                ) : tenantPermissions.length === 0 ? (
                  <div className="flex flex-col items-center justify-center h-32 text-center px-8">
                    <p className="text-sm text-gray-400">
                      No tenant-level permissions found in the catalog.
                    </p>
                  </div>
                ) : (
                  <div className="overflow-auto max-h-[520px]">
                    {Object.entries(catalogByCategory).map(([category, perms]) => (
                      <div key={category}>
                        <div className="px-5 py-2 bg-gray-50 sticky top-0 z-10 border-b border-gray-100">
                          <p className="text-xs font-semibold uppercase tracking-wide text-gray-400">
                            {category}
                          </p>
                        </div>
                        {perms.map(perm => {
                          const checked  = isChecked(perm.id);
                          const modified =
                            pendingAdd.has(perm.id) || pendingRemove.has(perm.id);

                          return (
                            <label
                              key={perm.id}
                              className={clsx(
                                'flex items-start gap-3 px-5 py-3 border-b border-gray-50 last:border-0',
                                isEditable
                                  ? 'cursor-pointer hover:bg-gray-50'
                                  : 'cursor-default',
                              )}
                            >
                              <input
                                type="checkbox"
                                checked={checked}
                                onChange={() => toggle(perm.id)}
                                disabled={!isEditable || saving}
                                className="mt-0.5 h-4 w-4 rounded border-gray-300 text-primary focus:ring-primary focus:ring-offset-0 disabled:opacity-50 flex-shrink-0"
                              />
                              <div className="flex-1 min-w-0">
                                <div className="flex items-center gap-2 flex-wrap">
                                  <p className="text-sm font-medium text-gray-900">
                                    {perm.name}
                                  </p>
                                  {modified && (
                                    <span className="text-xs px-1.5 py-0.5 rounded bg-amber-50 text-amber-600 border border-amber-200">
                                      unsaved
                                    </span>
                                  )}
                                </div>
                                {perm.description && (
                                  <p className="text-xs text-gray-500 mt-0.5">
                                    {perm.description}
                                  </p>
                                )}
                                <p className="text-xs font-mono text-gray-400 mt-0.5">
                                  {perm.code}
                                </p>
                              </div>
                            </label>
                          );
                        })}
                      </div>
                    ))}
                  </div>
                )}

                {/* Save bar at bottom when pending changes exist */}
                {isEditable && hasPending && (
                  <div className="sticky bottom-0 flex items-center justify-end gap-2 px-5 py-3 bg-white border-t border-gray-200">
                    <span className="text-xs text-gray-500 mr-auto">
                      {pendingAdd.size + pendingRemove.size} unsaved change{pendingAdd.size + pendingRemove.size !== 1 ? 's' : ''}
                    </span>
                    <button
                      onClick={cancel}
                      disabled={saving}
                      className="px-3 py-1.5 text-xs font-medium text-gray-600 hover:text-gray-900 hover:bg-gray-100 rounded-lg transition-colors disabled:opacity-50"
                    >
                      Cancel
                    </button>
                    <button
                      onClick={save}
                      disabled={saving}
                      className="px-3 py-1.5 text-xs font-medium text-white bg-primary hover:bg-primary/90 rounded-lg transition-colors disabled:opacity-50 flex items-center gap-1.5"
                    >
                      {saving && <i className="ri-loader-4-line animate-spin" />}
                      {saving ? 'Saving…' : 'Save changes'}
                    </button>
                  </div>
                )}
              </>
            )}
          </div>
        </div>
      )}

      {/* ── Permission Catalog View ─────────────────────────────────────── */}
      {view === 'catalog' && (
        <div className="rounded-xl border border-gray-200 bg-white overflow-hidden">
          <div className="px-5 py-4 border-b border-gray-100 bg-gray-50">
            <h2 className="text-sm font-semibold text-gray-900">Tenant Permission Catalog</h2>
            <p className="text-xs text-gray-500 mt-0.5">
              All available tenant-level permissions ({tenantPermissions.length} total).
              These can be assigned to tenant roles via the Role Permissions view.
            </p>
          </div>

          {tenantPermissions.length === 0 ? (
            <div className="flex flex-col items-center justify-center h-32 text-center px-8">
              <i className="ri-key-2-line text-3xl text-gray-200 mb-2" />
              <p className="text-sm text-gray-400">No tenant permissions found.</p>
            </div>
          ) : (
            <div>
              {Object.entries(catalogByCategory).map(([category, perms], idx) => (
                <div key={category} className={idx > 0 ? 'border-t border-gray-100' : ''}>
                  <div className="px-5 py-2.5 bg-gray-50">
                    <p className="text-xs font-semibold uppercase tracking-wide text-gray-400">
                      {category}
                    </p>
                  </div>
                  {perms.map(perm => (
                    <div
                      key={perm.id}
                      className="flex items-start gap-3 px-5 py-3 border-t border-gray-50 hover:bg-gray-50 transition-colors"
                    >
                      <div className="flex h-6 w-6 items-center justify-center rounded-md bg-primary/5 flex-shrink-0 mt-0.5">
                        <i className="ri-key-2-line text-primary text-xs" />
                      </div>
                      <div className="flex-1 min-w-0">
                        <p className="text-sm font-medium text-gray-900">{perm.name}</p>
                        {perm.description && (
                          <p className="text-xs text-gray-500 mt-0.5">{perm.description}</p>
                        )}
                        <p className="text-xs font-mono text-gray-400 mt-0.5">{perm.code}</p>
                      </div>
                    </div>
                  ))}
                </div>
              ))}
            </div>
          )}

          <div className="px-5 py-3 border-t border-gray-100 bg-gray-50">
            <p className="text-xs text-gray-400">
              <i className="ri-information-line mr-1" />
              Product-level permissions are governed by the platform and are not shown here.
              Contact your platform administrator to adjust product permissions.
            </p>
          </div>
        </div>
      )}
    </div>
  );
}
