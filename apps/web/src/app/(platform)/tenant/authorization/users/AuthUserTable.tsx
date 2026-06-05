'use client';

import { useState, useMemo, useCallback } from 'react';
import { useRouter } from 'next/navigation';
import type { TenantUser } from '@/types/tenant';
import { AddUserModal } from './AddUserModal';
import { EditUserModal } from './EditUserModal';
import { ConfirmDialog, Modal } from '@/components/lien/modal';
import { tenantClientApi, ApiError } from '@/lib/tenant-client-api';
import { useToast } from '@/lib/toast-context';

type StatusFilter = 'All' | 'Active' | 'Inactive' | 'Invited';
type StatusAction = 'activate' | 'deactivate';

const PAGE_SIZE = 15;
const TENANT_ADMIN_ROLE = 'TenantAdmin';

function initials(firstName?: string | null, lastName?: string | null): string {
  const f = (firstName ?? '').trim();
  const l = (lastName ?? '').trim();
  if (!f && !l) return '?';
  if (!f) return l.charAt(0).toUpperCase();
  if (!l) return f.charAt(0).toUpperCase();
  return `${f.charAt(0)}${l.charAt(0)}`.toUpperCase();
}

function displayName(u: TenantUser): string {
  const f = (u.firstName ?? '').trim();
  const l = (u.lastName ?? '').trim();
  if (!f && !l) return (u.email ?? '').trim() || 'Unknown User';
  return [f, l].filter(Boolean).join(' ');
}

function getUserStatus(u: TenantUser): string {
  if (u.status) return u.status;
  return u.isActive ? 'Active' : 'Inactive';
}

function StatusBadge({ status }: { status: string }) {
  if (status === 'Active') {
    return (
      <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded text-[11px] font-semibold border bg-green-50 text-green-700 border-green-200">
        <span className="w-1.5 h-1.5 rounded-full inline-block bg-green-500" />
        Active
      </span>
    );
  }
  if (status === 'Invited') {
    return (
      <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded text-[11px] font-semibold border bg-amber-50 text-amber-700 border-amber-200">
        <i className="ri-mail-send-line text-[11px]" />
        Invite sent
      </span>
    );
  }
  return (
    <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded text-[11px] font-semibold border bg-gray-100 text-gray-500 border-gray-200">
      <span className="w-1.5 h-1.5 rounded-full inline-block bg-gray-400" />
      Inactive
    </span>
  );
}

function CountBadge({ count, color = 'gray' }: { count: number; color?: string }) {
  const colors: Record<string, string> = {
    gray:   'bg-gray-100 text-gray-600',
    blue:   'bg-blue-50 text-blue-700',
    indigo: 'bg-indigo-50 text-indigo-700',
    purple: 'bg-purple-50 text-purple-700',
  };
  if (count === 0) return <span className="text-gray-300 text-xs">0</span>;
  return (
    <span className={`inline-flex items-center justify-center min-w-[20px] px-1.5 py-0.5 rounded text-[11px] font-semibold ${colors[color] ?? colors.gray}`}>
      {count}
    </span>
  );
}

