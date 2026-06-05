import { isRedirectError }        from 'next/dist/client/components/redirect-error';
import { requirePlatformAdmin }   from '@/lib/auth-guards';
import { getTenantContext }       from '@/lib/auth';
import { controlCenterServerApi } from '@/lib/control-center-api';
import { CCShell }                from '@/components/shell/cc-shell';

export const dynamic = 'force-dynamic';

interface Props {
  searchParams: Promise<{
    actorId?:   string;
    category?:  string;
    dateFrom?:  string;
    dateTo?:    string;
    page?:      string;
  }>;
}

const PAGE_SIZE = 20;

/**
 * ACCESS EVENTS  (login/logout/role/impersonation)
 * ADMIN EVENTS   (deactivate/entitlement/org-relationship)
 * All categories with no pre-filter
 */
const CATEGORY_TABS = [
  { label: 'All Events',     value: '',               icon: 'ri-list-check' },
  { label: 'Access',         value: 'Security',       icon: 'ri-login-circle-line' },
  { label: 'Admin Actions',  value: 'Administrative', icon: 'ri-admin-line' },
  { label: 'Clinical',       value: 'Business',       icon: 'ri-heart-pulse-line' },
];

/**
 * /synqaudit/user-activity — Pre-filtered user access and activity report.
 *
 * Shows who did what and when across the platform. The category tab row
 * lets admins pivot between Access events (login/logout/impersonation),
 * Administrative events (role changes, entitlement updates), and all events.
 * The actor filter narrows to a specific user's trail.
 */
