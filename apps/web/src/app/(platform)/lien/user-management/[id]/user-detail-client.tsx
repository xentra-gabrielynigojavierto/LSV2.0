'use client';

import { useState } from 'react';
import { useLienStore } from '@/stores/lien-store';
import { useRoleAccess } from '@/hooks/use-role-access';
import { formatDate, formatDateTime } from '@/lib/lien-utils';
import { DetailHeader, DetailSection } from '@/components/lien/detail-section';
import { StatusBadge } from '@/components/lien/status-badge';
import { ConfirmDialog } from '@/components/lien/modal';

export function UserDetailClient({ id }: { id: string }) {
  const users = useLienStore((s) => s.users);
  const userDetails = useLienStore((s) => s.userDetails);
  const updateUser = useLienStore((s) => s.updateUser);
  const addToast = useLienStore((s) => s.addToast);
  const ra = useRoleAccess();
  const [confirmAction, setConfirmAction] = useState<{ status: string; label: string } | null>(null);

  const summary = users.find((u) => u.id === id);
  const detail = userDetails[id];
  const user = detail ? { ...summary, ...detail } : summary;
  if (!user) return <div className="p-10 text-center text-gray-400">User not found.</div>;
  const d = user as any;
  const isAdmin = ra.isAdmin || ra.isTenantAdmin;

  return (
    <div className="space-y-5">
      <DetailHeader title={d.name} subtitle={d.email}
        badge={<StatusBadge status={d.status} size="md" />}
        backHref="/lien/user-management" backLabel="Back to Users"
        meta={[
          { label: 'Role', value: d.role },
          { label: 'Department', value: d.department },
          { label: 'Joined', value: formatDate(d.createdAtUtc) },
        ]}
        actions={isAdmin ? (
          <div className="flex gap-2">
            <button onClick={() => addToast({ type: 'info', title: 'Edit', description: 'Edit mode simulated' })} className="text-sm px-3 py-1.5 border border-gray-200 rounded-lg hover:bg-gray-50 text-gray-600">Edit</button>
            {d.status === 'Locked' && <button onClick={() => { updateUser(id, { status: 'Active' }); addToast({ type: 'success', title: 'User Unlocked' }); }} className="text-sm px-3 py-1.5 bg-primary text-white rounded-lg hover:bg-primary/90">Unlock</button>}
            {d.status === 'Active' && <button onClick={() => setConfirmAction({ status: 'Inactive', label: 'Deactivate' })} className="text-sm px-3 py-1.5 border border-red-200 text-red-600 rounded-lg hover:bg-red-50">Deactivate</button>}
            {d.status === 'Inactive' && <button onClick={() => { updateUser(id, { status: 'Active' }); addToast({ type: 'success', title: 'User Activated' }); }} className="text-sm px-3 py-1.5 bg-primary text-white rounded-lg hover:bg-primary/90">Activate</button>}
          </div>
        ) : undefined}
      />

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-5">
        <DetailSection title="User Information" icon="ri-user-3-line" fields={[
          { label: 'Name', value: d.name },
          { label: 'Email', value: d.email },
          { label: 'Phone', value: d.phone },
          { label: 'Title', value: d.title },
          { label: 'Department', value: d.department },
          { label: 'Last Login', value: d.lastLoginAtUtc ? formatDateTime(d.lastLoginAtUtc) : 'Never' },
        ]} />
        <DetailSection title="Role & Access" icon="ri-shield-user-line" fields={[
          { label: 'Role', value: d.role },
          { label: 'Status', value: <StatusBadge status={d.status} /> },
        ]} />
      </div>

      {d.permissions && d.permissions.length > 0 && (
        <div className="bg-white border border-gray-200 rounded-xl p-5">
          <h3 className="text-sm font-semibold text-gray-800 mb-3">Permissions ({d.permissions.length})</h3>
          <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-4 gap-2">
            {d.permissions.map((p: string) => (
              <div key={p} className="flex items-center gap-2 text-xs text-gray-600 bg-gray-50 rounded-lg px-3 py-2">
                <i className="ri-checkbox-circle-line text-green-500" />{p}
              </div>
            ))}
          </div>
        </div>
      )}

      {d.activityLog && d.activityLog.length > 0 && (
        <div className="bg-white border border-gray-200 rounded-xl p-5">
          <h3 className="text-sm font-semibold text-gray-800 mb-4">Recent Activity</h3>
          <div className="space-y-3">
            {d.activityLog.map((log: any, i: number) => (
              <div key={i} className="flex items-center justify-between py-2 border-b border-gray-50 last:border-0">
                <span className="text-sm text-gray-700">{log.action}</span>
                <span className="text-xs text-gray-400">{formatDateTime(log.timestamp)}</span>
              </div>
            ))}
          </div>
        </div>
      )}

      {confirmAction && (
        <ConfirmDialog open onClose={() => setConfirmAction(null)}
          onConfirm={() => { updateUser(id, { status: confirmAction.status }); addToast({ type: 'warning', title: `User ${confirmAction.label}d` }); setConfirmAction(null); }}
          title={`${confirmAction.label} User`} description={`Are you sure you want to ${confirmAction.label.toLowerCase()} ${d.name}?`}
          confirmLabel={confirmAction.label} confirmVariant="danger"
        />
      )}
    </div>
  );
}
