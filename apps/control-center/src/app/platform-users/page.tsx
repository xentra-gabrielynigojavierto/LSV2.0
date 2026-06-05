import { requirePlatformAdmin } from '@/lib/auth-guards';
import { controlCenterServerApi } from '@/lib/control-center-api';
import { CCShell } from '@/components/shell/cc-shell';
import { PlatformUserTable } from '@/components/platform-users/platform-user-table';
import { InvitePlatformUserButton } from '@/components/platform-users/invite-platform-user-button';

export const dynamic = 'force-dynamic';

type StatusFilter = 'all' | 'active' | 'inactive' | 'invited';

const STATUS_FILTERS: { value: StatusFilter; label: string }[] = [
  { value: 'all',      label: 'All'      },
  { value: 'active',   label: 'Active'   },
  { value: 'inactive', label: 'Inactive' },
  { value: 'invited',  label: 'Invited'  },
];

interface PlatformUsersPageProps {
  searchParams: Promise<{
    page?:   string;
    search?: string;
    status?: string;
  }>;
}

/**
 * /platform-users — PlatformInternal user list.
 *
 * Access: PlatformAdmin only (enforced by requirePlatformAdmin).
 *
 * Fetches only UserType=PlatformInternal users via the userType query param
 * supported by GET /identity/api/admin/users (PUM-B01).
 */
export default async function PlatformUsersPage({ searchParams }: PlatformUsersPageProps) {
  const sp      = await searchParams;
  const session = await requirePlatformAdmin();

  const page   = Math.max(1, parseInt(sp.page ?? '1') || 1);
  const search = sp.search ?? '';
  const status = (STATUS_FILTERS.map(f => f.value) as string[]).includes(sp.status ?? '')
    ? (sp.status as StatusFilter)
    : 'all';

  const hasFilters = Boolean(search || (status && status !== 'all'));

  let result = null;
  let fetchError: string | null = null;
  let platformRoles = null;

  try {
    [result, platformRoles] = await Promise.all([
      controlCenterServerApi.users.list({
        page,
        pageSize: 20,
        search,
        status:   status !== 'all' ? status : undefined,
        userType: 'PlatformInternal',
      }),
      controlCenterServerApi.roles.list(),
    ]);
  } catch (err) {
    fetchError = err instanceof Error ? err.message : 'Failed to load platform users.';
  }

  const filteredPlatformRoles = (platformRoles ?? []).filter(r =>
    r.name.startsWith('Platform'),
  );

  const baseHref = [
    search ? `search=${encodeURIComponent(search)}` : '',
    status && status !== 'all' ? `status=${status}` : '',
  ].filter(Boolean).join('&');

  const paginationBase = baseHref ? `?${baseHref}&` : '?';

  return (
    <CCShell userEmail={session.email}>
      <div className="space-y-4">

        {/* Header */}
        <div className="flex items-center justify-between">
          <div>
            <h1 className="text-xl font-semibold text-gray-900">Platform Staff</h1>
            <p className="text-sm text-gray-500 mt-0.5">
              LegalSynq internal users with platform-level access
            </p>
          </div>
          <InvitePlatformUserButton platformRoles={filteredPlatformRoles} />
        </div>

        {/* Search + Status filters */}
        <div className="flex flex-wrap items-center gap-2">
          <form method="GET" className="flex items-center gap-2 flex-1 min-w-0">
            {status && status !== 'all' && (
              <input type="hidden" name="status" value={status} />
            )}
            <input
              type="text"
              name="search"
              defaultValue={search}
              placeholder="Search by name or email…"
              className="w-full sm:w-72 text-sm border border-gray-200 rounded-md px-3 py-1.5 text-gray-900 placeholder-gray-400 focus:outline-none focus:ring-1 focus:ring-indigo-400 focus:border-indigo-400"
            />
            <button
              type="submit"
              className="text-sm px-3 py-1.5 rounded-md border border-gray-200 bg-white text-gray-600 hover:bg-gray-50 transition-colors"
            >
              Search
            </button>
            {hasFilters && (
              <a href="/platform-users" className="text-xs text-gray-400 hover:text-gray-700 underline">
                Clear
              </a>
            )}
          </form>

          {/* Status filter tabs */}
          <div className="flex items-center gap-1">
            {STATUS_FILTERS.map(f => (
              <a
                key={f.value}
                href={`/platform-users?${search ? `search=${encodeURIComponent(search)}&` : ''}${f.value !== 'all' ? `status=${f.value}` : ''}`}
                className={[
                  'text-xs px-2.5 py-1 rounded-md border transition-colors',
                  status === f.value
                    ? 'bg-indigo-600 text-white border-indigo-600'
                    : 'bg-white text-gray-600 border-gray-200 hover:bg-gray-50',
                ].join(' ')}
              >
                {f.label}
              </a>
            ))}
          </div>
        </div>

        {/* Error */}
        {fetchError && (
          <div className="bg-red-50 border border-red-200 rounded-lg px-4 py-3 text-sm text-red-700">
            {fetchError}
          </div>
        )}

        {/* Table */}
        {result && (
          <PlatformUserTable
            users={result.items}
            totalCount={result.totalCount}
            page={result.page}
            pageSize={result.pageSize}
            baseHref={paginationBase}
            hasFilters={hasFilters}
          />
        )}
      </div>
    </CCShell>
  );
}
