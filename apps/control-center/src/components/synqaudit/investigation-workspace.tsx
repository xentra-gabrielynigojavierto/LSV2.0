'use client';

import { useState, useTransition, useCallback } from 'react';
import { useRouter, useSearchParams, usePathname } from 'next/navigation';
import type { CanonicalAuditEvent } from '@/types/control-center';
import { SeverityBadge, CategoryBadge, OutcomeBadge, formatUtc, formatUtcFull } from './synqaudit-badges';

interface Props {
  entries:    CanonicalAuditEvent[];
  totalCount: number;
  page:       number;
  totalPages: number;
  filters: {
    eventType:     string;
    category:      string;
    severity:      string;
    actorId:       string;
    targetType:    string;
    correlationId: string;
    dateFrom:      string;
    dateTo:        string;
    search:        string;
  };
}

/**
 * InvestigationWorkspace — fully interactive SynqAudit investigation view.
 *
 * Client component that owns:
 *   - The live filter bar (controlled inputs, URL-push on submit)
 *   - The event stream table (row click → detail panel)
 *   - The full event detail side panel (all fields, Before/After, metadata, tags)
 *   - Pagination controls
 */
export function InvestigationWorkspace({
  entries,
  totalCount,
  page,
  totalPages,
  filters,
}: Props) {
  const router   = useRouter();
  const pathname = usePathname();
  const [, startTransition] = useTransition();

  const [selected, setSelected] = useState<CanonicalAuditEvent | null>(null);

  const [localFilters, setLocalFilters] = useState(filters);

  const applyFilters = useCallback(() => {
    const params = new URLSearchParams();
    if (localFilters.eventType)     params.set('eventType',     localFilters.eventType);
    if (localFilters.category)      params.set('category',      localFilters.category);
    if (localFilters.severity)      params.set('severity',      localFilters.severity);
    if (localFilters.actorId)       params.set('actorId',       localFilters.actorId);
    if (localFilters.targetType)    params.set('targetType',    localFilters.targetType);
    if (localFilters.correlationId) params.set('correlationId', localFilters.correlationId);
    if (localFilters.dateFrom)      params.set('dateFrom',      localFilters.dateFrom);
    if (localFilters.dateTo)        params.set('dateTo',        localFilters.dateTo);
    if (localFilters.search)        params.set('search',        localFilters.search);
    startTransition(() => {
      router.push(`${pathname ?? "/"}${params.size ? `?${params}` : ''}`);
    });
  }, [localFilters, pathname, router]);

  const clearFilters = useCallback(() => {
    setLocalFilters({ eventType: '', category: '', severity: '', actorId: '', targetType: '', correlationId: '', dateFrom: '', dateTo: '', search: '' });
    startTransition(() => router.push(pathname ?? '/'));
  }, [pathname, router]);

  const paginationHref = useCallback((p: number) => {
    const params = new URLSearchParams();
    if (filters.eventType)     params.set('eventType',     filters.eventType);
    if (filters.category)      params.set('category',      filters.category);
    if (filters.severity)      params.set('severity',      filters.severity);
    if (filters.actorId)       params.set('actorId',       filters.actorId);
    if (filters.targetType)    params.set('targetType',    filters.targetType);
    if (filters.correlationId) params.set('correlationId', filters.correlationId);
    if (filters.dateFrom)      params.set('dateFrom',      filters.dateFrom);
    if (filters.dateTo)        params.set('dateTo',        filters.dateTo);
    if (filters.search)        params.set('search',        filters.search);
    if (p > 1)                 params.set('page',          String(p));
    return `${pathname ?? "/"}${params.size ? `?${params}` : ''}`;
  }, [filters, pathname]);

  const hasFilters = Object.values(filters).some(Boolean);
  const startItem  = totalCount === 0 ? 0 : (page - 1) * 15 + 1;
  const endItem    = Math.min(page * 15, totalCount);

  const inputCls = 'w-full h-9 rounded-md border border-gray-300 px-3 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500 bg-white';
  const labelCls = 'block text-xs font-medium text-gray-600 mb-1';

  return (
    <div className="space-y-4">

      {/* ── Filter bar ──────────────────────────────────────────────────── */}
      <div className="rounded-lg border border-gray-200 bg-white px-4 py-3 space-y-3">
        <div className="flex flex-wrap items-end gap-3">
          <div className="flex-1 min-w-36">
            <label className={labelCls}>Search description</label>
            <input
              type="search"
              placeholder="Keyword…"
              value={localFilters.search}
              onChange={e => setLocalFilters(f => ({ ...f, search: e.target.value }))}
              onKeyDown={e => e.key === 'Enter' && applyFilters()}
              className={inputCls}
            />
          </div>

          <div className="w-44">
            <label className={labelCls}>Event Type</label>
            <input
              type="text"
              placeholder="identity.user.login…"
              value={localFilters.eventType}
              onChange={e => setLocalFilters(f => ({ ...f, eventType: e.target.value }))}
              onKeyDown={e => e.key === 'Enter' && applyFilters()}
              className={inputCls}
            />
          </div>

          <div className="w-36">
            <label className={labelCls}>Category</label>
            <select
              value={localFilters.category}
              onChange={e => setLocalFilters(f => ({ ...f, category: e.target.value }))}
              className={inputCls}
            >
              <option value="">All categories</option>
              <option value="security">Security</option>
              <option value="access">Access</option>
              <option value="business">Business</option>
              <option value="administrative">Administrative</option>
              <option value="compliance">Compliance</option>
              <option value="dataChange">Data Change</option>
            </select>
          </div>

          <div className="w-28">
            <label className={labelCls}>Severity</label>
            <select
              value={localFilters.severity}
              onChange={e => setLocalFilters(f => ({ ...f, severity: e.target.value }))}
              className={inputCls}
            >
              <option value="">All</option>
              <option value="info">Info</option>
              <option value="warn">Warn</option>
              <option value="error">Error</option>
              <option value="critical">Critical</option>
            </select>
          </div>

          <div className="w-40">
            <label className={labelCls}>Actor ID</label>
            <input
              type="text"
              placeholder="user-uuid…"
              value={localFilters.actorId}
              onChange={e => setLocalFilters(f => ({ ...f, actorId: e.target.value }))}
              onKeyDown={e => e.key === 'Enter' && applyFilters()}
              className={inputCls}
            />
          </div>

          <div className="w-44">
            <label className={labelCls}>Correlation ID</label>
            <input
              type="text"
              placeholder="req-xxxxxxxx"
              value={localFilters.correlationId}
              onChange={e => setLocalFilters(f => ({ ...f, correlationId: e.target.value }))}
              onKeyDown={e => e.key === 'Enter' && applyFilters()}
              className={inputCls}
            />
          </div>

          <div className="w-36">
            <label className={labelCls}>From</label>
            <input
              type="date"
              value={localFilters.dateFrom}
              onChange={e => setLocalFilters(f => ({ ...f, dateFrom: e.target.value }))}
              className={inputCls}
            />
          </div>

          <div className="w-36">
            <label className={labelCls}>To</label>
            <input
              type="date"
              value={localFilters.dateTo}
              onChange={e => setLocalFilters(f => ({ ...f, dateTo: e.target.value }))}
              className={inputCls}
            />
          </div>

          <div className="flex items-center gap-2">
            <button
              onClick={applyFilters}
              className="h-9 px-4 text-sm font-medium text-white bg-indigo-600 hover:bg-indigo-700 rounded-md transition-colors"
            >
              Apply
            </button>
            {hasFilters && (
              <button
                onClick={clearFilters}
                className="h-9 px-3 text-sm font-medium text-gray-600 hover:text-gray-900 bg-white border border-gray-300 rounded-md transition-colors"
              >
                Clear
              </button>
            )}
          </div>
        </div>

        {/* Active filter chips */}
        {hasFilters && (
          <div className="flex flex-wrap gap-1.5">
            {filters.search        && <FilterChip label={`"${filters.search}"`} />}
            {filters.eventType     && <FilterChip label={`event: ${filters.eventType}`} />}
            {filters.category      && <FilterChip label={`cat: ${filters.category}`} />}
            {filters.severity      && <FilterChip label={`sev: ${filters.severity}`} />}
            {filters.actorId       && <FilterChip label={`actor: ${filters.actorId}`} />}
            {filters.correlationId && <FilterChip label={`corr: ${filters.correlationId}`} />}
            {filters.dateFrom      && <FilterChip label={`from: ${filters.dateFrom}`} />}
            {filters.dateTo        && <FilterChip label={`to: ${filters.dateTo}`} />}
          </div>
        )}
      </div>

      {/* ── Result count ────────────────────────────────────────────────── */}
      <div className="flex items-center justify-between text-xs text-gray-400 px-0.5">
        <span>
          {totalCount === 0
            ? 'No matching events'
            : `Showing ${startItem}–${endItem} of ${totalCount.toLocaleString()} event${totalCount !== 1 ? 's' : ''}`}
        </span>
        {totalPages > 1 && <span>Page {page} of {totalPages}</span>}
      </div>

      {/* ── Table + Detail panel ─────────────────────────────────────────── */}
      <div className="flex gap-4 items-start">

        {/* Table */}
        <div className="flex-1 overflow-x-auto rounded-lg border border-gray-200 bg-white min-w-0">
          {entries.length === 0 ? (
            <div className="px-6 py-12 text-center">
              <p className="text-sm text-gray-400">No events match the current filters.</p>
            </div>
          ) : (
            <table className="min-w-full divide-y divide-gray-100 text-sm">
              <thead className="bg-gray-50 text-xs text-gray-500 uppercase tracking-wide">
                <tr>
                  <th className="px-4 py-3 text-left font-medium whitespace-nowrap">Time (UTC)</th>
                  <th className="px-4 py-3 text-left font-medium whitespace-nowrap">Severity</th>
                  <th className="px-4 py-3 text-left font-medium whitespace-nowrap">Event Type</th>
                  <th className="px-4 py-3 text-left font-medium whitespace-nowrap">Category</th>
                  <th className="px-4 py-3 text-left font-medium whitespace-nowrap">Actor</th>
                  <th className="px-4 py-3 text-left font-medium whitespace-nowrap">Target</th>
                  <th className="px-4 py-3 text-left font-medium whitespace-nowrap">Outcome</th>
                  <th className="px-4 py-3 text-left font-medium whitespace-nowrap">Correlation</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {entries.map((e) => {
                  const isSelected = selected?.id === e.id;
                  return (
                    <tr
                      key={e.id}
                      onClick={() => setSelected(isSelected ? null : e)}
                      className={[
                        'cursor-pointer transition-colors',
                        isSelected
                          ? 'bg-indigo-50 ring-1 ring-inset ring-indigo-200'
                          : 'hover:bg-gray-50',
                      ].join(' ')}
                    >
                      <td className="px-4 py-2.5 text-gray-500 whitespace-nowrap font-mono text-[11px]">
                        {formatUtc(e.occurredAtUtc)}
                      </td>
                      <td className="px-4 py-2.5">
                        <SeverityBadge value={e.severity} />
                      </td>
                      <td className="px-4 py-2.5 text-gray-700 font-mono text-[11px] whitespace-nowrap">
                        {e.eventType}
                      </td>
                      <td className="px-4 py-2.5">
                        <CategoryBadge value={e.category} />
                      </td>
                      <td className="px-4 py-2.5 text-gray-700 whitespace-nowrap">
                        <span className="block text-xs font-medium">
                          {e.actorLabel ?? e.actorId ?? <span className="text-gray-400 italic">system</span>}
                        </span>
                        {e.actorId && e.actorLabel && (
                          <span className="block text-[10px] text-gray-400 font-mono">{e.actorId}</span>
                        )}
                      </td>
                      <td className="px-4 py-2.5 text-gray-600 whitespace-nowrap text-xs">
                        {e.targetType && <span className="mr-1 text-[10px] font-semibold text-gray-400 uppercase">{e.targetType}</span>}
                        {e.targetId   && <span className="font-mono text-[11px]">{e.targetId}</span>}
                        {!e.targetType && !e.targetId && <span className="text-gray-300 italic text-[11px]">—</span>}
                      </td>
                      <td className="px-4 py-2.5">
                        <OutcomeBadge value={e.outcome} />
                      </td>
                      <td className="px-4 py-2.5 text-gray-400 font-mono text-[10px] whitespace-nowrap">
                        {e.correlationId ? (
                          <span title={e.correlationId}>
                            {e.correlationId.length > 12 ? `${e.correlationId.slice(0, 12)}…` : e.correlationId}
                          </span>
                        ) : <span className="text-gray-200">—</span>}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          )}
        </div>

        {/* Detail panel */}
        {selected && (
          <EventDetailPanel event={selected} onClose={() => setSelected(null)} />
        )}
      </div>

      {/* ── Pagination ──────────────────────────────────────────────────── */}
      {totalPages > 1 && (
        <Pagination page={page} totalPages={totalPages} buildHref={paginationHref} />
      )}
    </div>
  );
}

// ── Event Detail Panel ────────────────────────────────────────────────────────

function EventDetailPanel({
  event: e,
  onClose,
}: {
  event:   CanonicalAuditEvent;
  onClose: () => void;
}) {
  return (
    <div className="w-[22rem] shrink-0 rounded-lg border border-gray-200 bg-white shadow-sm overflow-hidden">
      <div className="flex items-center justify-between px-4 py-3 border-b border-gray-100 bg-gray-50">
        <h3 className="text-sm font-semibold text-gray-700">Event Detail</h3>
        <button onClick={onClose} className="text-gray-400 hover:text-gray-700 transition-colors" aria-label="Close">
          <svg className="h-4 w-4" viewBox="0 0 20 20" fill="currentColor">
            <path fillRule="evenodd" d="M4.293 4.293a1 1 0 011.414 0L10 8.586l4.293-4.293a1 1 0 111.414 1.414L11.414 10l4.293 4.293a1 1 0 01-1.414 1.414L10 11.414l-4.293 4.293a1 1 0 01-1.414-1.414L8.586 10 4.293 5.707a1 1 0 010-1.414z" clipRule="evenodd" />
          </svg>
        </button>
      </div>

      <div className="px-4 py-3 space-y-3 overflow-y-auto max-h-[75vh]">

        <Section label="Classification">
          <Row label="Event Type" mono>{e.eventType}</Row>
          <Row label="Category"><CategoryBadge value={e.category} /></Row>
          <Row label="Severity"><SeverityBadge value={e.severity} /></Row>
          <Row label="Outcome"><OutcomeBadge value={e.outcome} /></Row>
          {e.action && <Row label="Action" mono>{e.action}</Row>}
        </Section>

        <Section label="Source">
          <Row label="System">{e.source}</Row>
          {e.sourceService && <Row label="Service">{e.sourceService}</Row>}
        </Section>

        <Section label="Timing">
          <Row label="Occurred" mono>{formatUtcFull(e.occurredAtUtc)}</Row>
          <Row label="Ingested" mono>{formatUtcFull(e.ingestedAtUtc)}</Row>
        </Section>

        <Section label="Actor">
          {e.actorLabel && <Row label="Name">{e.actorLabel}</Row>}
          {e.actorId    && <Row label="ID" mono>{e.actorId}</Row>}
          {e.actorType  && <Row label="Type">{e.actorType}</Row>}
          {e.ipAddress  && <Row label="IP" mono>{e.ipAddress}</Row>}
          {!e.actorLabel && !e.actorId && (
            <p className="text-[11px] text-gray-400 italic">System / anonymous</p>
          )}
        </Section>

        {(e.targetType || e.targetId) && (
          <Section label="Target">
            {e.targetType && <Row label="Type">{e.targetType}</Row>}
            {e.targetId   && <Row label="ID" mono>{e.targetId}</Row>}
          </Section>
        )}

        {e.tenantId && (
          <Section label="Scope">
            <Row label="Tenant ID" mono>{e.tenantId}</Row>
          </Section>
        )}

        <Section label="Tracing">
          <Row label="Event ID" mono>{e.id}</Row>
          {e.correlationId && <Row label="Correlation" mono>{e.correlationId}</Row>}
          {e.requestId     && <Row label="Request ID"  mono>{e.requestId}</Row>}
          {e.sessionId     && <Row label="Session ID"  mono>{e.sessionId}</Row>}
          {e.hash          && <Row label="Hash" mono>{e.hash.slice(0, 16)}…</Row>}
        </Section>

        {e.description && (
          <Section label="Description">
            <p className="text-[12px] text-gray-700 leading-relaxed">{e.description}</p>
          </Section>
        )}

        {e.tags && e.tags.length > 0 && (
          <Section label="Tags">
            <div className="flex flex-wrap gap-1 mt-0.5">
              {e.tags.map(tag => (
                <span key={tag} className="inline-flex items-center px-1.5 py-0.5 rounded text-[10px] font-medium bg-gray-100 text-gray-600 border border-gray-200">
                  {tag}
                </span>
              ))}
            </div>
          </Section>
        )}

        {e.before && <JsonSection label="Before State" raw={e.before} />}
        {e.after  && <JsonSection label="After State"  raw={e.after}  />}
        {e.metadata && <JsonSection label="Metadata"   raw={e.metadata} />}

        {/* Correlation engine — Related Events */}
        <a
          href={`/synqaudit/related/${encodeURIComponent(e.id)}`}
          className="mt-1 flex items-center gap-1 text-[11px] text-indigo-600 hover:text-indigo-800 font-medium transition-colors"
        >
          <svg className="h-3 w-3" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
            <path strokeLinecap="round" strokeLinejoin="round" d="M7 20l4-16m2 16l4-16M6 9h14M4 15h14" />
          </svg>
          Find related events
        </a>

        {/* Link to trace view */}
        {e.correlationId && (
          <a
            href={`/synqaudit/trace?correlationId=${encodeURIComponent(e.correlationId)}`}
            className="mt-1 flex items-center gap-1 text-[11px] text-indigo-600 hover:text-indigo-800 font-medium transition-colors"
          >
            <svg className="h-3 w-3" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M13.828 10.172a4 4 0 00-5.656 0l-4 4a4 4 0 105.656 5.656l1.102-1.101m-.758-4.899a4 4 0 005.656 0l4-4a4 4 0 00-5.656-5.656l-1.1 1.1" />
            </svg>
            Trace this correlation ID
          </a>
        )}
      </div>
    </div>
  );
}

// ── Panel sub-components ──────────────────────────────────────────────────────

function Section({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div>
      <p className="text-[10px] font-semibold text-gray-400 uppercase tracking-wider mb-1">{label}</p>
      <div className="space-y-0.5">{children}</div>
    </div>
  );
}

function Row({ label, mono = false, children }: { label: string; mono?: boolean; children: React.ReactNode }) {
  return (
    <div className="flex justify-between gap-2 text-[11px] py-0.5">
      <span className="text-gray-400 shrink-0">{label}</span>
      <span className={['text-gray-700 text-right break-all', mono ? 'font-mono' : ''].join(' ')}>
        {children}
      </span>
    </div>
  );
}

function JsonSection({ label, raw }: { label: string; raw: string }) {
  let formatted = raw;
  try { formatted = JSON.stringify(JSON.parse(raw), null, 2); } catch { }
  return (
    <div>
      <p className="text-[10px] font-semibold text-gray-400 uppercase tracking-wider mb-1">{label}</p>
      <pre className="bg-gray-50 border border-gray-100 rounded p-2 text-[10px] font-mono text-gray-600 overflow-auto max-h-40 whitespace-pre-wrap break-all">
        {formatted}
      </pre>
    </div>
  );
}

function FilterChip({ label }: { label: string }) {
  return (
    <span className="inline-flex items-center px-2 py-0.5 rounded-full text-[10px] font-medium bg-indigo-50 text-indigo-700 border border-indigo-200">
      {label}
    </span>
  );
}

// ── Pagination ────────────────────────────────────────────────────────────────

function Pagination({ page, totalPages, buildHref }: { page: number; totalPages: number; buildHref: (p: number) => string }) {
  const pages = buildPageRange(page, totalPages);
  return (
    <nav className="flex items-center justify-center gap-1" aria-label="Pagination">
      <PagerLink href={buildHref(page - 1)} disabled={page <= 1} label="← Prev" />
      {pages.map((p, i) =>
        p === '…' ? (
          <span key={`e${i}`} className="px-2 py-1 text-xs text-gray-400">…</span>
        ) : (
          <PagerLink key={p} href={buildHref(p)} active={p === page} label={String(p)} />
        ),
      )}
      <PagerLink href={buildHref(page + 1)} disabled={page >= totalPages} label="Next →" />
    </nav>
  );
}

function PagerLink({ href, label, active = false, disabled = false }: { href: string; label: string; active?: boolean; disabled?: boolean }) {
  if (disabled) return <span className="px-3 py-1.5 text-xs rounded-md text-gray-300 cursor-not-allowed">{label}</span>;
  return (
    <a href={href} className={['px-3 py-1.5 text-xs rounded-md font-medium transition-colors', active ? 'bg-indigo-600 text-white' : 'text-gray-600 hover:bg-gray-100'].join(' ')}>
      {label}
    </a>
  );
}

function buildPageRange(current: number, total: number): (number | '…')[] {
  if (total <= 7) return Array.from({ length: total }, (_, i) => i + 1);
  const pages: (number | '…')[] = [1];
  if (current > 3) pages.push('…');
  for (let p = Math.max(2, current - 1); p <= Math.min(total - 1, current + 1); p++) pages.push(p);
  if (current < total - 2) pages.push('…');
  pages.push(total);
  return pages;
}
