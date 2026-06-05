/**
 * AvailabilityBars — 24 hourly color-coded bars visualizing uptime history.
 *
 * Designed for the public /status page. Each bar represents one UTC hour
 * bucket from the last 24 hours.
 *
 * Color mapping:
 *   Healthy          → green
 *   Degraded         → amber
 *   Down             → red
 *   Unknown / empty  → gray (insufficient data or no check results for that hour)
 *
 * Public-safe: receives only sanitized bucket data with no internal IDs.
 * No hover-only interactions — visual meaning is conveyed by color + legend.
 */

import type { PublicUptimeBucket } from '@/app/api/monitoring/uptime/route';

interface AvailabilityBarsProps {
  buckets:         PublicUptimeBucket[];
  uptimePercent:   number | null;
  /** Total expected bars (fills remaining with gray). Default 24. */
  totalBars?:      number;
}

const STATUS_BAR: Record<string, string> = {
  Healthy:  'bg-green-400',
  Degraded: 'bg-amber-400',
  Down:     'bg-red-500',
  Unknown:  'bg-gray-200',
};

/** A single bar — minimal tooltip via native title attribute. */
function Bar({ bucket }: { bucket: PublicUptimeBucket }) {
  const color = STATUS_BAR[bucket.dominantStatus] ?? 'bg-gray-200';
  const label =
    bucket.insufficientData
      ? 'No data'
      : `${bucket.dominantStatus} · ${bucket.uptimePercent.toFixed(1)}%`;

  const hour = (() => {
    try {
      return new Date(bucket.bucketStartUtc).toLocaleTimeString('en-US', {
        hour:     '2-digit',
        minute:   '2-digit',
        hour12:   false,
        timeZone: 'UTC',
      });
    } catch {
      return '';
    }
  })();

  return (
    <span
      className={`block h-full w-full rounded-[1px] ${color}`}
      title={hour ? `${hour} UTC — ${label}` : label}
      aria-label={label}
    />
  );
}

/** A placeholder bar (no data for that hour). */
function EmptyBar() {
  return (
    <span
      className="block h-full w-full rounded-[1px] bg-gray-200"
      title="No data"
      aria-label="No data"
    />
  );
}

export function AvailabilityBars({
  buckets,
  uptimePercent,
  totalBars = 24,
}: AvailabilityBarsProps) {
  const sortedBuckets = [...buckets].sort(
    (a, b) => new Date(a.bucketStartUtc).getTime() - new Date(b.bucketStartUtc).getTime(),
  );

  const emptyCount = Math.max(0, totalBars - sortedBuckets.length);

  return (
    <div className="flex flex-col gap-0.5">
      {/* Bar strip */}
      <div
        className="flex items-end gap-px h-5"
        aria-label="24-hour availability history"
        role="img"
      >
        {/* Leading empty bars (hours before check history started) */}
        {Array.from({ length: emptyCount }, (_, i) => (
          <span key={`empty-${i}`} className="flex-1 h-full">
            <EmptyBar />
          </span>
        ))}

        {sortedBuckets.map((bucket, i) => (
          <span key={bucket.bucketStartUtc ?? i} className="flex-1 h-full">
            <Bar bucket={bucket} />
          </span>
        ))}
      </div>

      {/* Uptime % label */}
      <div className="flex items-center justify-between">
        <span className="text-[10px] text-gray-400 leading-none">24 h</span>
        {uptimePercent !== null ? (
          <span className="text-[10px] text-gray-500 leading-none tabular-nums">
            {uptimePercent.toFixed(1)}%
          </span>
        ) : (
          <span className="text-[10px] text-gray-400 leading-none">—</span>
        )}
      </div>
    </div>
  );
}

/** Legend to place once at the bottom of the component section. */
export function AvailabilityLegend() {
  const items = [
    { color: 'bg-green-400', label: 'Operational'        },
    { color: 'bg-amber-400', label: 'Degraded'           },
    { color: 'bg-red-500',   label: 'Outage'             },
    { color: 'bg-gray-200',  label: 'No data'            },
  ];

  return (
    <div className="px-5 py-2.5 border-t border-gray-100 bg-gray-50 flex items-center gap-4 flex-wrap">
      {items.map(({ color, label }) => (
        <span key={label} className="flex items-center gap-1.5">
          <span className={`inline-block h-2.5 w-2.5 rounded-[1px] shrink-0 ${color}`} />
          <span className="text-[11px] text-gray-500">{label}</span>
        </span>
      ))}
    </div>
  );
}
