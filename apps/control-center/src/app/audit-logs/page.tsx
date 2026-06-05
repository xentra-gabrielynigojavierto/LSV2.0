import { requireAdmin }              from '@/lib/auth-guards';
import { getTenantContext }           from '@/lib/auth';
import { controlCenterServerApi }     from '@/lib/control-center-api';
import { CCShell }                    from '@/components/shell/cc-shell';
import { AuditLogTable }                    from '@/components/audit-logs/audit-log-table';
import { CanonicalAuditTableInteractive }  from '@/components/audit-logs/canonical-audit-table-interactive';
import type { AuditLogEntry, CanonicalAuditEvent, AuditReadMode } from '@/types/control-center';

export const dynamic = 'force-dynamic';

interface AuditLogsPageProps {
  searchParams: Promise<{
    search?:        string;
    entityType?:    string;
    actor?:         string;
    eventType?:     string;
    severity?:      string;
    category?:      string;
    correlationId?: string;
    dateFrom?:      string;
    dateTo?:        string;
    page?:          string;
  }>;
}

const PAGE_SIZE = 15;

/**
 * Resolved from AUDIT_READ_MODE env at build/request time.
 *   legacy    → GET /identity/api/admin/audit              (default — backwards compatible)
 *   canonical → GET /audit-service/audit/events            (Platform Audit Event Service)
 *   hybrid    → canonical first, falls back to legacy on error
 */
const MODE: AuditReadMode =
  (process.env['AUDIT_READ_MODE'] as AuditReadMode | undefined) ?? 'legacy';

const MODE_LABELS: Record<AuditReadMode, string> = {
  legacy:    'Legacy (Identity DB)',
  canonical: 'Canonical (Audit Service)',
  hybrid:    'Hybrid',
};

const MODE_COLORS: Record<AuditReadMode, string> = {
  legacy:    'bg-gray-100 text-gray-600 border border-gray-200',
  canonical: 'bg-blue-100 text-blue-700 border border-blue-300',
  hybrid:    'bg-violet-100 text-violet-700 border border-violet-300',
};

/**
 * /audit-logs — System-wide audit log viewer with canonical/legacy hybrid support.
 *
 * Access: PlatformAdmin or TenantAdmin (requireAdmin). Read-only.
 * TenantAdmin: scoped to their tenant via JWT claims propagated to the audit service.
 * PlatformAdmin: can query across all tenants.
 * Filtering: server-side via URL searchParams (plain GET form, no JS required).
 */
