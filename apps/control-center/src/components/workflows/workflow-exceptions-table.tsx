import Link from 'next/link';
import type {
  WorkflowInstanceListItem,
  WorkflowClassification,
} from '@/types/control-center';

interface WorkflowExceptionsTableProps {
  rows:                WorkflowInstanceListItem[];
  totalCount:          number;
  page:                number;
  pageSize:            number;
  /** Echoed from the server so labels stay in sync ("Stuck >24h"). */
  staleThresholdHours: number;
  /** Preserve filters when paginating or opening the drawer. */
  baseQuery:           Record<string, string | undefined>;
  selectedId?:         string | null;
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

/**
 * Per-classification chip styling. Intentionally subdued — this is an
 * ops surface, not a red-alert dashboard.
 */
const CLASSIFICATION_STYLES: Record<WorkflowClassification, string> = {
  Failed:       'bg-red-50    text-red-700    border-red-200',
  Cancelled:    'bg-gray-100  text-gray-600   border-gray-200',
  Stuck:        'bg-amber-50  text-amber-700  border-amber-200',
  ErrorPresent: 'bg-orange-50 text-orange-700 border-orange-200',
};

function formatTimestamp(iso: string | null): string {
  if (!iso) return '—';
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return '—';
  return d.toLocaleString('en-US', {
    month: 'short', day: 'numeric', year: 'numeric',
    hour:  '2-digit', minute: '2-digit',
  });
}

/**
 * E9.3 — short, human-readable "age since last touched" label, used to
 * answer "how long has this been in trouble?" without requiring the
 * operator to do date math.
 */
function formatAge(iso: string | null): string {
  if (!iso) return '—';
  const t = new Date(iso).getTime();
  if (Number.isNaN(t)) return '—';
  const ms = Date.now() - t;
  if (ms < 0) return '—';
  const minutes = Math.floor(ms / 60_000);
  if (minutes < 60)  return `${minutes}m`;
  const hours = Math.floor(minutes / 60);
  if (hours   < 48)  return `${hours}h`;
  const days  = Math.floor(hours / 24);
  return `${days}d`;
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

function ClassificationBadge({
  label,
  staleThresholdHours,
}: {
  label:               WorkflowClassification;
  staleThresholdHours: number;
}) {
  const cls  = CLASSIFICATION_STYLES[label];
  const text = label === 'Stuck'        ? `Stuck >${staleThresholdHours}h`
             : label === 'ErrorPresent' ? 'Error present'
             : label;
  return (
    <span className={`inline-flex items-center px-2 py-0.5 rounded text-[11px] font-medium border ${cls}`}>
      {text}
    </span>
  );
}

function buildSelectHref(
  base: Record<string, string | undefined>,
  page: number,
  id:   string,
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
  base: Record<string, string | undefined>,
  page: number,
): string {
  const params = new URLSearchParams();
  for (const [k, v] of Object.entries(base)) {
    if (v !== undefined && v !== '') params.set(k, v);
  }
  params.set('page', String(page));
  return `?${params.toString()}`;
}

function truncate(text: string, max = 80): string {
  if (text.length <= max) return text;
  return `${text.slice(0, max - 1)}…`;
}

export function WorkflowExceptionsTable({
  rows,
  totalCount,
  page,
  pageSize,
  staleThresholdHours,
  baseQuery,
  selectedId,
}: WorkflowExceptionsTableProps) {
  if (rows.length === 0) {
    return (
      <div className="bg-white border border-gray-200 rounded-lg p-10 text-center">
        <p className="text-sm text-gray-500">No workflows currently require attention.</p>
        <p className="text-xs text-gray-400 mt-1">
          Stale threshold: {staleThresholdHours}h.
        </p>
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
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Reason</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Status</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Product</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Source</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Tenant</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Age</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Updated</th>
              <th className="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase tracking-wide w-20">
                <span className="sr-only">Actions</span>
              </th>
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-100">
            {rows.map(row => {
              const labels = (row.classifications ?? []) as WorkflowClassification[];
              const isSelected = selectedId === row.id;
              return (
                <tr
                  key={row.id}
                  className={`align-top transition-colors ${
                    isSelected ? 'bg-indigo-50/60' : 'hover:bg-gray-50'
                  }`}
                >
                  <td className="px-4 py-3">
                    <p className="text-sm font-medium text-gray-900">{row.workflowName ?? '—'}</p>
                    <p className="text-[11px] text-gray-400 mt-0.5 font-mono">{shortId(row.id)}</p>
                    {row.lastErrorMessage && (
                      <p className="mt-1 text-[11px] text-red-600/90 leading-snug">
                        {truncate(row.lastErrorMessage, 90)}
                      </p>
                    )}
                  </td>
                  <td className="px-4 py-3">
                    <div className="flex flex-wrap gap-1">
                      {labels.length === 0
                        ? <span className="text-[11px] text-gray-400">—</span>
                        : labels.map(l => (
                          <ClassificationBadge
                            key={l}
                            label={l}
                            staleThresholdHours={staleThresholdHours}
                          />
                        ))}
                    </div>
                  </td>
                  <td className="px-4 py-3">
                    <StatusBadge status={row.status} />
                  </td>
                  <td className="px-4 py-3 text-sm text-gray-700 whitespace-nowrap">
                    {PRODUCT_LABELS[row.productKey] ?? row.productKey}
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
                  <td className="px-4 py-3 text-xs text-gray-500 whitespace-nowrap">
                    {formatAge(row.updatedAt ?? row.createdAt)}
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
              );
            })}
          </tbody>
        </table>
      </div>

      <div className="px-4 py-3 border-t border-gray-100 flex items-center justify-between">
        <p className="text-xs text-gray-400">
          Showing {(page - 1) * pageSize + 1}–{Math.min(page * pageSize, totalCount)} of {totalCount}
          <span className="ml-2 text-gray-300">·</span>
          <span className="ml-2">Stale threshold {staleThresholdHours}h</span>
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
