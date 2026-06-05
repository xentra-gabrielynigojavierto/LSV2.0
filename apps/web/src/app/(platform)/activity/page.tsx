import { requireOrg }    from '@/lib/auth-guards';
import { serverApi }     from '@/lib/server-api-client';

export const dynamic = 'force-dynamic';


const PAGE_SIZE = 20;

interface SearchParams {
  eventType?: string;
  category?:  string;
  severity?:  string;
  actorId?:   string;
  dateFrom?:  string;
  dateTo?:    string;
  page?:      string;
}

interface PageProps {
  searchParams: Promise<SearchParams>;
}

// ── Wire types matching the actual audit service JSON shape ────────────────────

interface AuditActor {
  id?:        string;
  name?:      string;
  type?:      string;
  ipAddress?: string;
}

interface AuditEntity {
  type?: string;
  id?:   string;
}

interface AuditScope {
  tenantId?:      string;
  organizationId?: string;
  userScopeId?:   string;
}

interface AuditEventRecord {
  auditId:        string;
  eventType:      string;
  eventCategory:  string;
  severity:       string;
  sourceSystem:   string;
  action:         string;
  description:    string;
  actor:          AuditActor;
  entity?:        AuditEntity;
  scope:          AuditScope;
  occurredAtUtc:  string;
  recordedAtUtc?: string;
}

// The audit service wraps everything in ApiResponse<T>
interface AuditQueryData {
  items:      AuditEventRecord[];
  totalCount: number;
  page:       number;
  pageSize:   number;
}

interface AuditApiResponse {
  success: boolean;
  data:    AuditQueryData;
  traceId?: string;
}

const CATEGORY_TABS = [
  { label: 'All',      value: '',               title: 'All event types' },
  { label: 'Access',   value: 'Security',        title: 'Login, logout, and role events' },
  { label: 'Admin',    value: 'Administrative',  title: 'User and configuration changes' },
  { label: 'Clinical', value: 'Business',        title: 'Referrals and appointments' },
];

/**
 * /activity — Tenant portal: activity & audit log viewer.
 *
 * Access: authenticated org member (requireOrg guard).
 * Scope: events scoped to the authenticated user's tenantId only.
 */