export default async function AuditLogsPage({ searchParams }: AuditLogsPageProps) {
  const searchParamsData = await searchParams;
  const session   = await requireAdmin();
  const tenantCtx = await getTenantContext();

  const search        = searchParamsData.search        ?? '';
  const entityType    = searchParamsData.entityType    ?? '';
  const actor         = searchParamsData.actor         ?? '';
  const eventType     = searchParamsData.eventType     ?? '';
  const severity      = searchParamsData.severity      ?? '';
  const category      = searchParamsData.category      ?? '';
  const correlationId = searchParamsData.correlationId ?? '';
  const dateFrom      = searchParamsData.dateFrom      ?? '';
  const dateTo        = searchParamsData.dateTo        ?? '';
  const page          = Math.max(1, parseInt(searchParamsData.page ?? '1', 10));

  // ── Data fetching ───────────────────────────────────────────────────────────

  let legacyResult:    { items: AuditLogEntry[];       totalCount: number } | null = null;
  let canonicalResult: { items: CanonicalAuditEvent[]; totalCount: number } | null = null;
  let fetchError:      string | null = null;
  let actualMode:      AuditReadMode = MODE;
  let canonicalFallbackReason: string | null = null;

  if (MODE === 'canonical') {
    try {
      canonicalResult = await controlCenterServerApi.auditCanonical.list({
        page, pageSize: PAGE_SIZE,
        tenantId:      tenantCtx?.tenantId,
        eventType:     eventType     || undefined,
        severity:      severity      || undefined,
        category:      category      || undefined,
        actorId:       actor         || undefined,
        correlationId: correlationId || undefined,
        dateFrom:      dateFrom      || undefined,
        dateTo:        dateTo        || undefined,
        search:        search        || undefined,
      });
    } catch (err) {
      fetchError = err instanceof Error ? err.message : 'Failed to load canonical audit logs.';
    }
  } else if (MODE === 'hybrid') {
    // Try canonical first; fall back to legacy on error.
    // IMPORTANT: canonical failures are now logged and surfaced in the UI.
    // Operators must not rely on silent fallback to mask upstream problems.
    try {
      canonicalResult = await controlCenterServerApi.auditCanonical.list({
        page, pageSize: PAGE_SIZE,
        tenantId:      tenantCtx?.tenantId,
        eventType:     eventType     || undefined,
        severity:      severity      || undefined,
        category:      category      || undefined,
        actorId:       actor         || undefined,
        correlationId: correlationId || undefined,
        dateFrom:      dateFrom      || undefined,
        dateTo:        dateTo        || undefined,
        search:        search        || undefined,
      });
    } catch (canonicalErr) {
      // Log the canonical failure clearly — operators must see this in server logs.
      const reason = canonicalErr instanceof Error
        ? canonicalErr.message
        : 'Canonical audit service unavailable';
      console.error(
        `[AUDIT_HYBRID_FALLBACK] Canonical fetch failed — falling back to legacy. ` +
        `Reason: ${reason}`,
        canonicalErr,
      );

      actualMode = 'legacy';
      canonicalFallbackReason = reason;

      try {
        legacyResult = await controlCenterServerApi.audit.list({
          page, pageSize: PAGE_SIZE,
          search:     search     || undefined,
          entityType: entityType || undefined,
          actor:      actor      || undefined,
          tenantId:   tenantCtx?.tenantId,
        });
      } catch (err2) {
        fetchError = err2 instanceof Error ? err2.message : 'Failed to load audit logs.';
      }
    }
  } else {
    // legacy (default)
    try {
      legacyResult = await controlCenterServerApi.audit.list({
        page, pageSize: PAGE_SIZE,
        search:     search     || undefined,
        entityType: entityType || undefined,
        actor:      actor      || undefined,
        tenantId:   tenantCtx?.tenantId,
      });
    } catch (err) {
      fetchError = err instanceof Error ? err.message : 'Failed to load audit logs.';
    }
  }

  const totalCount = canonicalResult?.totalCount ?? legacyResult?.totalCount ?? 0;
  const totalPages = Math.max(1, Math.ceil(totalCount / PAGE_SIZE));
  const hasFilters = !!(search || entityType || actor || eventType || severity || category || correlationId || dateFrom || dateTo);
  const startItem  = totalCount === 0 ? 0 : (page - 1) * PAGE_SIZE + 1;
  const endItem    = Math.min(page * PAGE_SIZE, totalCount);
  const isCanonical = !!canonicalResult;

  // Build pagination href (preserves all active filters)
  function paginationHref(p: number) {
    const params = new URLSearchParams();
    if (search)        params.set('search',        search);
    if (entityType)    params.set('entityType',    entityType);
    if (actor)         params.set('actor',         actor);
    if (eventType)     params.set('eventType',     eventType);
    if (severity)      params.set('severity',      severity);
    if (category)      params.set('category',      category);
    if (correlationId) params.set('correlationId', correlationId);
    if (dateFrom)      params.set('dateFrom',      dateFrom);
    if (dateTo)        params.set('dateTo',        dateTo);
    if (p > 1)         params.set('page',          String(p));
    const qs = params.toString();
    return `/audit-logs${qs ? `?${qs}` : ''}`;
  }

  return (
    <CCShell userEmail={session.email}>
      <div className="space-y-4">

        {/* Page header */}
        <div>
          <div className="flex items-center gap-3 flex-wrap">
            <h1 className="text-xl font-semibold text-gray-900">Audit Logs</h1>

            {/* Source-mode badge */}
            <span className={`inline-flex items-center text-[11px] font-semibold px-2.5 py-1 rounded-full ${MODE_COLORS[actualMode]}`}>
              {MODE_LABELS[actualMode]}
            </span>

            {tenantCtx && (
              <span className="inline-flex items-center gap-1.5 px-2 py-0.5 rounded-full bg-amber-100 border border-amber-300 text-[11px] font-semibold text-amber-700">
                <span className="h-1.5 w-1.5 rounded-full bg-amber-500" />
                Scoped to {tenantCtx.tenantName}
              </span>
            )}
          </div>
          <p className="text-sm text-gray-500 mt-0.5">
            {tenantCtx
              ? `Events for ${tenantCtx.tenantName} (${tenantCtx.tenantCode})`
              : 'System-wide activity log — all platform and tenant events'}
          </p>
        </div>

        {/* Filter bar (native GET form — no JS required) */}
        <form method="GET" action="/audit-logs" className="flex flex-wrap items-end gap-3">

          <div className="flex-1 min-w-40">
            <label htmlFor="search" className="block text-xs font-medium text-gray-600 mb-1">Search</label>
            <input id="search" name="search" type="search" defaultValue={search}
              placeholder="Action, entity, actor…"
              className="w-full h-9 rounded-md border border-gray-300 px-3 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500" />
          </div>

          {/* Canonical / Hybrid filters */}
          {(MODE === 'canonical' || MODE === 'hybrid') && (
            <>
              <div className="w-40">
                <label htmlFor="eventType" className="block text-xs font-medium text-gray-600 mb-1">Event Type</label>
                <input id="eventType" name="eventType" defaultValue={eventType}
                  placeholder="user.login.succeeded"
                  className="w-full h-9 rounded-md border border-gray-300 px-3 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500" />
              </div>

              <div className="w-32">
                <label htmlFor="category" className="block text-xs font-medium text-gray-600 mb-1">Category</label>
                <select id="category" name="category" defaultValue={category}
                  className="w-full h-9 rounded-md border border-gray-300 px-3 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-indigo-500">
                  <option value="">All</option>
                  <option value="security">Security</option>
                  <option value="access">Access</option>
                  <option value="business">Business</option>
                  <option value="administrative">Administrative</option>
                  <option value="compliance">Compliance</option>
                  <option value="dataChange">Data Change</option>
                </select>
              </div>

              <div className="w-28">
                <label htmlFor="severity" className="block text-xs font-medium text-gray-600 mb-1">Severity</label>
                <select id="severity" name="severity" defaultValue={severity}
                  className="w-full h-9 rounded-md border border-gray-300 px-3 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-indigo-500">
                  <option value="">All</option>
                  <option value="info">Info</option>
                  <option value="warn">Warn</option>
                  <option value="error">Error</option>
                  <option value="critical">Critical</option>
                </select>
              </div>

              <div className="w-44">
                <label htmlFor="correlationId" className="block text-xs font-medium text-gray-600 mb-1">Correlation ID</label>
                <input id="correlationId" name="correlationId" defaultValue={correlationId}
                  placeholder="req-xxxxxxxx"
                  className="w-full h-9 rounded-md border border-gray-300 px-3 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500" />
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
            </>
          )}

          {/* Legacy-only filters */}
          {MODE === 'legacy' && (
            <>
              <div className="w-44">
                <label htmlFor="entityType" className="block text-xs font-medium text-gray-600 mb-1">Entity Type</label>
                <select id="entityType" name="entityType" defaultValue={entityType}
                  className="w-full h-9 rounded-md border border-gray-300 px-3 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-indigo-500">
                  <option value="">All types</option>
                  <option value="User">User</option>
                  <option value="Tenant">Tenant</option>
                  <option value="Entitlement">Entitlement</option>
                  <option value="Role">Role</option>
                  <option value="System">System</option>
                </select>
              </div>

              <div className="w-52">
                <label htmlFor="actor" className="block text-xs font-medium text-gray-600 mb-1">Actor</label>
                <input id="actor" name="actor" type="search" defaultValue={actor}
                  placeholder="Email or service name…"
                  className="w-full h-9 rounded-md border border-gray-300 px-3 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500" />
              </div>
            </>
          )}

          <div className="flex items-center gap-2 pb-0.5">
            <button type="submit"
              className="h-9 px-4 text-sm font-medium text-white bg-indigo-600 hover:bg-indigo-700 rounded-md transition-colors focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:ring-offset-1">
              Filter
            </button>
            {hasFilters && (
              <a href="/audit-logs"
                className="inline-flex items-center h-9 px-3 text-sm font-medium text-gray-600 hover:text-gray-900 bg-white border border-gray-300 hover:border-gray-400 rounded-md transition-colors">
                Clear
              </a>
            )}
          </div>
        </form>

        {/* Quick-filter presets for access changes */}
        {(MODE === 'canonical' || MODE === 'hybrid') && !hasFilters && (
          <div className="flex items-center gap-2 flex-wrap">
            <span className="text-xs font-medium text-gray-400 uppercase tracking-wide">Quick filters:</span>
            <a href="/audit-logs?category=access" className="inline-flex items-center px-2.5 py-1 rounded-md text-xs font-medium bg-purple-50 text-purple-700 border border-purple-200 hover:bg-purple-100 transition-colors">
              Access Changes
            </a>
            <a href="/audit-logs?category=security" className="inline-flex items-center px-2.5 py-1 rounded-md text-xs font-medium bg-red-50 text-red-700 border border-red-200 hover:bg-red-100 transition-colors">
              Security Events
            </a>
            <a href="/audit-logs?eventType=role.assigned" className="inline-flex items-center px-2.5 py-1 rounded-md text-xs font-medium bg-blue-50 text-blue-700 border border-blue-200 hover:bg-blue-100 transition-colors">
              Role Assignments
            </a>
            <a href="/audit-logs?eventType=group.membership" className="inline-flex items-center px-2.5 py-1 rounded-md text-xs font-medium bg-green-50 text-green-700 border border-green-200 hover:bg-green-100 transition-colors">
              Group Membership
            </a>
            <a href="/audit-logs?eventType=product.access" className="inline-flex items-center px-2.5 py-1 rounded-md text-xs font-medium bg-amber-50 text-amber-700 border border-amber-200 hover:bg-amber-100 transition-colors">
              Product Access
            </a>
            <a href="/audit-logs?eventType=monitoring.service.changed" className="inline-flex items-center px-2.5 py-1 rounded-md text-xs font-medium bg-indigo-50 text-indigo-700 border border-indigo-200 hover:bg-indigo-100 transition-colors">
              Monitoring Config
            </a>
          </div>
        )}

        {/* Active filter chips */}
        {hasFilters && !fetchError && (
          <div className="flex items-center flex-wrap gap-2 text-sm text-gray-500">
            <span>Filters:</span>
            {search        && <FilterChip label={`"${search}"`} />}
            {entityType    && <FilterChip label={`type: ${entityType}`} />}
            {actor         && <FilterChip label={`actor: ${actor}`} />}
            {eventType     && <FilterChip label={`event: ${eventType}`} />}
            {severity      && <FilterChip label={`severity: ${severity}`} />}
            {category      && <FilterChip label={`category: ${category}`} />}
            {correlationId && <FilterChip label={`corr: ${correlationId}`} />}
            {dateFrom      && <FilterChip label={`from: ${dateFrom}`} />}
            {dateTo        && <FilterChip label={`to: ${dateTo}`} />}
          </div>
        )}

        {/* Error banner */}
        {fetchError && (
          <div className="bg-red-50 border border-red-200 rounded-lg px-4 py-3 text-sm text-red-700">
            {fetchError}
          </div>
        )}

        {/* Hybrid fallback warning — shown when canonical fetch failed and legacy was used */}
        {MODE === 'hybrid' && canonicalFallbackReason && !fetchError && (
          <div className="flex items-start gap-3 bg-amber-50 border border-amber-300 rounded-lg px-4 py-3 text-sm text-amber-800">
            <svg className="mt-0.5 h-4 w-4 shrink-0 text-amber-500" viewBox="0 0 20 20" fill="currentColor" aria-hidden="true">
              <path fillRule="evenodd" d="M8.485 2.495c.673-1.167 2.357-1.167 3.03 0l6.28 10.875c.673 1.167-.17 2.625-1.516 2.625H3.72c-1.347 0-2.189-1.458-1.515-2.625L8.485 2.495zM10 5a.75.75 0 01.75.75v3.5a.75.75 0 01-1.5 0v-3.5A.75.75 0 0110 5zm0 9a1 1 0 100-2 1 1 0 000 2z" clipRule="evenodd" />
            </svg>
            <span>
              <strong className="font-semibold">Canonical audit service unavailable —</strong>{' '}
              displaying legacy audit data. Check that the Platform Audit Event Service is running on port 5007.
              {' '}<span className="text-amber-600 font-mono text-xs break-all">{canonicalFallbackReason}</span>
            </span>
          </div>
        )}

        {/* Result count header */}
        {!fetchError && (canonicalResult || legacyResult) && (
          <div className="flex items-center justify-between text-xs text-gray-400">
            <span>
              {totalCount === 0
                ? 'No matching events'
                : `Showing ${startItem}–${endItem} of ${totalCount.toLocaleString()} event${totalCount !== 1 ? 's' : ''}`}
            </span>
            {totalPages > 1 && <span>Page {page} of {totalPages}</span>}
          </div>
        )}

        {/* Table — canonical path (interactive: row click opens detail panel) */}
        {!fetchError && canonicalResult && (
          <CanonicalAuditTableInteractive entries={canonicalResult.items} />
        )}

        {/* Table — legacy path */}
        {!fetchError && legacyResult && (
          <AuditLogTable entries={legacyResult.items} />
        )}

        {/* Pagination */}
        {!fetchError && totalPages > 1 && (
          <Pagination
            page={page}
            totalPages={totalPages}
            buildHref={paginationHref}
          />
        )}

      </div>
    </CCShell>
  );
}

