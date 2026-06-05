'use client';

import { useCallback, useState } from 'react';
import type { IntegrationStatus, MonitoringStatus, SystemAlert } from '@/types/control-center';
import type { StatusFilter } from './monitoring-filter-section';
import type { InternalLatencyResponse, LatencyBucket } from '@/app/api/monitoring/latency/route';
import type { SupportedWindow } from '@/lib/uptime-aggregation';
import { StatusBadge } from './system-health-card';
import { LatencySparkline } from './latency-sparkline';

interface ComponentStatusListProps {
  integrations:            IntegrationStatus[];
  alerts?:                 SystemAlert[];
  externalFilter?:         StatusFilter;
  onExternalFilterChange?: (f: StatusFilter) => void;
}

type FilterValue = 'All' | MonitoringStatus | 'Alerts';

const FILTERS: FilterValue[] = ['All', 'Healthy', 'Degraded', 'Down', 'Alerts'];

const STATUS_ORDER: Record<MonitoringStatus, number> = { Down: 0, Degraded: 1, Healthy: 2 };

const CATEGORY_LABELS: Record<string, string> = {
  infrastructure: 'Infra',
  product:        'Product',
};

// Map shared StatusFilter (lowercase) to internal FilterValue (capitalized)
function toFilterValue(f: StatusFilter): FilterValue {
  switch (f) {
    case 'healthy':  return 'Healthy';
    case 'degraded': return 'Degraded';
    case 'down':     return 'Down';
    case 'alerts':   return 'Alerts';
    default:         return 'All';
  }
}

// Map internal FilterValue back to shared StatusFilter
function toStatusFilter(f: FilterValue): StatusFilter {
  switch (f) {
    case 'Healthy':  return 'healthy';
    case 'Degraded': return 'degraded';
    case 'Down':     return 'down';
    case 'Alerts':   return 'alerts';
    default:         return 'all';
  }
}

// ── Latency data cache (per-component name → buckets) ─────────────────────────

type LatencyCache = Map<string, LatencyBucket[]>;
type LatencyFetchState = 'idle' | 'loading' | 'done' | 'error';

const WINDOW_OPTIONS: { value: SupportedWindow; label: string }[] = [
  { value: '24h', label: '24h' },
  { value: '7d',  label: '7d'  },
  { value: '30d', label: '30d' },
];

const WINDOW_LABELS: Record<SupportedWindow, string> = {
  '24h': 'last 24 hours',
  '7d':  'last 7 days',
  '30d': 'last 30 days',
};

/**
 * ComponentStatusList — unified, filterable list of all monitored entities.
 *
 * Replaces the two split IntegrationStatusTable cards (Platform Services / Products).
 * Client component: filter state is local; no additional API calls are made.
 * Data comes directly from MonitoringSummary.integrations — no status recomputation.
 *
 * When externalFilter + onExternalFilterChange are provided the component operates
 * in controlled mode: the banner drives the filter and the internal buttons stay
 * in sync. Without them the component is fully self-contained (backward-compatible).
 *
 * MON-INT-04-004: Each row can be expanded to reveal a latency sparkline.
 * MON-INT-05-001: Window selector (24h / 7d / 30d) drives the sparkline data.
 *   Latency data is fetched once per window selection from /api/monitoring/latency
 *   and shared across all rows.
 */
