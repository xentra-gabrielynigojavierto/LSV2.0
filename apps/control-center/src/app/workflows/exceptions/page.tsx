import { requirePlatformAdmin } from '@/lib/auth-guards';
import { getTenantContext } from '@/lib/auth';
import { controlCenterServerApi } from '@/lib/control-center-api';
import { CCShell } from '@/components/shell/cc-shell';
import { WorkflowExceptionsTable } from '@/components/workflows/workflow-exceptions-table';
import { WorkflowDetailDrawer } from '@/components/workflows/workflow-detail-drawer';
import type {

  WorkflowInstanceDetail,
  WorkflowInstancePagedResponse,
} from '@/types/control-center';

export const dynamic = 'force-dynamic';

interface ExceptionsPageProps {
  searchParams: Promise<{
    page?:                string;
    productKey?:          string;
    status?:              string;
    tenantId?:            string;
    search?:              string;
    classification?:      string;
    staleThresholdHours?: string;
    selected?:            string;
  }>;
}

const PRODUCT_FILTER_OPTIONS: { value: string; label: string }[] = [
  { value: '',                 label: 'All products'   },
  { value: 'FLOW_GENERIC',     label: 'Flow (generic)' },
  { value: 'SYNQ_LIENS',       label: 'SynqLien'       },
  { value: 'SYNQ_FUND',        label: 'SynqFund'       },
  { value: 'SYNQ_BILL',        label: 'SynqBill'       },
  { value: 'SYNQ_RX',          label: 'SynqRx'         },
  { value: 'SYNQ_PAYOUT',      label: 'SynqPayout'     },
  { value: 'SYNQ_CARECONNECT', label: 'CareConnect'    },
];

const STATUS_FILTER_OPTIONS: { value: string; label: string }[] = [
  { value: '',          label: 'Any status' },
  { value: 'Active',    label: 'Active'     },
  { value: 'Pending',   label: 'Pending'    },
  { value: 'Cancelled', label: 'Cancelled'  },
  { value: 'Failed',    label: 'Failed'     },
];

const CLASSIFICATION_FILTER_OPTIONS: { value: string; label: string }[] = [
  { value: '',             label: 'All exception types' },
  { value: 'Failed',       label: 'Failed only'         },
  { value: 'Cancelled',    label: 'Cancelled only'      },
  { value: 'Stuck',        label: 'Stuck only'          },
  { value: 'ErrorPresent', label: 'Error present only'  },
];

const STALE_THRESHOLD_OPTIONS: { value: string; label: string }[] = [
  { value: '24',  label: '24h' },
  { value: '48',  label: '48h' },
  { value: '72',  label: '72h' },
  { value: '168', label: '7d'  },
];

/**
 * /workflows/exceptions — E9.3 read-only operational diagnostics view.
 *
 * Access: PlatformAdmin only at the page boundary (matches the E9.1
 * Workflows page; the Flow admin endpoint additionally allows
 * TenantAdmin scoped to their own tenant for future expansion).
 *
 * Data: Flow `GET /api/v1/admin/workflow-instances?exceptionOnly=true`
 * via the gateway. The endpoint server-side narrows to rows that match
 * one or more deterministic classifications and tags every row with
 * the labels that apply (`classifications`). The same response shape
 * also echoes the stale threshold the server used so the UI can label
 * the "Stuck >Nh" chip without guessing.
 *
 * Detail: row "Open →" reuses the E9.2 `WorkflowDetailDrawer` via the
 * shared `?selected=<id>` URL contract, so list/filter state survives
 * inspection.
 */
