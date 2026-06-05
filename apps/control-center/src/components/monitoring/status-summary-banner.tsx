'use client';

import type { MonitoringStatus } from '@/types/control-center';
import type { StatusFilter } from './monitoring-filter-section';

interface StatusSummaryBannerProps {
  systemStatus:    MonitoringStatus;
  total:           number;
  healthy:         number;
  degraded:        number;
  down:            number;
  alerts:          number;
  lastCheckedAt:   string;
  statusFilter?:   StatusFilter;
  onFilterChange?: (f: StatusFilter) => void;
}

const STATUS_STYLES: Record<MonitoringStatus, {
  border: string;
  bg:     string;
  dot:    string;
  text:   string;
  label:  string;
}> = {
  Healthy:  { border: 'border-green-200', bg: 'bg-green-50',  dot: 'bg-green-500', text: 'text-green-700',  label: 'Healthy'  },
  Degraded: { border: 'border-amber-200', bg: 'bg-amber-50',  dot: 'bg-amber-500', text: 'text-amber-700',  label: 'Degraded' },
  Down:     { border: 'border-red-200',   bg: 'bg-red-50',    dot: 'bg-red-600',   text: 'text-red-700',    label: 'Down'     },
};

/**
 * StatusSummaryBanner — compact horizontal status strip.
 *
 * Shows overall system status + per-level service counts + active alert count.
 * When statusFilter + onFilterChange are provided the count pills become
 * clickable filter controls. Clicking an active filter resets to 'all'.
 */
export function StatusSummaryBanner({
  systemStatus,
  total,
  healthy,
  degraded,
  down,
  alerts,
  lastCheckedAt,
  statusFilter,
  onFilterChange,
}: StatusSummaryBannerProps) {
  const cfg      = STATUS_STYLES[systemStatus];
  const since    = formatTime(lastCheckedAt);
  const isActive = (f: StatusFilter) => statusFilter === f;
  const toggle   = (f: StatusFilter) => onFilterChange?.(statusFilter === f ? 'all' : f);
  const interactive = !!onFilterChange;

  return (
    <div
      role="status"
      aria-label={`Platform status: ${cfg.label}`}
      className={`rounded-lg border ${cfg.border} ${cfg.bg} px-4 py-3 flex flex-wrap items-center gap-x-5 gap-y-2`}
    >
      {/* Status indicator */}
      <div className="flex items-center gap-2 shrink-0">
        <span className={`h-2.5 w-2.5 rounded-full shrink-0 ${cfg.dot}`} />
        <span className={`text-sm font-semibold ${cfg.text}`}>{cfg.label}</span>
      </div>

      {/* Vertical divider */}
      <span className="h-4 w-px bg-gray-300 hidden sm:block shrink-0" />

      {/* Service counts */}
      <div className="flex items-center gap-4 flex-wrap">

        <StatPill
          value={total}
          label="total"
          color="text-gray-700"
          interactive={false}
        />

        <StatPill
          value={healthy}
          label="healthy"
          color="text-green-700"
          interactive={interactive}
          active={isActive('healthy')}
          activeRing="ring-green-400"
          onClick={() => toggle('healthy')}
        />

        {degraded > 0 && (
          <StatPill
            value={degraded}
            label="degraded"
            color="text-amber-700"
            interactive={interactive}
            active={isActive('degraded')}
            activeRing="ring-amber-400"
            onClick={() => toggle('degraded')}
          />
        )}

        {down > 0 && (
          <StatPill
            value={down}
            label="down"
            color="text-red-700"
            bold
            interactive={interactive}
            active={isActive('down')}
            activeRing="ring-red-400"
            onClick={() => toggle('down')}
          />
        )}

        {alerts > 0 && (
          <>
            <span className="h-3 w-px bg-gray-300 hidden sm:block shrink-0" />
            <StatPill
              value={alerts}
              label={alerts === 1 ? 'alert' : 'alerts'}
              color="text-red-700"
              bold
              interactive={interactive}
              active={isActive('alerts')}
              activeRing="ring-red-400"
              onClick={() => toggle('alerts')}
            />
          </>
        )}

      </div>

      {/* Spacer */}
      <span className="flex-1" />

      {/* Last checked */}
      <span className="text-xs text-gray-500 tabular-nums shrink-0">
        Checked {since}
      </span>
    </div>
  );
}

// ── Error fallback ─────────────────────────────────────────────────────────────

/**
 * Shown when the monitoring summary cannot be loaded.
 * Prevents a blank/crashed page from confusing operators.
 */
export function StatusSummaryBannerError() {
  return (
    <div
      role="status"
      aria-label="Monitoring unavailable"
      className="rounded-lg border border-gray-200 bg-gray-50 px-4 py-3 flex items-center gap-3"
    >
      <span className="h-2.5 w-2.5 rounded-full bg-gray-400 shrink-0" />
      <span className="text-sm font-medium text-gray-500">Monitoring unavailable</span>
    </div>
  );
}

// ── Sub-components ─────────────────────────────────────────────────────────────

function StatPill({
  value,
  label,
  color,
  bold,
  interactive,
  active,
  activeRing,
  onClick,
}: {
  value:        number;
  label:        string;
  color:        string;
  bold?:        boolean;
  interactive?: boolean;
  active?:      boolean;
  activeRing?:  string;
  onClick?:     () => void;
}) {
  const base = `text-xs tabular-nums ${color} ${bold ? 'font-semibold' : ''}`;

  if (!interactive) {
    return (
      <span className={base}>
        <span className={`text-sm font-semibold ${color}`}>{value}</span>
        {' '}{label}
      </span>
    );
  }

  return (
    <button
      type="button"
      onClick={onClick}
      aria-pressed={active}
      className={[
        'inline-flex items-center gap-1 rounded-md px-1.5 py-0.5',
        'transition-all focus:outline-none focus-visible:ring-2',
        active
          ? `bg-white/70 ring-2 ${activeRing} shadow-sm`
          : 'hover:bg-white/50',
        base,
      ].join(' ')}
    >
      <span className={`text-sm font-semibold ${color}`}>{value}</span>
      {' '}{label}
    </button>
  );
}

// ── helpers ────────────────────────────────────────────────────────────────────

function formatTime(iso: string): string {
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
