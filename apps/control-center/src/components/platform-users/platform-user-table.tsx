import Link from 'next/link';
import type { UserSummary, UserStatus } from '@/types/control-center';
import { Routes } from '@/lib/routes';

interface PlatformUserTableProps {
  users:      UserSummary[];
  totalCount: number;
  page:       number;
  pageSize:   number;
  baseHref?:  string;
  hasFilters?: boolean;
}

function formatLoginDate(iso: string): string {
  const d        = new Date(iso);
  const now      = new Date();
  const diffMs   = now.getTime() - d.getTime();
  const diffDays = Math.floor(diffMs / (1000 * 60 * 60 * 24));

  if (diffDays === 0) return 'Today';
  if (diffDays === 1) return 'Yesterday';
  if (diffDays < 7)  return `${diffDays}d ago`;
  return d.toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' });
}

function fullName(user: UserSummary): string {
  return `${user.firstName} ${user.lastName}`.trim();
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

function StatusBadge({ status }: { status: UserStatus }) {
  return (
    <span className={`inline-flex items-center gap-1.5 px-2 py-0.5 rounded text-[11px] font-semibold border ${STATUS_STYLES[status]}`}>
      <span className={`w-1.5 h-1.5 rounded-full inline-block flex-shrink-0 ${STATUS_DOT[status]}`} />
      {status}
    </span>
  );
}

export function PlatformUserTable({
  users,
  totalCount,
  page,
  pageSize,
  baseHref = '?',
  hasFilters = false,
}: PlatformUserTableProps) {
  const totalPages = Math.ceil(totalCount / pageSize);
  const prevHref   = page > 1 ? `${baseHref}page=${page - 1}` : null;
  const nextHref   = page < totalPages ? `${baseHref}page=${page + 1}` : null;

  if (users.length === 0) {
    return (
      <div className="border border-gray-200 rounded-lg bg-white px-6 py-12 text-center">
        <div className="flex flex-col items-center gap-2">
          <span className="ri-shield-user-line text-3xl text-gray-300" />
          <p className="text-sm font-medium text-gray-700">
            {hasFilters ? 'No platform users match your filters' : 'No platform users found'}
          </p>
          {!hasFilters && (
            <p className="text-xs text-gray-400">
              Use "Invite Platform User" to add LegalSynq staff accounts.
            </p>
          )}
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-3">
      <div className="overflow-hidden rounded-lg border border-gray-200 bg-white">
        <table className="min-w-full divide-y divide-gray-100 text-sm">
          <thead className="bg-gray-50 text-[11px] uppercase tracking-wide text-gray-500">
            <tr>
              <th scope="col" className="px-4 py-2.5 text-left font-semibold">Name</th>
              <th scope="col" className="px-4 py-2.5 text-left font-semibold">Email</th>
              <th scope="col" className="px-4 py-2.5 text-left font-semibold">Status</th>
              <th scope="col" className="px-4 py-2.5 text-left font-semibold">Last Login</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-100">
            {users.map(user => (
              <tr key={user.id} className="hover:bg-gray-50 transition-colors">
                <td className="px-4 py-3">
                  <Link
                    href={Routes.platformUserDetail(user.id)}
                    className="font-medium text-gray-900 hover:text-indigo-600 transition-colors"
                  >
                    {fullName(user) || <span className="text-gray-400 italic">Unnamed</span>}
                  </Link>
                </td>
                <td className="px-4 py-3 text-gray-500">
                  {user.email}
                </td>
                <td className="px-4 py-3">
                  <StatusBadge status={user.status} />
                </td>
                <td className="px-4 py-3 text-gray-400 text-xs">
                  {user.lastLoginAtUtc
                    ? formatLoginDate(user.lastLoginAtUtc)
                    : <span className="italic">Never</span>}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {/* Pagination */}
      <div className="flex items-center justify-between text-xs text-gray-500">
        <span>
          Showing {(page - 1) * pageSize + 1}–{Math.min(page * pageSize, totalCount)} of {totalCount}
        </span>
        <div className="flex items-center gap-1">
          {prevHref ? (
            <Link href={prevHref} className="px-2.5 py-1 rounded border border-gray-200 bg-white hover:bg-gray-50 text-gray-700 transition-colors">
              ← Prev
            </Link>
          ) : (
            <span className="px-2.5 py-1 rounded border border-gray-100 text-gray-300 cursor-not-allowed">← Prev</span>
          )}
          <span className="px-2 text-gray-400">
            Page {page} of {totalPages || 1}
          </span>
          {nextHref ? (
            <Link href={nextHref} className="px-2.5 py-1 rounded border border-gray-200 bg-white hover:bg-gray-50 text-gray-700 transition-colors">
              Next →
            </Link>
          ) : (
            <span className="px-2.5 py-1 rounded border border-gray-100 text-gray-300 cursor-not-allowed">Next →</span>
          )}
        </div>
      </div>
    </div>
  );
}
