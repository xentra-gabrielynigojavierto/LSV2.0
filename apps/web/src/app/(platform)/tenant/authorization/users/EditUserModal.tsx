'use client';

import { useState, useEffect, useCallback } from 'react';
import { Modal } from '@/components/lien/modal';
import { tenantClientApi, ApiError } from '@/lib/tenant-client-api';
import { useToast } from '@/lib/toast-context';
import type { TenantUser, TenantGroup, AssignableRoleItem } from '@/types/tenant';

interface EditUserModalProps {
  open:         boolean;
  user:         TenantUser;
  isLastAdmin?: boolean;
  onClose:      () => void;
  onSuccess:    () => void;
}

function ReadOnlyField({ label, value }: { label: string; value: string }) {
  return (
    <div className="space-y-1">
      <p className="text-xs font-medium text-gray-500">{label}</p>
      <p className="text-sm text-gray-700 py-2 px-3 rounded-md bg-gray-50 border border-gray-200 min-h-[38px]">
        {value || <span className="text-gray-400">—</span>}
      </p>
    </div>
  );
}

function SectionHeader({ icon, title }: { icon: string; title: string }) {
  return (
    <div className="flex items-center gap-2 pt-2">
      <i className={`${icon} text-gray-400 text-base`} />
      <h4 className="text-xs font-semibold text-gray-500 uppercase tracking-wide">{title}</h4>
      <div className="flex-1 h-px bg-gray-100" />
    </div>
  );
}

function isTenantRelevantRole(role: AssignableRoleItem): boolean {
  if (role.isProductRole) return true;
  if (role.isSystemRole && (role.name === 'TenantAdmin' || role.name === 'TenantUser')) return true;
  return false;
}

