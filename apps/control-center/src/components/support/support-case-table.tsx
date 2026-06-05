import Link from 'next/link';
import type { SupportCase, SupportCaseStatus, SupportCasePriority } from '@/types/control-center';
import { Routes } from '@/lib/routes';

interface SupportCaseTableProps {
  cases:         SupportCase[];
  totalCount:    number;
  page:          number;
  pageSize:      number;
  search:        string;
  status:        string;
  priority:      string;
  tenantId:      string;
  tenantMap:     Record<string, string>;
  tenantOptions: { id: string; name: string }[];
}

const STATUS_STYLES: Record<SupportCaseStatus, string> = {
  Open:          'bg-blue-100   text-blue-700   border-blue-300',
  Investigating: 'bg-amber-100  text-amber-700  border-amber-300',
  Resolved:      'bg-green-100  text-green-700  border-green-300',
  Closed:        'bg-gray-100   text-gray-500   border-gray-300',
};

const PRIORITY_STYLES: Record<SupportCasePriority, string> = {
  High:   'bg-red-100  text-red-700  border-red-300',
  Medium: 'bg-amber-50 text-amber-600 border-amber-200',
  Low:    'bg-gray-100 text-gray-500 border-gray-200',
};

const TENANT_TAG_COLORS = [
  'bg-violet-100 text-violet-700 border-violet-300',
  'bg-sky-100    text-sky-700    border-sky-300',
  'bg-teal-100   text-teal-700   border-teal-300',
  'bg-orange-100 text-orange-700 border-orange-300',
  'bg-pink-100   text-pink-700   border-pink-300',
  'bg-indigo-100 text-indigo-700 border-indigo-300',
  'bg-lime-100   text-lime-700   border-lime-300',
  'bg-cyan-100   text-cyan-700   border-cyan-300',
];

function tenantTagColor(tenantId: string): string {
  let hash = 0;
  for (let i = 0; i < tenantId.length; i++) {
    hash = (hash * 31 + tenantId.charCodeAt(i)) % TENANT_TAG_COLORS.length;
  }
  return TENANT_TAG_COLORS[Math.abs(hash)];
}

const ALL_STATUSES:   SupportCaseStatus[]   = ['Open', 'Investigating', 'Resolved', 'Closed'];
const ALL_PRIORITIES: SupportCasePriority[] = ['High', 'Medium', 'Low'];

/**
 * SupportCaseTable — filterable, paginated list of support cases.
 * Each row displays a coloured tenant tag so platform admins can
 * quickly identify which tenant a ticket belongs to.
 * Pure server component — filters via plain GET form.
 */
