import { Suspense }               from 'react';
import { requirePlatformAdmin }  from '@/lib/auth-guards';
import { getTenantContext }       from '@/lib/auth';
import { controlCenterServerApi } from '@/lib/control-center-api';
import { CCShell }                from '@/components/shell/cc-shell';
import { OutboxSummaryCards }     from '@/components/outbox/outbox-summary-cards';
import { OutboxOperationsTable }  from '@/components/outbox/outbox-operations-table';
import { OutboxDetailDrawer }     from '@/components/outbox/outbox-detail-drawer';
import type {

  OutboxListResponse,
  OutboxDetail,
  OutboxSummary,
} from '@/types/control-center';

export const dynamic = 'force-dynamic';

interface OutboxPageProps {
  searchParams: Promise<{
    page?:               string;
    status?:             string;
    eventType?:          string;
    tenantId?:           string;
    workflowInstanceId?: string;
    search?:             string;
    selected?:           string;
  }>;
}

const STATUS_FILTER_OPTIONS: { value: string; label: string }[] = [
  { value: '',              label: 'All statuses'  },
  { value: 'Pending',       label: 'Pending'       },
  { value: 'Processing',    label: 'Processing'    },
  { value: 'Failed',        label: 'Failed'        },
  { value: 'DeadLettered',  label: 'Dead Letters'  },
  { value: 'Succeeded',     label: 'Succeeded'     },
];

const EVENT_TYPE_OPTIONS: { value: string; label: string }[] = [
  { value: '',                          label: 'All event types'    },
  { value: 'workflow.start',            label: 'workflow.start'     },
  { value: 'workflow.advance',          label: 'workflow.advance'   },
  { value: 'workflow.complete',         label: 'workflow.complete'  },
  { value: 'workflow.cancel',           label: 'workflow.cancel'    },
  { value: 'workflow.fail',             label: 'workflow.fail'      },
  { value: 'workflow.admin.retry',      label: 'admin.retry'        },
  { value: 'workflow.admin.force_complete', label: 'admin.force_complete' },
  { value: 'workflow.admin.cancel',     label: 'admin.cancel'       },
  { value: 'workflow.sla.dueSoon',      label: 'sla.dueSoon'        },
  { value: 'workflow.sla.overdue',      label: 'sla.overdue'        },
  { value: 'workflow.sla.escalated',    label: 'sla.escalated'      },
];

/**
 * /operations/outbox — E17 outbox ops visibility page.
 *
 * Access: PlatformAdmin only. Exposes the Flow async outbox so operators
 * can inspect pending, retrying, failed, and dead-lettered items, and
 * perform a governed manual retry where eligible.
 *
 * Data: Flow `GET /api/v1/admin/outbox` + `GET /api/v1/admin/outbox/summary`
 * via the gateway. PlatformAdmin sees all rows across all tenants.
 */
