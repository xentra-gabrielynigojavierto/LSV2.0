import { requireCCPlatformAdmin } from '@/lib/auth-guards';
import { controlCenterServerApi } from '@/lib/control-center-api';
import { TenantListTable }        from '@/components/control-center/tenant-list-table';

export const dynamic = 'force-dynamic';


interface TenantsPageProps {
  searchParams: Promise<{
    page?:   string;
    search?: string;
  }>;
}

/**
 * /control-center/tenants — Tenants list.
 *
 * Access: PlatformAdmin only (enforced by requireCCPlatformAdmin).
 *
 * Data: currently served from a mock stub in controlCenterServerApi.tenants.list().
 * TODO: When GET /identity/api/admin/tenants is live, the stub auto-wires — no page change needed.
 */
export default async function TenantsPage({ searchParams }: TenantsPageProps) {
  await requireCCPlatformAdmin();

  const sp     = await searchParams;
  const page   = Math.max(1, parseInt(sp.page ?? '1') || 1);
  const search = sp.search ?? '';

  let result = null;
  let fetchError: string | null = null;

  try {
    result = await controlCenterServerApi.tenants.list({ page, pageSize: 20, search });
  } catch (err) {
    fetchError = err instanceof Error ? err.message : 'Failed to load tenants.';
  }

  return (
    <div className="space-y-4">
      {/* Header */}
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold text-gray-900">Tenants</h1>
        <button
          type="button"
          disabled
          className="bg-indigo-600 text-white text-sm font-medium px-4 py-2 rounded-md opacity-50 cursor-not-allowed"
          title="Coming soon"
        >
          Create Tenant
        </button>
      </div>

      {/* Search bar (non-functional — query param wiring is backend-ready) */}
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
          <a
            href="?"
            className="text-xs text-gray-400 hover:text-gray-700 underline"
          >
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

      {/* Tenants table */}
      {result && (
        <TenantListTable
          tenants={result.items}
          totalCount={result.totalCount}
          page={result.page}
          pageSize={result.pageSize}
        />
      )}
    </div>
  );
}
