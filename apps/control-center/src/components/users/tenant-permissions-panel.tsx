'use client';

import { useState, useEffect, useCallback } from 'react';

interface Role { id: string; name: string; code?: string; isSystemRole?: boolean; isProductRole?: boolean; }
interface Permission { id: string; code: string; name: string; category?: string; productCode?: string; }
interface RolePermission { id: string; code: string; name: string; productCode?: string; }

type View = 'roles' | 'catalog';

function groupByCategory(items: Permission[]): Record<string, Permission[]> {
  return items.reduce<Record<string, Permission[]>>((acc, p) => {
    const cat = p.category ?? 'General';
    (acc[cat] ??= []).push(p);
    return acc;
  }, {});
}

interface Props { tenantId: string; }

export function TenantPermissionsPanel({ tenantId: _tenantId }: Props) {
  const [view,           setView]           = useState<View>('roles');
  const [roles,          setRoles]          = useState<Role[]>([]);
  const [rolesLoading,   setRolesLoading]   = useState(true);
  const [catalog,        setCatalog]        = useState<Permission[]>([]);
  const [catalogLoading, setCatalogLoading] = useState(false);
  const [selectedRoleId, setSelectedRoleId] = useState<string | null>(null);
  const [rolePerms,      setRolePerms]      = useState<RolePermission[]>([]);
  const [permsLoading,   setPermsLoading]   = useState(false);
  const [permsError,     setPermsError]     = useState<string | null>(null);
  const [pendingAdd,     setPendingAdd]     = useState<Set<string>>(new Set());
  const [pendingRemove,  setPendingRemove]  = useState<Set<string>>(new Set());
  const [saving,         setSaving]         = useState(false);
  const [toast,          setToast]          = useState<{ type: 'success' | 'error'; msg: string } | null>(null);

  const showToast = useCallback((type: 'success' | 'error', msg: string) => {
    setToast({ type, msg });
    setTimeout(() => setToast(null), 4000);
  }, []);

  useEffect(() => {
    void (async () => {
      setRolesLoading(true);
      try {
        const res  = await fetch('/api/identity/admin/roles');
        const data = await res.json() as Role[];
        setRoles(Array.isArray(data) ? data : []);
      } catch {
        setRoles([]);
      } finally {
        setRolesLoading(false);
      }
    })();
  }, []);

  useEffect(() => {
    if (view !== 'catalog') return;
    if (catalog.length > 0) return;
    void (async () => {
      setCatalogLoading(true);
      try {
        const res  = await fetch('/api/identity/admin/permissions');
        const data = await res.json() as Permission[];
        setCatalog(Array.isArray(data) ? data : []);
      } catch {
        setCatalog([]);
      } finally {
        setCatalogLoading(false);
      }
    })();
  }, [view, catalog.length]);

  const loadRolePerms = useCallback(async (roleId: string) => {
    setPermsLoading(true);
    setPermsError(null);
    setPendingAdd(new Set());
    setPendingRemove(new Set());
    try {
      const res  = await fetch(`/api/identity/admin/roles/${encodeURIComponent(roleId)}/permissions`);
      const data = await res.json() as RolePermission[];
      setRolePerms(Array.isArray(data) ? data : []);
    } catch {
      setPermsError('Failed to load role permissions.');
      setRolePerms([]);
    } finally {
      setPermsLoading(false);
    }
  }, []);

  useEffect(() => {
    if (!selectedRoleId) return;
    void loadRolePerms(selectedRoleId);
  }, [selectedRoleId, loadRolePerms]);

  const selectedRole = roles.find(r => r.id === selectedRoleId) ?? null;
  const isEditable   = !!selectedRole && !selectedRole.isSystemRole && !selectedRole.isProductRole;
  const hasPending   = pendingAdd.size > 0 || pendingRemove.size > 0;

  const allPerms = useCallback(() => {
    if (catalog.length > 0) return catalog;
    return rolePerms.map(p => ({ id: p.id, code: p.code, name: p.name, category: undefined, productCode: p.productCode }));
  }, [catalog, rolePerms]);

  function isChecked(permId: string): boolean {
    if (pendingAdd.has(permId))    return true;
    if (pendingRemove.has(permId)) return false;
    return rolePerms.some(p => p.id === permId);
  }

  function toggle(permId: string) {
    if (!isEditable || saving) return;
    if (isChecked(permId)) {
      setPendingRemove(s => { const n = new Set(s); n.add(permId); return n; });
      setPendingAdd(s    => { const n = new Set(s); n.delete(permId); return n; });
    } else {
      setPendingAdd(s    => { const n = new Set(s); n.add(permId); return n; });
      setPendingRemove(s => { const n = new Set(s); n.delete(permId); return n; });
    }
  }

  async function save() {
    if (!selectedRoleId || !hasPending) return;
    setSaving(true);
    try {
      await Promise.all([
        ...[...pendingAdd].map(pid =>
          fetch(`/api/identity/admin/roles/${encodeURIComponent(selectedRoleId)}/permissions`, {
            method: 'POST', headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ capabilityId: pid }),
          }),
        ),
        ...[...pendingRemove].map(pid =>
          fetch(`/api/identity/admin/roles/${encodeURIComponent(selectedRoleId)}/permissions/${encodeURIComponent(pid)}`, {
            method: 'DELETE',
          }),
        ),
      ]);
      await loadRolePerms(selectedRoleId);
      showToast('success', 'Permissions updated.');
    } catch {
      showToast('error', 'Failed to save permissions.');
    } finally {
      setSaving(false);
    }
  }

  const tenantRoles   = roles.filter(r => !r.isProductRole);
  const catalogByCategory = groupByCategory(
    view === 'catalog'
      ? catalog
      : allPerms(),
  );

  return (
    <div className="space-y-4">

      {/* Toast */}
      {toast && (
        <div className={`fixed bottom-4 right-4 z-50 flex items-center gap-2 px-4 py-3 rounded-lg border shadow-lg text-sm ${toast.type === 'success' ? 'bg-green-50 border-green-200 text-green-800' : 'bg-red-50 border-red-200 text-red-800'}`}>
          {toast.msg}
          <button onClick={() => setToast(null)} className="ml-2 opacity-60 hover:opacity-100">✕</button>
        </div>
      )}

      {/* View switcher */}
      <div className="flex items-center gap-1">
        <button
          onClick={() => setView('roles')}
          className={`flex items-center gap-1.5 px-3 py-1.5 text-sm font-medium rounded-lg transition-colors ${view === 'roles' ? 'bg-indigo-50 text-indigo-700' : 'text-gray-600 hover:text-gray-900 hover:bg-gray-50'}`}
        >
          <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 12l2 2 4-4m5.618-4.016A11.955 11.955 0 0112 2.944a11.955 11.955 0 01-8.618 3.04A12.02 12.02 0 003 9c0 5.591 3.824 10.29 9 11.622 5.176-1.332 9-6.03 9-11.622 0-1.042-.133-2.052-.382-3.016z" /></svg>
          Role Permissions
        </button>
        <button
          onClick={() => setView('catalog')}
          className={`flex items-center gap-1.5 px-3 py-1.5 text-sm font-medium rounded-lg transition-colors ${view === 'catalog' ? 'bg-indigo-50 text-indigo-700' : 'text-gray-600 hover:text-gray-900 hover:bg-gray-50'}`}
        >
          <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 6h16M4 10h16M4 14h16M4 18h16" /></svg>
          Permission Catalog
        </button>
      </div>

      {/* ── Role Permissions view ──────────────────────────────────────────── */}
      {view === 'roles' && (
        <div className="grid grid-cols-1 lg:grid-cols-3 gap-4">

          {/* Role list */}
          <div className="bg-white border border-gray-200 rounded-xl overflow-hidden">
            <div className="px-4 py-3 border-b border-gray-100 bg-gray-50/50">
              <span className="text-xs font-semibold text-gray-500 uppercase tracking-wider">Roles</span>
            </div>
            {rolesLoading ? (
              <div className="py-8 text-center text-sm text-gray-400">Loading…</div>
            ) : (
              <ul className="divide-y divide-gray-50 max-h-96 overflow-y-auto">
                {tenantRoles.length === 0 ? (
                  <li className="px-4 py-6 text-sm text-gray-400 text-center">No roles found</li>
                ) : tenantRoles.map(r => (
                  <li key={r.id}>
                    <button
                      onClick={() => setSelectedRoleId(r.id)}
                      className={`w-full text-left px-4 py-3 text-sm transition-colors ${selectedRoleId === r.id ? 'bg-indigo-50 text-indigo-700 font-medium' : 'text-gray-700 hover:bg-gray-50'}`}
                    >
                      <div className="font-medium">{r.name}</div>
                      {r.isSystemRole && <div className="text-[11px] text-gray-400 mt-0.5">System role</div>}
                    </button>
                  </li>
                ))}
              </ul>
            )}
          </div>

          {/* Permission matrix */}
          <div className="lg:col-span-2 bg-white border border-gray-200 rounded-xl overflow-hidden">
            {!selectedRole ? (
              <div className="py-16 text-center space-y-2">
                <svg className="mx-auto w-8 h-8 text-gray-300" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5} d="M9 12l2 2 4-4m5.618-4.016A11.955 11.955 0 0112 2.944a11.955 11.955 0 01-8.618 3.04A12.02 12.02 0 003 9c0 5.591 3.824 10.29 9 11.622 5.176-1.332 9-6.03 9-11.622 0-1.042-.133-2.052-.382-3.016z" /></svg>
                <p className="text-sm text-gray-400">Select a role to view its permissions</p>
              </div>
            ) : (
              <div>
                <div className="px-4 py-3 border-b border-gray-100 bg-gray-50/50 flex items-center justify-between gap-2">
                  <div>
                    <span className="text-sm font-semibold text-gray-800">{selectedRole.name}</span>
                    {!isEditable && <span className="ml-2 text-[11px] text-gray-400">(read-only)</span>}
                  </div>
                  {isEditable && hasPending && (
                    <div className="flex items-center gap-2">
                      <button
                        onClick={() => { setPendingAdd(new Set()); setPendingRemove(new Set()); }}
                        className="text-xs px-3 py-1 rounded border border-gray-200 text-gray-500 hover:bg-gray-50"
                      >
                        Discard
                      </button>
                      <button
                        onClick={save}
                        disabled={saving}
                        className="text-xs px-3 py-1 rounded bg-indigo-600 hover:bg-indigo-700 text-white disabled:opacity-50 transition-colors"
                      >
                        {saving ? 'Saving…' : `Save ${pendingAdd.size + pendingRemove.size} change${pendingAdd.size + pendingRemove.size !== 1 ? 's' : ''}`}
                      </button>
                    </div>
                  )}
                </div>

                {permsLoading ? (
                  <div className="py-10 text-center text-sm text-gray-400">Loading permissions…</div>
                ) : permsError ? (
                  <div className="px-4 py-4 text-sm text-red-600">{permsError}</div>
                ) : rolePerms.length === 0 && !isEditable ? (
                  <div className="py-10 text-center text-sm text-gray-400">No permissions assigned to this role.</div>
                ) : (
                  <div className="divide-y divide-gray-50 max-h-96 overflow-y-auto">
                    {Object.entries(catalogByCategory).length === 0 && isEditable ? (
                      <div className="px-4 py-8 text-sm text-gray-400 text-center">
                        Load the Permission Catalog tab first to enable editing.
                      </div>
                    ) : null}
                    {isEditable && catalog.length > 0 ? (
                      Object.entries(groupByCategory(catalog)).map(([cat, perms]) => (
                        <div key={cat}>
                          <div className="px-4 py-2 text-[11px] font-semibold text-gray-400 uppercase tracking-wider bg-gray-50/60">{cat}</div>
                          {perms.map(p => (
                            <label key={p.id} className={`flex items-center gap-3 px-4 py-2.5 cursor-pointer hover:bg-indigo-50/40 transition-colors ${pendingAdd.has(p.id) ? 'bg-green-50/40' : pendingRemove.has(p.id) ? 'bg-red-50/40' : ''}`}>
                              <input
                                type="checkbox"
                                checked={isChecked(p.id)}
                                onChange={() => toggle(p.id)}
                                className="w-3.5 h-3.5 rounded border-gray-300 text-indigo-600 focus:ring-indigo-400/30"
                              />
                              <div className="flex-1 min-w-0">
                                <div className="text-sm text-gray-800">{p.name}</div>
                                <div className="text-[11px] text-gray-400 font-mono">{p.code}</div>
                              </div>
                            </label>
                          ))}
                        </div>
                      ))
                    ) : (
                      rolePerms.map(p => (
                        <div key={p.id} className="flex items-center gap-3 px-4 py-2.5">
                          <span className="w-3.5 h-3.5 rounded-full bg-indigo-100 flex items-center justify-center shrink-0">
                            <span className="w-1.5 h-1.5 rounded-full bg-indigo-600" />
                          </span>
                          <div>
                            <div className="text-sm text-gray-800">{p.name}</div>
                            <div className="text-[11px] text-gray-400 font-mono">{p.code}</div>
                          </div>
                        </div>
                      ))
                    )}
                  </div>
                )}
              </div>
            )}
          </div>
        </div>
      )}

      {/* ── Permission Catalog view ────────────────────────────────────────── */}
      {view === 'catalog' && (
        <div className="bg-white border border-gray-200 rounded-xl overflow-hidden">
          {catalogLoading ? (
            <div className="py-16 text-center text-sm text-gray-400">Loading permission catalog…</div>
          ) : catalog.length === 0 ? (
            <div className="py-16 text-center text-sm text-gray-400">No permissions found.</div>
          ) : (
            <div className="divide-y divide-gray-50">
              {Object.entries(groupByCategory(catalog)).map(([cat, perms]) => (
                <div key={cat}>
                  <div className="px-4 py-2 text-[11px] font-semibold text-gray-400 uppercase tracking-wider bg-gray-50/60 flex items-center justify-between">
                    <span>{cat}</span>
                    <span className="text-gray-300 font-normal">{perms.length}</span>
                  </div>
                  {perms.map(p => (
                    <div key={p.id} className="flex items-center gap-3 px-4 py-2.5 hover:bg-gray-50/50 transition-colors">
                      <div className="flex-1 min-w-0">
                        <div className="text-sm text-gray-800">{p.name}</div>
                        <div className="text-[11px] text-gray-400 font-mono">{p.code}</div>
                      </div>
                      {p.productCode && (
                        <span className="shrink-0 text-[11px] bg-indigo-50 text-indigo-600 border border-indigo-100 px-1.5 py-0.5 rounded">{p.productCode}</span>
                      )}
                    </div>
                  ))}
                </div>
              ))}
            </div>
          )}
        </div>
      )}
    </div>
  );
}