function RowActionsMenu({
  user,
  onView,
  onEdit,
  onActivate,
  onDeactivate,
  onResetPassword,
  onResendInvite,
  onCancelInvite,
}: {
  user:             TenantUser;
  onView:           () => void;
  onEdit:           () => void;
  onActivate:       () => void;
  onDeactivate:     () => void;
  onResetPassword:  () => void;
  onResendInvite:   () => void;
  onCancelInvite:   () => void;
}) {
  const [open, setOpen] = useState(false);
  function close() { setOpen(false); }
  const userStatus = getUserStatus(user);
  const isInvited  = userStatus === 'Invited';

  return (
    <div className="relative inline-block">
      <button
        onClick={(e) => { e.stopPropagation(); setOpen(v => !v); }}
        aria-label="User actions"
        className="p-1.5 rounded hover:bg-gray-100 text-gray-400 hover:text-gray-600 transition-colors"
      >
        <i className="ri-more-2-fill text-base" />
      </button>

      {open && (
        <>
          <div
            className="fixed inset-0 z-10"
            onClick={(e) => { e.stopPropagation(); close(); }}
          />
          <div className="absolute right-0 z-20 mt-1 w-52 bg-white rounded-lg border border-gray-200 shadow-lg py-1 text-sm">
            <button
              onClick={(e) => { e.stopPropagation(); close(); onView(); }}
              className="w-full text-left px-3 py-2 hover:bg-gray-50 text-gray-700 flex items-center gap-2"
            >
              <i className="ri-user-line text-gray-400" />
              View Profile
            </button>
            <button
              onClick={(e) => { e.stopPropagation(); close(); onEdit(); }}
              className="w-full text-left px-3 py-2 hover:bg-gray-50 text-gray-700 flex items-center gap-2"
            >
              <i className="ri-edit-line text-gray-400" />
              Edit
            </button>

            {isInvited ? (
              <>
                <button
                  onClick={(e) => { e.stopPropagation(); close(); onResendInvite(); }}
                  className="w-full text-left px-3 py-2 hover:bg-amber-50 text-amber-700 flex items-start gap-2"
                >
                  <i className="ri-mail-send-line mt-0.5" />
                  <span>
                    <span className="block">Resend Invite</span>
                    <span className="block text-xs text-amber-500 font-normal leading-snug">Use if the user did not receive the original email</span>
                  </span>
                </button>
                <button
                  onClick={(e) => { e.stopPropagation(); close(); onCancelInvite(); }}
                  className="w-full text-left px-3 py-2 hover:bg-red-50 text-red-600 flex items-start gap-2"
                >
                  <i className="ri-mail-close-line mt-0.5" />
                  <span>
                    <span className="block">Cancel Invite</span>
                    <span className="block text-xs text-red-400 font-normal leading-snug">Revokes the pending invitation link</span>
                  </span>
                </button>
              </>
            ) : (
              <button
                onClick={(e) => { e.stopPropagation(); close(); onResetPassword(); }}
                className="w-full text-left px-3 py-2 hover:bg-gray-50 text-gray-700 flex items-center gap-2"
              >
                <i className="ri-lock-password-line text-gray-400" />
                Reset Password
              </button>
            )}

            <div className="border-t border-gray-100 my-1" />
            {user.isActive ? (
              <button
                onClick={(e) => { e.stopPropagation(); close(); onDeactivate(); }}
                className="w-full text-left px-3 py-2 hover:bg-red-50 text-red-600 flex items-center gap-2"
              >
                <i className="ri-user-unfollow-line" />
                Deactivate
              </button>
            ) : (
              <button
                onClick={(e) => { e.stopPropagation(); close(); onActivate(); }}
                className="w-full text-left px-3 py-2 hover:bg-green-50 text-green-700 flex items-center gap-2"
              >
                <i className="ri-user-follow-line" />
                Activate
              </button>
            )}
          </div>
        </>
      )}
    </div>
  );
}

