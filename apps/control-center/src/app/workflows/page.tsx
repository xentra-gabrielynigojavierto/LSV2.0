import { requirePlatformAdmin } from '@/lib/auth-guards';
import { getTenantContext } from '@/lib/auth';
import { controlCenterServerApi } from '@/lib/control-center-api';
import { CCShell } from '@/components/shell/cc-shell';
import { WorkflowOperationsTable } from '@/components/workflows/workflow-operations-table';
import { WorkflowDetailDrawer } from '@/components/workflows/workflow-detail-drawer';
import type {

  PagedResponse,
  WorkflowInstanceListItem,
  WorkflowInstanceDetail,
} from '@/types/control-center';

export const dynamic = 'force-dynamic';

interface WorkflowsPageProps {
  searchParams: Promise<{
    page?:       string;
    productKey?: string;
    status?:     string;
    tenantId?:   string;
    search?:     string;
    selected?:   string;
  }>;
}

const PRODUCT_FILTER_OPTIONS: { value: string; label: string }[] = [
  { value: '',                   label: 'All products'   },
  { value: 'FLOW_GENERIC',       label: 'Flow (generic)' },
  { value: 'SYNQ_LIENS',         label: 'SynqLien'       },
  { value: 'SYNQ_FUND',          label: 'SynqFund'       },
  { value: 'SYNQ_BILL',          label: 'SynqBill'       },
  { value: 'SYNQ_RX',            label: 'SynqRx'         },
  { value: 'SYNQ_PAYOUT',        label: 'SynqPayout'     },
  { value: 'SYNQ_CARECONNECT',   label: 'CareConnect'    },
];

const STATUS_FILTER_OPTIONS: { value: string; label: string }[] = [
  { value: '',          label: 'All statuses' },
  { value: 'Active',    label: 'Active'       },
  { value: 'Pending',   label: 'Pending'      },
  { value: 'Completed', label: 'Completed'    },
  { value: 'Cancelled', label: 'Cancelled'    },
  { value: 'Failed',    label: 'Failed'       },
];

/**
 * /workflows — E9.1 cross-tenant / cross-product workflow operations list.
 *
 * Access: PlatformAdmin only at the page boundary. The Flow admin endpoint
 * additionally allows TenantAdmin (server-side scoped to their own tenant),
 * but the Control Center surface is platform-operations-only today.
 *
 * Data: Flow `GET /api/v1/admin/workflow-instances` via the gateway.
 *   - PlatformAdmin sees rows across all tenants; the optional `tenantId`
 *     filter narrows the listing.
 *   - The endpoint bypasses the per-tenant EF query filter and re-applies
 *     scoping in code so cross-tenant visibility cannot leak to other roles.
 */
