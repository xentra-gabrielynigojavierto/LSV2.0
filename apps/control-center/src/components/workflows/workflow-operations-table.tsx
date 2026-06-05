import Link from 'next/link';
import type { WorkflowInstanceListItem } from '@/types/control-center';

interface WorkflowOperationsTableProps {
  rows:         WorkflowInstanceListItem[];
  totalCount:   number;
  page:         number;
  pageSize:     number;
  /** Preserve current filters when paginating or opening the drawer. */
  baseQuery:    Record<string, string | undefined>;
  /** Currently-selected workflow id (driven by `?selected=`). */
  selectedId?:  string | null;
}

/**
 * Build an `?selected=<id>&page=N&...` href that opens the detail
 * drawer while preserving every other filter the operator has applied.
 * The current page number is preserved so closing the drawer returns to
 * the same paginated slice.
 */
function buildSelectHref(
  base: Record<string, string | undefined>,
  page: number,
  id: string,
): string {
  const params = new URLSearchParams();
  for (const [k, v] of Object.entries(base)) {
    if (v !== undefined && v !== '') params.set(k, v);
  }
  if (page > 1) params.set('page', String(page));
  params.set('selected', id);
  return `?${params.toString()}`;
}

const STATUS_STYLES: Record<string, string> = {
  Active:    'bg-blue-50   text-blue-700   border-blue-200',
  Pending:   'bg-amber-50  text-amber-700  border-amber-200',
  Completed: 'bg-green-50  text-green-700  border-green-200',
  Cancelled: 'bg-gray-100  text-gray-500   border-gray-200',
  Failed:    'bg-red-50    text-red-700    border-red-200',
};

const PRODUCT_LABELS: Record<string, string> = {
  FLOW_GENERIC:        'Flow',
  SYNQ_LIENS:          'SynqLien',
  SYNQ_LIEN:           'SynqLien',
  SYNQ_FUND:           'SynqFund',
  SYNQ_BILL:           'SynqBill',
  SYNQ_RX:             'SynqRx',
  SYNQ_PAYOUT:         'SynqPayout',
  SYNQ_CARECONNECT:    'CareConnect',
  CARE_CONNECT:        'CareConnect',
};

function formatProduct(key: string): string {
  if (!key) return '—';
  return PRODUCT_LABELS[key] ?? key;
}

function formatTimestamp(iso: string | null): string {
  if (!iso) return '—';
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return '—';
  return d.toLocaleString('en-US', {
    month:  'short',
    day:    'numeric',
    year:   'numeric',
    hour:   '2-digit',
    minute: '2-digit',
  });
}

function shortId(id: string): string {
  if (!id) return '—';
  return id.length > 8 ? id.slice(0, 8) : id;
}

function StatusBadge({ status }: { status: string }) {
  const cls = STATUS_STYLES[status] ?? 'bg-gray-100 text-gray-600 border-gray-200';
  return (
    <span className={`inline-flex items-center px-2 py-0.5 rounded text-[11px] font-semibold border ${cls}`}>
      {status || 'Unknown'}
    </span>
  );
}

function buildPageHref(base: Record<string, string | undefined>, page: number): string {
  const params = new URLSearchParams();
  for (const [k, v] of Object.entries(base)) {
    if (v !== undefined && v !== '') params.set(k, v);
  }
  params.set('page', String(page));
  return `?${params.toString()}`;
}

export function WorkflowOperationsTable({
  rows,
  totalCount,
  page,
  pageSize,
  baseQuery,
  selectedId,
}: WorkflowOperationsTableProps) {
  if (rows.length === 0) {
    return (
      <div className="bg-white border border-gray-200 rounded-lg p-10 text-center">
        <p className="text-sm text-gray-400">No workflow instances match the current filters.</p>
      </div>
    );
  }

  return (
    <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
      <div className="overflow-x-auto">
        <table className="min-w-full divide-y divide-gray-100">
          <thead>
            <tr className="bg-gray-50">
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Workflow</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Product</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Status</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Current Step</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Source Entity</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Tenant</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Assigned</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Started</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Updated</th>
              <th className="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase tracking-wide w-20">
                <span className="sr-only">Actions</span>
              </th>
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-100">
            {rows.map(row => (
              <tr
                key={row.id}
                className={`transition-colors ${
                  selectedId === row.id ? 'bg-indigo-50/60' : 'hover:bg-gray-50'
                }`}
              >
                <td className="px-4 py-3">
                  <p className="text-sm font-medium text-gray-900">{row.workflowName ?? '—'}</p>
                  <p className="text-[11px] text-gray-400 mt-0.5 font-mono">{shortId(row.id)}</p>
                </td>
                <td className="px-4 py-3 text-sm text-gray-700 whitespace-nowrap">
                  {formatProduct(row.productKey)}
                </td>
                <td className="px-4 py-3">
                  <StatusBadge status={row.status} />
                </td>
                <td className="px-4 py-3 text-sm text-gray-700">
                  {row.currentStepKey ?? <span className="text-gray-400">—</span>}
                </td>
                <td className="px-4 py-3 text-sm text-gray-700">
                  {row.sourceEntityType
                    ? (
                      <div>
                        <p className="text-xs text-gray-500">{row.sourceEntityType}</p>
                        <p className="text-[11px] text-gray-400 font-mono">{row.sourceEntityId ?? '—'}</p>
                      </div>
                    )
                    : <span className="text-gray-400">—</span>}
                </td>
                <td className="px-4 py-3 text-[11px] text-gray-500 font-mono whitespace-nowrap">
                  {shortId(row.tenantId)}
                </td>
                <td className="px-4 py-3 text-[11px] text-gray-500 font-mono">
                  {row.assignedToUserId ? shortId(row.assignedToUserId) : <span className="text-gray-400">—</span>}
                </td>
                <td className="px-4 py-3 text-xs text-gray-400 whitespace-nowrap">
                  {formatTimestamp(row.startedAt)}
                </td>
                <td className="px-4 py-3 text-xs text-gray-400 whitespace-nowrap">
                  {formatTimestamp(row.updatedAt ?? row.createdAt)}
                </td>
                <td className="px-4 py-3 text-right whitespace-nowrap">
                  <Link
                    href={buildSelectHref(baseQuery, page, row.id)}
                    scroll={false}
                    className="text-xs text-indigo-600 hover:underline"
                    aria-label={`Open workflow ${row.workflowName ?? row.id}`}
                  >
                    Open →
                  </Link>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <div className="px-4 py-3 border-t border-gray-100 flex items-center justify-between">
        <p className="text-xs text-gray-400">
          Showing {(page - 1) * pageSize + 1}–{Math.min(page * pageSize, totalCount)} of {totalCount}
        </p>
        <div className="flex items-center gap-2">
          {page > 1 && (
            <Link href={buildPageHref(baseQuery, page - 1)} className="text-xs text-indigo-600 hover:underline">
              ← Previous
            </Link>
          )}
          {page * pageSize < totalCount && (
            <Link href={buildPageHref(baseQuery, page + 1)} className="text-xs text-indigo-600 hover:underline">
              Next →
            </Link>
          )}
        </div>
      </div>
    </div>
  );
}