export function ComponentStatusList({
  integrations,
  alerts = [],
  externalFilter,
  onExternalFilterChange,
}: ComponentStatusListProps) {
  const [internalFilter, setInternalFilter] = useState<FilterValue>('All');
  const [expandedNames,  setExpandedNames]  = useState<Set<string>>(new Set());

  // Latency state — scoped to selected window
  const [selectedWindow, setSelectedWindow] = useState<SupportedWindow>('24h');
  const [latencyCache,   setLatencyCache]   = useState<LatencyCache>(new Map());
  const [latencyState,   setLatencyState]   = useState<LatencyFetchState>('idle');

  const controlled     = externalFilter !== undefined && !!onExternalFilterChange;
  const activeFilter   = controlled ? toFilterValue(externalFilter!) : internalFilter;

  function handleFilterClick(f: FilterValue) {
    const sf = toStatusFilter(f);
    if (controlled) {
      onExternalFilterChange!(activeFilter === f ? 'all' : sf);
    } else {
      setInternalFilter(activeFilter === f ? 'All' : f);
    }
  }

  // Fetch the full latency payload for a given window (called once per window)
  const fetchLatency = useCallback(async (window: SupportedWindow) => {
    setLatencyState('loading');
    setLatencyCache(new Map());
    try {
      const res = await fetch(`/api/monitoring/latency?window=${window}`, { cache: 'no-store' });
      if (!res.ok) throw new Error(`HTTP ${res.status}`);
      const data: InternalLatencyResponse = await res.json();
      const cache: LatencyCache = new Map(
        data.components.map(c => [c.name, c.buckets])
      );
      setLatencyCache(cache);
      setLatencyState('done');
    } catch {
      setLatencyState('error');
    }
  }, []);

  function handleWindowChange(window: SupportedWindow) {
    if (window === selectedWindow) return;
    setSelectedWindow(window);
    // Only re-fetch if there are expanded rows
    if (expandedNames.size > 0) {
      fetchLatency(window);
    } else {
      // Reset so next expansion fetches fresh data
      setLatencyState('idle');
      setLatencyCache(new Map());
    }
  }

  function toggleExpanded(name: string) {
    setExpandedNames(prev => {
      const next = new Set(prev);
      if (next.has(name)) {
        next.delete(name);
      } else {
        next.add(name);
        // Trigger fetch on first expansion (or if window changed since last fetch)
        if (latencyState === 'idle') {
          fetchLatency(selectedWindow);
        }
      }
      return next;
    });
  }

  // Build set of entity names that have active alerts (for 'Alerts' filter)
  const alertEntityNames = new Set(
    alerts.filter(a => !a.resolvedAtUtc && a.entityName).map(a => a.entityName!)
  );

  const sorted = [...integrations].sort((a, b) => {
    const diff = STATUS_ORDER[a.status] - STATUS_ORDER[b.status];
    return diff !== 0 ? diff : a.name.localeCompare(b.name);
  });

  const visible =
    activeFilter === 'All'     ? sorted :
    activeFilter === 'Alerts'  ? sorted.filter(i => alertEntityNames.has(i.name)) :
                                 sorted.filter(i => i.status === activeFilter);

  const hasCategory = integrations.some(i => i.category);

  // Per-filter counts for the filter buttons
  const counts: Record<FilterValue, number> = {
    All:      integrations.length,
    Healthy:  integrations.filter(i => i.status === 'Healthy').length,
    Degraded: integrations.filter(i => i.status === 'Degraded').length,
    Down:     integrations.filter(i => i.status === 'Down').length,
    Alerts:   alertEntityNames.size,
  };

  function emptyMessage(): string {
    if (activeFilter === 'Alerts') return 'No active alerts.';
    return `No ${activeFilter.toLowerCase()} components.`;
  }

  return (
    <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">

      {/* Header */}
      <div className="px-5 py-3.5 border-b border-gray-100 bg-gray-50 flex flex-wrap items-center gap-3">
        <div className="flex-1 min-w-0">
          <h2 className="text-xs font-semibold uppercase tracking-wide text-gray-500">
            All Components
          </h2>
          <p className="text-[11px] text-gray-400 mt-0.5">
            {counts.Healthy} / {counts.All} healthy
            {activeFilter !== 'All' && ` · showing ${visible.length} ${activeFilter.toLowerCase()}`}
          </p>
        </div>

        {/* Filter buttons */}
        <div className="flex items-center gap-1.5 flex-wrap shrink-0" role="group" aria-label="Filter by status">
          {FILTERS.map(f => {
            if (f === 'Alerts' && counts.Alerts === 0) return null;
            return (
              <FilterButton
                key={f}
                label={f}
                count={counts[f]}
                active={activeFilter === f}
                onClick={() => handleFilterClick(f)}
              />
            );
          })}
        </div>
      </div>

      {/* Rows */}
      {integrations.length === 0 ? (
        <EmptyState message="No monitored components." />
      ) : visible.length === 0 ? (
        <EmptyState message={emptyMessage()} />
      ) : (
        <div className="divide-y divide-gray-100">
          {visible.map(item => (
            <ComponentRow
              key={item.name}
              item={item}
              showCategory={hasCategory}
              expanded={expandedNames.has(item.name)}
              onToggle={() => toggleExpanded(item.name)}
              latencyBuckets={latencyCache.get(item.name) ?? null}
              latencyState={latencyState}
              selectedWindow={selectedWindow}
              onWindowChange={handleWindowChange}
            />
          ))}
        </div>
      )}
    </div>
  );
}

