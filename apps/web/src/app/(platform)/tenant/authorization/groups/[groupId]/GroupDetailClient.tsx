'use client';

import { useState, useMemo, useCallback } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { tenantClientApi, ApiError } from '@/lib/tenant-client-api';
import type { TenantGroup, TenantUser, GroupMember, GroupProductAccess, GroupRoleAssignment } from '@/types/tenant';

interface Props {
  group: TenantGroup;
  members: GroupMember[];
  products: GroupProductAccess[];
  roles: GroupRoleAssignment[];
  allUsers: TenantUser[];
  allRoles: { id: string; name: string }[];
  tenantId: string;
}

function SectionCard({ title, icon, children, action, count }: {
  title: string;
  icon: string;
  children: React.ReactNode;
  action?: React.ReactNode;
  count?: number;
}) {
  return (
    <div className="rounded-xl border border-gray-200 bg-white">
      <div className="flex items-center justify-between px-5 py-3 border-b border-gray-100">
        <div className="flex items-center gap-2">
          <i className={`${icon} text-base text-gray-500`} />
          <h3 className="text-sm font-semibold text-gray-900">{title}</h3>
          {count !== undefined && (
            <span className="text-[10px] px-1.5 py-0.5 rounded-full bg-gray-100 text-gray-500 font-medium">{count}</span>
          )}
        </div>
        {action}
      </div>
      <div className="px-5 py-4">{children}</div>
    </div>
  );
}

function ConfirmModal({ open, onClose, onConfirm, title, description, loading, confirmLabel }: {
  open: boolean;
  onClose: () => void;
  onConfirm: () => void;
  title: string;
  description: string;
  loading: boolean;
  confirmLabel?: string;
}) {
  if (!open) return null;
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4" role="dialog" aria-modal="true">
      <div className="fixed inset-0 bg-black/40" aria-hidden="true" onClick={onClose} />
      <div className="relative bg-white rounded-xl shadow-xl w-full max-w-sm p-6">
        <h3 className="text-base font-semibold text-gray-900 mb-2">{title}</h3>
        <p className="text-sm text-gray-600 mb-5">{description}</p>
        <div className="flex items-center justify-end gap-2">
          <button onClick={onClose} className="text-sm px-4 py-2 border border-gray-200 rounded-lg hover:bg-gray-50 text-gray-600">Cancel</button>
          <button onClick={onConfirm} disabled={loading} className="text-sm px-4 py-2 bg-red-600 hover:bg-red-700 text-white rounded-lg disabled:opacity-50">
            {loading ? 'Removing...' : (confirmLabel ?? 'Remove')}
          </button>
        </div>
      </div>
    </div>
  );
}

function Toast({ message, type, onClose }: { message: string; type: 'success' | 'error'; onClose: () => void }) {
  const bg = type === 'success' ? 'bg-green-50 border-green-200 text-green-800' : 'bg-red-50 border-red-200 text-red-800';
  const icon = type === 'success' ? 'ri-check-line' : 'ri-error-warning-line';
  return (
    <div className={`fixed bottom-4 right-4 z-50 flex items-center gap-2 px-4 py-3 rounded-lg border shadow-lg text-sm ${bg}`}>
      <i className={`${icon} text-base`} />
      {message}
      <button onClick={onClose} className="ml-2 opacity-60 hover:opacity-100"><i className="ri-close-line" /></button>
    </div>
  );
}

const AVAILABLE_PRODUCTS = [
  { code: 'SYNQ_FUND', label: 'Synq Funds' },
  { code: 'SYNQ_LIEN', label: 'Synq Liens' },
  { code: 'SYNQ_CARECONNECT', label: 'Synq CareConnect' },
  { code: 'SYNQ_AI', label: 'Synq AI' },
  { code: 'SYNQ_INSIGHTS', label: 'Synq Insights' },
];

