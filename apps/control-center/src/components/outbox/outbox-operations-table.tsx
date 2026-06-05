import Link from 'next/link';
import type { OutboxListItem } from '@/types/control-center';

interface OutboxOperationsTableProps {
  rows:        OutboxListItem[];
  totalCount:  number;
  page:        number;
  pageSize:    number;
  baseQuery:   Record<string, string | undefined>;
  selectedId?: string | null;
}

function buildSelectHref(
  base:   Record<string, string | undefined>,
  page:   number,
  id:     string,
): string {
  const params = new URLSearchParams();
  for (const [k, v] of Object.entries(base)) {
    if (v !== undefined && v !== '') params.set(k, v);
  }
  if (page > 1) params.set('page', String(page));
  params.set('selected', id);
  return `?${params.toString()}`;
}

function buildPageHref(
  base:  Record<string, string | undefined>,
  page:  number,
): string {
  const params = new URLSearchParams();
  for (const [k, v] of Object.entries(base)) {
    if (v !== undefined && v !== '') params.set(k, v);
  }
  if (page > 1) params.set('page', String(page));
  return `?${params.toString()}`;
}

const STATUS_STYLES: Record<string, string> = {
  Pending:      'bg-amber-50  text-amber-700  border-amber-200',
  Processing:   'bg-blue-50   text-blue-700   border-blue-200',
  Succeeded:    'bg-green-50  text-green-700  border-green-200',
  Failed:       'bg-red-50    text-red-700    border-red-200',
  DeadLettered: 'bg-red-100   text-red-800    border-red-300',
};

const STATUS_ICONS: Record<string, string> = {
  Pending:      'ri-time-line',
  Processing:   'ri-loader-4-line',
  Succeeded:    'ri-checkbox-circle-line',
  Failed:       'ri-error-warning-line',
  DeadLettered: 'ri-skull-line',
};

function fmtDate(iso: string | null | undefined): string {
  if (!iso) return '—';
  try {
    return new Date(iso).toLocaleString('en-US', {
      month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit',
    });
  } catch {
    return iso;
  }
}

function truncateId(id: string): string {
  return id.length > 8 ? id.slice(0, 8) + '…' : id;
}

function eventTypeLabel(t: string): string {
  return t
    .replace(/^workflow\.admin\./, 'admin/')
    .replace(/^workflow\.sla\./,   'sla/')
    .replace(/^workflow\./,        '');
}

/**
 * E17 — server-rendered outbox operations table.
 *
 * Each row is a link that sets `?selected=<id>` in the URL to open the
 * detail drawer without a full page navigation. Includes a compact
 * attempt/retry indicator and a truncated error preview for quick triage.
 */