// ── ComponentRow ───────────────────────────────────────────────────────────────

function ComponentRow({
  item,
  showCategory,
  expanded,
  onToggle,
  latencyBuckets,
  latencyState,
  selectedWindow,
  onWindowChange,
}: {
  item:            IntegrationStatus;
  showCategory:    boolean;
  expanded:        boolean;
  onToggle:        () => void;
  latencyBuckets:  LatencyBucket[] | null;
  latencyState:    LatencyFetchState;
  selectedWindow:  SupportedWindow;
  onWindowChange:  (w: SupportedWindow) => void;
}) {
  const latency = item.latencyMs !== undefined ? `${item.latencyMs} ms` : '—';
  const latencyColor =
    item.latencyMs === undefined ? 'text-gray-400'             :
    item.latencyMs > 1000        ? 'text-red-600 font-semibold' :
    item.latencyMs > 400         ? 'text-amber-600 font-semibold' :
                                   'text-gray-700';

  const checked  = formatTimestamp(item.lastCheckedAtUtc);
  const catLabel = item.category ? (CATEGORY_LABELS[item.category] ?? item.category) : null;

  return (
    <div>
      {/* Main row */}
      <div className="flex items-center gap-3 px-5 py-3.5">

        {/* Expand/collapse toggle */}
        <button
          type="button"
          onClick={onToggle}
          aria-label={expanded ? `Collapse ${item.name} latency chart` : `Expand ${item.name} latency chart`}
          aria-expanded={expanded}
          className="shrink-0 w-5 h-5 flex items-center justify-center rounded text-gray-300 hover:text-gray-500 hover:bg-gray-100 transition-colors"
        >
          <svg
            className={`w-3 h-3 transition-transform duration-150 ${expanded ? 'rotate-90' : ''}`}
            viewBox="0 0 12 12"
            fill="none"
            aria-hidden="true"
          >
            <path d="M4 2.5L7.5 6 4 9.5" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" />
          </svg>
        </button>

        {/* Status dot */}
        <StatusDot status={item.status} />

        {/* Name */}
        <span className="flex-1 text-sm font-medium text-gray-900 truncate min-w-0">
          {item.name}
        </span>

        {/* Category chip (only if any row has a category) */}
        {showCategory && (
          <span className="text-[10px] font-medium uppercase tracking-wide text-gray-400 w-14 text-right hidden md:block shrink-0">
            {catLabel ?? '—'}
          </span>
        )}

        {/* Latency */}
        <span className={`text-xs tabular-nums w-20 text-right shrink-0 ${latencyColor}`}>
          {latency}
        </span>

        {/* Last checked */}
        <span className="text-xs text-gray-400 w-24 text-right hidden sm:block shrink-0">
          {checked}
        </span>

        {/* Badge */}
        <div className="w-20 flex justify-end shrink-0">
          <StatusBadge status={item.status} />
        </div>
      </div>

      {/* Expandable latency panel */}
      {expanded && (
        <div className="px-8 pb-4 pt-2 bg-gray-50 border-t border-gray-100">
          {/* Panel header: title + window selector */}
          <div className="flex items-center justify-between mb-2 flex-wrap gap-2">
            <p className="text-[10px] font-semibold uppercase tracking-wide text-gray-400">
              Response Time · {WINDOW_LABELS[selectedWindow]}
            </p>
            {/* Window selector — inline pill buttons */}
            <div
              className="inline-flex items-center gap-px rounded-md border border-gray-200 bg-white overflow-hidden"
              role="group"
              aria-label="Select latency window"
            >
              {WINDOW_OPTIONS.map(({ value, label }) => {
                const isActive = value === selectedWindow;
                return (
                  <button
                    key={value}
                    type="button"
                    onClick={() => onWindowChange(value)}
                    aria-pressed={isActive}
                    className={`px-2.5 py-1 text-[11px] font-medium transition-colors ${
                      isActive
                        ? 'bg-indigo-600 text-white'
                        : 'text-gray-500 hover:bg-gray-50'
                    }`}
                  >
                    {label}
                  </button>
                );
              })}
            </div>
          </div>

          <LatencySparkline
            buckets={latencyBuckets ?? []}
            loading={latencyState === 'loading'}
            error={latencyState === 'error' ? 'Latency unavailable' : null}
          />
        </div>
      )}
    </div>
  );
}

