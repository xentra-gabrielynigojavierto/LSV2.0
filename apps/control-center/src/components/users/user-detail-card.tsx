import type { ReactNode } from 'react';
import Link               from 'next/link';
import type { UserDetail, UserStatus } from '@/types/control-center';
import { Routes }         from '@/lib/routes';
import { AdminPhoneEditor } from '@/components/users/admin-phone-editor';

interface UserDetailCardProps {
  user: UserDetail;
}

function formatDate(iso: string): string {
  return new Date(iso).toLocaleDateString('en-US', {
    month: 'long',
    day:   'numeric',
    year:  'numeric',
  });
}

function formatDateTime(iso: string): string {
  return new Date(iso).toLocaleString('en-US', {
    month:  'short',
    day:    'numeric',
    year:   'numeric',
    hour:   '2-digit',
    minute: '2-digit',
  });
}

/**
 * User detail card — informational summary sections.
 * Sections: User Information, Account Status, Effective Access Summary.
 *
 * NOTE: Role, Org, and Group management panels are rendered separately on
 * the detail page as interactive Access Control panels (UIX-003).
 * This component is a pure Server Component — receives a resolved UserDetail prop.
 */
export function UserDetailCard({ user }: UserDetailCardProps) {
  const isLocked  = user.isLocked ?? false;
  const isInvited = user.status === 'Invited';

  const initials = `${user.firstName.charAt(0)}${user.lastName.charAt(0)}`.toUpperCase();
  const avatarSrc = user.avatarDocumentId
    ? `/api/admin/users/${user.id}/avatar/${user.avatarDocumentId}?tenantId=${user.tenantId}`
    : null;

  return (
    <div className="space-y-5">

      {/* ── Avatar ───────────────────────────────────────────────────── */}
      <div className="flex items-center gap-4 px-1">
        {avatarSrc ? (
          <img
            src={avatarSrc}
            alt={`${user.firstName} ${user.lastName}`}
            className="w-14 h-14 rounded-full object-cover border border-gray-200 shadow-sm shrink-0"
          />
        ) : (
          <div
            className="w-14 h-14 rounded-full flex items-center justify-center text-white text-xl font-bold border border-white/20 shadow-sm shrink-0"
            style={{ backgroundColor: '#f97316' }}
          >
            {initials}
          </div>
        )}
        <div>
          <p className="text-base font-semibold text-gray-900">
            {user.firstName} {user.lastName}
          </p>
          <p className="text-sm text-gray-500">{user.email}</p>
        </div>
      </div>

      {/* ── User Information ──────────────────────────────────────────── */}
      <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
        <div className="px-5 py-3 border-b border-gray-100 bg-gray-50 flex items-center justify-between">
          <h2 className="text-xs font-semibold uppercase tracking-wide text-gray-500">
            User Information
          </h2>
          <span className="text-[10px] text-gray-400 font-medium uppercase tracking-wide">
            Read-only · Informational
          </span>
        </div>
        <dl className="divide-y divide-gray-100">
          <InfoRow label="First Name"   value={user.firstName} />
          <InfoRow label="Last Name"    value={user.lastName} />
          <InfoRow label="Email"        value={
            <a href={`mailto:${user.email}`} className="text-indigo-600 hover:underline">
              {user.email}
            </a>
          } />
          <InfoRow label="Phone"        value={<AdminPhoneEditor userId={user.id} phone={user.phone} />} />
          <InfoRow label="Platform Role" value={<RolePill role={user.role} />} />
          <InfoRow label="Status"       value={<StatusPill status={user.status} />} />
          <InfoRow label="Tenant"       value={
            <Link
              href={Routes.tenantDetail(user.tenantId)}
              className="text-indigo-600 hover:underline inline-flex items-center gap-1.5"
            >
              {user.tenantDisplayName}
              <span className="font-mono text-[10px] bg-gray-100 px-1 py-0.5 rounded text-gray-500">
                {user.tenantCode}
              </span>
            </Link>
          } />
          <InfoRow label="Created"      value={formatDate(user.createdAtUtc)} />
          <InfoRow label="Last Updated" value={formatDate(user.updatedAtUtc)} />
        </dl>
      </div>

      {/* ── Account Status ────────────────────────────────────────────── */}
      <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
        <div className="px-5 py-3 border-b border-gray-100 bg-gray-50 flex items-center justify-between">
          <h2 className="text-xs font-semibold uppercase tracking-wide text-gray-500">
            Account Status
          </h2>
          <span className="text-[10px] text-gray-400 font-medium uppercase tracking-wide">
            Read-only · Informational
          </span>
        </div>
        <dl className="divide-y divide-gray-100">
          <InfoRow label="Account State" value={<StatusPill status={user.status} />} />
          <InfoRow
            label="Locked"
            value={
              isLocked
                ? <LockedIndicator locked />
                : <LockedIndicator locked={false} />
            }
          />
          <InfoRow
            label="Invite State"
            value={
              isInvited
                ? <span className="text-sm text-blue-700 font-medium">Pending acceptance</span>
                : <span className="text-sm text-gray-400 italic">—</span>
            }
          />
          {user.inviteSentAtUtc && (
            <InfoRow label="Invite Sent" value={formatDateTime(user.inviteSentAtUtc)} />
          )}
          <InfoRow
            label="Last Login"
            value={
              user.lastLoginAtUtc
                ? formatDateTime(user.lastLoginAtUtc)
                : <span className="text-gray-400 italic">Never logged in</span>
            }
          />
        </dl>
      </div>

      {/* ── Effective Access Summary ───────────────────────────────────── */}
      <EffectiveAccessSummary user={user} />

    </div>
  );
}

