import Link from 'next/link';
import { requireAdmin } from '@/lib/auth-guards';
import { getTenantContext } from '@/lib/auth';
import { controlCenterServerApi } from '@/lib/control-center-api';
import { CCShell } from '@/components/shell/cc-shell';
import { UserListTable } from '@/components/users/user-list-table';
import { Routes } from '@/lib/routes';

export const dynamic = 'force-dynamic';

type StatusFilter = 'all' | 'active' | 'inactive' | 'invited';

interface TenantUsersPageProps {
  searchParams: Promise<{
    page?:   string;
    search?: string;
    status?: string;
  }>;
}

/**
 * /tenant-users — Platform-wide user list (all tenants).
 *
 * Access: PlatformAdmin only.
 *
 * Data: served from mock stub in controlCenterServerApi.users.list().
 * TODO: When GET /identity/api/admin/users is live, the stub auto-wires —
 *       no page change needed.
 */
const STATUS_FILTERS: { value: StatusFilter; label: string }[] = [
  { value: 'all',      label: 'All'      },
  { value: 'active',   label: 'Active'   },
  { value: 'inactive', label: 'Inactive' },
  { value: 'invited',  label: 'Invited'  },
];

export default async function TenantUsersPage({ searchParams }: TenantUsersPageProps) {
  const searchParamsData = await searchParams;
  const session   = await requireAdmin();
  const tenantCtx = await getTenantContext();

  const page   = Math.max(1, parseInt(searchParamsData.page ?? '1') || 1);
  const search = searchParamsData.search ?? '';
  const status = (STATUS_FILTERS.map(f => f.value) as string[]).includes(searchParamsData.status ?? '')
    ? (searchParamsData.status as StatusFilter)
    : 'all';

  let result = null;
  let fetchError: string | null = null;

  try {
    result = await controlCenterServerApi.users.list({
      page,
      pageSize: 20,
      search,
      tenantId: tenantCtx?.tenantId,
      status:   status !== 'all' ? status : undefined,
    });
  } catch (err) {
    fetchError = err instanceof Error ? err.message : 'Failed to load users.';
  }

  return (
    <CCShell userEmail={session.email}>
      <div className="space-y-4">

        {/* Header */}
        <div className="flex items-center justify-between">
          <div>
            <div className="flex items-center gap-3">
              <h1 className="text-xl font-semibold text-gray-900">Tenant Users</h1>
              {tenantCtx && (
                <span className="inline-flex items-center gap-1.5 px-2 py-0.5 rounded-full bg-amber-100 border border-amber-300 text-[11px] font-semibold text-amber-700">
                  <span className="h-1.5 w-1.5 rounded-full bg-amber-500" />
                  Scoped to {tenantCtx.tenantName}
                </span>
              )}
            </div>
            <p className="text-sm text-gray-500 mt-0.5">
              {tenantCtx
                ? `Users within ${tenantCtx.tenantName}`
                : 'All users across all tenants'}
            </p>
          </div>
          <Link
            href="/tenant-users/invite"
            className="bg-indigo-600 text-white text-sm font-medium px-4 py-2 rounded-md hover:bg-indigo-700 transition-colors"
          >
            Invite User
          </Link>
        </div>

        {/* Search + Status filter row */}
        <form method="GET" className="flex flex-wrap items-center gap-2">
          <input
            type="text"
            name="search"
            defaultValue={search}
            placeholder="Search by name, email or role…"
            className="w-full sm:w-72 text-sm border border-gray-200 rounded-md px-3 py-1.5 text-gray-900 placeholder-gray-400 focus:outline-none focus:ring-1 focus:ring-indigo-400 focus:border-indigo-400"
          />

          {/* Status pills — each submits the form with status= param */}
          <div className="flex items-center gap-1 bg-gray-100 rounded-lg p-0.5">
            {STATUS_FILTERS.map(f => (
              <button
                key={f.value}
                type="submit"
                name="status"
                value={f.value}
                className={
                  status === f.value
                    ? 'text-xs px-3 py-1 rounded-md bg-white shadow-sm text-indigo-700 font-semibold border border-gray-200'
                    : 'text-xs px-3 py-1 rounded-md text-gray-500 hover:text-gray-800 transition-colors'
                }
              >
                {f.label}
              </button>
            ))}
          </div>

          <button
            type="submit"
            className="text-sm px-3 py-1.5 rounded-md border border-gray-200 bg-white text-gray-600 hover:bg-gray-50 transition-colors"
          >
            Search
          </button>
          {(search || status !== 'all') && (
            <a href="?" className="text-xs text-gray-400 hover:text-gray-700 underline">
              Clear
            </a>
          )}
        </form>

        {/* Summary */}
        {result && !fetchError && (
          <p className="text-xs text-gray-400">
            {result.totalCount} user{result.totalCount !== 1 ? 's' : ''} found
            {search && ` matching "${search}"`}
            {status !== 'all' && ` · ${status}`}
          </p>
        )}

        {/* Error banner */}
        {fetchError && (
          <div className="bg-red-50 border border-red-200 rounded-lg px-4 py-3 text-sm text-red-700">
            {fetchError}
          </div>
        )}

        {/* Table — hide tenant column when scoped (redundant info) */}
        {result && (
          <UserListTable
            users={result.items}
            totalCount={result.totalCount}
            page={result.page}
            pageSize={result.pageSize}
            showTenantColumn={!tenantCtx}
            hasFilters={Boolean(search || status !== 'all')}
            baseHref={(() => {
              const params = new URLSearchParams();
              if (search)          params.set('search', search);
              if (status !== 'all') params.set('status', status);
              const qs = params.toString();
              return qs ? `?${qs}&` : '?';
            })()}
          />
        )}
      </div>
    </CCShell>
  );
}