// ── FilterButton ───────────────────────────────────────────────────────────────

function FilterButton({
  label,
  count,
  active,
  onClick,
}: {
  label:   FilterValue;
  count:   number;
  active:  boolean;
  onClick: () => void;
}) {
  const activeStyles: Record<FilterValue, string> = {
    All:      'bg-gray-700  text-white  border-gray-700',
    Healthy:  'bg-green-600 text-white  border-green-600',
    Degraded: 'bg-amber-500 text-white  border-amber-500',
    Down:     'bg-red-600   text-white  border-red-600',
    Alerts:   'bg-red-600   text-white  border-red-600',
  };
  const idleStyles: Record<FilterValue, string> = {
    All:      'bg-white text-gray-600 border-gray-200 hover:bg-gray-50',
    Healthy:  'bg-white text-green-700 border-gray-200 hover:bg-green-50',
    Degraded: 'bg-white text-amber-700 border-gray-200 hover:bg-amber-50',
    Down:     'bg-white text-red-700   border-gray-200 hover:bg-red-50',
    Alerts:   'bg-white text-red-700   border-gray-200 hover:bg-red-50',
  };

  return (
    <button
      type="button"
      onClick={onClick}
      aria-pressed={active}
      className={`inline-flex items-center gap-1 px-2.5 py-1 rounded-md text-xs font-medium border transition-colors ${active ? activeStyles[label] : idleStyles[label]}`}
    >
      {label}
      <span className={`tabular-nums ${active ? 'opacity-80' : 'opacity-60'}`}>
        {count}
      </span>
    </button>
  );
}

// ── StatusDot ──────────────────────────────────────────────────────────────────

function StatusDot({ status }: { status: MonitoringStatus }) {
  const colors: Record<MonitoringStatus, string> = {
    Healthy:  'bg-green-500',
    Degraded: 'bg-amber-400',
    Down:     'bg-red-600',
  };
  return <span className={`h-2 w-2 rounded-full shrink-0 ${colors[status]}`} />;
}

// ── EmptyState ─────────────────────────────────────────────────────────────────

function EmptyState({ message }: { message: string }) {
  return (
    <div className="px-5 py-8 text-center">
      <p className="text-sm text-gray-400">{message}</p>
    </div>
  );
}

// ── helpers ────────────────────────────────────────────────────────────────────

function formatTimestamp(iso: string): string {
  try {
    return new Date(iso).toLocaleTimeString('en-US', {
      hour:         '2-digit',
      minute:       '2-digit',
      second:       '2-digit',
      hour12:       false,
      timeZone:     'UTC',
      timeZoneName: 'short',
    });
  } catch {
    return iso;
  }
}
