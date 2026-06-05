import { isRedirectError }         from 'next/dist/client/components/redirect-error';
import { requirePlatformAdmin }    from '@/lib/auth-guards';
import { getTenantContext }        from '@/lib/auth';
import { controlCenterServerApi }  from '@/lib/control-center-api';
import { CCShell }                 from '@/components/shell/cc-shell';
import { InvestigationWorkspace }  from '@/components/synqaudit/investigation-workspace';

export const dynamic = 'force-dynamic';

interface Props {
  searchParams: Promise<{
    eventType?:     string;
    category?:      string;
    severity?:      string;
    actorId?:       string;
    targetType?:    string;
    correlationId?: string;
    dateFrom?:      string;
    dateTo?:        string;
    search?:        string;
    page?:          string;
  }>;
}

const PAGE_SIZE = 15;

/**
 * /synqaudit/investigation — Interactive canonical audit investigation workspace.
 *
 * Server component fetches paged events with full filter params, then passes
 * the data to InvestigationWorkspace (client component) for interactivity.
 */
export default async function InvestigationPage({ searchParams }: Props) {
  const searchParamsData = await searchParams;
  const session   = await requirePlatformAdmin();
  const tenantCtx = await getTenantContext();

  const eventType     = searchParamsData.eventType     ?? '';
  const category      = searchParamsData.category      ?? '';
  const severity      = searchParamsData.severity      ?? '';
  const actorId       = searchParamsData.actorId       ?? '';
  const targetType    = searchParamsData.targetType    ?? '';
  const correlationId = searchParamsData.correlationId ?? '';
  const dateFrom      = searchParamsData.dateFrom      ?? '';
  const dateTo        = searchParamsData.dateTo        ?? '';
  const search        = searchParamsData.search        ?? '';
  const page          = Math.max(1, parseInt(searchParamsData.page ?? '1', 10));

  let items:      Awaited<ReturnType<typeof controlCenterServerApi.auditCanonical.list>>['items'] = [];
  let totalCount  = 0;
  let fetchError: string | null = null;

  try {
    const result = await controlCenterServerApi.auditCanonical.list({
      page,
      pageSize:      PAGE_SIZE,
      tenantId:      tenantCtx?.tenantId,
      eventType:     eventType     || undefined,
      category:      category      || undefined,
      severity:      severity      || undefined,
      actorId:       actorId       || undefined,
      targetType:    targetType    || undefined,
      correlationId: correlationId || undefined,
      dateFrom:      dateFrom      || undefined,
      dateTo:        dateTo        || undefined,
      search:        search        || undefined,
    });
    items      = result.items;
    totalCount = result.totalCount;
  } catch (err) {
    if (isRedirectError(err)) throw err;
    fetchError = err instanceof Error ? err.message : 'Failed to load audit events.';
  }

  const totalPages = Math.max(1, Math.ceil(totalCount / PAGE_SIZE));

  return (
    <CCShell userEmail={session.email}>
      <div className="space-y-4">

        {/* Header */}
        <div className="flex items-start justify-between gap-4">
          <div>
            <h1 className="text-xl font-semibold text-gray-900">Investigation</h1>
            <p className="text-sm text-gray-500 mt-0.5">
              {tenantCtx
                ? `Scoped to ${tenantCtx.tenantName} — canonical event stream`
                : 'Platform-wide canonical event stream'}
            </p>
          </div>
          <a
            href="/synqaudit/exports"
            className="inline-flex items-center gap-1.5 h-9 px-3 text-sm font-medium text-gray-600 hover:text-gray-900 bg-white border border-gray-300 hover:border-gray-400 rounded-md transition-colors whitespace-nowrap"
          >
            <i className="ri-download-cloud-line text-sm" />
            Export
          </a>
        </div>

        {/* Error banner */}
        {fetchError && (
          <div className="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
            {fetchError}
          </div>
        )}

        {/* Workspace (interactive client component) */}
        {!fetchError && (
          <InvestigationWorkspace
            entries={items}
            totalCount={totalCount}
            page={page}
            totalPages={totalPages}
            filters={{ eventType, category, severity, actorId, targetType, correlationId, dateFrom, dateTo, search }}
          />
        )}
      </div>
    </CCShell>
  );
}