export default async function ActivityPage({ searchParams }: PageProps) {
  const searchParamsData = await searchParams;
  const session = await requireOrg();

  const category  = searchParamsData.category  || undefined;
  const eventType = searchParamsData.eventType?.trim() || undefined;
  const severity  = searchParamsData.severity  || undefined;
  const dateFrom  = searchParamsData.dateFrom  || undefined;
  const dateTo    = searchParamsData.dateTo    || undefined;
  const page      = Math.max(1, parseInt(searchParamsData.page ?? '1', 10));

  const rawActorId = searchParamsData.actorId || undefined;
  const actorId    = rawActorId === 'me' ? session.userId : rawActorId;
  const isMyView   = rawActorId === 'me' || actorId === session.userId;

  const qs = buildQs({
    tenantId:  session.tenantId,
    eventType,
    category,
    severity,
    actorId,
    dateFrom,
    dateTo,
    page,
    pageSize:  PAGE_SIZE,
  });

  let result: AuditApiResponse | null = null;
  let fetchError: string | null = null;

  try {
    result = await serverApi.get<AuditApiResponse>(`/audit-service/audit/events${qs}`);
  } catch (err: unknown) {
    fetchError = err instanceof Error ? err.message : 'Unable to load activity log.';
  }

  const items      = result?.data?.items ?? [];
  const totalCount = result?.data?.totalCount ?? 0;
  const totalPages = Math.max(1, Math.ceil(totalCount / PAGE_SIZE));
  const startItem  = totalCount === 0 ? 0 : (page - 1) * PAGE_SIZE + 1;
  const endItem    = Math.min(page * PAGE_SIZE, totalCount);
  const hasFilters = !!(eventType || category || severity || actorId || dateFrom || dateTo);

  function hrefFor(overrides: Record<string, string | number | undefined>) {
    const params = new URLSearchParams();
    const vals = { eventType, category, severity, actorId: rawActorId, dateFrom, dateTo, ...overrides };
    for (const [k, v] of Object.entries(vals)) {
      if (v !== undefined && v !== '' && !(k === 'page' && Number(v) <= 1)) {
        params.set(k, String(v));
      }
    }
    const q = params.toString();
    return `/activity${q ? `?${q}` : ''}`;
  }

  function tabHref(cat: string) { return hrefFor({ category: cat || undefined, page: 1 }); }
  function pageHref(p: number)   { return hrefFor({ page: p }); }

  const myActivityHref = isMyView ? '/activity' : hrefFor({ actorId: 'me', page: 1 });

  return (
    <div className="min-h-screen bg-gray-50">
      <div className="max-w-6xl mx-auto px-4 py-8 space-y-6">

        {/* ── Page header ───────────────────────────────────────────────── */}
        <div className="flex items-start justify-between gap-4">
          <div>
            <h1 className="text-xl font-semibold text-gray-900">Activity Log</h1>
            <p className="text-sm text-gray-500 mt-0.5">
              A read-only record of all platform events for your organisation.
            </p>
          </div>
          <a
            href={myActivityHref}
            className={[
              'inline-flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium rounded-md border transition-colors whitespace-nowrap',
              isMyView
                ? 'bg-indigo-600 text-white border-indigo-600 hover:bg-indigo-700'
                : 'bg-white text-gray-600 border-gray-300 hover:bg-gray-50',
            ].join(' ')}
          >
            <span>{isMyView ? '← All Users' : 'My Activity'}</span>
          </a>
        </div>

        {/* ── Category tabs ──────────────────────────────────────────────── */}
        <div className="flex gap-1 bg-gray-100 p-1 rounded-lg w-fit">
          {CATEGORY_TABS.map((tab) => (
            <a
              key={tab.value}
              href={tabHref(tab.value)}
              title={tab.title}
              className={[
                'px-3 py-1.5 rounded-md text-xs font-medium transition-colors whitespace-nowrap',
                (category ?? '') === tab.value
                  ? 'bg-white text-gray-900 shadow-sm'
                  : 'text-gray-600 hover:text-gray-900 hover:bg-gray-50',
              ].join(' ')}
            >
              {tab.label}
            </a>
          ))}
        </div>

        {/* ── Filter bar ────────────────────────────────────────────────── */}
        <form method="GET" action="/activity" className="flex flex-wrap items-end gap-3 bg-white border border-gray-200 rounded-lg px-4 py-3">
          {category && <input type="hidden" name="category" value={category} />}

          <div className="flex-1 min-w-40">
            <label htmlFor="eventType" className="block text-xs font-medium text-gray-600 mb-1">Event Type</label>
            <input
              id="eventType" name="eventType" type="search"
              defaultValue={eventType}
              placeholder="e.g. identity.user.login.succeeded"
              className="w-full h-9 rounded-md border border-gray-300 px-3 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
            />
          </div>

          <div className="flex-1 min-w-32">
            <label htmlFor="actorId" className="block text-xs font-medium text-gray-600 mb-1">Actor (user ID)</label>
            <input
              id="actorId" name="actorId" type="search"
              defaultValue={rawActorId === 'me' ? session.userId : rawActorId}
              placeholder="Filter by user UUID"
              className="w-full h-9 rounded-md border border-gray-300 px-3 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
            />
          </div>

          <div className="w-28">
            <label htmlFor="severity" className="block text-xs font-medium text-gray-600 mb-1">Severity</label>
            <select id="severity" name="severity" defaultValue={severity ?? ''}
              className="w-full h-9 rounded-md border border-gray-300 px-3 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-indigo-500">
              <option value="">All</option>
              <option value="Info">Info</option>
              <option value="Warn">Warn</option>
              <option value="Error">Error</option>
              <option value="Critical">Critical</option>
            </select>
          </div>

          <div className="w-36">
            <label htmlFor="dateFrom" className="block text-xs font-medium text-gray-600 mb-1">From</label>
            <input id="dateFrom" name="dateFrom" type="date" defaultValue={dateFrom ?? ''}
              className="w-full h-9 rounded-md border border-gray-300 px-3 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500" />
          </div>

          <div className="w-36">
            <label htmlFor="dateTo" className="block text-xs font-medium text-gray-600 mb-1">To</label>
            <input id="dateTo" name="dateTo" type="date" defaultValue={dateTo ?? ''}
              className="w-full h-9 rounded-md border border-gray-300 px-3 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500" />
          </div>

          <div className="flex items-center gap-2 pb-0.5">
            <button type="submit"
              className="h-9 px-4 text-sm font-medium text-white bg-indigo-600 hover:bg-indigo-700 rounded-md transition-colors">
              Filter
            </button>
            {hasFilters && (
              <a href="/activity"
                className="inline-flex items-center h-9 px-3 text-sm font-medium text-gray-600 hover:text-gray-900 bg-white border border-gray-300 rounded-md transition-colors">
                Clear
              </a>
            )}
          </div>
        </form>

        {/* ── Active filter chips ──────────────────────────────────────── */}
        {hasFilters && (
          <div className="flex items-center flex-wrap gap-2 text-sm text-gray-500">
            <span>Filters:</span>
            {isMyView     && <Chip label="My Activity" />}
            {actorId && !isMyView && <Chip label={`actor: ${actorId}`} />}
            {eventType    && <Chip label={`event: ${eventType}`} />}
            {category     && <Chip label={`category: ${category}`} />}
            {severity     && <Chip label={`severity: ${severity}`} />}
            {dateFrom     && <Chip label={`from: ${dateFrom}`} />}
            {dateTo       && <Chip label={`to: ${dateTo}`} />}
          </div>
        )}

        {/* ── Error banner ──────────────────────────────────────────────── */}
        {fetchError && (
          <div className="rounded-lg border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-800">
            <strong className="font-semibold">Activity log unavailable.</strong>{' '}
            The platform audit service could not be reached. Please try again or contact support if the issue persists.
          </div>
        )}

        {/* ── Result count ──────────────────────────────────────────────── */}
        {!fetchError && result && (
          <div className="flex items-center justify-between text-xs text-gray-400">
            <span>
              {totalCount === 0
                ? 'No events found'
                : `Showing ${startItem}–${endItem} of ${totalCount.toLocaleString()} event${totalCount !== 1 ? 's' : ''}`}
            </span>
            {totalPages > 1 && <span>Page {page} of {totalPages}</span>}
          </div>
        )}

        {/* ── Timeline table ─────────────────────────────────────────────── */}
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
                  <th className="px-4 py-3 text-left font-medium whitespace-nowrap">IP Address</th>
                  <th className="px-4 py-3 text-left font-medium whitespace-nowrap">Target</th>
                  <th className="px-4 py-3 text-left font-medium whitespace-nowrap">Description</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {items.map((e) => (
                  <tr key={e.auditId} className="hover:bg-gray-50 transition-colors">

                    <td className="px-4 py-2.5 text-gray-500 whitespace-nowrap font-mono text-[11px]">
                      {e.occurredAtUtc ? formatUtc(e.occurredAtUtc) : '—'}
                    </td>

                    <td className="px-4 py-2.5 text-gray-700 font-mono text-[11px] whitespace-nowrap">
                      {e.eventType ?? '—'}
                    </td>

                    <td className="px-4 py-2.5">
                      {e.eventCategory
                        ? <CategoryBadge value={e.eventCategory} />
                        : <span className="text-gray-300 text-[11px]">—</span>}
                    </td>

                    <td className="px-4 py-2.5">
                      {e.severity
                        ? <SeverityBadge value={e.severity} />
                        : <span className="text-gray-300 text-[11px]">—</span>}
                    </td>

                    <td className="px-4 py-2.5 text-gray-700 whitespace-nowrap">
                      <span className="block text-xs font-medium">
                        {e.actor?.name ?? <span className="text-gray-400 italic text-[11px]">system</span>}
                      </span>
                      {e.actor?.id && (
                        <a
                          href={`/activity?actorId=${encodeURIComponent(e.actor.id)}`}
                          className="block text-[10px] text-indigo-500 hover:text-indigo-700 hover:underline font-mono"
                        >
                          {e.actor.id.slice(0, 14)}…
                        </a>
                      )}
                    </td>

                    <td className="px-4 py-2.5 whitespace-nowrap">
                      {e.actor?.ipAddress
                        ? (
                          <span className="font-mono text-[11px] text-gray-600 bg-gray-50 border border-gray-200 rounded px-1.5 py-0.5">
                            {e.actor.ipAddress}
                          </span>
                        )
                        : <span className="text-gray-300 text-[11px]">—</span>
                      }
                    </td>

                    <td className="px-4 py-2.5 text-gray-600 whitespace-nowrap text-xs">
                      {e.entity?.type && (
                        <span className="mr-1 text-[10px] font-semibold text-gray-400 uppercase">{e.entity.type}</span>
                      )}
                      {e.entity?.id && (
                        <span className="font-mono text-[11px]">{e.entity.id}</span>
                      )}
                      {!e.entity?.type && !e.entity?.id && (
                        <span className="text-gray-300 italic text-[11px]">—</span>
                      )}
                    </td>

                    <td className="px-4 py-2.5 text-gray-500 text-xs max-w-xs truncate">
                      {e.description ?? ''}
                    </td>

                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

        {/* ── Empty state ───────────────────────────────────────────────── */}
        {!fetchError && result && items.length === 0 && (
          <div className="rounded-lg border border-gray-200 bg-white px-6 py-12 text-center">
            <p className="text-sm text-gray-400">
              {hasFilters ? 'No events match the current filters.' : 'No activity recorded yet for your organisation.'}
            </p>
          </div>
        )}

        {/* ── Pagination ────────────────────────────────────────────────── */}
        {!fetchError && totalPages > 1 && (
          <nav className="flex items-center justify-center gap-1" aria-label="Pagination">
            {page > 1 ? (
              <a href={pageHref(page - 1)} className="px-3 py-1.5 text-xs rounded-md font-medium text-gray-600 hover:bg-gray-100">← Prev</a>
            ) : (
              <span className="px-3 py-1.5 text-xs rounded-md text-gray-300 cursor-not-allowed">← Prev</span>
            )}
            {buildPageRange(page, totalPages).map((p, i) =>
              p === '…' ? (
                <span key={`e-${i}`} className="px-2 py-1 text-xs text-gray-400">…</span>
              ) : (
                <a
                  key={p}
                  href={pageHref(p)}
                  className={[
                    'px-3 py-1.5 text-xs rounded-md font-medium transition-colors',
                    p === page ? 'bg-indigo-600 text-white' : 'text-gray-600 hover:bg-gray-100',
                  ].join(' ')}
                >
                  {p}
                </a>
              ),
            )}
            {page < totalPages ? (
              <a href={pageHref(page + 1)} className="px-3 py-1.5 text-xs rounded-md font-medium text-gray-600 hover:bg-gray-100">Next →</a>
            ) : (
              <span className="px-3 py-1.5 text-xs rounded-md text-gray-300 cursor-not-allowed">Next →</span>
            )}
          </nav>
        )}

      </div>
    </div>
  );
}

// ── Helpers ────────────────────────────────────────────────────────────────────

function buildQs(params: Record<string, string | number | undefined>): string {
  const entries = Object.entries(params).filter(([, v]) => v !== undefined && v !== '');
  if (entries.length === 0) return '';
  return '?' + entries.map(([k, v]) => `${encodeURIComponent(k)}=${encodeURIComponent(String(v))}`).join('&');
}

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

// ── Badge components ───────────────────────────────────────────────────────────

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
    access:         'bg-orange-50 text-orange-700 border border-orange-200',
    business:       'bg-green-50  text-green-700  border border-green-200',
    administrative: 'bg-gray-100  text-gray-600   border border-gray-200',
    compliance:     'bg-purple-50 text-purple-700  border border-purple-200',
    datachange:     'bg-blue-50   text-blue-700    border border-blue-200',
  };
  const key = value.toLowerCase().replace(/\s+/g, '');
  const cls = MAP[key] ?? 'bg-gray-100 text-gray-500 border border-gray-200';
  return (
    <span className={`inline-flex items-center px-2 py-0.5 rounded-full text-[10px] font-medium capitalize ${cls}`}>
      {value}
    </span>
  );
}

function Chip({ label }: { label: string }) {
  return (
    <span className="inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium bg-indigo-50 text-indigo-700 border border-indigo-200">
      {label}
    </span>
  );
}
