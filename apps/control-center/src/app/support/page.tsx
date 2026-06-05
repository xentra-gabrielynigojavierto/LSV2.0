import { requirePlatformAdmin } from '@/lib/auth-guards';
import { getTenantContext } from '@/lib/auth';
import { controlCenterServerApi } from '@/lib/control-center-api';
import { CCShell } from '@/components/shell/cc-shell';
import { SupportCaseTable } from '@/components/support/support-case-table';
import type { SupportCase } from '@/types/control-center';

export const dynamic = 'force-dynamic';

interface SupportPageProps {
  searchParams: Promise<{
    page?:     string;
    search?:   string;
    status?:   string;
    priority?: string;
    tenantId?: string;
  }>;
}

const PAGE_SIZE = 10;

/**
 * /support — Support Tools list page.
 *
 * Access: PlatformAdmin only.
 * Filtering: search, status, priority, tenantId — all via URL params.
 * Tenant names are resolved server-side by joining with the tenant roster
 * so each ticket row can display a coloured tenant tag.
 */
export default async function SupportPage({ searchParams }: SupportPageProps) {
  const searchParamsData = await searchParams;
  const session   = await requirePlatformAdmin();
  const tenantCtx = await getTenantContext();

  const page      = Math.max(1, parseInt(searchParamsData.page ?? '1', 10) || 1);
  const search    = searchParamsData.search   ?? '';
  const status    = searchParamsData.status   ?? '';
  const priority  = searchParamsData.priority ?? '';
  const tenantId  = searchParamsData.tenantId ?? tenantCtx?.tenantId ?? '';

  let result: { items: SupportCase[]; totalCount: number } | null = null;
  let fetchError: string | null = null;
  let tenantMap:  Record<string, string> = {};
  let tenantOptions: { id: string; name: string }[] = [];

  try {
    const [ticketsResult, tenantsResult] = await Promise.all([
      controlCenterServerApi.support.list({
        page,
        pageSize: PAGE_SIZE,
        search,
        status,
        priority,
        tenantId: tenantId || undefined,
      }),
      controlCenterServerApi.tenants.list({ pageSize: 200 }).catch(() => ({ items: [], totalCount: 0 })),
    ]);
    result = ticketsResult;
    tenantOptions = tenantsResult.items
      .map(t => ({ id: t.id, name: t.displayName || t.id }))
      .sort((a, b) => a.name.localeCompare(b.name));
    tenantMap = Object.fromEntries(tenantOptions.map(t => [t.id, t.name]));
  } catch (err) {
    fetchError = err instanceof Error ? err.message : 'Failed to load support cases.';
  }

  const openCount         = result?.items.filter(c => c.status === 'Open').length ?? 0;
  const investigatingCount = result?.items.filter(c => c.status === 'Investigating').length ?? 0;

  return (
    <CCShell userEmail={session.email}>
      <div className="min-h-full bg-gray-50">
        <div className="max-w-5xl mx-auto px-6 py-8">

          {/* Page header */}
          <div className="mb-6 flex items-start justify-between gap-4">
            <div>
              <div className="flex items-center gap-3">
                <h1 className="text-xl font-semibold text-gray-900">Support Tools</h1>
                {tenantCtx && (
                  <span className="inline-flex items-center gap-1.5 px-2 py-0.5 rounded-full bg-amber-100 border border-amber-300 text-[11px] font-semibold text-amber-700">
                    <span className="h-1.5 w-1.5 rounded-full bg-amber-500" />
                    Scoped to {tenantCtx.tenantName}
                  </span>
                )}
              </div>
              <p className="text-sm text-gray-500 mt-1">
                {tenantCtx
                  ? `Cases for ${tenantCtx.tenantName} — track, investigate, and resolve.`
                  : 'Internal case management — track, investigate, and resolve tenant issues.'}
              </p>
            </div>

            {/* Quick-glance counts */}
            {result && (
              <div className="flex items-center gap-3 shrink-0">
                {openCount > 0 && (
                  <span className="text-xs font-semibold px-2.5 py-1 rounded-full bg-blue-100 text-blue-700 border border-blue-300">
                    {openCount} Open
                  </span>
                )}
                {investigatingCount > 0 && (
                  <span className="text-xs font-semibold px-2.5 py-1 rounded-full bg-amber-100 text-amber-700 border border-amber-300">
                    {investigatingCount} Investigating
                  </span>
                )}
              </div>
            )}
          </div>

          {/* Error state */}
          {fetchError ? (
            <div className="bg-red-50 border border-red-200 rounded-lg px-5 py-4">
              <p className="text-sm text-red-700 font-medium">Failed to load support cases</p>
              <p className="text-xs text-red-600 mt-1">{fetchError}</p>
            </div>
          ) : (
            <SupportCaseTable
              cases={result?.items ?? []}
              totalCount={result?.totalCount ?? 0}
              page={page}
              pageSize={PAGE_SIZE}
              search={search}
              status={status}
              priority={priority}
              tenantId={tenantId}
              tenantMap={tenantMap}
              tenantOptions={tenantOptions}
            />
          )}

        </div>
      </div>
    </CCShell>
  );
}