export default async function OutboxPage({ searchParams }: OutboxPageProps) {
  const sp      = await searchParams;
  const session = await requirePlatformAdmin();
  const tenantCtx = await getTenantContext();

  const page               = Math.max(1, parseInt(sp.page ?? '1') || 1);
  const status             = sp.status             ?? '';
  const eventType          = sp.eventType          ?? '';
  const tenantId           = sp.tenantId           ?? tenantCtx?.tenantId ?? '';
  const workflowInstanceId = sp.workflowInstanceId ?? '';
  const search             = sp.search             ?? '';
  const selected           = (sp.selected ?? '').trim() || null;

  let result: OutboxListResponse | null = null;
  let fetchError: string | null = null;

  let summary: OutboxSummary = {
    pendingCount: 0, processingCount: 0,
    failedCount:  0, deadLetteredCount: 0, succeededCount: 0,
  };

  const [listResult, summaryResult] = await Promise.allSettled([
    controlCenterServerApi.outbox.list({
      page,
      pageSize:           20,
      status:             status             || undefined,
      eventType:          eventType          || undefined,
      tenantId:           tenantId           || undefined,
      workflowInstanceId: workflowInstanceId || undefined,
      search:             search             || undefined,
    }),
    controlCenterServerApi.outbox.summary(),
  ]);

  if (listResult.status === 'fulfilled') {
    result = listResult.value;
  } else {
    const e = listResult.reason;
    fetchError = e instanceof Error ? e.message : 'Failed to load outbox items.';
  }

  if (summaryResult.status === 'fulfilled') {
    summary = summaryResult.value;
  }

  // Detail drawer hydration — failures must NOT break the list page.
  let detail: OutboxDetail | null = null;
  let detailError: string | null = null;
  if (selected) {
    try {
      detail = await controlCenterServerApi.outbox.getById(selected);
    } catch (err) {
      detailError = err instanceof Error ? err.message : 'Failed to load outbox detail.';
    }
  }

  const baseQuery: Record<string, string | undefined> = {
    status:             status             || undefined,
    eventType:          eventType          || undefined,
    tenantId:           tenantId           || undefined,
    workflowInstanceId: workflowInstanceId || undefined,
    search:             search             || undefined,
  };

  const hasFilters = status || eventType || tenantId || workflowInstanceId || search;

  return (
    <CCShell userEmail={session.email}>
      <div className="space-y-4">
        {/* Header */}
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-3">
            <h1 className="text-xl font-semibold text-gray-900">Async Outbox</h1>
            <span className="inline-flex items-center gap-1.5 px-2 py-0.5 rounded-full bg-indigo-50 border border-indigo-200 text-[11px] font-semibold text-indigo-700">
              <span className="h-1.5 w-1.5 rounded-full bg-indigo-500" />
              Ops visibility
            </span>
            {tenantCtx && (
              <span className="inline-flex items-center gap-1.5 px-2 py-0.5 rounded-full bg-amber-100 border border-amber-300 text-[11px] font-semibold text-amber-700">
                <span className="h-1.5 w-1.5 rounded-full bg-amber-500" />
                Scoped to {tenantCtx.tenantName}
              </span>
            )}
          </div>
          <a
            href="/operations/outbox?status=DeadLettered"
            className="inline-flex items-center gap-1.5 text-xs text-red-700 hover:underline"
          >
            <i className="ri-skull-line" aria-hidden="true" />
            View dead letters
          </a>
        </div>

        {/* Summary cards */}
        <OutboxSummaryCards summary={summary} />

        {/* Filters */}
        <form method="GET" className="flex flex-wrap items-center gap-2 bg-white border border-gray-200 rounded-lg p-3">
          <select
            name="status"
            defaultValue={status}
            className="text-sm border border-gray-200 rounded-md px-2 py-1.5 text-gray-700 bg-white focus:outline-none focus:ring-1 focus:ring-indigo-400 focus:border-indigo-400"
          >
            {STATUS_FILTER_OPTIONS.map(o => (
              <option key={o.value} value={o.value}>{o.label}</option>
            ))}
          </select>

          <select
            name="eventType"
            defaultValue={eventType}
            className="text-sm border border-gray-200 rounded-md px-2 py-1.5 text-gray-700 bg-white focus:outline-none focus:ring-1 focus:ring-indigo-400 focus:border-indigo-400"
          >
            {EVENT_TYPE_OPTIONS.map(o => (
              <option key={o.value} value={o.value}>{o.label}</option>
            ))}
          </select>

          <input
            type="text"
            name="tenantId"
            defaultValue={tenantId}
            placeholder="Tenant id (optional)"
            className="w-44 text-sm border border-gray-200 rounded-md px-3 py-1.5 text-gray-900 placeholder-gray-400 focus:outline-none focus:ring-1 focus:ring-indigo-400 focus:border-indigo-400"
          />

          <input
            type="text"
            name="workflowInstanceId"
            defaultValue={workflowInstanceId}
            placeholder="Workflow instance id"
            className="w-56 text-sm border border-gray-200 rounded-md px-3 py-1.5 text-gray-900 placeholder-gray-400 focus:outline-none focus:ring-1 focus:ring-indigo-400 focus:border-indigo-400"
          />

          <input
            type="text"
            name="search"
            defaultValue={search}
            placeholder="Exact outbox id"
            className="flex-1 min-w-[180px] text-sm border border-gray-200 rounded-md px-3 py-1.5 text-gray-900 placeholder-gray-400 focus:outline-none focus:ring-1 focus:ring-indigo-400 focus:border-indigo-400"
          />

          <button
            type="submit"
            className="text-sm px-3 py-1.5 rounded-md border border-gray-200 bg-white text-gray-600 hover:bg-gray-50 transition-colors"
          >
            Apply
          </button>

          {hasFilters && (
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

        {/* Info banner when filtering by dead-letter */}
        {status === 'DeadLettered' && (
          <div className="bg-red-50 border border-red-200 rounded-lg px-4 py-3 text-sm text-red-800 flex items-start gap-2">
            <i className="ri-skull-line mt-0.5 shrink-0 text-[15px]" aria-hidden="true" />
            <span>
              <strong>Dead-lettered items</strong> have exhausted all automatic retry attempts.
              Open an item to inspect the error and perform a governed manual retry if appropriate.
            </span>
          </div>
        )}

        {/* Table */}
        {result && (
          <OutboxOperationsTable
            rows={result.items}
            totalCount={result.totalCount}
            page={result.page}
            pageSize={result.pageSize}
            baseQuery={baseQuery}
            selectedId={selected}
          />
        )}
      </div>

      {/* Detail drawer — wrapped in Suspense because it uses useSearchParams */}
      <Suspense>
        <OutboxDetailDrawer
          selectedId={selected}
          detail={detail}
          errorMessage={detailError}
        />
      </Suspense>
    </CCShell>
  );
}