export default async function WorkflowsPage({ searchParams }: WorkflowsPageProps) {
  const sp        = await searchParams;
  const session   = await requirePlatformAdmin();
  const tenantCtx = await getTenantContext();

  const page       = Math.max(1, parseInt(sp.page ?? '1') || 1);
  const productKey = sp.productKey ?? '';
  const status     = sp.status     ?? '';
  // If the operator is currently scoped to a tenant via the CC tenant
  // switcher, default the filter to that tenant — explicit ?tenantId=
  // overrides.
  const tenantId   = sp.tenantId   ?? tenantCtx?.tenantId ?? '';
  const search     = sp.search     ?? '';
  const selected   = (sp.selected ?? '').trim() || null;

  let result: PagedResponse<WorkflowInstanceListItem> | null = null;
  let fetchError: string | null = null;

  try {
    result = await controlCenterServerApi.workflows.list({
      page,
      pageSize:   20,
      productKey: productKey || undefined,
      status:     status     || undefined,
      tenantId:   tenantId   || undefined,
      search:     search     || undefined,
    });
  } catch (err) {
    fetchError = err instanceof Error ? err.message : 'Failed to load workflow instances.';
  }

  // E9.2 — when the URL carries `?selected=<id>`, hydrate the detail
  // drawer server-side. Failures here must NOT break the list page; we
  // surface a compact error inside the drawer instead.
  let detail: WorkflowInstanceDetail | null = null;
  let detailError: string | null = null;
  if (selected) {
    try {
      detail = await controlCenterServerApi.workflows.getById(selected);
    } catch (err) {
      detailError = err instanceof Error ? err.message : 'Failed to load workflow detail.';
    }
  }

  const baseQuery = { productKey, status, tenantId, search };

  return (
    <CCShell userEmail={session.email}>
      <div className="space-y-4">
        {/* Header */}
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-3">
            <h1 className="text-xl font-semibold text-gray-900">Workflows</h1>
            <span className="inline-flex items-center gap-1.5 px-2 py-0.5 rounded-full bg-blue-50 border border-blue-200 text-[11px] font-semibold text-blue-700">
              <span className="h-1.5 w-1.5 rounded-full bg-blue-500" />
              Cross-product operations view
            </span>
            {tenantCtx && (
              <span className="inline-flex items-center gap-1.5 px-2 py-0.5 rounded-full bg-amber-100 border border-amber-300 text-[11px] font-semibold text-amber-700">
                <span className="h-1.5 w-1.5 rounded-full bg-amber-500" />
                Scoped to {tenantCtx.tenantName}
              </span>
            )}
          </div>
          <a
            href="/workflows/exceptions"
            className="inline-flex items-center gap-1.5 text-xs text-amber-700 hover:underline"
          >
            <i className="ri-error-warning-line" aria-hidden="true" />
            View exceptions
          </a>
        </div>

        {/* Filters */}
        <form method="GET" className="flex flex-wrap items-center gap-2 bg-white border border-gray-200 rounded-lg p-3">
          <select
            name="productKey"
            defaultValue={productKey}
            className="text-sm border border-gray-200 rounded-md px-2 py-1.5 text-gray-700 bg-white focus:outline-none focus:ring-1 focus:ring-indigo-400 focus:border-indigo-400"
          >
            {PRODUCT_FILTER_OPTIONS.map(o => (
              <option key={o.value} value={o.value}>{o.label}</option>
            ))}
          </select>

          <select
            name="status"
            defaultValue={status}
            className="text-sm border border-gray-200 rounded-md px-2 py-1.5 text-gray-700 bg-white focus:outline-none focus:ring-1 focus:ring-indigo-400 focus:border-indigo-400"
          >
            {STATUS_FILTER_OPTIONS.map(o => (
              <option key={o.value} value={o.value}>{o.label}</option>
            ))}
          </select>

          <input
            type="text"
            name="tenantId"
            defaultValue={tenantId}
            placeholder="Tenant id (optional)"
            className="w-56 text-sm border border-gray-200 rounded-md px-3 py-1.5 text-gray-900 placeholder-gray-400 focus:outline-none focus:ring-1 focus:ring-indigo-400 focus:border-indigo-400"
          />

          <input
            type="text"
            name="search"
            defaultValue={search}
            placeholder="Search id, correlation key or step…"
            className="flex-1 min-w-[220px] text-sm border border-gray-200 rounded-md px-3 py-1.5 text-gray-900 placeholder-gray-400 focus:outline-none focus:ring-1 focus:ring-indigo-400 focus:border-indigo-400"
          />

          <button
            type="submit"
            className="text-sm px-3 py-1.5 rounded-md border border-gray-200 bg-white text-gray-600 hover:bg-gray-50 transition-colors"
          >
            Apply
          </button>

          {(productKey || status || tenantId || search) && (
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
          <WorkflowOperationsTable
            rows={result.items}
            totalCount={result.totalCount}
            page={result.page}
            pageSize={result.pageSize}
            baseQuery={baseQuery}
            selectedId={selected}
          />
        )}
      </div>

      <WorkflowDetailDrawer
        selectedId={selected}
        detail={detail}
        errorMessage={detailError}
        canAdmin
      />
    </CCShell>
  );
}