export function OutboxOperationsTable({
  rows,
  totalCount,
  page,
  pageSize,
  baseQuery,
  selectedId,
}: OutboxOperationsTableProps) {
  const totalPages = Math.ceil(totalCount / pageSize) || 1;
  const hasPrev    = page > 1;
  const hasNext    = page < totalPages;

  if (rows.length === 0) {
    return (
      <div className="bg-white border border-gray-200 rounded-lg px-6 py-10 text-center text-sm text-gray-500">
        No outbox items match the current filters.
      </div>
    );
  }

  return (
    <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
      <div className="overflow-x-auto">
        <table className="min-w-full text-sm">
          <thead className="bg-gray-50 border-b border-gray-200">
            <tr>
              <th className="px-4 py-2.5 text-left text-[11px] font-semibold uppercase tracking-wide text-gray-500 w-28">ID</th>
              <th className="px-4 py-2.5 text-left text-[11px] font-semibold uppercase tracking-wide text-gray-500">Event Type</th>
              <th className="px-4 py-2.5 text-left text-[11px] font-semibold uppercase tracking-wide text-gray-500">Status</th>
              <th className="px-4 py-2.5 text-left text-[11px] font-semibold uppercase tracking-wide text-gray-500 w-20">Attempts</th>
              <th className="px-4 py-2.5 text-left text-[11px] font-semibold uppercase tracking-wide text-gray-500">Tenant</th>
              <th className="px-4 py-2.5 text-left text-[11px] font-semibold uppercase tracking-wide text-gray-500">Workflow</th>
              <th className="px-4 py-2.5 text-left text-[11px] font-semibold uppercase tracking-wide text-gray-500">Created</th>
              <th className="px-4 py-2.5 text-left text-[11px] font-semibold uppercase tracking-wide text-gray-500">Next / Processed</th>
              <th className="px-4 py-2.5 text-left text-[11px] font-semibold uppercase tracking-wide text-gray-500">Last Error</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-100">
            {rows.map(row => {
              const isSelected = row.id === selectedId;
              const isTerminal = row.status === 'Succeeded' || row.status === 'DeadLettered';
              const nextOrProcessed = isTerminal
                ? fmtDate(row.processedAt)
                : fmtDate(row.nextAttemptAt);
              const nextLabel = isTerminal ? 'processed' : 'next retry';

              return (
                <Link
                  key={row.id}
                  href={buildSelectHref(baseQuery, page, row.id)}
                  className={`flex-1 table-row cursor-pointer transition-colors ${
                    isSelected
                      ? 'bg-indigo-50 hover:bg-indigo-100'
                      : 'hover:bg-gray-50'
                  }`}
                >
                  <td className="px-4 py-2.5">
                    <span
                      className="font-mono text-[11px] text-gray-500 bg-gray-100 px-1.5 py-0.5 rounded"
                      title={row.id}
                    >
                      {truncateId(row.id)}
                    </span>
                  </td>
                  <td className="px-4 py-2.5">
                    <span className="font-mono text-[12px] text-gray-700">
                      {eventTypeLabel(row.eventType)}
                    </span>
                  </td>
                  <td className="px-4 py-2.5">
                    <span className={`inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-[11px] font-semibold border ${
                      STATUS_STYLES[row.status] ?? 'bg-gray-50 text-gray-500 border-gray-200'
                    }`}>
                      <i
                        className={`${STATUS_ICONS[row.status] ?? 'ri-question-line'} text-[12px]`}
                        aria-hidden="true"
                      />
                      {row.status === 'DeadLettered' ? 'Dead Letter' : row.status}
                    </span>
                  </td>
                  <td className="px-4 py-2.5 text-center tabular-nums text-gray-700">
                    {row.attemptCount}
                  </td>
                  <td className="px-4 py-2.5">
                    <span className="text-xs text-gray-600 font-mono truncate max-w-[120px] block" title={row.tenantId}>
                      {row.tenantId}
                    </span>
                  </td>
                  <td className="px-4 py-2.5">
                    {row.workflowInstanceId ? (
                      <Link
                        href={`/workflows?selected=${row.workflowInstanceId}`}
                        className="text-indigo-600 hover:underline text-[11px] font-mono"
                        title={row.workflowInstanceId}
                        onClick={e => e.stopPropagation()}
                      >
                        {truncateId(row.workflowInstanceId)}
                      </Link>
                    ) : (
                      <span className="text-gray-400 text-xs">—</span>
                    )}
                  </td>
                  <td className="px-4 py-2.5 text-xs text-gray-500 whitespace-nowrap">
                    {fmtDate(row.createdAt)}
                  </td>
                  <td className="px-4 py-2.5 text-xs text-gray-500 whitespace-nowrap">
                    <span className="text-[10px] text-gray-400 block">{nextLabel}</span>
                    {nextOrProcessed}
                  </td>
                  <td className="px-4 py-2.5 max-w-[220px]">
                    {row.lastError ? (
                      <span
                        className="text-[11px] text-red-700 line-clamp-2 break-words"
                        title={row.lastError}
                      >
                        {row.lastError}
                      </span>
                    ) : (
                      <span className="text-gray-300 text-xs">—</span>
                    )}
                  </td>
                </Link>
              );
            })}
          </tbody>
        </table>
      </div>

      {/* Pagination */}
      <div className="flex items-center justify-between px-4 py-2.5 border-t border-gray-100 bg-gray-50">
        <span className="text-xs text-gray-500">
          {totalCount.toLocaleString()} item{totalCount !== 1 ? 's' : ''} · page {page} of {totalPages}
        </span>
        <div className="flex gap-2">
          {hasPrev && (
            <Link
              href={buildPageHref(baseQuery, page - 1)}
              className="text-xs px-3 py-1.5 rounded border border-gray-200 bg-white text-gray-600 hover:bg-gray-50"
            >
              Previous
            </Link>
          )}
          {hasNext && (
            <Link
              href={buildPageHref(baseQuery, page + 1)}
              className="text-xs px-3 py-1.5 rounded border border-gray-200 bg-white text-gray-600 hover:bg-gray-50"
            >
              Next
            </Link>
          )}
        </div>
      </div>
    </div>
  );
}