export function GroupDetailClient({ group, members, products, roles, allUsers, allRoles, tenantId }: Props) {
  const router = useRouter();
  const [toast, setToast] = useState<{ message: string; type: 'success' | 'error' } | null>(null);
  const [loading, setLoading] = useState(false);
  const [confirm, setConfirm] = useState<{ title: string; description: string; action: () => Promise<void>; label?: string } | null>(null);

  const [showMemberPicker, setShowMemberPicker] = useState(false);
  const [memberSearch, setMemberSearch] = useState('');
  const [showProductPicker, setShowProductPicker] = useState(false);
  const [showRolePicker, setShowRolePicker] = useState(false);
  const [showEdit, setShowEdit] = useState(false);
  const [editName, setEditName] = useState(group.name);
  const [editDesc, setEditDesc] = useState(group.description ?? '');

  const showToast = useCallback((message: string, type: 'success' | 'error') => {
    setToast({ message, type });
    setTimeout(() => setToast(null), 4000);
  }, []);

  async function handleAction(fn: () => Promise<void>, successMsg: string) {
    setLoading(true);
    try {
      await fn();
      showToast(successMsg, 'success');
      router.refresh();
    } catch (err) {
      const msg = err instanceof ApiError ? err.message : 'Operation failed';
      showToast(msg, 'error');
    } finally {
      setLoading(false);
      setConfirm(null);
    }
  }

  async function handleEdit() {
    if (!editName.trim()) return;
    setLoading(true);
    try {
      await tenantClientApi.updateGroup(tenantId, group.id, {
        name: editName.trim(),
        description: editDesc.trim() || undefined,
      });
      showToast('Group updated', 'success');
      setShowEdit(false);
      router.refresh();
    } catch (err) {
      showToast(err instanceof ApiError ? err.message : 'Failed to update group', 'error');
    } finally {
      setLoading(false);
    }
  }

  const memberUserIds = new Set(members.filter((m) => m.membershipStatus === 'Active').map((m) => m.userId));
  const activeProducts = products.filter((p) => p.accessStatus === 'Active');
  const activeRoles = roles.filter((r) => r.assignmentStatus === 'Active');

  const availableUsers = useMemo(() => {
    const base = allUsers.filter((u) => u.isActive && !memberUserIds.has(u.id));
    if (!memberSearch.trim()) return base.slice(0, 20);
    const q = memberSearch.toLowerCase();
    return base.filter((u) =>
      u.firstName.toLowerCase().includes(q) ||
      u.lastName.toLowerCase().includes(q) ||
      u.email.toLowerCase().includes(q)
    ).slice(0, 20);
  }, [allUsers, memberUserIds, memberSearch]);

  const assignedProductCodes = new Set(activeProducts.map((p) => p.productCode));
  const availableProducts = AVAILABLE_PRODUCTS.filter((p) => !assignedProductCodes.has(p.code));

  const assignedRoleCodes = new Set(activeRoles.map((r) => r.roleCode));
  const availableRoles = allRoles.filter((r) => !assignedRoleCodes.has(r.name));

  const userMap = useMemo(() => {
    const map = new Map<string, TenantUser>();
    allUsers.forEach((u) => map.set(u.id, u));
    return map;
  }, [allUsers]);

  return (
    <div className="space-y-6">
      <div className="flex items-center gap-3">
        <Link
          href="/tenant/authorization/groups"
          className="p-2 rounded-lg hover:bg-gray-100 text-gray-400 hover:text-gray-600 transition-colors"
          aria-label="Back to Groups"
        >
          <i className="ri-arrow-left-line text-lg" />
        </Link>
        <div className="flex-1">
          <h2 className="text-lg font-semibold text-gray-900">{group.name}</h2>
          {group.description && <p className="text-sm text-gray-500">{group.description}</p>}
        </div>
        <div className="flex items-center gap-2">
          <button
            onClick={() => setShowEdit(true)}
            className="flex items-center gap-1.5 px-3 py-2 text-sm font-medium text-gray-600 bg-white border border-gray-200 rounded-lg hover:bg-gray-50 transition-colors"
          >
            <i className="ri-edit-line text-base" />
            Edit
          </button>
          {group.status === 'Active' && (
            <button
              onClick={() => setConfirm({
                title: 'Archive Group',
                description: `Archive "${group.name}"? Members will lose access inherited from this group.`,
                action: () => tenantClientApi.archiveGroup(tenantId, group.id).then(() => { router.push('/tenant/authorization/groups'); }),
                label: 'Archive',
              })}
              className="flex items-center gap-1.5 px-3 py-2 text-sm font-medium text-red-600 bg-white border border-red-200 rounded-lg hover:bg-red-50 transition-colors"
            >
              <i className="ri-archive-line text-base" />
              Archive
            </button>
          )}
        </div>
      </div>

      {showEdit && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4" role="dialog" aria-modal="true">
          <div className="fixed inset-0 bg-black/40" aria-hidden="true" onClick={() => setShowEdit(false)} />
          <div className="relative bg-white rounded-xl shadow-xl w-full max-w-md p-6">
            <h3 className="text-base font-semibold text-gray-900 mb-4">Edit Group</h3>
            <div className="space-y-4">
              <div>
                <label className="block text-xs font-medium text-gray-600 mb-1">Group Name *</label>
                <input
                  type="text"
                  value={editName}
                  onChange={(e) => setEditName(e.target.value)}
                  className="w-full px-3 py-2 text-sm border border-gray-200 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary"
                  autoFocus
                />
              </div>
              <div>
                <label className="block text-xs font-medium text-gray-600 mb-1">Description</label>
                <textarea
                  value={editDesc}
                  onChange={(e) => setEditDesc(e.target.value)}
                  className="w-full px-3 py-2 text-sm border border-gray-200 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary resize-none"
                  rows={3}
                />
              </div>
            </div>
            <div className="flex items-center justify-end gap-2 mt-5">
              <button onClick={() => setShowEdit(false)} className="text-sm px-4 py-2 border border-gray-200 rounded-lg hover:bg-gray-50 text-gray-600">Cancel</button>
              <button
                onClick={handleEdit}
                disabled={loading || !editName.trim()}
                className="text-sm px-4 py-2 bg-primary hover:bg-primary/90 text-white rounded-lg disabled:opacity-50"
              >
                {loading ? 'Saving...' : 'Save'}
              </button>
            </div>
          </div>
        </div>
      )}

      <SectionCard title="Summary" icon="ri-information-line">
        <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
          <div>
            <p className="text-[11px] font-medium text-gray-400 uppercase tracking-wider mb-1">Status</p>
            <span className={`inline-flex items-center gap-1 px-2 py-0.5 rounded text-[11px] font-semibold border ${
              group.status === 'Active' ? 'bg-green-50 text-green-700 border-green-200' : 'bg-gray-100 text-gray-500 border-gray-200'
            }`}>
              <span className={`w-1.5 h-1.5 rounded-full ${group.status === 'Active' ? 'bg-green-500' : 'bg-gray-400'}`} />
              {group.status}
            </span>
          </div>
          <div>
            <p className="text-[11px] font-medium text-gray-400 uppercase tracking-wider mb-1">Scope</p>
            <span className="text-xs px-2 py-0.5 rounded bg-gray-100 text-gray-600 font-medium">{group.scopeType}</span>
          </div>
          <div>
            <p className="text-[11px] font-medium text-gray-400 uppercase tracking-wider mb-1">Members</p>
            <p className="text-sm text-gray-900 font-medium">{memberUserIds.size}</p>
          </div>
          <div>
            <p className="text-[11px] font-medium text-gray-400 uppercase tracking-wider mb-1">Created</p>
            <p className="text-sm text-gray-900">{new Date(group.createdAtUtc).toLocaleDateString()}</p>
          </div>
        </div>
      </SectionCard>

      <div className="rounded-xl border border-amber-200 bg-amber-50 px-5 py-3 flex items-center gap-3">
        <i className="ri-user-star-line text-lg text-amber-600" />
        <div>
          <p className="text-sm font-medium text-amber-800">This group affects {memberUserIds.size} user{memberUserIds.size !== 1 ? 's' : ''}</p>
          <p className="text-xs text-amber-600">
            {activeProducts.length > 0 && `${activeProducts.length} product${activeProducts.length !== 1 ? 's' : ''}`}
            {activeProducts.length > 0 && activeRoles.length > 0 && ' and '}
            {activeRoles.length > 0 && `${activeRoles.length} role${activeRoles.length !== 1 ? 's' : ''}`}
            {activeProducts.length === 0 && activeRoles.length === 0 && 'No products or roles assigned yet'}
            {(activeProducts.length > 0 || activeRoles.length > 0) && ' inherited by members'}
          </p>
        </div>
      </div>

      <SectionCard
        title="Members"
        icon="ri-user-line"
        count={memberUserIds.size}
        action={
          <button
            onClick={() => { setShowMemberPicker(!showMemberPicker); setMemberSearch(''); }}
            className="text-xs text-primary hover:text-primary/80 font-medium flex items-center gap-1"
          >
            <i className="ri-add-line" /> Add Member
          </button>
        }
      >
        {showMemberPicker && (
          <div className="mb-4 p-3 rounded-lg border border-gray-200 bg-gray-50">
            <div className="relative mb-2">
              <i className="ri-search-line absolute left-2.5 top-1/2 -translate-y-1/2 text-gray-400 text-xs" />
              <input
                type="text"
                placeholder="Search users by name or email..."
                value={memberSearch}
                onChange={(e) => setMemberSearch(e.target.value)}
                className="w-full pl-8 pr-3 py-1.5 text-xs border border-gray-200 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary bg-white"
                autoFocus
              />
            </div>
            {availableUsers.length === 0 ? (
              <p className="text-xs text-gray-400 py-2">{allUsers.length === 0 ? 'No users available' : 'No matching users found'}</p>
            ) : (
              <div className="max-h-48 overflow-y-auto space-y-1">
                {availableUsers.map((u) => (
                  <button
                    key={u.id}
                    disabled={loading}
                    onClick={() => handleAction(
                      () => tenantClientApi.addToGroup(tenantId, group.id, u.id).then(() => {}),
                      `${u.firstName} ${u.lastName} added`
                    ).then(() => { setShowMemberPicker(false); setMemberSearch(''); })}
                    className="w-full flex items-center gap-3 px-3 py-2 rounded-lg hover:bg-blue-50 text-left disabled:opacity-40 transition-colors"
                  >
                    <div className="w-7 h-7 rounded-full bg-indigo-100 flex items-center justify-center flex-shrink-0">
                      <span className="text-[10px] font-bold text-indigo-600">{u.firstName.charAt(0)}{u.lastName.charAt(0)}</span>
                    </div>
                    <div>
                      <p className="text-xs font-medium text-gray-900">{u.firstName} {u.lastName}</p>
                      <p className="text-[10px] text-gray-500">{u.email}</p>
                    </div>
                  </button>
                ))}
              </div>
            )}
          </div>
        )}
        {memberUserIds.size === 0 ? (
          <p className="text-sm text-gray-400 py-2">No members in this group</p>
        ) : (
          <div className="space-y-1.5">
            {members.filter((m) => m.membershipStatus === 'Active').map((m) => {
              const u = userMap.get(m.userId);
              return (
                <div key={m.id} className="flex items-center justify-between py-2 px-3 rounded-lg hover:bg-gray-50">
                  <div className="flex items-center gap-3">
                    <div className="w-8 h-8 rounded-full bg-indigo-100 flex items-center justify-center flex-shrink-0">
                      <span className="text-[11px] font-bold text-indigo-600">
                        {u ? `${u.firstName.charAt(0)}${u.lastName.charAt(0)}` : '??'}
                      </span>
                    </div>
                    <div>
                      <p className="text-sm font-medium text-gray-900">
                        {u ? `${u.firstName} ${u.lastName}` : m.userId}
                      </p>
                      {u && <p className="text-[11px] text-gray-500">{u.email}</p>}
                    </div>
                  </div>
                  <button
                    onClick={() => setConfirm({
                      title: 'Remove Member',
                      description: `Remove ${u ? `${u.firstName} ${u.lastName}` : 'this user'} from ${group.name}?`,
                      action: () => tenantClientApi.removeFromGroup(tenantId, group.id, m.userId).then(() => {}),
                    })}
                    className="text-xs text-red-600 hover:text-red-700 font-medium"
                  >
                    Remove
                  </button>
                </div>
              );
            })}
          </div>
        )}
      </SectionCard>

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        <SectionCard
          title="Product Access"
          icon="ri-apps-line"
          count={activeProducts.length}
          action={
            <button
              onClick={() => setShowProductPicker(!showProductPicker)}
              className="text-xs text-primary hover:text-primary/80 font-medium flex items-center gap-1"
            >
              <i className="ri-add-line" /> Add Product
            </button>
          }
        >
          {showProductPicker && (
            <div className="mb-4 p-3 rounded-lg border border-gray-200 bg-gray-50">
              <p className="text-xs font-medium text-gray-600 mb-2">Select product to grant:</p>
              {availableProducts.length === 0 ? (
                <p className="text-xs text-gray-400">All products already assigned</p>
              ) : (
                <div className="flex flex-wrap gap-2">
                  {availableProducts.map((p) => (
                    <button
                      key={p.code}
                      disabled={loading}
                      onClick={() => handleAction(
                        () => tenantClientApi.grantGroupProduct(tenantId, group.id, p.code).then(() => {}),
                        `${p.label} granted`
                      ).then(() => setShowProductPicker(false))}
                      className="text-xs px-3 py-1.5 rounded-lg border border-gray-200 bg-white hover:bg-blue-50 hover:border-blue-200 text-gray-700 disabled:opacity-40 transition-colors"
                    >
                      {p.label}
                    </button>
                  ))}
                </div>
              )}
            </div>
          )}
          {activeProducts.length === 0 ? (
            <p className="text-sm text-gray-400 py-2">No product access configured</p>
          ) : (
            <div className="space-y-2">
              {activeProducts.map((p) => {
                const label = AVAILABLE_PRODUCTS.find((ap) => ap.code === p.productCode)?.label ?? p.productCode;
                return (
                  <div key={p.id} className="flex items-center justify-between py-2 px-3 rounded-lg hover:bg-gray-50">
                    <div className="flex items-center gap-2">
                      <i className="ri-apps-line text-base text-blue-500" />
                      <span className="text-sm font-medium text-gray-900">{label}</span>
                      <span className="text-[10px] text-gray-400 font-mono">{p.productCode}</span>
                    </div>
                    <button
                      onClick={() => setConfirm({
                        title: 'Revoke Product',
                        description: `Revoke ${label} from ${group.name}? All members will lose inherited access.`,
                        action: () => tenantClientApi.revokeGroupProduct(tenantId, group.id, p.productCode).then(() => {}),
                      })}
                      className="text-xs text-red-600 hover:text-red-700 font-medium"
                    >
                      Revoke
                    </button>
                  </div>
                );
              })}
            </div>
          )}
        </SectionCard>

        <SectionCard
          title="Role Assignments"
          icon="ri-shield-user-line"
          count={activeRoles.length}
          action={
            <button
              onClick={() => setShowRolePicker(!showRolePicker)}
              className="text-xs text-primary hover:text-primary/80 font-medium flex items-center gap-1"
            >
              <i className="ri-add-line" /> Assign Role
            </button>
          }
        >
          {showRolePicker && (
            <div className="mb-4 p-3 rounded-lg border border-gray-200 bg-gray-50">
              <p className="text-xs font-medium text-gray-600 mb-2">Select role to assign:</p>
              {availableRoles.length === 0 ? (
                <p className="text-xs text-gray-400">All roles already assigned</p>
              ) : (
                <div className="flex flex-wrap gap-2 max-h-40 overflow-y-auto">
                  {availableRoles.map((r) => (
                    <button
                      key={r.id}
                      disabled={loading}
                      onClick={() => handleAction(
                        () => tenantClientApi.assignGroupRole(tenantId, group.id, r.name).then(() => {}),
                        `Role ${r.name} assigned`
                      ).then(() => setShowRolePicker(false))}
                      className="text-xs px-3 py-1.5 rounded-lg border border-gray-200 bg-white hover:bg-indigo-50 hover:border-indigo-200 text-gray-700 disabled:opacity-40 transition-colors"
                    >
                      {r.name}
                    </button>
                  ))}
                </div>
              )}
            </div>
          )}
          {activeRoles.length === 0 ? (
            <p className="text-sm text-gray-400 py-2">No roles assigned</p>
          ) : (
            <div className="space-y-2">
              {activeRoles.map((r) => (
                <div key={r.id} className="flex items-center justify-between py-2 px-3 rounded-lg hover:bg-gray-50">
                  <div className="flex items-center gap-2">
                    <i className="ri-shield-user-line text-base text-indigo-500" />
                    <span className="text-sm font-medium text-gray-900">{r.roleCode}</span>
                    {r.productCode && <span className="text-[10px] text-gray-400">({r.productCode})</span>}
                  </div>
                  <button
                    onClick={() => setConfirm({
                      title: 'Remove Role',
                      description: `Remove role ${r.roleCode} from ${group.name}? All members will lose this role.`,
                      action: () => tenantClientApi.removeGroupRole(tenantId, group.id, r.id).then(() => {}),
                    })}
                    className="text-xs text-red-600 hover:text-red-700 font-medium"
                  >
                    Remove
                  </button>
                </div>
              ))}
            </div>
          )}
        </SectionCard>
      </div>

      <SectionCard title="Effective Access Preview" icon="ri-shield-check-line">
        <div className="space-y-5">
          <div>
            <h4 className="text-xs font-semibold text-gray-500 uppercase tracking-wider mb-3">Products Granted</h4>
            {activeProducts.length === 0 ? (
              <p className="text-sm text-gray-400">No products — members receive no product access from this group</p>
            ) : (
              <div className="space-y-1.5">
                {activeProducts.map((p) => {
                  const label = AVAILABLE_PRODUCTS.find((ap) => ap.code === p.productCode)?.label ?? p.productCode;
                  return (
                    <div key={p.id} className="flex items-center gap-2 py-1.5 px-3 rounded-lg bg-blue-50/50">
                      <i className="ri-checkbox-circle-line text-sm text-blue-500" />
                      <span className="text-sm text-gray-900">{label}</span>
                      <span className="text-[10px] px-1.5 py-0.5 rounded bg-purple-50 text-purple-700 font-medium ml-auto">Group</span>
                    </div>
                  );
                })}
              </div>
            )}
          </div>

          <div>
            <h4 className="text-xs font-semibold text-gray-500 uppercase tracking-wider mb-3">Roles Granted</h4>
            {activeRoles.length === 0 ? (
              <p className="text-sm text-gray-400">No roles — members receive no roles from this group</p>
            ) : (
              <div className="space-y-1.5">
                {activeRoles.map((r) => (
                  <div key={r.id} className="flex items-center gap-2 py-1.5 px-3 rounded-lg bg-indigo-50/50">
                    <i className="ri-shield-check-line text-sm text-indigo-500" />
                    <span className="text-sm text-gray-900">{r.roleCode}</span>
                    {r.productCode && <span className="text-[10px] text-gray-400">{r.productCode}</span>}
                    <span className="text-[10px] px-1.5 py-0.5 rounded bg-purple-50 text-purple-700 font-medium ml-auto">Group</span>
                  </div>
                ))}
              </div>
            )}
          </div>

          {(activeProducts.length > 0 || activeRoles.length > 0) && (
            <div className="pt-3 border-t border-gray-100">
              <p className="text-xs text-gray-500">
                <i className="ri-information-line mr-1" />
                All {memberUserIds.size} member{memberUserIds.size !== 1 ? 's' : ''} of this group inherit the above access.
                View a specific user&apos;s effective access on their detail page.
              </p>
            </div>
          )}
        </div>
      </SectionCard>

      <ConfirmModal
        open={!!confirm}
        onClose={() => setConfirm(null)}
        onConfirm={() => confirm && handleAction(confirm.action, 'Done')}
        title={confirm?.title ?? ''}
        description={confirm?.description ?? ''}
        loading={loading}
        confirmLabel={confirm?.label}
      />

      {toast && <Toast message={toast.message} type={toast.type} onClose={() => setToast(null)} />}
    </div>
  );
}
