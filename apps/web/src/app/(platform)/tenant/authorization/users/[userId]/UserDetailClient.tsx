'use client';

import { useState, useCallback } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { tenantClientApi, ApiError } from '@/lib/tenant-client-api';
import type { TenantUserDetail, AccessDebugResponse, TenantGroup, AssignableRolesResponse } from '@/types/tenant';
import { Modal } from '@/components/lien/modal';

interface Props {
  user: TenantUserDetail;
  accessDebug: AccessDebugResponse | null;
  groups: TenantGroup[];
  assignableRoles: AssignableRolesResponse | null;
  tenantId: string;
}

function SectionCard({ title, icon, children, action }: {
  title: string;
  icon: string;
  children: React.ReactNode;
  action?: React.ReactNode;
}) {
  return (
    <div className="rounded-xl border border-gray-200 bg-white">
      <div className="flex items-center justify-between px-5 py-3 border-b border-gray-100">
        <div className="flex items-center gap-2">
          <i className={`${icon} text-base text-gray-500`} />
          <h3 className="text-sm font-semibold text-gray-900">{title}</h3>
        </div>
        {action}
      </div>
      <div className="px-5 py-4">{children}</div>
    </div>
  );
}

function ConfirmModal({ open, onClose, onConfirm, title, description, loading }: {
  open: boolean;
  onClose: () => void;
  onConfirm: () => void;
  title: string;
  description: string;
  loading: boolean;
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
            {loading ? 'Removing...' : 'Remove'}
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

function SourceBadge({ source }: { source: string; groupName?: string }) {
  const cls =
    source === 'Direct' ? 'bg-blue-50 text-blue-700' :
    source === 'Group' ? 'bg-purple-50 text-purple-700' :
    source === 'Tenant' ? 'bg-amber-50 text-amber-700' :
    'bg-gray-100 text-gray-600';
  return (
    <span className={`text-[10px] px-1.5 py-0.5 rounded font-medium ${cls}`}>
      {source}
    </span>
  );
}

const AVAILABLE_PRODUCTS = [
  { code: 'SYNQ_FUND', label: 'Synq Funds' },
  { code: 'SYNQ_LIEN', label: 'Synq Liens' },
  { code: 'SYNQ_CARECONNECT', label: 'Synq CareConnect' },
  { code: 'SYNQ_AI', label: 'Synq AI' },
  { code: 'SYNQ_INSIGHTS', label: 'Synq Insights' },
];

export function UserDetailClient({ user, accessDebug, groups, assignableRoles, tenantId }: Props) {
  const router = useRouter();
  const [toast, setToast] = useState<{ message: string; type: 'success' | 'error' } | null>(null);
  const [loading, setLoading] = useState(false);
  const [confirm, setConfirm] = useState<{ title: string; description: string; action: () => Promise<void> } | null>(null);

  const [showProductPicker, setShowProductPicker] = useState(false);
  const [showRolePicker, setShowRolePicker] = useState(false);
  const [showGroupPicker, setShowGroupPicker] = useState(false);

  const [resending, setResending] = useState(false);
  const [cancelling, setCancelling] = useState(false);
  const [showResendConfirm, setShowResendConfirm] = useState(false);
  const [showCancelConfirm, setShowCancelConfirm] = useState(false);
  const [inviteResult, setInviteResult] = useState<{ link: string } | null>(null);
  const [inviteCopyLabel, setInviteCopyLabel] = useState('Copy link');

  const isInvited = user.status === 'Invited';

  const showToast = useCallback((message: string, type: 'success' | 'error') => {
    setToast({ message, type });
    setTimeout(() => setToast(null), 4000);
  }, []);

  async function handleResendInvite() {
    setResending(true);
    try {
      const result = await tenantClientApi.resendInvite(user.id);
      setShowResendConfirm(false);
      if (result.data?.inviteToken) {
        const link = `${window.location.origin}/accept-invite?token=${encodeURIComponent(result.data.inviteToken)}`;
        setInviteResult({ link });
        setInviteCopyLabel('Copy link');
      } else {
        showToast(`Invitation resent to ${user.email}.`, 'success');
      }
      router.refresh();
    } catch (err) {
      const msg = err instanceof ApiError ? err.message : 'Failed to resend invitation.';
      showToast(msg, 'error');
      setShowResendConfirm(false);
    } finally {
      setResending(false);
    }
  }

  async function handleCancelInvite() {
    setCancelling(true);
    try {
      await tenantClientApi.cancelInvite(user.id);
      setShowCancelConfirm(false);
      showToast(`Invitation cancelled for ${user.email}.`, 'success');
      router.refresh();
    } catch (err) {
      const msg = err instanceof ApiError ? err.message : 'Failed to cancel invitation.';
      showToast(msg, 'error');
      setShowCancelConfirm(false);
    } finally {
      setCancelling(false);
    }
  }

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

  const directProducts = accessDebug
    ? accessDebug.products.filter((p) => p.source === 'Direct').map((p) => p.productCode)
    : [];

  const directRoles = user.roles ?? [];

  const userGroupNames = user.groups?.map((g) => g.groupName) ?? [];

  const assignableItems = assignableRoles?.items.filter((r) => r.assignable && !r.isAssigned) ?? [];

  const availableGroups = groups.filter((g) => !userGroupNames.includes(g.name));

  return (
    <div className="space-y-6">
      <div className="flex items-center gap-3">
        <Link
          href="/tenant/authorization/users"
          className="p-2 rounded-lg hover:bg-gray-100 text-gray-400 hover:text-gray-600 transition-colors"
          aria-label="Back to Users"
        >
          <i className="ri-arrow-left-line text-lg" />
        </Link>
        <div className="flex-1">
          <h2 className="text-lg font-semibold text-gray-900">{user.firstName} {user.lastName}</h2>
          <p className="text-sm text-gray-500">{user.email}</p>
        </div>
        <Link
          href={`/tenant/authorization/simulator?userId=${user.id}&tenantId=${tenantId}`}
          className="flex items-center gap-1.5 px-3 py-2 text-sm font-medium text-amber-700 bg-amber-50 border border-amber-200 rounded-lg hover:bg-amber-100 transition-colors"
        >
          <i className="ri-test-tube-line text-base" />
          Simulate Access
        </Link>
      </div>

      <SectionCard
        title="Identity"
        icon="ri-user-line"
        action={isInvited ? (
          <div className="flex items-center gap-2">
            <button
              onClick={() => setShowResendConfirm(true)}
              disabled={resending || cancelling}
              className="inline-flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium rounded-lg bg-amber-50 text-amber-700 border border-amber-200 hover:bg-amber-100 disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
            >
              <i className="ri-mail-send-line text-sm" />
              Resend Invite
            </button>
            <button
              onClick={() => setShowCancelConfirm(true)}
              disabled={resending || cancelling}
              className="inline-flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium rounded-lg bg-red-50 text-red-600 border border-red-200 hover:bg-red-100 disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
            >
              <i className="ri-mail-close-line text-sm" />
              Cancel Invite
            </button>
          </div>
        ) : undefined}
      >
        {isInvited && (
          <div className="mb-4 rounded-lg border border-amber-200 bg-amber-50 px-4 py-3 flex items-start gap-3">
            <i className="ri-mail-send-line text-amber-600 text-base mt-0.5 shrink-0" />
            <div>
              <p className="text-sm font-medium text-amber-800">Invitation pending</p>
              <p className="text-xs text-amber-600 mt-0.5">
                This user has been invited but has not yet activated their account. Use <strong>Resend Invite</strong> if they did not receive the email, or <strong>Cancel Invite</strong> to revoke access.
              </p>
            </div>
          </div>
        )}
        <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
          <div>
            <p className="text-[11px] font-medium text-gray-400 uppercase tracking-wider mb-1">Name</p>
            <p className="text-sm text-gray-900">{user.firstName} {user.lastName}</p>
          </div>
          <div>
            <p className="text-[11px] font-medium text-gray-400 uppercase tracking-wider mb-1">Email</p>
            <p className="text-sm text-gray-900">{user.email}</p>
          </div>
          <div>
            <p className="text-[11px] font-medium text-gray-400 uppercase tracking-wider mb-1">Status</p>
            <span className={`inline-flex items-center gap-1 px-2 py-0.5 rounded text-[11px] font-semibold border ${
              user.status === 'Active' ? 'bg-green-50 text-green-700 border-green-200' :
              user.status === 'Invited' ? 'bg-amber-50 text-amber-700 border-amber-200' :
              'bg-gray-100 text-gray-500 border-gray-200'
            }`}>
              {user.status === 'Invited'
                ? <i className="ri-mail-send-line text-[11px]" />
                : <span className={`w-1.5 h-1.5 rounded-full ${user.status === 'Active' ? 'bg-green-500' : 'bg-gray-400'}`} />
              }
              {user.status === 'Invited' ? 'Invite sent' : user.status}
            </span>
          </div>
          <div>
            <p className="text-[11px] font-medium text-gray-400 uppercase tracking-wider mb-1">Tenant</p>
            <p className="text-sm text-gray-900">{user.tenantDisplayName || user.tenantCode}</p>
          </div>
        </div>
      </SectionCard>

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        <SectionCard
          title="Direct Product Access"
          icon="ri-apps-line"
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
              <p className="text-xs font-medium text-gray-600 mb-2">Select product to grant access:</p>
              <div className="flex flex-wrap gap-2">
                {AVAILABLE_PRODUCTS.filter((p) => !directProducts.includes(p.code)).map((p) => (
                  <button
                    key={p.code}
                    disabled={loading}
                    onClick={() => handleAction(
                      () => tenantClientApi.assignProduct(tenantId, user.id, p.code).then(() => {}),
                      `${p.label} access granted`
                    ).then(() => setShowProductPicker(false))}
                    className="text-xs px-3 py-1.5 rounded-lg border border-gray-200 bg-white hover:bg-blue-50 hover:border-blue-200 text-gray-700 disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
                  >
                    {p.label}
                  </button>
                ))}
              </div>
            </div>
          )}
          {directProducts.length === 0 ? (
            <p className="text-sm text-gray-400 py-2">No direct product access configured</p>
          ) : (
            <div className="space-y-2">
              {directProducts.map((code) => {
                const label = AVAILABLE_PRODUCTS.find((p) => p.code === code)?.label ?? code;
                return (
                  <div key={code} className="flex items-center justify-between py-2 px-3 rounded-lg hover:bg-gray-50">
                    <div className="flex items-center gap-2">
                      <i className="ri-apps-line text-base text-blue-500" />
                      <span className="text-sm font-medium text-gray-900">{label}</span>
                      <span className="text-[10px] text-gray-400 font-mono">{code}</span>
                    </div>
                    <button
                      onClick={() => setConfirm({
                        title: 'Remove Product Access',
                        description: `Remove ${label} access from ${user.firstName} ${user.lastName}?`,
                        action: () => tenantClientApi.removeProduct(tenantId, user.id, code).then(() => {}),
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

        <SectionCard
          title="Direct Roles"
          icon="ri-shield-user-line"
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
              {assignableItems.length === 0 ? (
                <p className="text-xs text-gray-400">No assignable roles available</p>
              ) : (
                <div className="flex flex-wrap gap-2 max-h-40 overflow-y-auto">
                  {assignableItems.map((r) => (
                    <button
                      key={r.id}
                      disabled={loading}
                      onClick={() => handleAction(
                        () => tenantClientApi.assignRole(user.id, r.id).then(() => {}),
                        `Role ${r.name} assigned`
                      ).then(() => setShowRolePicker(false))}
                      className="text-xs px-3 py-1.5 rounded-lg border border-gray-200 bg-white hover:bg-indigo-50 hover:border-indigo-200 text-gray-700 disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
                      title={r.description || undefined}
                    >
                      {r.name}
                      {r.productName && <span className="text-gray-400 ml-1">({r.productName})</span>}
                    </button>
                  ))}
                </div>
              )}
            </div>
          )}
          {directRoles.length === 0 ? (
            <p className="text-sm text-gray-400 py-2">No roles assigned</p>
          ) : (
            <div className="space-y-2">
              {directRoles.map((r) => (
                <div key={r.assignmentId} className="flex items-center justify-between py-2 px-3 rounded-lg hover:bg-gray-50">
                  <div className="flex items-center gap-2">
                    <i className="ri-shield-user-line text-base text-indigo-500" />
                    <span className="text-sm font-medium text-gray-900">{r.roleName}</span>
                  </div>
                  <button
                    onClick={() => setConfirm({
                      title: 'Remove Role',
                      description: `Remove role ${r.roleName} from ${user.firstName} ${user.lastName}?`,
                      action: () => tenantClientApi.removeRole(user.id, r.roleId).then(() => {}),
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

      <SectionCard
        title="Group Membership"
        icon="ri-group-line"
        action={
          <button
            onClick={() => setShowGroupPicker(!showGroupPicker)}
            className="text-xs text-primary hover:text-primary/80 font-medium flex items-center gap-1"
          >
            <i className="ri-add-line" /> Add to Group
          </button>
        }
      >
        {showGroupPicker && (
          <div className="mb-4 p-3 rounded-lg border border-gray-200 bg-gray-50">
            <p className="text-xs font-medium text-gray-600 mb-2">Select group:</p>
            {availableGroups.length === 0 ? (
              <p className="text-xs text-gray-400">
                {groups.length === 0 ? 'No groups available in this tenant' : 'User is already in all available groups'}
              </p>
            ) : (
              <div className="flex flex-wrap gap-2 max-h-32 overflow-y-auto">
                {availableGroups.map((g) => (
                  <button
                    key={g.id}
                    disabled={loading}
                    onClick={() => handleAction(
                      () => tenantClientApi.addToGroup(tenantId, g.id, user.id).then(() => {}),
                      `Added to ${g.name}`
                    ).then(() => setShowGroupPicker(false))}
                    className="text-xs px-3 py-1.5 rounded-lg border border-gray-200 bg-white hover:bg-purple-50 hover:border-purple-200 text-gray-700 disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
                  >
                    {g.name}
                  </button>
                ))}
              </div>
            )}
          </div>
        )}
        {user.groups.length === 0 ? (
          <p className="text-sm text-gray-400 py-2">No groups assigned</p>
        ) : (
          <div className="space-y-2">
            {user.groups.map((g) => (
              <div key={g.groupId} className="flex items-center justify-between py-2 px-3 rounded-lg hover:bg-gray-50">
                <div className="flex items-center gap-2">
                  <i className="ri-group-line text-base text-purple-500" />
                  <span className="text-sm font-medium text-gray-900">{g.groupName}</span>
                </div>
                <button
                  onClick={() => setConfirm({
                    title: 'Remove from Group',
                    description: `Remove ${user.firstName} ${user.lastName} from ${g.groupName}?`,
                    action: () => tenantClientApi.removeFromGroup(tenantId, g.groupId, user.id).then(() => {}),
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

      <SectionCard title="Effective Access" icon="ri-shield-check-line">
        {!accessDebug ? (
          <p className="text-sm text-gray-400 py-2">Access debug data not available. The identity service may not be running.</p>
        ) : (
          <div className="space-y-6">
            <div>
              <h4 className="text-xs font-semibold text-gray-500 uppercase tracking-wider mb-3">Effective Products</h4>
              {accessDebug.products.length === 0 ? (
                <p className="text-sm text-gray-400">No effective products</p>
              ) : (
                <div className="space-y-1.5">
                  {accessDebug.products.map((p, i) => (
                    <div key={i} className="flex items-center justify-between py-1.5 px-3 rounded-lg bg-gray-50">
                      <div className="flex items-center gap-2">
                        <span className="text-sm font-medium text-gray-900">{p.productCode}</span>
                        {p.groupName && <span className="text-[10px] text-gray-400">({p.groupName})</span>}
                      </div>
                      <SourceBadge source={p.source} groupName={p.groupName} />
                    </div>
                  ))}
                </div>
              )}
            </div>

            <div>
              <h4 className="text-xs font-semibold text-gray-500 uppercase tracking-wider mb-3">Effective Roles</h4>
              {accessDebug.roles.length === 0 ? (
                <p className="text-sm text-gray-400">No effective roles</p>
              ) : (
                <div className="space-y-1.5">
                  {accessDebug.roles.map((r, i) => (
                    <div key={i} className="flex items-center justify-between py-1.5 px-3 rounded-lg bg-gray-50">
                      <div className="flex items-center gap-2">
                        <span className="text-sm font-medium text-gray-900">{r.roleCode}</span>
                        {r.productCode && <span className="text-[10px] text-gray-400">{r.productCode}</span>}
                        {r.groupName && <span className="text-[10px] text-gray-400">({r.groupName})</span>}
                      </div>
                      <SourceBadge source={r.source} groupName={r.groupName} />
                    </div>
                  ))}
                </div>
              )}
            </div>

            <div>
              <h4 className="text-xs font-semibold text-gray-500 uppercase tracking-wider mb-3">Effective Permissions</h4>
              {accessDebug.permissionSources.length === 0 ? (
                <p className="text-sm text-gray-400">No effective permissions</p>
              ) : (
                <div className="space-y-2">
                  {accessDebug.permissionSources.map((p, i) => (
                    <div key={i} className="py-2 px-3 rounded-lg bg-gray-50">
                      <div className="flex items-center justify-between">
                        <span className="text-sm font-mono text-gray-900">{p.permissionCode}</span>
                        <SourceBadge source={p.source} groupName={p.groupName} />
                      </div>
                      <p className="text-[11px] text-gray-500 mt-0.5">
                        via role: <span className="font-medium text-gray-700">{p.viaRoleCode}</span>
                        {p.groupName && (
                          <> via group: <span className="font-medium text-gray-700">{p.groupName}</span></>
                        )}
                      </p>
                    </div>
                  ))}
                </div>
              )}
            </div>

            {accessDebug.policies && accessDebug.policies.length > 0 && (
              <div>
                <h4 className="text-xs font-semibold text-red-500 uppercase tracking-wider mb-3">Policy Impact</h4>
                <div className="space-y-3">
                  {accessDebug.policies.map((entry, i) => (
                    <div key={i} className="rounded-lg bg-red-50 border border-red-100 p-3">
                      <div className="flex items-center gap-2 mb-2">
                        <i className="ri-forbid-line text-sm text-red-500" />
                        <span className="text-sm font-medium text-red-700">{entry.permission}</span>
                      </div>
                      {entry.linkedPolicies.map((lp, j) => (
                        <div key={j} className="flex items-center gap-2 py-1 px-2 rounded bg-red-100/50 mb-1 last:mb-0">
                          <span className="text-[11px] font-medium text-red-800">{lp.policyName}</span>
                          <span className="text-[10px] text-red-500 font-mono">{lp.policyCode}</span>
                          <span className="ml-auto text-[10px] px-1.5 py-0.5 rounded font-medium bg-red-100 text-red-700">
                            P{lp.priority} · {lp.rulesCount} rule{lp.rulesCount !== 1 ? 's' : ''}
                          </span>
                        </div>
                      ))}
                    </div>
                  ))}
                </div>
              </div>
            )}
          </div>
        )}
      </SectionCard>

      <ConfirmModal
        open={!!confirm}
        onClose={() => setConfirm(null)}
        onConfirm={() => confirm && handleAction(confirm.action, 'Removed successfully')}
        title={confirm?.title ?? ''}
        description={confirm?.description ?? ''}
        loading={loading}
      />

      {/* Resend Invite confirm */}
      {showResendConfirm && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4" role="dialog" aria-modal="true">
          <div className="fixed inset-0 bg-black/40" aria-hidden="true" onClick={() => { if (!resending) setShowResendConfirm(false); }} />
          <div className="relative bg-white rounded-xl shadow-xl w-full max-w-sm p-6">
            <h3 className="text-base font-semibold text-gray-900 mb-2">Resend Invite to {user.firstName}?</h3>
            <p className="text-sm text-gray-600 mb-5">
              A new invitation link will be created and sent to <strong>{user.email}</strong>. The previous invitation will be invalidated.
            </p>
            <div className="flex items-center justify-end gap-2">
              <button
                onClick={() => setShowResendConfirm(false)}
                disabled={resending}
                className="text-sm px-4 py-2 border border-gray-200 rounded-lg hover:bg-gray-50 text-gray-600 disabled:opacity-50"
              >
                Cancel
              </button>
              <button
                onClick={handleResendInvite}
                disabled={resending}
                className="text-sm px-4 py-2 bg-primary hover:bg-primary/90 text-white rounded-lg disabled:opacity-50 flex items-center gap-1.5"
              >
                {resending && <i className="ri-loader-4-line animate-spin" />}
                Resend Invite
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Cancel Invite confirm */}
      {showCancelConfirm && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4" role="dialog" aria-modal="true">
          <div className="fixed inset-0 bg-black/40" aria-hidden="true" onClick={() => { if (!cancelling) setShowCancelConfirm(false); }} />
          <div className="relative bg-white rounded-xl shadow-xl w-full max-w-sm p-6">
            <h3 className="text-base font-semibold text-gray-900 mb-2">Cancel Invite for {user.firstName}?</h3>
            <p className="text-sm text-gray-600 mb-5">
              The pending invitation for <strong>{user.email}</strong> will be revoked immediately. They will not be able to use the existing link to activate their account. You can invite them again at any time.
            </p>
            <div className="flex items-center justify-end gap-2">
              <button
                onClick={() => setShowCancelConfirm(false)}
                disabled={cancelling}
                className="text-sm px-4 py-2 border border-gray-200 rounded-lg hover:bg-gray-50 text-gray-600 disabled:opacity-50"
              >
                Keep Invite
              </button>
              <button
                onClick={handleCancelInvite}
                disabled={cancelling}
                className="text-sm px-4 py-2 bg-red-600 hover:bg-red-700 text-white rounded-lg disabled:opacity-50 flex items-center gap-1.5"
              >
                {cancelling && <i className="ri-loader-4-line animate-spin" />}
                Cancel Invite
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Dev/non-production invite link modal */}
      <Modal
        open={!!inviteResult}
        onClose={() => setInviteResult(null)}
        title="New Invitation Link"
        size="sm"
      >
        {inviteResult && (
          <div className="space-y-4 py-1">
            <p className="text-sm text-gray-600">
              A new invitation link has been created for <strong>{user.firstName} {user.lastName}</strong> ({user.email}). Share this link so they can set their password and activate their account.
            </p>
            <div className="rounded-lg border border-amber-200 bg-amber-50 px-3.5 py-3 space-y-2">
              <div className="flex items-start gap-2">
                <i className="ri-information-line text-[15px] text-amber-600 shrink-0 mt-0.5" />
                <p className="text-[13px] text-amber-700 leading-snug">
                  <span className="font-medium">Non-production only.</span> In production this link is delivered via email. Do not share this link in an insecure channel.
                </p>
              </div>
              <a
                href={inviteResult.link}
                target="_blank"
                rel="noreferrer"
                className="inline-flex items-center gap-1.5 text-[13px] font-medium text-orange-600 underline underline-offset-2 break-all"
              >
                <i className="ri-mail-send-line text-[14px] shrink-0" />
                {inviteResult.link}
              </a>
            </div>
            <button
              type="button"
              onClick={() => {
                navigator.clipboard.writeText(inviteResult.link).catch(() => {});
                setInviteCopyLabel('Copied!');
                setTimeout(() => setInviteCopyLabel('Copy link'), 2500);
              }}
              className="w-full flex items-center justify-center gap-2 rounded-lg border border-gray-200 px-4 py-2.5 text-sm font-medium text-gray-700 bg-white hover:bg-gray-50 transition-colors"
            >
              <i className={`${inviteCopyLabel === 'Copied!' ? 'ri-check-line text-green-600' : 'ri-file-copy-line'} text-[15px]`} />
              {inviteCopyLabel}
            </button>
          </div>
        )}
      </Modal>

      {toast && <Toast message={toast.message} type={toast.type} onClose={() => setToast(null)} />}
    </div>
  );
}
