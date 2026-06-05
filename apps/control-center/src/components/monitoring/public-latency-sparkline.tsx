/**
 * PublicLatencySparkline — lightweight SVG sparkline for public /status page.
 *
 * Renders an avg-latency trend line from per-bucket data.
 *
 * Design constraints:
 *   - Pure SVG — zero charting library dependency.
 *   - No axis labels, no heavy legend (per spec).
 *   - No maxLatencyMs reference (not exposed publicly).
 *   - Null / insufficient-data buckets appear as gaps (no fabrication).
 *   - Responsive width via viewBox; fixed height (default 32px).
 *
 * Public-safe:
 *   - Receives only pre-sanitized avgLatencyMs values from the uptime BFF.
 *   - No entityId, no raw check counts, no internal URLs.
 *
 * States:
 *   empty   — no buckets with valid avgLatencyMs → fallback text.
 *   partial — some null buckets → segments with gaps rendered.
 *   full    — continuous line rendered.
 */

'use client';

export interface PublicLatencyBucket {
  bucketStartUtc:   string;
  avgLatencyMs:     number | null;
  insufficientData: boolean;
}

// ── Colour ─────────────────────────────────────────────────────────────────────

const LINE_COLOR = '#6366f1'; // indigo-500
const FILL_COLOR = '#6366f118';
const BASE_COLOR = '#e5e7eb'; // gray-200

// ── Helpers ───────────────────────────────────────────────────────────────────

function formatMs(ms: number): string {
  if (ms >= 1000) return `${(ms / 1000).toFixed(1)}s`;
  return `${Math.round(ms)}ms`;
}

function formatLabel(iso: string): string {
  try {
    return new Date(iso + (iso.endsWith('Z') ? '' : 'Z'))
      .toLocaleTimeString('en-US', {
        hour:     '2-digit',
        minute:   '2-digit',
        hour12:   false,
        timeZone: 'UTC',
      }) + ' UTC';
  } catch {
    return '';
  }
}

// ── Props ──────────────────────────────────────────────────────────────────────

interface PublicLatencySparklineProps {
  buckets: PublicLatencyBucket[];
  /** Chart height in px. Default 32. */
  height?: number;
}

// ── Component ──────────────────────────────────────────────────────────────────

export function PublicLatencySparkline({
  buckets,
  height = 32,
}: PublicLatencySparklineProps) {
  const validBuckets = buckets.filter(
    b => b.avgLatencyMs !== null && !b.insufficientData,
  );

  if (validBuckets.length === 0) {
    return (
      <p className="text-[10px] text-gray-400 leading-none" role="status">
        No response time history
      </p>
    );
  }

  // Sort chronologically
  const sorted = [...buckets].sort(
    (a, b) => new Date(a.bucketStartUtc).getTime() - new Date(b.bucketStartUtc).getTime(),
  );

  const total   = sorted.length;
  const allAvg  = sorted.map(b => b.avgLatencyMs).filter((v): v is number => v !== null);
  const dataMax = Math.max(...allAvg);
  const dataMin = 0;

  const VW     = 240;
  const VH     = height;
  const PAD_T  = 2;
  const PAD_B  = 2;
  const chartH = VH - PAD_T - PAD_B;

  function xOf(i: number): number {
    if (total <= 1) return VW / 2;
    return (i / (total - 1)) * VW;
  }

  function yOf(ms: number | null): number | null {
    if (ms === null) return null;
    if (dataMax === dataMin) return PAD_T + chartH / 2;
    return PAD_T + (1 - (ms - dataMin) / (dataMax - dataMin)) * chartH;
  }

  // Build line segments — gaps where bucket is null/insufficient
  const segments: Array<Array<{ x: number; y: number }>> = [];
  let current: Array<{ x: number; y: number }> = [];

  sorted.forEach((b, i) => {
    const y = yOf(b.avgLatencyMs);
    if (y === null || b.insufficientData) {
      if (current.length > 0) { segments.push(current); current = []; }
    } else {
      current.push({ x: xOf(i), y });
    }
  });
  if (current.length > 0) segments.push(current);

  // Filled area under the first contiguous segment
  const allPoints = segments.flat();
  const fillPath =
    allPoints.length >= 2
      ? [
          ...allPoints.map((p, i) => `${i === 0 ? 'M' : 'L'}${p.x.toFixed(1)},${p.y.toFixed(1)}`),
          `L${allPoints[allPoints.length - 1].x.toFixed(1)},${(PAD_T + chartH).toFixed(1)}`,
          `L${allPoints[0].x.toFixed(1)},${(PAD_T + chartH).toFixed(1)}`,
          'Z',
        ].join(' ')
      : '';

  return (
    <div className="w-full" aria-label="Response time trend">
      <svg
        viewBox={`0 0 ${VW} ${VH}`}
        width="100%"
        height={VH}
        preserveAspectRatio="none"
        role="img"
        aria-label="Average response time trend"
      >
        {/* Baseline */}
        <line
          x1={0} y1={PAD_T + chartH}
          x2={VW} y2={PAD_T + chartH}
          stroke={BASE_COLOR}
          strokeWidth="0.5"
        />

        {/* Filled area */}
        {fillPath && <path d={fillPath} fill={FILL_COLOR} />}

        {/* Line segments */}
        {segments.map((seg, si) =>
          seg.length >= 2 ? (
            <polyline
              key={si}
              points={seg.map(p => `${p.x.toFixed(1)},${p.y.toFixed(1)}`).join(' ')}
              fill="none"
              stroke={LINE_COLOR}
              strokeWidth="1.5"
              strokeLinejoin="round"
              strokeLinecap="round"
            />
          ) : seg.length === 1 ? (
            <circle key={si} cx={seg[0].x} cy={seg[0].y} r="1.5" fill={LINE_COLOR} />
          ) : null,
        )}

        {/* Invisible hit targets with native SVG tooltip */}
        {sorted.map((b, i) => {
          const y = yOf(b.avgLatencyMs);
          if (y === null) return null;
          const label = b.insufficientData
            ? `${formatLabel(b.bucketStartUtc)} — No data`
            : `${formatLabel(b.bucketStartUtc)} — ${formatMs(b.avgLatencyMs!)} avg`;
          const colW = VW / total;
          return (
            <rect
              key={i}
              x={xOf(i) - colW / 2}
              y={PAD_T}
              width={colW}
              height={chartH}
              fill="transparent"
            >
              <title>{label}</title>
            </rect>
          );
        })}
      </svg>

      {/* Minimal label row */}
      <div className="flex items-center justify-between mt-0.5">
        <span className="flex items-center gap-1">
          <span className="inline-block w-3 h-px bg-indigo-500" />
          <span className="text-[9px] text-gray-400 leading-none">Avg response time</span>
        </span>
        <span className="text-[9px] text-gray-400 leading-none">
          {dataMax > 0 ? `peak ${formatMs(dataMax)}` : ''}
        </span>
      </div>
    </div>
  );
}