export function SupportCaseTable({
  cases, totalCount, page, pageSize,
  search, status, priority, tenantId,
  tenantMap, tenantOptions,
}: SupportCaseTableProps) {
  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));
  const start      = (page - 1) * pageSize + 1;
  const end        = Math.min(page * pageSize, totalCount);

  function pageHref(p: number) {
    const params = new URLSearchParams();
    if (search)   params.set('search',   search);
    if (status)   params.set('status',   status);
    if (priority) params.set('priority', priority);
    if (tenantId) params.set('tenantId', tenantId);
    params.set('page', String(p));
    return `${Routes.support}?${params.toString()}`;
  }

  const hasFilters = !!(search || status || priority || tenantId);

  return (
    <div className="space-y-3">

      {/* Filters */}
      <form method="GET" action={Routes.support} className="flex flex-wrap gap-2">
        <input
          name="search"
          defaultValue={search}
          placeholder="Search cases…"
          className="flex-1 min-w-40 text-sm border border-gray-300 rounded-md px-3 py-1.5 focus:outline-none focus:ring-2 focus:ring-indigo-500"
        />

        {/* Tenant filter — only shown when viewing all tenants */}
        {tenantOptions.length > 0 && (
          <select
            name="tenantId"
            defaultValue={tenantId}
            className="text-sm border border-gray-300 rounded-md px-2 py-1.5 focus:outline-none focus:ring-2 focus:ring-indigo-500"
          >
            <option value="">All Tenants</option>
            {tenantOptions.map(t => (
              <option key={t.id} value={t.id}>{t.name}</option>
            ))}
          </select>
        )}

        <select
          name="status"
          defaultValue={status}
          className="text-sm border border-gray-300 rounded-md px-2 py-1.5 focus:outline-none focus:ring-2 focus:ring-indigo-500"
        >
          <option value="">All Statuses</option>
          {ALL_STATUSES.map(s => <option key={s} value={s}>{s}</option>)}
        </select>
        <select
          name="priority"
          defaultValue={priority}
          className="text-sm border border-gray-300 rounded-md px-2 py-1.5 focus:outline-none focus:ring-2 focus:ring-indigo-500"
        >
          <option value="">All Priorities</option>
          {ALL_PRIORITIES.map(p => <option key={p} value={p}>{p}</option>)}
        </select>
        <button
          type="submit"
          className="px-4 py-1.5 text-sm font-medium bg-indigo-600 text-white rounded-md hover:bg-indigo-700 transition-colors"
        >
          Filter
        </button>
        {hasFilters && (
          <Link
            href={Routes.support}
            className="px-3 py-1.5 text-sm text-gray-500 hover:text-gray-700 rounded-md border border-gray-200 hover:border-gray-300 transition-colors"
          >
            Clear
          </Link>
        )}
      </form>

      {/* Table card */}
      <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">

        {cases.length === 0 ? (
          <div className="px-6 py-10 text-center text-sm text-gray-400">
            No cases match the current filters.
          </div>
        ) : (
          <>
            <table className="w-full text-sm">
              <thead>
                <tr className="bg-gray-50 border-b border-gray-100 text-xs font-semibold text-gray-500 uppercase tracking-wide">
                  <th className="text-left px-4 py-3">Case</th>
                  <th className="text-left px-4 py-3 hidden lg:table-cell">Category</th>
                  <th className="text-left px-4 py-3">Priority</th>
                  <th className="text-left px-4 py-3">Status</th>
                  <th className="text-left px-4 py-3 hidden sm:table-cell">Updated</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {cases.map(c => {
                  const resolvedTenantName = (c.tenantId && tenantMap[c.tenantId])
                    ? tenantMap[c.tenantId]
                    : (c.tenantName || null);
                  const tagId = c.tenantId || c.tenantName;

                  return (
                    <tr key={c.id} className="hover:bg-gray-50 transition-colors">
                      <td className="px-4 py-3">
                        <div className="flex flex-col gap-1.5">
                          <Link
                            href={`${Routes.support}/${c.id}`}
                            className="font-medium text-indigo-700 hover:text-indigo-900 hover:underline leading-snug"
                          >
                            {c.title}
                          </Link>
                          <div className="flex items-center gap-2 flex-wrap">
                            {/* Tenant tag */}
                            {resolvedTenantName && tagId && (
                              <span
                                className={`inline-flex items-center gap-1 px-1.5 py-0.5 rounded text-[10px] font-semibold border ${tenantTagColor(tagId)}`}
                              >
                                <svg className="w-2.5 h-2.5 shrink-0" viewBox="0 0 16 16" fill="currentColor">
                                  <path d="M2 3a1 1 0 0 1 1-1h4.586a1 1 0 0 1 .707.293l5.414 5.414a1 1 0 0 1 0 1.414l-4.586 4.586a1 1 0 0 1-1.414 0L2.293 8.707A1 1 0 0 1 2 8V3Zm2 1a1 1 0 1 0 0 2 1 1 0 0 0 0-2Z"/>
                                </svg>
                                {resolvedTenantName}
                              </span>
                            )}
                            {/* Requester identity */}
                            {(c.userName || c.requesterEmail) && (
                              <span className="inline-flex items-center gap-1 text-[11px] text-gray-500">
                                <svg className="w-3 h-3 shrink-0 text-gray-400" viewBox="0 0 16 16" fill="currentColor">
                                  <path d="M8 8a3 3 0 1 0 0-6 3 3 0 0 0 0 6Zm-5 6a5 5 0 0 1 10 0H3Z"/>
                                </svg>
                                {c.userName && c.requesterEmail
                                  ? <><span className="font-medium text-gray-700">{c.userName}</span><span className="text-gray-400">·</span><span className="text-gray-400">{c.requesterEmail}</span></>
                                  : c.userName
                                    ? <span className="font-medium text-gray-700">{c.userName}</span>
                                    : <span className="text-gray-400">{c.requesterEmail}</span>
                                }
                              </span>
                            )}
                          </div>
                        </div>
                      </td>
                      <td className="px-4 py-3 text-gray-500 hidden lg:table-cell">
                        {c.category}
                      </td>
                      <td className="px-4 py-3">
                        <span className={`inline-flex items-center px-2 py-0.5 rounded text-[11px] font-semibold border ${PRIORITY_STYLES[c.priority]}`}>
                          {c.priority}
                        </span>
                      </td>
                      <td className="px-4 py-3">
                        <span className={`inline-flex items-center px-2 py-0.5 rounded text-[11px] font-semibold border ${STATUS_STYLES[c.status]}`}>
                          {c.status}
                        </span>
                      </td>
                      <td className="px-4 py-3 text-xs text-gray-400 hidden sm:table-cell tabular-nums">
                        {formatRelative(c.updatedAtUtc)}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>

            {/* Pagination */}
            {totalPages > 1 && (
              <div className="px-4 py-3 border-t border-gray-100 flex items-center justify-between bg-gray-50">
                <span className="text-xs text-gray-500 tabular-nums">
                  {start}–{end} of {totalCount}
                </span>
                <div className="flex gap-1">
                  {page > 1 && (
                    <Link
                      href={pageHref(page - 1)}
                      className="px-3 py-1 text-xs font-medium rounded border border-gray-300 bg-white text-gray-600 hover:bg-gray-50"
                    >
                      ← Prev
                    </Link>
                  )}
                  {page < totalPages && (
                    <Link
                      href={pageHref(page + 1)}
                      className="px-3 py-1 text-xs font-medium rounded border border-gray-300 bg-white text-gray-600 hover:bg-gray-50"
                    >
                      Next →
                    </Link>
                  )}
                </div>
              </div>
            )}
          </>
        )}
      </div>

      {/* Result count */}
      {totalCount > 0 && (
        <p className="text-xs text-gray-400 tabular-nums px-1">
          {totalCount} case{totalCount !== 1 ? 's' : ''} total
        </p>
      )}
    </div>
  );
}

// ── helpers ───────────────────────────────────────────────────────────────────

function formatRelative(iso: string): string {
  try {
    const diffMs  = Date.now() - new Date(iso).getTime();
    const minutes = Math.floor(diffMs / 60_000);
    if (minutes < 1)  return 'just now';
    if (minutes < 60) return `${minutes}m ago`;
    const hours = Math.floor(minutes / 60);
    if (hours < 24)   return `${hours}h ago`;
    return `${Math.floor(hours / 24)}d ago`;
  } catch {
    return iso;
  }
}
