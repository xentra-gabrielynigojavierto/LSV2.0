'use client';

import { useState, useTransition, useCallback } from 'react';
import { useRouter, usePathname }               from 'next/navigation';
import type { CanonicalAuditEvent }             from '@/types/control-center';
import {
  SeverityBadge,
  OutcomeBadge,
  formatUtc,
  formatUtcFull,
} from './synqaudit-badges';

// ── Scope preset definitions ──────────────────────────────────────────────────

interface ScopeOption {
  value:       string;
  label:       string;
  eventType:   string;
  chipColor:   string;
}

const SCOPE_OPTIONS: ScopeOption[] = [
  { value: '',                        label: 'All permission changes',          eventType: '',                                chipColor: 'bg-gray-100 text-gray-600 border border-gray-300 hover:bg-gray-200' },
  { value: 'user-role-assigned',      label: 'User role — assigned',            eventType: 'identity.user.role.assigned',     chipColor: 'bg-indigo-50 text-indigo-700 border border-indigo-200 hover:bg-indigo-100' },
  { value: 'user-role-revoked',       label: 'User role — revoked',             eventType: 'identity.user.role.revoked',      chipColor: 'bg-red-50 text-red-700 border border-red-200 hover:bg-red-100' },
  { value: 'user-product-assigned',   label: 'User product — assigned',         eventType: 'identity.user.product.assigned',  chipColor: 'bg-green-50 text-green-700 border border-green-200 hover:bg-green-100' },
  { value: 'user-product-revoked',    label: 'User product — revoked',          eventType: 'identity.user.product.revoked',   chipColor: 'bg-orange-50 text-orange-700 border border-orange-200 hover:bg-orange-100' },
  { value: 'user-product-reactivated', label: 'User product — reactivated',     eventType: 'identity.user.product.reactivated', chipColor: 'bg-teal-50 text-teal-700 border border-teal-200 hover:bg-teal-100' },
  { value: 'group-member-added',      label: 'Group member — added',            eventType: 'identity.group.member.added',     chipColor: 'bg-blue-50 text-blue-700 border border-blue-200 hover:bg-blue-100' },
  { value: 'group-member-removed',    label: 'Group member — removed',          eventType: 'identity.group.member.removed',   chipColor: 'bg-rose-50 text-rose-700 border border-rose-200 hover:bg-rose-100' },
  { value: 'group-member-reactivated', label: 'Group member — reactivated',     eventType: 'identity.group.member.reactivated', chipColor: 'bg-cyan-50 text-cyan-700 border border-cyan-200 hover:bg-cyan-100' },
  { value: 'group-role-assigned',     label: 'Group role — assigned',           eventType: 'identity.group.role.assigned',    chipColor: 'bg-violet-50 text-violet-700 border border-violet-200 hover:bg-violet-100' },
  { value: 'group-role-revoked',      label: 'Group role — revoked',            eventType: 'identity.group.role.revoked',     chipColor: 'bg-pink-50 text-pink-700 border border-pink-200 hover:bg-pink-100' },
  { value: 'group-product-assigned',  label: 'Group product — assigned',        eventType: 'identity.group.product.assigned', chipColor: 'bg-emerald-50 text-emerald-700 border border-emerald-200 hover:bg-emerald-100' },
  { value: 'group-product-revoked',   label: 'Group product — revoked',         eventType: 'identity.group.product.revoked',  chipColor: 'bg-amber-50 text-amber-700 border border-amber-200 hover:bg-amber-100' },
  { value: 'tenant-product-assigned', label: 'Tenant product — assigned',       eventType: 'identity.tenant.product.assigned', chipColor: 'bg-purple-50 text-purple-700 border border-purple-200 hover:bg-purple-100' },
];

// ── Component props ───────────────────────────────────────────────────────────

interface Filters {
  scope:    string;
  actorId:  string;
  tenantId: string;
  dateFrom: string;
  dateTo:   string;
  search:   string;
}

interface Props {
  entries:       CanonicalAuditEvent[];
  totalCount:    number;
  page:          number;
  totalPages:    number;
  filters:       Filters;
  tenantCtxName?: string;
}