export function EditUserModal({ open, user, isLastAdmin = false, onClose, onSuccess }: EditUserModalProps) {
  const { show: showToast } = useToast();

  const [assignableRoles,    setAssignableRoles]    = useState<AssignableRoleItem[]>([]);
  const [roleId,             setRoleId]             = useState('');
  const [phone,              setPhone]              = useState('');
  const [initialRoleId,      setInitialRoleId]      = useState('');
  const [initialPhone,       setInitialPhone]       = useState('');

  const [allProducts,         setAllProducts]         = useState<{ code: string; name: string }[]>([]);
  const [tenantProductCodes,  setTenantProductCodes]  = useState<string[]>([]);
  const [selectedProductCodes,  setSelectedProductCodes]  = useState<Set<string>>(new Set());
  const [originalProductCodes,  setOriginalProductCodes]  = useState<Set<string>>(new Set());

  const [tenantGroups,    setTenantGroups]    = useState<TenantGroup[]>([]);
  const [selectedGroupIds, setSelectedGroupIds] = useState<Set<string>>(new Set());
  const [originalGroupIds, setOriginalGroupIds] = useState<Set<string>>(new Set());

  const [loading,     setLoading]     = useState(false);
  const [submitting,  setSubmitting]  = useState(false);
  const [apiError,    setApiError]    = useState<string | null>(null);
  const [roleError,   setRoleError]   = useState<string | null>(null);

  const loadData = useCallback(async () => {
    setLoading(true);
    setApiError(null);
    setRoleError(null);

    const [
      detailResult,
      rolesResult,
      allProductsResult,
      tenantProductsResult,
      userProductsResult,
      groupsResult,
    ] = await Promise.allSettled([
      tenantClientApi.getUserDetail(user.id),
      tenantClientApi.getAssignableRoles(user.id),
      tenantClientApi.getProducts(),
      tenantClientApi.getTenantProducts(user.tenantId),
      tenantClientApi.getUserProducts(user.tenantId, user.id),
      tenantClientApi.getGroups(user.tenantId),
    ]);

    if (detailResult.status === 'fulfilled') {
      const detail = detailResult.value.data;
      const currentRoleId = detail.roles[0]?.roleId ?? '';
      setRoleId(currentRoleId);
      setInitialRoleId(currentRoleId);
      setPhone(detail.phone ?? '');
      setInitialPhone(detail.phone ?? '');

      const currentGroupIds = new Set((detail.groups ?? []).map(g => g.groupId));
      setSelectedGroupIds(new Set(currentGroupIds));
      setOriginalGroupIds(new Set(currentGroupIds));
    } else {
      setApiError('Unable to load user details. Please try again.');
    }

    if (rolesResult.status === 'fulfilled') {
      setAssignableRoles((rolesResult.value.data?.items ?? []).filter(isTenantRelevantRole));
    }

    if (allProductsResult.status === 'fulfilled') {
      setAllProducts(allProductsResult.value.data ?? []);
    }

    if (tenantProductsResult.status === 'fulfilled') {
      const enabled = (tenantProductsResult.value.data ?? [])
        .filter(tp => tp.status === 'Active')
        .map(tp => tp.productCode);
      setTenantProductCodes(enabled);
    }

    if (userProductsResult.status === 'fulfilled') {
      const granted = new Set(
        (userProductsResult.value.data ?? [])
          .filter(up => up.accessStatus === 'Granted')
          .map(up => up.productCode)
      );
      setSelectedProductCodes(new Set(granted));
      setOriginalProductCodes(new Set(granted));
    }

    if (groupsResult.status === 'fulfilled') {
      setTenantGroups(
        (groupsResult.value.data ?? []).filter(g => g.status !== 'Archived')
      );
    }

    setLoading(false);
  }, [user.id, user.tenantId]);

  useEffect(() => {
    if (open) {
      setRoleId('');
      setPhone('');
      setInitialRoleId('');
      setInitialPhone('');
      setAssignableRoles([]);
      setAllProducts([]);
      setTenantProductCodes([]);
      setSelectedProductCodes(new Set());
      setOriginalProductCodes(new Set());
      setTenantGroups([]);
      setSelectedGroupIds(new Set());
      setOriginalGroupIds(new Set());
      setApiError(null);
      setRoleError(null);
      loadData();
    }
  }, [open, loadData]);

  function toggleProduct(code: string) {
    setSelectedProductCodes(prev => {
      const next = new Set(prev);
      if (next.has(code)) next.delete(code);
      else next.add(code);
      return next;
    });
  }

  function toggleGroup(groupId: string) {
    setSelectedGroupIds(prev => {
      const next = new Set(prev);
      if (next.has(groupId)) next.delete(groupId);
      else next.add(groupId);
      return next;
    });
  }

  async function handleSave() {
    if (!roleId) {
      setRoleError('Please select a role.');
      return;
    }

    const roleChanged  = roleId !== initialRoleId;
    const phoneChanged = phone.trim() !== initialPhone;

    if (roleChanged && isLastAdmin) {
      const selectedRole = assignableRoles.find(r => r.id === roleId);
      const userIsCurrentAdmin = (user.roles ?? []).includes('TenantAdmin');
      if (userIsCurrentAdmin && selectedRole?.name !== 'TenantAdmin') {
        setRoleError('Cannot remove TenantAdmin from the last active tenant administrator. Assign another admin first.');
        return;
      }
    }

    const productsToGrant  = [...selectedProductCodes].filter(c => !originalProductCodes.has(c));
    const productsToRevoke = [...originalProductCodes].filter(c => !selectedProductCodes.has(c));
    const groupsToAdd      = [...selectedGroupIds].filter(id => !originalGroupIds.has(id));
    const groupsToRemove   = [...originalGroupIds].filter(id => !selectedGroupIds.has(id));

    const hasChanges = roleChanged || phoneChanged ||
      productsToGrant.length > 0 || productsToRevoke.length > 0 ||
      groupsToAdd.length > 0 || groupsToRemove.length > 0;

    if (!hasChanges) {
      onClose();
      return;
    }

    setSubmitting(true);
    setApiError(null);

    try {
      const ops: Promise<unknown>[] = [];

      if (roleChanged) {
        const detail = await tenantClientApi.getUserDetail(user.id);
        const currentRoleIds = detail.data.roles.map(r => r.roleId);
        if (currentRoleIds.length > 0) {
          await Promise.all(currentRoleIds.map(rid => tenantClientApi.removeRole(user.id, rid)));
        }
        await tenantClientApi.assignRole(user.id, roleId);
      }

      if (phoneChanged) {
        ops.push(tenantClientApi.updatePhone(user.id, phone.trim() || null));
      }

      productsToGrant.forEach(code => {
        ops.push(tenantClientApi.assignProduct(user.tenantId, user.id, code));
      });
      productsToRevoke.forEach(code => {
        ops.push(tenantClientApi.removeProduct(user.tenantId, user.id, code));
      });

      groupsToAdd.forEach(groupId => {
        ops.push(tenantClientApi.addToGroup(user.tenantId, groupId, user.id));
      });
      groupsToRemove.forEach(groupId => {
        ops.push(tenantClientApi.removeFromGroup(user.tenantId, groupId, user.id));
      });

      if (ops.length > 0) await Promise.all(ops);

      showToast('User updated successfully.', 'success');
      onSuccess();
    } catch (err) {
      if (err instanceof ApiError) {
        if (err.isForbidden) {
          setApiError('You do not have permission to edit this user.');
        } else if (err.status === 400) {
          setApiError(err.message || 'Invalid data. Please check your input.');
        } else if (err.status === 422) {
          setApiError(err.message || 'This action is not allowed.');
        } else {
          setApiError('Something went wrong. Please try again.');
        }
      } else {
        setApiError('Something went wrong. Please try again.');
      }
    } finally {
      setSubmitting(false);
    }
  }

  const displayName = [user.firstName, user.lastName].filter(Boolean).join(' ') || user.email || 'Unknown User';

  const enabledProducts = allProducts.filter(p => tenantProductCodes.includes(p.code));

  return (
    <Modal
      open={open}
      onClose={onClose}
      title="Edit User"
      subtitle={displayName}
      size="lg"
      footer={
        <>
          <button
            onClick={onClose}
            disabled={submitting}
            className="text-sm px-4 py-2 border border-gray-200 rounded-lg hover:bg-gray-50 text-gray-600 disabled:opacity-50"
          >
            Cancel
          </button>
          <button
            onClick={handleSave}
            disabled={loading || submitting}
            className="text-sm px-4 py-2 bg-primary hover:bg-primary/90 text-white rounded-lg disabled:opacity-50 flex items-center gap-2"
          >
            {submitting && <i className="ri-loader-4-line animate-spin text-base" />}
            {submitting ? 'Saving...' : 'Save Changes'}
          </button>
        </>
      }
    >
      <div className="space-y-5">
        {apiError && (
          <div className="rounded-lg border border-red-200 bg-red-50 px-3 py-2.5 text-sm text-red-700 flex items-start gap-2">
            <i className="ri-error-warning-line text-base mt-0.5 shrink-0" />
            <span>{apiError}</span>
          </div>
        )}

        {loading ? (
          <div className="flex items-center justify-center py-12">
            <i className="ri-loader-4-line animate-spin text-2xl text-gray-300" />
          </div>
        ) : (
          <>
            <SectionHeader icon="ri-user-line" title="Identity" />

            <div className="grid grid-cols-2 gap-4">
              <ReadOnlyField label="First Name" value={(user.firstName ?? '').trim()} />
              <ReadOnlyField label="Last Name"  value={(user.lastName  ?? '').trim()} />
            </div>
            <ReadOnlyField label="Email" value={(user.email ?? '').trim()} />

            <SectionHeader icon="ri-shield-user-line" title="Role & Phone" />

            <div className="space-y-1">
              <label className="block text-xs font-medium text-gray-700">
                Role <span className="text-red-500">*</span>
              </label>
              <select
                value={roleId}
                onChange={e => { setRoleId(e.target.value); setRoleError(null); }}
                className={`w-full rounded-md border py-2 px-3 text-sm text-gray-900 focus:outline-none focus:ring-2 focus:ring-primary/50 focus:border-primary ${roleError ? 'border-red-400 bg-red-50' : 'border-gray-300 bg-white'}`}
              >
                <option value="">— Select a role —</option>
                {assignableRoles.map(r => (
                  <option
                    key={r.id}
                    value={r.id}
                    disabled={!r.assignable && !r.isAssigned}
                    title={!r.assignable && !r.isAssigned ? (r.disabledReason ?? '') : undefined}
                  >
                    {r.name}
                    {r.productName ? ` (${r.productName})` : ''}
                    {!r.assignable && !r.isAssigned && r.disabledReason ? ` — ${r.disabledReason}` : ''}
                  </option>
                ))}
              </select>
              {roleError && <p className="text-xs text-red-600">{roleError}</p>}
              {assignableRoles.length === 0 && (
                <p className="text-xs text-gray-400">No roles available for this tenant.</p>
              )}
            </div>

            <div className="space-y-1">
              <label className="block text-xs font-medium text-gray-700">Phone</label>
              <input
                type="tel"
                value={phone}
                onChange={e => setPhone(e.target.value)}
                placeholder="+1 555 000 0000"
                autoComplete="tel"
                className="w-full rounded-md border border-gray-300 bg-white py-2 px-3 text-sm text-gray-900 placeholder-gray-400 focus:outline-none focus:ring-2 focus:ring-primary/50 focus:border-primary"
              />
              <p className="text-[11px] text-gray-400">Optional. Must be in E.164 format (e.g. +15551234567).</p>
            </div>

            <SectionHeader icon="ri-apps-line" title="Products" />

            {enabledProducts.length === 0 ? (
              <p className="text-sm text-gray-400 italic py-1">No products are enabled for this tenant.</p>
            ) : (
              <div className="grid grid-cols-2 gap-2">
                {enabledProducts.map(product => {
                  const checked = selectedProductCodes.has(product.code);
                  return (
                    <label
                      key={product.code}
                      className={`flex items-center gap-3 rounded-lg border px-3 py-2.5 cursor-pointer transition-colors ${
                        checked
                          ? 'border-blue-200 bg-blue-50'
                          : 'border-gray-200 bg-white hover:bg-gray-50'
                      }`}
                    >
                      <input
                        type="checkbox"
                        checked={checked}
                        onChange={() => toggleProduct(product.code)}
                        className="h-4 w-4 rounded border-gray-300 text-blue-600 focus:ring-blue-500"
                      />
                      <div>
                        <p className={`text-sm font-medium ${checked ? 'text-blue-800' : 'text-gray-800'}`}>
                          {product.name}
                        </p>
                        <p className="text-[11px] text-gray-400 font-mono">{product.code}</p>
                      </div>
                    </label>
                  );
                })}
              </div>
            )}

            <SectionHeader icon="ri-group-line" title="Groups" />

            {tenantGroups.length === 0 ? (
              <p className="text-sm text-gray-400 italic py-1">No groups have been created for this tenant.</p>
            ) : (
              <div className="grid grid-cols-2 gap-2">
                {tenantGroups.map(group => {
                  const checked = selectedGroupIds.has(group.id);
                  return (
                    <label
                      key={group.id}
                      className={`flex items-center gap-3 rounded-lg border px-3 py-2.5 cursor-pointer transition-colors ${
                        checked
                          ? 'border-purple-200 bg-purple-50'
                          : 'border-gray-200 bg-white hover:bg-gray-50'
                      }`}
                    >
                      <input
                        type="checkbox"
                        checked={checked}
                        onChange={() => toggleGroup(group.id)}
                        className="h-4 w-4 rounded border-gray-300 text-purple-600 focus:ring-purple-500"
                      />
                      <div className="min-w-0">
                        <p className={`text-sm font-medium truncate ${checked ? 'text-purple-800' : 'text-gray-800'}`}>
                          {group.name}
                        </p>
                        {group.description && (
                          <p className="text-[11px] text-gray-400 truncate">{group.description}</p>
                        )}
                      </div>
                    </label>
                  );
                })}
              </div>
            )}

            <p className="text-[11px] text-gray-400 pt-1">
              Name and email cannot be changed here. Contact your platform administrator for profile updates.
            </p>
          </>
        )}
      </div>
    </Modal>
  );
}