// ── Effective Access Summary panel ───────────────────────────────────────────

function EffectiveAccessSummary({ user }: { user: UserDetail }) {
  const primaryMembership = user.memberships?.find(m => m.isPrimary);
  const roleCount         = user.roles?.length ?? 0;
  const groupCount        = user.groups?.length ?? 0;
  const membershipCount   = user.memberships?.length ?? 0;
  const isActive          = user.status === 'Active';
  const isLocked          = user.isLocked ?? false;

  const accessTier = (() => {
    const roleNames = (user.roles ?? []).map(r => r.roleName.toLowerCase());
    if (roleNames.some(n => n.includes('platformadmin') || n === 'platform admin'))
      return { label: 'Platform Admin', description: 'Full platform management access across all tenants.', color: 'text-red-700 bg-red-50 border-red-200' };
    if (roleNames.some(n => n.includes('tenantadmin') || n === 'tenant admin'))
      return { label: 'Tenant Admin', description: 'Full management access within their tenant.', color: 'text-indigo-700 bg-indigo-50 border-indigo-200' };
    if (roleNames.length > 0)
      return { label: roleNames[0], description: 'Scoped access based on assigned role.', color: 'text-gray-700 bg-gray-50 border-gray-200' };
    return { label: 'No system role', description: 'No platform-level role assigned. Access is group-scoped only.', color: 'text-amber-700 bg-amber-50 border-amber-200' };
  })();

  return (
    <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
      <div className="px-5 py-3 border-b border-gray-100 bg-gray-50 flex items-center justify-between">
        <h2 className="text-xs font-semibold uppercase tracking-wide text-gray-500">
          Effective Access Summary
        </h2>
        <span className="text-[10px] text-gray-400 font-medium uppercase tracking-wide">
          Read-only · Informational
        </span>
      </div>

      <div className="px-5 py-4 space-y-4">

        {/* Account state indicator */}
        <div className="flex items-center gap-3 flex-wrap">
          <span className={`inline-flex items-center gap-1.5 px-2.5 py-1 rounded text-[12px] font-semibold border ${isActive ? 'bg-green-50 text-green-700 border-green-200' : 'bg-gray-100 text-gray-500 border-gray-200'}`}>
            <span className={`w-1.5 h-1.5 rounded-full inline-block ${isActive ? 'bg-green-500' : 'bg-gray-400'}`} />
            {isActive ? 'Account Active' : `Account ${user.status}`}
          </span>
          {isLocked && (
            <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded text-[12px] font-semibold border bg-red-50 text-red-700 border-red-200">
              <span className="w-1.5 h-1.5 rounded-full bg-red-500 inline-block" />
              Account Locked
            </span>
          )}
          {!isActive && (
            <span className="text-xs text-gray-400 italic">
              {user.status === 'Invited' ? 'Awaiting invitation acceptance.' : 'Inactive — cannot access the platform.'}
            </span>
          )}
        </div>

        {/* Role / access tier */}
        <div className="space-y-1">
          <p className="text-xs font-medium text-gray-500">Access Tier</p>
          <div className="flex items-start gap-2">
            <span className={`inline-flex items-center px-2 py-0.5 rounded text-[11px] font-semibold border ${accessTier.color}`}>
              {accessTier.label}
            </span>
            <p className="text-xs text-gray-500 pt-0.5">{accessTier.description}</p>
          </div>
        </div>

        {/* Summary stats */}
        <div className="grid grid-cols-3 gap-3">
          <SummaryStat label="Organizations" value={membershipCount} />
          <SummaryStat label="Groups" value={groupCount} />
          <SummaryStat label="System Roles" value={roleCount} />
        </div>

        {/* Primary org callout */}
        {primaryMembership && (
          <div className="text-xs bg-amber-50 rounded-md px-3 py-2 border border-amber-100">
            <span className="text-gray-500">Primary org: </span>
            <span className="font-semibold text-gray-800">{primaryMembership.orgName}</span>
            <span className="text-gray-500"> · {primaryMembership.memberRole}</span>
          </div>
        )}

        {/* No primary org notice */}
        {!primaryMembership && membershipCount > 0 && (
          <p className="text-xs text-amber-600 bg-amber-50 rounded-md px-3 py-2 border border-amber-100">
            No primary organization set. Use the Organization Memberships panel below to set one.
          </p>
        )}

        {!primaryMembership && membershipCount === 0 && (
          <p className="text-xs text-amber-600 bg-amber-50 rounded-md px-3 py-2 border border-amber-100">
            No organization membership. Add the user to an organization using the panel below.
          </p>
        )}

      </div>
    </div>
  );
}