// ── Main component ────────────────────────────────────────────────────────────

/**
 * PermissionAuditWorkspace — interactive client component for the
 * /synqaudit/permissions page.
 *
 * Renders:
 *  1. A scope preset selector (dropdown + quick-chips) that maps to specific
 *     identity permission-change eventType values.
 *  2. Additional filters: actorId, tenantId (if not already scoped by context),
 *     date range, and a keyword search.
 *  3. A table of permission-change events with Before/After state indicators.
 *  4. A side-panel detail view showing all fields including before/after JSON.
 *  5. Pagination controls.
 */
export function PermissionAuditWorkspace({
  entries,
  totalCount,
  page,
  totalPages,
  filters,
  tenantCtxName,
}: Props) {
  const router   = useRouter();
  const pathname = usePathname();
  const [, startTransition] = useTransition();

  const [selected,     setSelected]     = useState<CanonicalAuditEvent | null>(null);
  const [localFilters, setLocalFilters] = useState<Filters>(filters);

  // Build URL params from current filter state and push to router.
  const applyFilters = useCallback((overrides?: Partial<Filters>) => {
    const f = { ...localFilters, ...overrides };
    const params = new URLSearchParams();
    if (f.scope)    params.set('scope',    f.scope);
    if (f.actorId)  params.set('actorId',  f.actorId);
    if (f.tenantId) params.set('tenantId', f.tenantId);
    if (f.dateFrom) params.set('dateFrom', f.dateFrom);
    if (f.dateTo)   params.set('dateTo',   f.dateTo);
    if (f.search)   params.set('search',   f.search);
    startTransition(() => {
      router.push(`${pathname ?? "/"}${params.size ? `?${params}` : ''}`);
    });
  }, [localFilters, pathname, router]);

  const clearFilters = useCallback(() => {
    setLocalFilters({ scope: '', actorId: '', tenantId: '', dateFrom: '', dateTo: '', search: '' });
    startTransition(() => router.push(pathname ?? '/'));
  }, [pathname, router]);

  const paginationHref = useCallback((p: number) => {
    const params = new URLSearchParams();
    if (filters.scope)    params.set('scope',    filters.scope);
    if (filters.actorId)  params.set('actorId',  filters.actorId);
    if (filters.tenantId) params.set('tenantId', filters.tenantId);
    if (filters.dateFrom) params.set('dateFrom', filters.dateFrom);
    if (filters.dateTo)   params.set('dateTo',   filters.dateTo);
    if (filters.search)   params.set('search',   filters.search);
    if (p > 1)            params.set('page',     String(p));
    return `${pathname ?? "/"}${params.size ? `?${params}` : ''}`;
  }, [filters, pathname]);

  const hasFilters = Object.values(filters).some(Boolean);
  const startItem  = totalCount === 0 ? 0 : (page - 1) * PAGE_SIZE + 1;
  const endItem    = Math.min(page * PAGE_SIZE, totalCount);
  const activeScope = SCOPE_OPTIONS.find(o => o.value === filters.scope);

  const inputCls = 'w-full h-9 rounded-md border border-gray-300 px-3 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500 bg-white';
  const labelCls = 'block text-xs font-medium text-gray-600 mb-1';

  return (
    <div className="space-y-4">

      {/* ── Filter panel ─────────────────────────────────────────────────── */}
      <div className="rounded-lg border border-gray-200 bg-white px-4 py-3 space-y-4">

        {/* Row 1: scope dropdown + actor + tenant + dates + search */}
        <div className="flex flex-wrap items-end gap-3">

          <div className="w-56">
            <label className={labelCls}>Permission scope</label>
            <select
              value={localFilters.scope}
              onChange={e => setLocalFilters(f => ({ ...f, scope: e.target.value }))}
              className={inputCls}
            >
              {SCOPE_OPTIONS.map(o => (
                <option key={o.value} value={o.value}>{o.label}</option>
              ))}
            </select>
          </div>

          <div className="w-44">
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

          {!tenantCtxName && (
            <div className="w-44">
              <label className={labelCls}>Tenant ID</label>
              <input
                type="text"
                placeholder="tenant-uuid…"
                value={localFilters.tenantId}
                onChange={e => setLocalFilters(f => ({ ...f, tenantId: e.target.value }))}
                onKeyDown={e => e.key === 'Enter' && applyFilters()}
                className={inputCls}
              />
            </div>
          )}

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

          <div className="flex-1 min-w-32">
            <label className={labelCls}>Keyword search</label>
            <input
              type="search"
              placeholder="Description…"
              value={localFilters.search}
              onChange={e => setLocalFilters(f => ({ ...f, search: e.target.value }))}
              onKeyDown={e => e.key === 'Enter' && applyFilters()}
              className={inputCls}
            />
          </div>

          <div className="flex items-center gap-2 pb-0.5">
            <button
              onClick={() => applyFilters()}
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

        {/* Row 2: quick-scope chips */}
        <div className="flex flex-wrap gap-1.5 items-center">
          <span className="text-[10px] font-semibold text-gray-400 uppercase tracking-wider shrink-0">Quick scope:</span>
          {SCOPE_OPTIONS.map(opt => (
            <button
              key={opt.value}
              onClick={() => {
                setLocalFilters(f => ({ ...f, scope: opt.value }));
                applyFilters({ scope: opt.value });
              }}
              className={[
                'inline-flex items-center px-2.5 py-1 rounded-md text-[11px] font-medium transition-colors cursor-pointer',
                opt.chipColor,
                filters.scope === opt.value ? 'ring-2 ring-offset-1 ring-indigo-400' : '',
              ].join(' ')}
            >
              {opt.label}
            </button>
          ))}
        </div>

        {/* Active filter chips */}
        {hasFilters && (
          <div className="flex flex-wrap gap-1.5 items-center">
            <span className="text-[10px] font-semibold text-gray-400 uppercase tracking-wider">Active:</span>
            {filters.scope    && <Chip label={`scope: ${activeScope?.label ?? filters.scope}`} />}
            {filters.actorId  && <Chip label={`actor: ${filters.actorId}`} />}
            {filters.tenantId && <Chip label={`tenant: ${filters.tenantId}`} />}
            {filters.dateFrom && <Chip label={`from: ${filters.dateFrom}`} />}
            {filters.dateTo   && <Chip label={`to: ${filters.dateTo}`} />}
            {filters.search   && <Chip label={`"${filters.search}"`} />}
          </div>
        )}
      </div>

      {/* ── Result count ─────────────────────────────────────────────────── */}
      <div className="flex items-center justify-between text-xs text-gray-400 px-0.5">
        <span>
          {totalCount === 0
            ? 'No matching events'
            : `Showing ${startItem}–${endItem} of ${totalCount.toLocaleString()} event${totalCount !== 1 ? 's' : ''}`}
        </span>
        {totalPages > 1 && <span>Page {page} of {totalPages}</span>}
      </div>

      {/* ── Table + Detail panel ──────────────────────────────────────────── */}
      <div className="flex gap-4 items-start">

        {/* Table */}
        <div className="flex-1 overflow-x-auto rounded-lg border border-gray-200 bg-white min-w-0">
          {entries.length === 0 ? (
            <div className="px-6 py-16 text-center">
              <i className="ri-key-2-line text-3xl text-gray-200 block mb-3" />
              <p className="text-sm text-gray-400">No permission-change events match the current filters.</p>
              <p className="text-xs text-gray-300 mt-1">
                Try selecting a scope preset or broadening the date range.
              </p>
            </div>
          ) : (
            <table className="min-w-full divide-y divide-gray-100 text-sm">
              <thead className="bg-gray-50 text-xs text-gray-500 uppercase tracking-wide">
                <tr>
                  <th className="px-4 py-3 text-left font-medium whitespace-nowrap">Time (UTC)</th>
                  <th className="px-4 py-3 text-left font-medium whitespace-nowrap">Severity</th>
                  <th className="px-4 py-3 text-left font-medium whitespace-nowrap">Event Type</th>
                  <th className="px-4 py-3 text-left font-medium whitespace-nowrap">Actor</th>
                  <th className="px-4 py-3 text-left font-medium whitespace-nowrap">Entity</th>
                  <th className="px-4 py-3 text-left font-medium whitespace-nowrap">Tenant</th>
                  <th className="px-4 py-3 text-left font-medium whitespace-nowrap">State Change</th>
                  <th className="px-4 py-3 text-left font-medium whitespace-nowrap">Outcome</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {entries.map(e => {
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
                      <td className="px-4 py-2.5 text-gray-700 font-mono text-[11px] whitespace-nowrap max-w-[18rem] truncate">
                        {e.eventType}
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
                        {e.targetType && (
                          <span className="mr-1 text-[10px] font-semibold text-gray-400 uppercase">{e.targetType}</span>
                        )}
                        {e.targetId && (
                          <span className="font-mono text-[11px]">{e.targetId}</span>
                        )}
                        {!e.targetType && !e.targetId && (
                          <span className="text-gray-300 italic text-[11px]">—</span>
                        )}
                      </td>
                      <td className="px-4 py-2.5 text-gray-500 font-mono text-[11px] whitespace-nowrap">
                        {e.tenantId
                          ? <span title={e.tenantId}>{e.tenantId.slice(0, 8)}…</span>
                          : <span className="text-gray-300">—</span>}
                      </td>
                      <td className="px-4 py-2.5 whitespace-nowrap">
                        <StateChangePill hasBefore={!!e.before} hasAfter={!!e.after} />
                      </td>
                      <td className="px-4 py-2.5">
                        <OutcomeBadge value={e.outcome} />
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
          <PermissionEventDetailPanel
            event={selected}
            onClose={() => setSelected(null)}
          />
        )}
      </div>

      {/* ── Pagination ────────────────────────────────────────────────────── */}
      {totalPages > 1 && (
        <Pagination page={page} totalPages={totalPages} buildHref={paginationHref} />
      )}
    </div>
  );
}

const PAGE_SIZE = 15;

// ── Detail panel ──────────────────────────────────────────────────────────────

function PermissionEventDetailPanel({
  event: e,
  onClose,
}: {
  event:   CanonicalAuditEvent;
  onClose: () => void;
}) {
  return (
    <div className="w-[24rem] shrink-0 rounded-lg border border-gray-200 bg-white shadow-sm overflow-hidden">

      {/* Panel header */}
      <div className="flex items-center justify-between px-4 py-3 border-b border-gray-100 bg-gray-50">
        <div>
          <h3 className="text-sm font-semibold text-gray-700">Permission Change Detail</h3>
          <p className="text-[10px] text-gray-400 font-mono mt-0.5 truncate max-w-[18rem]">{e.eventType}</p>
        </div>
        <button
          onClick={onClose}
          className="text-gray-400 hover:text-gray-700 transition-colors ml-2 shrink-0"
          aria-label="Close"
        >
          <svg className="h-4 w-4" viewBox="0 0 20 20" fill="currentColor">
            <path fillRule="evenodd" d="M4.293 4.293a1 1 0 011.414 0L10 8.586l4.293-4.293a1 1 0 111.414 1.414L11.414 10l4.293 4.293a1 1 0 01-1.414 1.414L10 11.414l-4.293 4.293a1 1 0 01-1.414-1.414L8.586 10 4.293 5.707a1 1 0 010-1.414z" clipRule="evenodd" />
          </svg>
        </button>
      </div>

      <div className="px-4 py-3 space-y-3 overflow-y-auto max-h-[80vh]">

        {/* Timing */}
        <Section label="Timing">
          <Row label="Occurred" mono>{formatUtcFull(e.occurredAtUtc)}</Row>
          <Row label="Ingested"  mono>{formatUtcFull(e.ingestedAtUtc)}</Row>
        </Section>

        {/* Actor */}
        <Section label="Actor (who made the change)">
          {e.actorLabel && <Row label="Name">{e.actorLabel}</Row>}
          {e.actorId    && <Row label="ID" mono>{e.actorId}</Row>}
          {e.actorType  && <Row label="Type">{e.actorType}</Row>}
          {e.ipAddress  && <Row label="IP" mono>{e.ipAddress}</Row>}
          {!e.actorLabel && !e.actorId && (
            <p className="text-[11px] text-gray-400 italic">System / anonymous</p>
          )}
        </Section>

        {/* Entity (who was affected) */}
        {(e.targetType || e.targetId) && (
          <Section label="Entity (who was affected)">
            {e.targetType && <Row label="Type">{e.targetType}</Row>}
            {e.targetId   && <Row label="ID" mono>{e.targetId}</Row>}
          </Section>
        )}

        {/* Tenant scope */}
        {e.tenantId && (
          <Section label="Tenant">
            <Row label="Tenant ID" mono>{e.tenantId}</Row>
          </Section>
        )}

        {/* Event classification */}
        <Section label="Classification">
          <Row label="Severity"><SeverityBadge value={e.severity} /></Row>
          <Row label="Outcome"><OutcomeBadge value={e.outcome} /></Row>
          {e.action && <Row label="Action" mono>{e.action}</Row>}
        </Section>

        {/* Description */}
        {e.description && (
          <Section label="Description">
            <p className="text-[12px] text-gray-700 leading-relaxed">{e.description}</p>
          </Section>
        )}

        {/* Before state */}
        {e.before ? (
          <JsonSection label="Before State" raw={e.before} accentClass="border-orange-200 bg-orange-50" />
        ) : (
          <Section label="Before State">
            <p className="text-[11px] text-gray-400 italic">Not captured — event predates before-state recording or entity was created.</p>
          </Section>
        )}

        {/* After state */}
        {e.after ? (
          <JsonSection label="After State" raw={e.after} accentClass="border-green-200 bg-green-50" />
        ) : (
          <Section label="After State">
            <p className="text-[11px] text-gray-400 italic">Not captured.</p>
          </Section>
        )}

        {/* Metadata */}
        {e.metadata && (
          <JsonSection label="Metadata" raw={e.metadata} accentClass="border-gray-100 bg-gray-50" />
        )}

        {/* Tags */}
        {e.tags && e.tags.length > 0 && (
          <Section label="Tags">
            <div className="flex flex-wrap gap-1 mt-0.5">
              {e.tags.map(tag => (
                <span
                  key={tag}
                  className="inline-flex items-center px-1.5 py-0.5 rounded text-[10px] font-medium bg-gray-100 text-gray-600 border border-gray-200"
                >
                  {tag}
                </span>
              ))}
            </div>
          </Section>
        )}

        {/* Tracing */}
        <Section label="Tracing">
          <Row label="Event ID" mono>{e.id}</Row>
          {e.correlationId && <Row label="Correlation" mono>{e.correlationId}</Row>}
          {e.requestId     && <Row label="Request ID"  mono>{e.requestId}</Row>}
          {e.sessionId     && <Row label="Session ID"  mono>{e.sessionId}</Row>}
          {e.hash          && <Row label="Hash" mono>{e.hash.slice(0, 16)}…</Row>}
        </Section>

        {/* Navigation links */}
        <div className="flex flex-col gap-1.5 pt-1">
          {e.correlationId && (
            <a
              href={`/synqaudit/trace?correlationId=${encodeURIComponent(e.correlationId)}`}
              className="flex items-center gap-1.5 text-[11px] text-indigo-600 hover:text-indigo-800 font-medium transition-colors"
            >
              <svg className="h-3 w-3" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                <path strokeLinecap="round" strokeLinejoin="round" d="M13.828 10.172a4 4 0 00-5.656 0l-4 4a4 4 0 105.656 5.656l1.102-1.101m-.758-4.899a4 4 0 005.656 0l4-4a4 4 0 00-5.656-5.656l-1.1 1.1" />
              </svg>
              Trace this correlation ID
            </a>
          )}
          {e.actorId && (
            <a
              href={`/synqaudit/investigation?actorId=${encodeURIComponent(e.actorId)}`}
              className="flex items-center gap-1.5 text-[11px] text-indigo-600 hover:text-indigo-800 font-medium transition-colors"
            >
              <svg className="h-3 w-3" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                <path strokeLinecap="round" strokeLinejoin="round" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
              </svg>
              All events by this actor
            </a>
          )}
        </div>

      </div>
    </div>
  );
}

// ── State change indicator ─────────────────────────────────────────────────────

function StateChangePill({ hasBefore, hasAfter }: { hasBefore: boolean; hasAfter: boolean }) {
  if (!hasBefore && !hasAfter) {
    return <span className="text-[10px] text-gray-300 italic">none</span>;
  }
  return (
    <span className="inline-flex items-center gap-1">
      {hasBefore && (
        <span className="inline-flex items-center px-1.5 py-0.5 rounded text-[10px] font-medium bg-orange-50 text-orange-700 border border-orange-200">
          before
        </span>
      )}
      {hasAfter && (
        <span className="inline-flex items-center px-1.5 py-0.5 rounded text-[10px] font-medium bg-green-50 text-green-700 border border-green-200">
          after
        </span>
      )}
    </span>
  );
}

// ── Panel sub-components ──────────────────────────────────────────────────────

function Section({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div>
      <p className="text-[10px] font-semibold text-gray-400 uppercase tracking-wider mb-1.5">{label}</p>
      <div className="space-y-0.5">{children}</div>
    </div>
  );
}

function Row({
  label,
  mono = false,
  children,
}: {
  label:    string;
  mono?:    boolean;
  children: React.ReactNode;
}) {
  return (
    <div className="flex justify-between gap-2 text-[11px] py-0.5">
      <span className="text-gray-400 shrink-0">{label}</span>
      <span className={['text-gray-700 text-right break-all', mono ? 'font-mono' : ''].join(' ')}>
        {children}
      </span>
    </div>
  );
}

function JsonSection({
  label,
  raw,
  accentClass,
}: {
  label:       string;
  raw:         string;
  accentClass: string;
}) {
  let formatted = raw;
  try { formatted = JSON.stringify(JSON.parse(raw), null, 2); } catch { }
  return (
    <div>
      <p className="text-[10px] font-semibold text-gray-400 uppercase tracking-wider mb-1.5">{label}</p>
      <pre
        className={[
          'rounded border p-2 text-[10px] font-mono text-gray-700 overflow-auto max-h-52 whitespace-pre-wrap break-all',
          accentClass,
        ].join(' ')}
      >
        {formatted}
      </pre>
    </div>
  );
}

function Chip({ label }: { label: string }) {
  return (
    <span className="inline-flex items-center px-2 py-0.5 rounded-full text-[10px] font-medium bg-indigo-50 text-indigo-700 border border-indigo-200">
      {label}
    </span>
  );
}

// ── Pagination ────────────────────────────────────────────────────────────────

function Pagination({
  page,
  totalPages,
  buildHref,
}: {
  page:      number;
  totalPages: number;
  buildHref:  (p: number) => string;
}) {
  const pages = buildPageRange(page, totalPages);
  return (
    <nav className="flex items-center justify-center gap-1" aria-label="Pagination">
      <PagerLink href={buildHref(page - 1)} disabled={page <= 1}         label="← Prev" />
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

function PagerLink({
  href,
  label,
  active   = false,
  disabled = false,
}: {
  href:     string;
  label:    string;
  active?:  boolean;
  disabled?: boolean;
}) {
  if (disabled) {
    return <span className="px-3 py-1.5 text-xs rounded-md text-gray-300 cursor-not-allowed">{label}</span>;
  }
  return (
    <a
      href={href}
      className={[
        'px-3 py-1.5 text-xs rounded-md font-medium transition-colors',
        active ? 'bg-indigo-600 text-white' : 'text-gray-600 hover:bg-gray-100',
      ].join(' ')}
    >
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
