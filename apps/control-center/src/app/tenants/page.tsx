import { requirePlatformAdmin } from '@/lib/auth-guards';
import { getTenantContext } from '@/lib/auth';
import { controlCenterServerApi } from '@/lib/control-center-api';
import { CCShell } from '@/components/shell/cc-shell';
import { TenantListTable } from '@/components/tenants/tenant-list-table';
import { CreateTenantButton } from '@/components/tenants/create-tenant-button';

export const dynamic = 'force-dynamic';

interface TenantsPageProps {
  searchParams: Promise<{
    page?:   string;
    search?: string;
  }>;
}

/**
 * /tenants — Tenants list page.
 *
 * Access: PlatformAdmin only (enforced by requirePlatformAdmin).
 *
 * Data: served from mock stub in controlCenterServerApi.tenants.list().
 * TODO: When GET /identity/api/admin/tenants is live, the stub auto-wires — no page change needed.
 */
export default async function TenantsPage({ searchParams }: TenantsPageProps) {
  const searchParamsData = await searchParams;
  const session    = await requirePlatformAdmin();
  const tenantCtx  = await getTenantContext();

  const page   = Math.max(1, parseInt(searchParamsData.page ?? '1') || 1);
  const search = searchParamsData.search ?? '';

  let result = null;
  let fetchError: string | null = null;

  try {
    result = await controlCenterServerApi.tenants.list({ page, pageSize: 20, search, tenantId: tenantCtx?.tenantId });
  } catch (err) {
    fetchError = err instanceof Error ? err.message : 'Failed to load tenants.';
  }

  return (
    <CCShell userEmail={session.email}>
      <div className="space-y-4">
        {/* Header */}
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-3">
            <h1 className="text-xl font-semibold text-gray-900">Tenants</h1>
            {tenantCtx && (
              <span className="inline-flex items-center gap-1.5 px-2 py-0.5 rounded-full bg-amber-100 border border-amber-300 text-[11px] font-semibold text-amber-700">
                <span className="h-1.5 w-1.5 rounded-full bg-amber-500" />
                Scoped to {tenantCtx.tenantName}
              </span>
            )}
          </div>
          <CreateTenantButton />
        </div>

        {/* Search */}
        <form method="GET" className="flex items-center gap-2">
          <input
            type="text"
            name="search"
            defaultValue={search}
            placeholder="Search by name, code or contact…"
            className="w-full sm:w-72 text-sm border border-gray-200 rounded-md px-3 py-1.5 text-gray-900 placeholder-gray-400 focus:outline-none focus:ring-1 focus:ring-indigo-400 focus:border-indigo-400"
          />
          <button
            type="submit"
            className="text-sm px-3 py-1.5 rounded-md border border-gray-200 bg-white text-gray-600 hover:bg-gray-50 transition-colors"
          >
            Search
          </button>
          {search && (
            <a href="?" className="text-xs text-gray-400 hover:text-gray-700 underline">
              Clear
            </a>
          )}
        </form>

        {/* Error banner */}
        {fetchError && (
          <div className="bg-red-50 border border-red-200 rounded-lg px-4 py-3 text-sm text-red-700">
            {fetchError}
          </div>
        )}

        {/* Table */}
        {result && (
          <TenantListTable
            tenants={result.items}
            totalCount={result.totalCount}
            page={result.page}
            pageSize={result.pageSize}
          />
        )}
      </div>
    </CCShell>
  );
}
