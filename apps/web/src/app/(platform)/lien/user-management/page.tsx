'use client';

import { useState } from 'react';
import Link from 'next/link';
import { PageHeader } from '@/components/lien/page-header';
import { FilterToolbar } from '@/components/lien/filter-toolbar';
import { StatusBadge } from '@/components/lien/status-badge';
import { ActionMenu } from '@/components/lien/action-menu';
import { AddUserForm } from '@/components/lien/forms/add-user-form';
import { ConfirmDialog } from '@/components/lien/modal';
import { useLienStore } from '@/stores/lien-store';
import { useRoleAccess } from '@/hooks/use-role-access';
import { formatDate } from '@/lib/lien-utils';

export const dynamic = 'force-dynamic';


export default function UserManagementPage() {
  const users = useLienStore((s) => s.users);
  const updateUser = useLienStore((s) => s.updateUser);
  const addToast = useLienStore((s) => s.addToast);
  const ra = useRoleAccess();
  const [search, setSearch] = useState('');
  const [statusFilter, setStatusFilter] = useState('');
  const [roleFilter, setRoleFilter] = useState('');
  const [showCreate, setShowCreate] = useState(false);
  const [confirmAction, setConfirmAction] = useState<{ id: string; status: string; label: string } | null>(null);

  const filtered = users.filter((u) => {
    if (search && !u.name.toLowerCase().includes(search.toLowerCase()) && !u.email.toLowerCase().includes(search.toLowerCase())) return false;
    if (statusFilter && u.status !== statusFilter) return false;
    if (roleFilter && u.role !== roleFilter) return false;
    return true;
  });

  const uniqueRoles = [...new Set(users.map((u) => u.role))];
  const isAdmin = ra.isAdmin || ra.isTenantAdmin;

  return (
    <div className="space-y-5">
      <PageHeader title="User Management" subtitle={`${filtered.length} users`}
        actions={isAdmin ? (
          <button onClick={() => setShowCreate(true)} className="flex items-center gap-1.5 text-sm font-medium text-white bg-primary hover:bg-primary/90 rounded-lg px-4 py-2 transition-colors">
            <i className="ri-user-add-line text-base" />Invite User
          </button>
        ) : undefined}
      />
      <FilterToolbar searchPlaceholder="Search users by name or email..." onSearch={setSearch} filters={[
        { label: 'All Statuses', value: statusFilter, onChange: setStatusFilter, options: [{ value: 'Active', label: 'Active' }, { value: 'Inactive', label: 'Inactive' }, { value: 'Invited', label: 'Invited' }, { value: 'Locked', label: 'Locked' }] },
        { label: 'All Roles', value: roleFilter, onChange: setRoleFilter, options: uniqueRoles.map((r) => ({ value: r, label: r })) },
      ]} />
      <div className="bg-white border border-gray-200 rounded-xl overflow-hidden">
        <div className="overflow-x-auto">
          <table className="min-w-full divide-y divide-gray-100">
            <thead><tr className="bg-gray-50">
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Name</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Email</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Role</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Department</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Status</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Last Login</th>
              <th className="px-4 py-3" />
            </tr></thead>
            <tbody className="divide-y divide-gray-100">
              {filtered.map((u) => (
                <tr key={u.id} className="hover:bg-gray-50 transition-colors">
                  <td className="px-4 py-3">
                    <Link href={`/lien/user-management/${u.id}`} className="flex items-center gap-2">
                      <div className="w-8 h-8 rounded-full bg-primary/10 flex items-center justify-center text-xs font-medium text-primary">{u.name.split(' ').map((n) => n[0]).join('')}</div>
                      <span className="text-sm font-medium text-gray-700 hover:text-primary">{u.name}</span>
                    </Link>
                  </td>
                  <td className="px-4 py-3 text-sm text-gray-500">{u.email}</td>
                  <td className="px-4 py-3 text-sm text-gray-600">{u.role}</td>
                  <td className="px-4 py-3 text-sm text-gray-500">{u.department}</td>
                  <td className="px-4 py-3"><StatusBadge status={u.status} /></td>
                  <td className="px-4 py-3 text-xs text-gray-400">{u.lastLoginAtUtc ? formatDate(u.lastLoginAtUtc) : 'Never'}</td>
                  <td className="px-4 py-3 text-right">
                    <ActionMenu items={[
                      { label: 'View Profile', icon: 'ri-eye-line', onClick: () => {} },
                      ...(isAdmin && u.status === 'Active' ? [{ label: 'Deactivate', icon: 'ri-user-unfollow-line', onClick: () => setConfirmAction({ id: u.id, status: 'Inactive', label: 'Deactivate User' }), variant: 'danger' as const, divider: true }] : []),
                      ...(isAdmin && u.status === 'Inactive' ? [{ label: 'Activate', icon: 'ri-user-follow-line', onClick: () => { updateUser(u.id, { status: 'Active' }); addToast({ type: 'success', title: 'User Activated' }); } }] : []),
                      ...(isAdmin && u.status === 'Locked' ? [{ label: 'Unlock', icon: 'ri-lock-unlock-line', onClick: () => { updateUser(u.id, { status: 'Active' }); addToast({ type: 'success', title: 'User Unlocked' }); } }] : []),
                    ]} />
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        {filtered.length === 0 && <div className="p-10 text-center text-sm text-gray-400">No users found.</div>}
      </div>

      <AddUserForm open={showCreate} onClose={() => setShowCreate(false)} />

      {confirmAction && (
        <ConfirmDialog open onClose={() => setConfirmAction(null)}
          onConfirm={() => { updateUser(confirmAction.id, { status: confirmAction.status }); addToast({ type: 'warning', title: confirmAction.label }); setConfirmAction(null); }}
          title={confirmAction.label} description={`Are you sure you want to ${confirmAction.label.toLowerCase()}?`}
          confirmLabel={confirmAction.label} confirmVariant="danger"
        />
      )}
    </div>
  );
}