function SummaryStat({ label, value }: { label: string; value: number }) {
  return (
    <div className="bg-gray-50 border border-gray-100 rounded-md px-3 py-2 text-center">
      <p className="text-lg font-semibold text-gray-800">{value}</p>
      <p className="text-[11px] text-gray-500 mt-0.5">{label}</p>
    </div>
  );
}

// ── Sub-components ────────────────────────────────────────────────────────────

function InfoRow({ label, value }: { label: string; value: ReactNode }) {
  return (
    <div className="px-5 py-3 flex items-center gap-4">
      <dt className="w-36 shrink-0 text-xs font-medium text-gray-500">{label}</dt>
      <dd className="text-sm text-gray-800">{value}</dd>
    </div>
  );
}

function StatusPill({ status }: { status: UserStatus }) {
  const dot: Record<UserStatus, string> = {
    Active:   'bg-green-500',
    Inactive: 'bg-gray-400',
    Invited:  'bg-blue-500',
  };
  const styles: Record<UserStatus, string> = {
    Active:   'bg-green-50 text-green-700 border-green-200',
    Inactive: 'bg-gray-100 text-gray-500 border-gray-200',
    Invited:  'bg-blue-50 text-blue-700 border-blue-200',
  };
  const meaning: Record<UserStatus, string> = {
    Active:   'Can sign in and access the platform',
    Inactive: 'Account disabled — cannot sign in',
    Invited:  'Invitation sent — awaiting acceptance',
  };
  return (
    <span
      className={`inline-flex items-center gap-1.5 px-2 py-0.5 rounded text-[11px] font-semibold border ${styles[status]}`}
      title={meaning[status]}
    >
      <span className={`w-1.5 h-1.5 rounded-full inline-block ${dot[status]}`} />
      {status}
    </span>
  );
}

function RolePill({ role }: { role: string }) {
  return (
    <span className="inline-flex items-center px-2 py-0.5 rounded text-[11px] font-semibold border bg-indigo-50 text-indigo-700 border-indigo-200">
      {role}
    </span>
  );
}

function LockedIndicator({ locked }: { locked: boolean }) {
  return locked
    ? <span className="inline-flex items-center gap-1.5 text-sm text-red-700 font-medium">
        <span className="w-1.5 h-1.5 rounded-full bg-red-500 inline-block" />
        Locked — sign-in blocked
      </span>
    : <span className="inline-flex items-center gap-1.5 text-sm text-green-700 font-medium">
        <span className="w-1.5 h-1.5 rounded-full bg-green-500 inline-block" />
        Unlocked
      </span>;
}