export function AuthUserTable({ users, tenantId }: { users: TenantUser[]; tenantId: string }) {
  const router = useRouter();
  const { show: showToast } = useToast();

  const [search,       setSearch]       = useState('');
  const [statusFilter, setStatus]       = useState<StatusFilter>('All');
  const [page,         setPage]         = useState(1);

  const [showAddUser,   setShowAddUser]   = useState(false);
  const [editUser,      setEditUser]      = useState<TenantUser | null>(null);
  const [confirmState,  setConfirmState]  = useState<{ user: TenantUser; action: StatusAction } | null>(null);
  const [resetPwdUser,  setResetPwdUser]  = useState<TenantUser | null>(null);
  const [actioning,     setActioning]     = useState(false);
  const [resetting,     setResetting]     = useState(false);
  const [resetResult,   setResetResult]   = useState<{ name: string; email: string; link: string } | null>(null);
  const [copyLabel,     setCopyLabel]     = useState('Copy link');

  const [resendInviteUser,  setResendInviteUser]  = useState<TenantUser | null>(null);
  const [resending,         setResending]         = useState(false);
  const [inviteResult,      setInviteResult]      = useState<{ name: string; email: string; link: string } | null>(null);
  const [inviteCopyLabel,   setInviteCopyLabel]   = useState('Copy link');

  const [cancelInviteUser,  setCancelInviteUser]  = useState<TenantUser | null>(null);
  const [cancelling,        setCancelling]        = useState(false);
  const [hiddenIds,         setHiddenIds]         = useState<Set<string>>(new Set());

  const activeAdminCount = useMemo(
    () => users.filter(u => u.isActive && (u.roles ?? []).includes(TENANT_ADMIN_ROLE)).length,
    [users]
  );

  function isLastActiveAdmin(u: TenantUser): boolean {
    return u.isActive && (u.roles ?? []).includes(TENANT_ADMIN_ROLE) && activeAdminCount <= 1;
  }

  const filtered = useMemo(() => {
    const q = search.toLowerCase().trim();
    return users.filter((u) => {
      if (hiddenIds.has(u.id)) return false;
      const uStatus = getUserStatus(u);
      const matchesStatus =
        statusFilter === 'All' ||
        statusFilter === uStatus;
      if (!matchesStatus) return false;
      if (!q) return true;
      const fullName = `${u.firstName ?? ''} ${u.lastName ?? ''}`.trim();
      return (
        (u.email ?? '').toLowerCase().includes(q) ||
        (u.firstName ?? '').toLowerCase().includes(q) ||
        (u.lastName ?? '').toLowerCase().includes(q) ||
        fullName.toLowerCase().includes(q)
      );
    });
  }, [users, hiddenIds, search, statusFilter]);

  const totalPages = Math.max(1, Math.ceil(filtered.length / PAGE_SIZE));
  const safePage   = Math.min(page, totalPages);
  const slice      = filtered.slice((safePage - 1) * PAGE_SIZE, safePage * PAGE_SIZE);

  function handleSearch(value: string) { setSearch(value); setPage(1); }
  function handleStatus(value: StatusFilter) { setStatus(value); setPage(1); }

  const handleUserCreated = useCallback(() => { setShowAddUser(false); router.refresh(); }, [router]);
  const handleUserEdited  = useCallback(() => { setEditUser(null);    router.refresh(); }, [router]);

  function handleDeactivateRequest(u: TenantUser) {
    if (isLastActiveAdmin(u)) {
      showToast(
        'Cannot deactivate the last active tenant administrator. Assign another administrator first.',
        'error',
      );
      return;
    }
    setConfirmState({ user: u, action: 'deactivate' });
  }

  async function handleStatusAction() {
    if (!confirmState) return;
    const { user: u, action } = confirmState;
    setActioning(true);
    try {
      if (action === 'activate') {
        await tenantClientApi.activateUser(u.id);
        showToast(`${displayName(u)} has been activated.`, 'success');
      } else {
        await tenantClientApi.deactivateUser(u.id);
        showToast(`${displayName(u)} has been deactivated.`, 'success');
      }
      setConfirmState(null);
      router.refresh();
    } catch (err) {
      let msg = 'Something went wrong. Please try again.';
      if (err instanceof ApiError) {
        if (err.isForbidden)  msg = 'You do not have permission to perform this action.';
        else if (err.isNotFound) msg = 'User not found.';
        else if (err.message) msg = err.message;
      }
      showToast(msg, 'error');
      setConfirmState(null);
    } finally {
      setActioning(false);
    }
  }

  async function handleResetPasswordConfirm() {
    if (!resetPwdUser) return;
    setResetting(true);
    try {
      const result = await tenantClientApi.resetPassword(resetPwdUser.id);
      const name  = displayName(resetPwdUser);
      const email = (resetPwdUser.email ?? '').trim();
      setResetPwdUser(null);
      if (result.data?.resetToken) {
        const link = `${window.location.origin}/reset-password?token=${encodeURIComponent(result.data.resetToken)}`;
        setResetResult({ name, email, link });
        setCopyLabel('Copy link');
      } else {
        showToast(
          email
            ? `Password reset email sent to ${email}.`
            : 'Password reset email sent.',
          'success',
        );
      }
    } catch (err) {
      let msg = 'Something went wrong. Please try again.';
      if (err instanceof ApiError) {
        if (err.isForbidden) msg = 'You do not have permission to reset this user\'s password.';
        else if (err.isNotFound) msg = 'User not found.';
        else if (err.message) msg = err.message;
      }
      showToast(msg, 'error');
      setResetPwdUser(null);
    } finally {
      setResetting(false);
    }
  }

  async function handleResendInviteConfirm() {
    if (!resendInviteUser) return;
    setResending(true);
    try {
      const result = await tenantClientApi.resendInvite(resendInviteUser.id);
      const name  = displayName(resendInviteUser);
      const email = (resendInviteUser.email ?? '').trim();
      setResendInviteUser(null);
      if (result.data?.inviteToken) {
        const link = `${window.location.origin}/accept-invite?token=${encodeURIComponent(result.data.inviteToken)}`;
        setInviteResult({ name, email, link });
        setInviteCopyLabel('Copy link');
      } else {
        showToast(
          email
            ? `Invitation resent to ${email}.`
            : 'Invitation resent.',
          'success',
        );
      }
      router.refresh();
    } catch (err) {
      let msg = 'Something went wrong. Please try again.';
      if (err instanceof ApiError) {
        if (err.isForbidden) msg = 'You do not have permission to resend invitations.';
        else if (err.isNotFound) msg = 'User not found.';
        else if (err.message) msg = err.message;
      }
      showToast(msg, 'error');
      setResendInviteUser(null);
    } finally {
      setResending(false);
    }
  }

  async function handleCancelInviteConfirm() {
    if (!cancelInviteUser) return;
    setCancelling(true);
    try {
      await tenantClientApi.cancelInvite(cancelInviteUser.id);
      const name  = displayName(cancelInviteUser);
      const email = (cancelInviteUser.email ?? '').trim();
      const cancelledId = cancelInviteUser.id;
      setCancelInviteUser(null);
      setHiddenIds(prev => new Set([...prev, cancelledId]));
      showToast(
        email
          ? `Invitation cancelled for ${email}.`
          : `Invitation cancelled for ${name}.`,
        'success',
      );
      router.refresh();
    } catch (err) {
      let msg = 'Something went wrong. Please try again.';
      if (err instanceof ApiError) {
        if (err.isForbidden) msg = 'You do not have permission to cancel invitations.';
        else if (err.isNotFound) msg = 'User not found.';
        else if (err.isConflict) msg = 'This user has no pending invitation to cancel.';
        else if (err.message) msg = err.message;
      }
      showToast(msg, 'error');
      setCancelInviteUser(null);
    } finally {
      setCancelling(false);
    }
  }

  const productCount = (u: TenantUser) => u.productCount ?? 0;
  const roleCount    = (u: TenantUser) => u.roles?.length ?? 0;

  const confirmUser    = confirmState?.user;
  const confirmName    = confirmUser ? displayName(confirmUser) : '';
  const isDeactivating = confirmState?.action === 'deactivate';

  const resetPwdName  = resetPwdUser ? displayName(resetPwdUser) : '';
  const resetPwdEmail = (resetPwdUser?.email ?? '').trim();

  const resendInviteName  = resendInviteUser ? displayName(resendInviteUser) : '';
  const resendInviteEmail = (resendInviteUser?.email ?? '').trim();

  const cancelInviteName  = cancelInviteUser ? displayName(cancelInviteUser) : '';
  const cancelInviteEmail = (cancelInviteUser?.email ?? '').trim();

  return (
    <>
      <AddUserModal
        open={showAddUser}
        tenantId={tenantId}
        onClose={() => setShowAddUser(false)}
        onSuccess={handleUserCreated}
      />

      {editUser && (
        <EditUserModal
          open={!!editUser}
          user={editUser}
          isLastAdmin={isLastActiveAdmin(editUser)}
          onClose={() => setEditUser(null)}
          onSuccess={handleUserEdited}
        />
      )}

      <ConfirmDialog
        open={!!confirmState}
        onClose={() => { if (!actioning) setConfirmState(null); }}
        onConfirm={handleStatusAction}
        loading={actioning}
        title={isDeactivating ? `Deactivate ${confirmName}?` : `Activate ${confirmName}?`}
        description={
          isDeactivating
            ? 'They will immediately lose access to the platform.'
            : 'They will regain access based on their assigned role.'
        }
        confirmLabel={isDeactivating ? 'Deactivate' : 'Activate'}
        confirmVariant={isDeactivating ? 'danger' : 'primary'}
      />

      <ConfirmDialog
        open={!!resetPwdUser}
        onClose={() => { if (!resetting) setResetPwdUser(null); }}
        onConfirm={handleResetPasswordConfirm}
        loading={resetting}
        title={`Reset Password for ${resetPwdName}?`}
        description={
          resetPwdEmail
            ? `A password reset link will be sent to ${resetPwdEmail}. Their existing password will remain valid until they complete the reset.`
            : 'A password reset link will be sent to this user. Their existing password will remain valid until they complete the reset.'
        }
        confirmLabel="Send Reset Link"
        confirmVariant="primary"
      />

      <ConfirmDialog
        open={!!resendInviteUser}
        onClose={() => { if (!resending) setResendInviteUser(null); }}
        onConfirm={handleResendInviteConfirm}
        loading={resending}
        title={`Resend Invite to ${resendInviteName}?`}
        description={
          resendInviteEmail
            ? `A new invitation link will be created and sent to ${resendInviteEmail}. The previous invitation will be invalidated. Use this if the user did not receive the original invitation email.`
            : 'A new invitation link will be created and the previous invitation will be invalidated. Use this if the user did not receive their original invitation email.'
        }
        confirmLabel="Resend Invite"
        confirmVariant="primary"
      />

      <ConfirmDialog
        open={!!cancelInviteUser}
        onClose={() => { if (!cancelling) setCancelInviteUser(null); }}
        onConfirm={handleCancelInviteConfirm}
        loading={cancelling}
        title={`Cancel Invite for ${cancelInviteName}?`}
        description={
          cancelInviteEmail
            ? `The pending invitation for ${cancelInviteEmail} will be revoked immediately. They will not be able to use the invitation link to activate their account. You can invite them again at any time.`
            : 'The pending invitation will be revoked immediately. The user will not be able to use the invitation link to activate their account. You can invite them again at any time.'
        }
        confirmLabel="Cancel Invite"
        confirmVariant="danger"
      />

      {/* LS-ID-TNT-005: Dev/non-production reset link delivery modal */}
      <Modal
        open={!!resetResult}
        onClose={() => setResetResult(null)}
        title="Password Reset Link"
        size="sm"
      >
        {resetResult && (
          <div className="space-y-4 py-1">
            <p className="text-sm text-gray-600">
              A reset link has been generated for <span className="font-medium text-gray-900">{resetResult.name}</span>
              {resetResult.email && <> ({resetResult.email})</>}.
              Share this link with them to complete the password reset.
            </p>

            <div className="rounded-lg border border-amber-200 bg-amber-50 px-3.5 py-3 space-y-2">
              <div className="flex items-start gap-2">
                <i className="ri-information-line text-[15px] text-amber-600 shrink-0 mt-0.5" />
                <p className="text-[13px] text-amber-700 leading-snug">
                  <span className="font-medium">Non-production only.</span> In production this link is delivered via email. Do not share this link in an insecure channel.
                </p>
              </div>
              <a
                href={resetResult.link}
                target="_blank"
                rel="noreferrer"
                className="inline-flex items-center gap-1.5 text-[13px] font-medium text-orange-600 underline underline-offset-2 break-all"
              >
                <i className="ri-lock-password-line text-[14px] shrink-0" />
                {resetResult.link}
              </a>
            </div>

            <button
              type="button"
              onClick={() => {
                navigator.clipboard.writeText(resetResult.link).catch(() => {});
                setCopyLabel('Copied!');
                setTimeout(() => setCopyLabel('Copy link'), 2500);
              }}
              className="w-full flex items-center justify-center gap-2 rounded-lg border border-gray-200 px-4 py-2.5 text-sm font-medium text-gray-700 bg-white hover:bg-gray-50 transition-colors"
            >
              <i className={`${copyLabel === 'Copied!' ? 'ri-check-line text-green-600' : 'ri-file-copy-line'} text-[15px]`} />
              {copyLabel}
            </button>
          </div>
        )}
      </Modal>

      {/* LS-ID-TNT-007: Dev/non-production invite link delivery modal */}
      <Modal
        open={!!inviteResult}
        onClose={() => setInviteResult(null)}
        title="Invitation Link"
        size="sm"
      >
        {inviteResult && (
          <div className="space-y-4 py-1">
            <p className="text-sm text-gray-600">
              A new invitation link has been created for <span className="font-medium text-gray-900">{inviteResult.name}</span>
              {inviteResult.email && <> ({inviteResult.email})</>}.
              Share this link so they can set their password and activate their account.
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

      <div className="space-y-4">
        <div className="flex flex-col sm:flex-row sm:items-center gap-3">
          <div className="relative flex-1 max-w-sm">
            <span className="absolute inset-y-0 left-3 flex items-center text-gray-400 pointer-events-none">
              <i className="ri-search-line text-base" />
            </span>
            <input
              type="text"
              placeholder="Search by name or email..."
              value={search}
              onChange={(e) => handleSearch(e.target.value)}
              className="w-full rounded-md border border-gray-300 bg-white py-2 pl-9 pr-3 text-sm text-gray-900 placeholder-gray-400 focus:outline-none focus:ring-2 focus:ring-primary/50 focus:border-primary"
            />
          </div>

          <div className="flex items-center gap-1 rounded-md border border-gray-200 bg-gray-50 p-1">
            {(['All', 'Active', 'Invited', 'Inactive'] as StatusFilter[]).map((s) => (
              <button
                key={s}
                onClick={() => handleStatus(s)}
                className={`px-3 py-1 rounded text-xs font-medium transition-colors ${
                  statusFilter === s
                    ? 'bg-white text-gray-900 shadow-sm border border-gray-200'
                    : 'text-gray-500 hover:text-gray-900'
                }`}
              >
                {s}
              </button>
            ))}
          </div>

          <span className="text-xs text-gray-400 whitespace-nowrap">
            {filtered.length} {filtered.length === 1 ? 'user' : 'users'}
          </span>

          <button
            onClick={() => setShowAddUser(true)}
            className="ml-auto inline-flex items-center gap-1.5 px-3 py-2 rounded-lg bg-primary hover:bg-primary/90 text-white text-sm font-medium transition-colors whitespace-nowrap"
          >
            <i className="ri-user-add-line text-base" />
            Invite User
          </button>
        </div>

        <div className="overflow-x-auto rounded-lg border border-gray-200 bg-white">
          {slice.length === 0 ? (
            <div className="px-6 py-14 text-center">
              <i className="ri-user-search-line text-3xl text-gray-300 mb-2 block" />
              <p className="text-sm text-gray-400">
                {search || statusFilter !== 'All'
                  ? 'No users match your current search or filters.'
                  : 'No users found for this tenant.'}
              </p>
            </div>
          ) : (
            <table className="min-w-full divide-y divide-gray-100 text-sm">
              <thead>
                <tr className="bg-gray-50 text-xs font-medium text-gray-500 uppercase tracking-wider">
                  <th className="px-4 py-3 text-left">User</th>
                  <th className="px-4 py-3 text-left">Email</th>
                  <th className="px-4 py-3 text-left">Status</th>
                  <th className="px-4 py-3 text-center">Products</th>
                  <th className="px-4 py-3 text-center hidden md:table-cell">Roles</th>
                  <th className="px-4 py-3 text-center hidden lg:table-cell">Groups</th>
                  <th className="px-4 py-3 text-right">Actions</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {slice.map((u) => (
                  <tr
                    key={u.id}
                    onClick={() => router.push(`/tenant/authorization/users/${u.id}`)}
                    className="hover:bg-gray-50 transition-colors cursor-pointer"
                  >
                    <td className="px-4 py-3 whitespace-nowrap">
                      <div className="flex items-center gap-3">
                        {u.avatarDocumentId ? (
                          <img
                            src={`/api/profile/avatar/${u.avatarDocumentId}`}
                            alt=""
                            className="h-8 w-8 rounded-full object-cover flex-shrink-0"
                          />
                        ) : (
                          <span className="inline-flex h-8 w-8 items-center justify-center rounded-full bg-indigo-100 text-indigo-700 text-xs font-semibold flex-shrink-0">
                            {initials(u.firstName, u.lastName)}
                          </span>
                        )}
                        <span className="font-medium text-gray-900">
                          {displayName(u)}
                        </span>
                      </div>
                    </td>
                    <td className="px-4 py-3 whitespace-nowrap text-gray-600">{u.email || '—'}</td>
                    <td className="px-4 py-3 whitespace-nowrap">
                      <div className="flex items-center gap-2 flex-wrap">
                        <StatusBadge status={getUserStatus(u)} />
                        {getUserStatus(u) === 'Invited' && (
                          <>
                            <button
                              onClick={(e) => { e.stopPropagation(); setResendInviteUser(u); }}
                              className="inline-flex items-center gap-1 text-[11px] font-medium px-2 py-0.5 rounded bg-amber-50 text-amber-700 border border-amber-200 hover:bg-amber-100 transition-colors"
                              title="Resend invitation email"
                            >
                              <i className="ri-mail-send-line" />
                              Resend
                            </button>
                            <button
                              onClick={(e) => { e.stopPropagation(); setCancelInviteUser(u); }}
                              className="inline-flex items-center gap-1 text-[11px] font-medium px-2 py-0.5 rounded bg-red-50 text-red-600 border border-red-200 hover:bg-red-100 transition-colors"
                              title="Cancel invitation"
                            >
                              <i className="ri-mail-close-line" />
                              Cancel
                            </button>
                          </>
                        )}
                      </div>
                    </td>
                    <td className="px-4 py-3 text-center"><CountBadge count={productCount(u)} color="blue" /></td>
                    <td className="px-4 py-3 text-center hidden md:table-cell"><CountBadge count={roleCount(u)} color="indigo" /></td>
                    <td className="px-4 py-3 text-center hidden lg:table-cell"><CountBadge count={u.groupCount ?? 0} color="purple" /></td>
                    <td className="px-4 py-3 text-right" onClick={(e) => e.stopPropagation()}>
                      <RowActionsMenu
                        user={u}
                        onView={() => router.push(`/tenant/authorization/users/${u.id}`)}
                        onEdit={() => setEditUser(u)}
                        onResetPassword={() => setResetPwdUser(u)}
                        onResendInvite={() => setResendInviteUser(u)}
                        onCancelInvite={() => setCancelInviteUser(u)}
                        onActivate={() => setConfirmState({ user: u, action: 'activate' })}
                        onDeactivate={() => handleDeactivateRequest(u)}
                      />
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>

        {totalPages > 1 && (
          <div className="flex items-center justify-between text-sm">
            <span className="text-gray-500">Page {safePage} of {totalPages}</span>
            <div className="flex items-center gap-2">
              <button
                onClick={() => setPage((p) => Math.max(1, p - 1))}
                disabled={safePage === 1}
                className="px-3 py-1.5 rounded border border-gray-200 text-gray-600 text-xs font-medium hover:bg-gray-50 disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
              >
                Previous
              </button>
              <button
                onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
                disabled={safePage === totalPages}
                className="px-3 py-1.5 rounded border border-gray-200 text-gray-600 text-xs font-medium hover:bg-gray-50 disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
              >
                Next
              </button>
            </div>
          </div>
        )}
      </div>
    </>
  );
}