// ── Local helpers ─────────────────────────────────────────────────────────────

function FilterChip({ label }: { label: string }) {
  return (
    <span className="inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium bg-indigo-50 text-indigo-700 border border-indigo-200">
      {label}
    </span>
  );
}

function Pagination({
  page,
  totalPages,
  buildHref,
}: {
  page:       number;
  totalPages: number;
  buildHref:  (p: number) => string;
}) {
  const pages: (number | '…')[] = buildPageRange(page, totalPages);

  return (
    <nav className="flex items-center justify-center gap-1" aria-label="Pagination">
      <PagerLink href={buildHref(page - 1)} disabled={page <= 1} label="← Prev" />
      {pages.map((p, i) =>
        p === '…' ? (
          <span key={`ellipsis-${i}`} className="px-2 py-1 text-xs text-gray-400">…</span>
        ) : (
          <PagerLink key={p} href={buildHref(p)} active={p === page} label={String(p)} />
        ),
      )}
      <PagerLink href={buildHref(page + 1)} disabled={page >= totalPages} label="Next →" />
    </nav>
  );
}

function PagerLink({
  href,
  label,
  active   = false,
  disabled = false,
}: {
  href:      string;
  label:     string;
  active?:   boolean;
  disabled?: boolean;
}) {
  if (disabled) {
    return (
      <span className="px-3 py-1.5 text-xs rounded-md text-gray-300 cursor-not-allowed">
        {label}
      </span>
    );
  }
  return (
    <a
      href={href}
      className={[
        'px-3 py-1.5 text-xs rounded-md font-medium transition-colors',
        active
          ? 'bg-indigo-600 text-white'
          : 'text-gray-600 hover:bg-gray-100 hover:text-gray-900',
      ].join(' ')}
    >
      {label}
    </a>
  );
}

function buildPageRange(current: number, total: number): (number | '…')[] {
  if (total <= 7) return Array.from({ length: total }, (_, i) => i + 1);
  const pages: (number | '…')[] = [1];
  if (current > 3)         pages.push('…');
  for (let p = Math.max(2, current - 1); p <= Math.min(total - 1, current + 1); p++) {
    pages.push(p);
  }
  if (current < total - 2) pages.push('…');
  pages.push(total);
  return pages;
}