export default async function WorkflowExceptionsPage({
  searchParams,
}: ExceptionsPageProps) {
  const sp        = await searchParams;
  const session   = await requirePlatformAdmin();
  const tenantCtx = await getTenantContext();

  const page = Math.max(1, parseInt(sp.page ?? '1') || 1);

  const productKey      = sp.productKey ?? '';
  const status          = sp.status     ?? '';
  const tenantId        = sp.tenantId   ?? tenantCtx?.tenantId ?? '';
  const search          = sp.search     ?? '';
  const classification  = sp.classification ?? '';
  const staleHoursParam = parseInt(sp.staleThresholdHours ?? '', 10);
  const staleHours      = Number.isFinite(staleHoursParam) && staleHoursParam > 0
    ? staleHoursParam
    : 24;
  const selected        = (sp.selected ?? '').trim() || null;

  let result: WorkflowInstancePagedResponse | null = null;
  let fetchError: string | null = null;
  try {
    result = await controlCenterServerApi.workflows.listExceptions({
      page,
      pageSize:            20,
      productKey:          productKey     || undefined,
      status:              status         || undefined,
      tenantId:            tenantId       || undefined,
      search:              search         || undefined,
      classification:      classification || undefined,
      staleThresholdHours: staleHours,
    });
  } catch (err) {
    fetchError = err instanceof Error ? err.message : 'Failed to load workflow exceptions.';
  }

  // Hydrate the drawer detail server-side when ?selected=<id> is present.
  // Failures here must not break the list page; the drawer renders its
  // own compact error state.
  let detail: WorkflowInstanceDetail | null = null;
  let detailError: string | null = null;
  if (selected) {
    try {
      detail = await controlCenterServerApi.workflows.getById(selected);
    } catch (err) {
      detailError = err instanceof Error ? err.message : 'Failed to load workflow detail.';
    }
  }

  const baseQuery = {
    productKey,
    status,
    tenantId,
    search,
    classification,
    staleThresholdHours: String(staleHours),
  };

  return (
    <CCShell userEmail={session.email}>
      <div className="space-y-4">
        {/* Header */}
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-3">
            <h1 className="text-xl font-semibold text-gray-900">Workflow exceptions</h1>
            <span className="inline-flex items-center gap-1.5 px-2 py-0.5 rounded-full bg-amber-50 border border-amber-200 text-[11px] font-semibold text-amber-700">
              <span className="h-1.5 w-1.5 rounded-full bg-amber-500" />
              Operational triage
            </span>
            {tenantCtx && (
              <span className="inline-flex items-center gap-1.5 px-2 py-0.5 rounded-full bg-amber-100 border border-amber-300 text-[11px] font-semibold text-amber-700">
                <span className="h-1.5 w-1.5 rounded-full bg-amber-500" />
                Scoped to {tenantCtx.tenantName}
              </span>
            )}
          </div>
          <a
            href="/workflows"
            className="text-xs text-indigo-600 hover:underline"
          >
            ← Back to all workflows
          </a>
        </div>

        {/* Description */}
        <p className="text-xs text-gray-500">
          Workflows currently classified as <strong>Failed</strong>, <strong>Cancelled</strong>,
          <strong> Stuck</strong> (no progress in the last {staleHours}h) or with
          a captured engine <strong>error message</strong>. Read-only — execution
          actions remain on the product surface.
        </p>

        {/* Filters */}
        <form
          method="GET"
          className="flex flex-wrap items-center gap-2 bg-white border border-gray-200 rounded-lg p-3"
        >
          <select
            name="classification"
            defaultValue={classification}
            className="text-sm border border-gray-200 rounded-md px-2 py-1.5 text-gray-700 bg-white focus:outline-none focus:ring-1 focus:ring-indigo-400 focus:border-indigo-400"
          >
            {CLASSIFICATION_FILTER_OPTIONS.map(o => (
              <option key={o.value} value={o.value}>{o.label}</option>
            ))}
          </select>

          <select
            name="staleThresholdHours"
            defaultValue={String(staleHours)}
            className="text-sm border border-gray-200 rounded-md px-2 py-1.5 text-gray-700 bg-white focus:outline-none focus:ring-1 focus:ring-indigo-400 focus:border-indigo-400"
            aria-label="Stale threshold"
          >
            {STALE_THRESHOLD_OPTIONS.map(o => (
              <option key={o.value} value={o.value}>Stale &gt; {o.label}</option>
            ))}
          </select>

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

          {(productKey || status || tenantId || search || classification || staleHours !== 24) && (
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
          <WorkflowExceptionsTable
            rows={result.items}
            totalCount={result.totalCount}
            page={result.page}
            pageSize={result.pageSize}
            staleThresholdHours={result.staleThresholdHours}
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
