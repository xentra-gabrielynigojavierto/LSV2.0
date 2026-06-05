import type { IntegrationStatus, MonitoringStatus } from '@/types/control-center';
import type { PublicUptimeBucket } from '@/app/api/monitoring/uptime/route';
import type { SupportedWindow } from '@/lib/uptime-aggregation';
import { AvailabilityBars, AvailabilityLegend } from './availability-bars';
import { PublicLatencySparkline } from './public-latency-sparkline';

interface PublicComponentListProps {
  integrations: IntegrationStatus[];
  /**
   * Optional uptime history keyed by component name (case-sensitive match
   * against IntegrationStatus.name). Omit or pass empty map to hide bars.
   */
  uptimeByName?: Map<string, { uptimePercent: number | null; buckets: PublicUptimeBucket[] }>;
  /** Total bars to render (24 for 24h, 7 for 7d, 30 for 30d). Default 24. */
  totalBars?:    number;
  /** Selected time window — used in the section subtitle label. */
  window?:       SupportedWindow;
  /**
   * When true the monitoring service could not be reached and availability
   * history is absent. A notice is shown instead of silently hiding bars.
   */
  monitoringUnavailable?: boolean;
}

const STATUS_ORDER: Record<MonitoringStatus, number> = { Down: 0, Degraded: 1, Healthy: 2 };

const STATUS_CONFIG: Record<MonitoringStatus, {
  dot:   string;
  badge: string;
  label: string;
}> = {
  Healthy:  { dot: 'bg-green-500',  badge: 'bg-green-50  text-green-700  ring-green-200',  label: 'Operational' },
  Degraded: { dot: 'bg-amber-400',  badge: 'bg-amber-50  text-amber-700  ring-amber-200',  label: 'Degraded'    },
  Down:     { dot: 'bg-red-600',    badge: 'bg-red-50    text-red-700    ring-red-200',    label: 'Outage'      },
};

const WINDOW_LABELS: Record<SupportedWindow, string> = {
  '24h': 'last 24 hours',
  '7d':  'last 7 days',
  '30d': 'last 30 days',
};

/**
 * PublicComponentList — simplified, read-only list of monitored service statuses.
 *
 * Designed for the public /status page. Shows only:
 *   - component name (safe display name, not internal ID)
 *   - status (Healthy / Degraded / Down) via an external-friendly label
 *   - last checked timestamp
 *   - optional availability bar strip (when uptimeByName is provided)
 *   - optional latency sparkline (when buckets contain avgLatencyMs)
 *
 * Does NOT expose:
 *   - internal entity IDs
 *   - maxLatencyMs or raw check counts
 *   - admin controls
 *   - filter controls
 */
export function PublicComponentList({ integrations, uptimeByName, totalBars = 24, window = '24h', monitoringUnavailable }: PublicComponentListProps) {
  if (integrations.length === 0) {
    return (
      <Section title="Components">
        <EmptyState message="No components are currently being monitored." />
      </Section>
    );
  }

  const sorted = [...integrations].sort((a, b) => {
    const diff = STATUS_ORDER[a.status] - STATUS_ORDER[b.status];
    return diff !== 0 ? diff : a.name.localeCompare(b.name);
  });

  const showBars = uptimeByName && uptimeByName.size > 0;
  const windowLabel = WINDOW_LABELS[window] ?? WINDOW_LABELS['24h'];

  return (
    <Section
      title="Components"
      subtitle={`${integrations.length} service${integrations.length !== 1 ? 's' : ''} monitored · ${windowLabel}`}
    >
      {monitoringUnavailable && (
        <div className="px-5 py-2.5 border-b border-amber-100 bg-amber-50 flex items-center gap-2">
          <span className="h-1.5 w-1.5 rounded-full bg-amber-400 shrink-0" />
          <p className="text-xs text-amber-700">
            Availability history is temporarily unavailable. Current status is shown below.
          </p>
        </div>
      )}
      <div className="divide-y divide-gray-100">
        {sorted.map(item => (
          <ComponentRow
            key={item.name}
            item={item}
            uptime={uptimeByName?.get(item.name)}
            totalBars={totalBars}
          />
        ))}
      </div>
      {showBars && <AvailabilityLegend />}
    </Section>
  );
}

// ── ComponentRow ───────────────────────────────────────────────────────────────

function ComponentRow({
  item,
  uptime,
  totalBars,
}: {
  item:      IntegrationStatus;
  uptime?:   { uptimePercent: number | null; buckets: PublicUptimeBucket[] };
  totalBars: number;
}) {
  const cfg     = STATUS_CONFIG[item.status];
  const checked = formatTimestamp(item.lastCheckedAtUtc);

  const hasLatency = uptime?.buckets.some(
    b => b.avgLatencyMs !== null && !b.insufficientData,
  ) ?? false;

  return (
    <div className="px-5 py-3">
      {/* Top row: dot, name, timestamp, badge */}
      <div className="flex items-center gap-3">
        <span className={`h-2 w-2 rounded-full shrink-0 ${cfg.dot}`} />

        <span className="flex-1 text-sm text-gray-900 truncate min-w-0">
          {item.name}
        </span>

        <span className="text-xs text-gray-400 hidden sm:block shrink-0">
          {checked}
        </span>

        <span className={`inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium ring-1 ring-inset shrink-0 ${cfg.badge}`}>
          {cfg.label}
        </span>
      </div>

      {/* Availability bars row (only when data is present) */}
      {uptime && uptime.buckets.length > 0 && (
        <div className="mt-2 ml-5">
          <AvailabilityBars
            buckets={uptime.buckets}
            uptimePercent={uptime.uptimePercent}
            totalBars={totalBars}
          />
        </div>
      )}

      {/* Fallback: bars requested but no bucket data yet */}
      {uptime && uptime.buckets.length === 0 && (
        <div className="mt-1.5 ml-5">
          <p className="text-[11px] text-gray-400">History unavailable</p>
        </div>
      )}

      {/* Latency sparkline — only when at least one bucket has valid avgLatencyMs */}
      {uptime && uptime.buckets.length > 0 && hasLatency && (
        <div className="mt-2 ml-5">
          <PublicLatencySparkline buckets={uptime.buckets} />
        </div>
      )}
    </div>
  );
}

// ── Section wrapper ────────────────────────────────────────────────────────────

function Section({
  title,
  subtitle,
  children,
}: {
  title:     string;
  subtitle?: string;
  children:  React.ReactNode;
}) {
  return (
    <div className="bg-white border border-gray-200 rounded-xl overflow-hidden">
      <div className="px-5 py-3.5 border-b border-gray-100 bg-gray-50">
        <h2 className="text-xs font-semibold uppercase tracking-wide text-gray-500">
          {title}
        </h2>
        {subtitle && (
          <p className="text-[11px] text-gray-400 mt-0.5">{subtitle}</p>
        )}
      </div>
      {children}
    </div>
  );
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
    return 'Unavailable';
  }
}