export default async function UserActivityPage({ searchParams }: Props) {
  const searchParamsData = await searchParams;
  const session   = await requirePlatformAdmin();
  const tenantCtx = await getTenantContext();

  const actorId  = searchParamsData.actorId  ?? '';
  const category = searchParamsData.category ?? '';
  const dateFrom = searchParamsData.dateFrom ?? '';
  const dateTo   = searchParamsData.dateTo   ?? '';
  const page     = Math.max(1, parseInt(searchParamsData.page ?? '1', 10));

  let items:      Awaited<ReturnType<typeof controlCenterServerApi.auditCanonical.list>>['items'] = [];
  let totalCount  = 0;
  let fetchError: string | null = null;

  try {
    const result = await controlCenterServerApi.auditCanonical.list({
      page,
      pageSize:  PAGE_SIZE,
      tenantId:  tenantCtx?.tenantId,
      category:  category  || undefined,
      actorId:   actorId   || undefined,
      dateFrom:  dateFrom  || undefined,
      dateTo:    dateTo    || undefined,
    });
    items      = result.items;
    totalCount = result.totalCount;
  } catch (err) {
    if (isRedirectError(err)) throw err;
    fetchError = err instanceof Error ? err.message : 'Failed to load user activity.';
  }

  const totalPages = Math.max(1, Math.ceil(totalCount / PAGE_SIZE));
  const startItem  = totalCount === 0 ? 0 : (page - 1) * PAGE_SIZE + 1;
  const endItem    = Math.min(page * PAGE_SIZE, totalCount);
  const hasFilters = !!(actorId || category || dateFrom || dateTo);

  function hrefFor(overrides: Record<string, string | number | undefined>) {
    const params = new URLSearchParams();
    const vals = { actorId, category, dateFrom, dateTo, ...overrides };
    for (const [k, v] of Object.entries(vals)) {
      if (v && String(v) !== '1') params.set(k, String(v));
      else if (k === 'page' && v && Number(v) > 1) params.set(k, String(v));
    }
    const q = params.toString();
    return `/synqaudit/user-activity${q ? `?${q}` : ''}`;
  }

  function tabHref(cat: string) {
    return hrefFor({ category: cat, page: 1 });
  }

  function pageHref(p: number) {
    return hrefFor({ page: p });
  }

  return (
    <CCShell userEmail={session.email}>
      <div className="space-y-5">

        {/* Header */}
        <div className="flex items-start justify-between gap-4">
          <div>
            <h1 className="text-xl font-semibold text-gray-900">User Activity</h1>
            <p className="text-sm text-gray-500 mt-0.5">
              Platform-wide access log — who did what, and when.
              {tenantCtx && (
                <span className="ml-2 inline-flex items-center px-2 py-0.5 rounded-full text-[10px] font-semibold bg-indigo-50 text-indigo-700 border border-indigo-200 uppercase tracking-wide">
                  Tenant: {tenantCtx.tenantName}
                </span>
              )}
            </p>
          </div>

          <a
            href="/synqaudit/investigation"
            className="inline-flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium text-gray-600 bg-white border border-gray-300 rounded-md hover:bg-gray-50 transition-colors"
          >
            <i className="ri-search-eye-line" />
            Full Investigation
          </a>
        </div>

        {/* Category tabs */}
        <div className="flex gap-1 bg-gray-100 p-1 rounded-lg w-fit">
          {CATEGORY_TABS.map((tab) => (
            <a
              key={tab.value}
              href={tabHref(tab.value)}
              className={[
                'inline-flex items-center gap-1.5 px-3 py-1.5 rounded-md text-xs font-medium transition-colors whitespace-nowrap',
                category === tab.value
                  ? 'bg-white text-gray-900 shadow-sm'
                  : 'text-gray-600 hover:text-gray-900 hover:bg-gray-50',
              ].join(' ')}
            >
              <i className={tab.icon} />
              {tab.label}
            </a>
          ))}
        </div>

        {/* Actor + date filter bar */}
        <form
          method="GET"
          action="/synqaudit/user-activity"
          className="flex flex-wrap items-end gap-3 bg-white border border-gray-200 rounded-lg px-4 py-3"
        >
          {/* Preserve category tab */}
          {category && <input type="hidden" name="category" value={category} />}

          <div className="flex-1 min-w-48">
            <label htmlFor="actorId" className="block text-xs font-medium text-gray-600 mb-1">
              Actor ID (filter by user)
            </label>
            <input
              id="actorId"
              name="actorId"
              type="search"
              defaultValue={actorId}
              placeholder="User UUID or partial ID"
              className="w-full h-9 rounded-md border border-gray-300 px-3 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
            />
          </div>

          <div className="w-36">
            <label htmlFor="dateFrom" className="block text-xs font-medium text-gray-600 mb-1">From</label>
            <input id="dateFrom" name="dateFrom" type="date" defaultValue={dateFrom}
              className="w-full h-9 rounded-md border border-gray-300 px-3 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500" />
          </div>

          <div className="w-36">
            <label htmlFor="dateTo" className="block text-xs font-medium text-gray-600 mb-1">To</label>
            <input id="dateTo" name="dateTo" type="date" defaultValue={dateTo}
              className="w-full h-9 rounded-md border border-gray-300 px-3 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500" />
          </div>

          <div className="flex items-center gap-2 pb-0.5">
            <button type="submit"
              className="h-9 px-4 text-sm font-medium text-white bg-indigo-600 hover:bg-indigo-700 rounded-md transition-colors">
              Apply
            </button>
            {hasFilters && (
              <a href="/synqaudit/user-activity"
                className="inline-flex items-center h-9 px-3 text-sm font-medium text-gray-600 hover:text-gray-900 bg-white border border-gray-300 rounded-md transition-colors">
                Clear
              </a>
            )}
          </div>
        </form>

        {/* Active filter chips */}
        {hasFilters && (
          <div className="flex items-center flex-wrap gap-2 text-sm text-gray-500">
            <span>Filters:</span>
            {actorId  && <FilterChip label={`actor: ${actorId}`} />}
            {category && <FilterChip label={`category: ${category}`} />}
            {dateFrom && <FilterChip label={`from: ${dateFrom}`} />}
            {dateTo   && <FilterChip label={`to: ${dateTo}`} />}
          </div>
        )}

        {/* Error banner */}
        {fetchError && (
          <div className="rounded-lg border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-800">
            <strong className="font-semibold">Activity feed unavailable.</strong>{' '}
            {fetchError}
          </div>
        )}

        {/* Result count */}
        {!fetchError && (
          <div className="flex items-center justify-between text-xs text-gray-400">
            <span>
              {totalCount === 0
                ? 'No events found'
                : `Showing ${startItem}–${endItem} of ${totalCount.toLocaleString()} event${totalCount !== 1 ? 's' : ''}`}
            </span>
            {totalPages > 1 && <span>Page {page} of {totalPages}</span>}
          </div>
        )}

        {/* Activity table */}
        {!fetchError && items.length > 0 && (
          <div className="overflow-x-auto rounded-lg border border-gray-200 bg-white">
            <table className="min-w-full divide-y divide-gray-100 text-sm">
              <thead className="bg-gray-50 text-xs text-gray-500 uppercase tracking-wide">
                <tr>
                  <th className="px-4 py-3 text-left font-medium whitespace-nowrap">Time (UTC)</th>
                  <th className="px-4 py-3 text-left font-medium whitespace-nowrap">Event</th>
                  <th className="px-4 py-3 text-left font-medium whitespace-nowrap">Category</th>
                  <th className="px-4 py-3 text-left font-medium whitespace-nowrap">Severity</th>
                  <th className="px-4 py-3 text-left font-medium whitespace-nowrap">Actor</th>
                  <th className="px-4 py-3 text-left font-medium whitespace-nowrap">Target</th>
                  <th className="px-4 py-3 text-left font-medium whitespace-nowrap">Description</th>
                  <th className="px-4 py-3 text-left font-medium whitespace-nowrap">Actions</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {items.map((e) => (
                  <tr key={e.id} className="hover:bg-gray-50 transition-colors">
                    <td className="px-4 py-2.5 text-gray-500 whitespace-nowrap font-mono text-[11px]">
                      {formatUtc(e.occurredAtUtc)}
                    </td>
                    <td className="px-4 py-2.5">
                      <span className="font-mono text-[11px] text-gray-700">
                        {e.eventType}
                      </span>
                    </td>
                    <td className="px-4 py-2.5">
                      <CategoryBadge value={e.category} />
                    </td>
                    <td className="px-4 py-2.5">
                      <SeverityBadge value={e.severity} />
                    </td>
                    <td className="px-4 py-2.5 whitespace-nowrap">
                      <span className="block text-xs font-medium text-gray-700">
                        {e.actorLabel ?? (e.actorId ? '—' : <span className="text-gray-400 italic text-[11px]">system</span>)}
                      </span>
                      {e.actorId && (
                        <a
                          href={`/synqaudit/user-activity?actorId=${encodeURIComponent(e.actorId)}`}
                          className="block font-mono text-[10px] text-indigo-500 hover:text-indigo-700 hover:underline"
                        >
                          {e.actorId.slice(0, 16)}…
                        </a>
                      )}
                    </td>
                    <td className="px-4 py-2.5 whitespace-nowrap text-xs text-gray-600">
                      {e.targetType && (
                        <span className="mr-1 text-[10px] font-semibold text-gray-400 uppercase">{e.targetType}</span>
                      )}
                      {e.targetId && (
                        <span className="font-mono text-[11px]">{e.targetId.slice(0, 14)}…</span>
                      )}
                      {!e.targetType && !e.targetId && (
                        <span className="text-gray-300 italic text-[11px]">—</span>
                      )}
                    </td>
                    <td className="px-4 py-2.5 text-gray-500 text-xs max-w-xs truncate">
                      {e.description}
                    </td>
                    <td className="px-4 py-2.5 whitespace-nowrap">
                      <a
                        href={`/synqaudit/investigation?search=${encodeURIComponent(e.id)}`}
                        className="text-[11px] font-medium text-indigo-600 hover:text-indigo-800 hover:underline"
                      >
                        Trace
                      </a>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

        {/* Empty state */}
        {!fetchError && items.length === 0 && (
          <div className="rounded-lg border border-gray-200 bg-white px-6 py-12 text-center">
            <i className="ri-user-heart-line text-3xl text-gray-300 block mb-2" />
            <p className="text-sm text-gray-400">
              {hasFilters ? 'No events match the current filters.' : 'No user activity recorded yet.'}
            </p>
          </div>
        )}

        {/* Pagination */}
        {!fetchError && totalPages > 1 && (
          <nav className="flex items-center justify-center gap-1" aria-label="Pagination">
            {page > 1
              ? <a href={pageHref(page - 1)} className="px-3 py-1.5 text-xs rounded-md font-medium text-gray-600 hover:bg-gray-100">← Prev</a>
              : <span className="px-3 py-1.5 text-xs rounded-md text-gray-300 cursor-not-allowed">← Prev</span>
            }
            {buildPageRange(page, totalPages).map((p, i) =>
              p === '…' ? (
                <span key={`e-${i}`} className="px-2 py-1 text-xs text-gray-400">…</span>
              ) : (
                <a key={p} href={pageHref(p)}
                  className={[
                    'px-3 py-1.5 text-xs rounded-md font-medium transition-colors',
                    p === page ? 'bg-indigo-600 text-white' : 'text-gray-600 hover:bg-gray-100',
                  ].join(' ')}
                >
                  {p}
                </a>
              ),
            )}
            {page < totalPages
              ? <a href={pageHref(page + 1)} className="px-3 py-1.5 text-xs rounded-md font-medium text-gray-600 hover:bg-gray-100">Next →</a>
              : <span className="px-3 py-1.5 text-xs rounded-md text-gray-300 cursor-not-allowed">Next →</span>
            }
          </nav>
        )}

      </div>
    </CCShell>
  );
}

// ── Helpers ───────────────────────────────────────────────────────────────────

function buildPageRange(current: number, total: number): (number | '…')[] {
  if (total <= 7) return Array.from({ length: total }, (_, i) => i + 1);
  const pages: (number | '…')[] = [1];
  if (current > 3)         pages.push('…');
  for (let p = Math.max(2, current - 1); p <= Math.min(total - 1, current + 1); p++) pages.push(p);
  if (current < total - 2) pages.push('…');
  pages.push(total);
  return pages;
}

function formatUtc(iso: string): string {
  try {
    const d = new Date(iso);
    const pad = (n: number) => String(n).padStart(2, '0');
    return `${d.getUTCFullYear()}-${pad(d.getUTCMonth() + 1)}-${pad(d.getUTCDate())} `
         + `${pad(d.getUTCHours())}:${pad(d.getUTCMinutes())}:${pad(d.getUTCSeconds())}`;
  } catch { return iso; }
}

// ── Badge components ──────────────────────────────────────────────────────────

function SeverityBadge({ value }: { value: string }) {
  const MAP: Record<string, string> = {
    info:     'bg-blue-50  text-blue-700  border border-blue-200',
    warn:     'bg-amber-50 text-amber-700 border border-amber-300',
    error:    'bg-red-50   text-red-700   border border-red-200',
    critical: 'bg-red-100  text-red-800   border border-red-400 font-bold',
  };
  const cls = MAP[value.toLowerCase()] ?? 'bg-gray-100 text-gray-500 border border-gray-200';
  return (
    <span className={`inline-flex items-center px-2 py-0.5 rounded-full text-[10px] font-semibold uppercase tracking-wide ${cls}`}>
      {value}
    </span>
  );
}

function CategoryBadge({ value }: { value: string }) {
  const MAP: Record<string, string> = {
    security:       'bg-red-50    text-red-700   border border-red-200',
    administrative: 'bg-gray-100  text-gray-600  border border-gray-200',
    business:       'bg-green-50  text-green-700 border border-green-200',
    compliance:     'bg-purple-50 text-purple-700 border border-purple-200',
  };
  const cls = MAP[value.toLowerCase()] ?? 'bg-gray-100 text-gray-500 border border-gray-200';
  return (
    <span className={`inline-flex items-center px-2 py-0.5 rounded-full text-[10px] font-medium capitalize ${cls}`}>
      {value}
    </span>
  );
}

function FilterChip({ label }: { label: string }) {
  return (
    <span className="inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium bg-indigo-50 text-indigo-700 border border-indigo-200">
      {label}
    </span>
  );
}
