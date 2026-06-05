import Link               from 'next/link';
import type { UserSummary, UserStatus } from '@/types/control-center';
import { Routes }          from '@/lib/routes';
import { UserRowActions }  from './user-row-actions';

interface UserListTableProps {
  users:             UserSummary[];
  totalCount:        number;
  page:              number;
  pageSize:          number;
  showTenantColumn?: boolean;
  /**
   * When true, the empty state message reflects "no results match your
   * filters" rather than "no users exist". Set by the page when any
   * search or status filter is active.
   */
  hasFilters?:       boolean;
  /**
   * Base href for pagination links — should include all current filter params
   * (search, status) so that prev/next links preserve active filters.
   * E.g. "?search=alice&status=active&" — pageHref appends page=N.
   * Defaults to "?" (no filter params preserved).
   */
  baseHref?:         string;
}

function formatDate(iso: string): string {
  return new Date(iso).toLocaleDateString('en-US', {
    month: 'short',
    day:   'numeric',
    year:  'numeric',
  });
}

function formatLoginDate(iso: string): string {
  const d       = new Date(iso);
  const now     = new Date();
  const diffMs  = now.getTime() - d.getTime();
  const diffDays = Math.floor(diffMs / (1000 * 60 * 60 * 24));

  if (diffDays === 0) return 'Today';
  if (diffDays === 1) return 'Yesterday';
  if (diffDays < 7)  return `${diffDays}d ago`;
  return formatDate(iso);
}

function fullName(user: UserSummary): string {
  return `${user.firstName} ${user.lastName}`;
}

const STATUS_DOT: Record<UserStatus, string> = {
  Active:   'bg-green-500',
  Inactive: 'bg-gray-400',
  Invited:  'bg-blue-500',
};

const STATUS_STYLES: Record<UserStatus, string> = {
  Active:   'bg-green-50 text-green-700 border-green-200',
  Inactive: 'bg-gray-100 text-gray-500 border-gray-200',
  Invited:  'bg-blue-50 text-blue-700 border-blue-200',
};

const STATUS_MEANING: Record<UserStatus, string> = {
  Active:   'Can sign in and access the platform',
  Inactive: 'Account disabled — cannot sign in',
  Invited:  'Invitation sent — awaiting acceptance',
};

function StatusBadge({ status }: { status: UserStatus }) {
  return (
    <span
      className={`inline-flex items-center gap-1.5 px-2 py-0.5 rounded text-[11px] font-semibold border ${STATUS_STYLES[status]}`}
      title={STATUS_MEANING[status]}
    >
      <span className={`w-1.5 h-1.5 rounded-full inline-block flex-shrink-0 ${STATUS_DOT[status]}`} />
      {status}
    </span>
  );
}

export function UserListTable({
  users,
  totalCount,
  page,
  pageSize,
  showTenantColumn = true,
  hasFilters = false,
  baseHref = '?',
}: UserListTableProps) {
  if (users.length === 0) {
    return (
      <div className="bg-white border border-gray-200 rounded-lg p-12 text-center space-y-2">
        {hasFilters ? (
          <>
            <p className="text-sm font-medium text-gray-600">No users match your filters</p>
            <p className="text-xs text-gray-400">
              Try clearing the search or selecting a different status.
            </p>
          </>
        ) : (
          <>
            <p className="text-sm font-medium text-gray-600">No users yet</p>
            <p className="text-xs text-gray-400">
              Invite a user to get started.
            </p>
          </>
        )}
      </div>
    );
  }

  const pageStart = (page - 1) * pageSize + 1;
  const pageEnd   = Math.min(page * pageSize, totalCount);
  const hasPrev   = page > 1;
  const hasNext   = page * pageSize < totalCount;

  function pageHref(p: number): string {
    const base = baseHref.endsWith('?') || baseHref.endsWith('&')
      ? baseHref
      : baseHref.includes('?') ? `${baseHref}&` : `${baseHref}?`;
    return `${base}page=${p}`;
  }

  return (
    <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
      <div className="overflow-x-auto">
        <table className="min-w-full divide-y divide-gray-100">
          <thead>
            <tr className="bg-gray-50">
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Name</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Email</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Role</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Status</th>
              {showTenantColumn && (
                <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Tenant</th>
              )}
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Primary Org</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Groups</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Last Login</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Actions</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-100">
            {users.map(user => (
              <tr key={user.id} className="hover:bg-gray-50 transition-colors">
                <td className="px-4 py-3">
                  <Link
                    href={Routes.userDetail(user.id)}
                    className="text-sm font-medium text-gray-900 hover:text-indigo-700 hover:underline transition-colors"
                  >
                    {fullName(user)}
                  </Link>
                </td>
                <td className="px-4 py-3 text-sm text-gray-700">
                  {user.email}
                </td>
                <td className="px-4 py-3 text-sm text-gray-700">
                  {user.role}
                </td>
                <td className="px-4 py-3">
                  <StatusBadge status={user.status} />
                </td>
                {showTenantColumn && (
                  <td className="px-4 py-3">
                    <p className="text-sm text-gray-700">{user.tenantCode}</p>
                  </td>
                )}
                <td className="px-4 py-3 text-sm text-gray-500 whitespace-nowrap">
                  {user.primaryOrg ?? <span className="text-gray-300">—</span>}
                </td>
                <td className="px-4 py-3 text-sm text-gray-500 whitespace-nowrap">
                  {user.groupCount !== undefined ? user.groupCount : <span className="text-gray-300">—</span>}
                </td>
                <td className="px-4 py-3 text-xs text-gray-400 whitespace-nowrap">
                  {user.lastLoginAtUtc ? formatLoginDate(user.lastLoginAtUtc) : '—'}
                </td>
                <td className="px-4 py-3">
                  <UserRowActions
                    userId={user.id}
                    userName={fullName(user)}
                    userEmail={user.email}
                    currentStatus={user.status}
                  />
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {/* Status legend */}
      <div className="px-4 py-2 border-t border-gray-100 bg-gray-50 flex flex-wrap items-center gap-4">
        {(Object.keys(STATUS_MEANING) as UserStatus[]).map(s => (
          <span key={s} className="inline-flex items-center gap-1.5 text-[11px] text-gray-500">
            <span className={`w-1.5 h-1.5 rounded-full inline-block ${STATUS_DOT[s]}`} />
            <span className="font-semibold text-gray-600">{s}</span>
            <span>— {STATUS_MEANING[s]}</span>
          </span>
        ))}
      </div>

      {/* Pagination footer */}
      <div className="px-4 py-3 border-t border-gray-100 flex items-center justify-between">
        <p className="text-xs text-gray-400">
          Showing {pageStart}–{pageEnd} of {totalCount}
        </p>
        <div className="flex items-center gap-2">
          {hasPrev && (
            <Link href={pageHref(page - 1)} className="text-xs text-indigo-600 hover:underline">
              ← Previous
            </Link>
          )}
          {hasNext && (
            <Link href={pageHref(page + 1)} className="text-xs text-indigo-600 hover:underline">
              Next →
            </Link>
          )}
        </div>
      </div>
    </div>
  );
}
