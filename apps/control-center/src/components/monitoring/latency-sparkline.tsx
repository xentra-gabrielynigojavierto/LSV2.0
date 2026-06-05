/**
 * LatencySparkline — lightweight SVG sparkline for per-component latency trends.
 *
 * Renders a 24-hour avg-latency trend line from hourly bucket data.
 * Max latency is shown as a faint dotted reference line when present.
 *
 * Design constraints:
 *   - Pure SVG — no charting library dependency.
 *   - Fixed height (40 px default). Width is responsive via viewBox.
 *   - Null/insufficient data buckets are treated as gaps (no fabricated values).
 *   - Public-safe: never rendered on /status; internal /monitoring only.
 *
 * States:
 *   loading      — caller passes loading=true; skeleton pulse shown.
 *   error        — caller passes error string; "Latency unavailable" shown.
 *   empty        — no buckets with valid avgLatencyMs; fallback text shown.
 *   data present — sparkline rendered.
 */

'use client';

import type { LatencyBucket } from '@/app/api/monitoring/latency/route';

// ── Colour palette ────────────────────────────────────────────────────────────

const LINE_COLOR      = '#6366f1'; // indigo-500
const FILL_COLOR      = '#6366f120';
const MAX_LINE_COLOR  = '#f59e0b80'; // amber-400 @ 50% opacity
const AXIS_COLOR      = '#e5e7eb'; // gray-200
const LABEL_COLOR     = '#9ca3af'; // gray-400

// ── Helpers ───────────────────────────────────────────────────────────────────

function buildPath(points: Array<{ x: number; y: number }>, closed: boolean): string {
  if (points.length === 0) return '';
  const d = points
    .map((p, i) => `${i === 0 ? 'M' : 'L'}${p.x.toFixed(2)},${p.y.toFixed(2)}`)
    .join(' ');
  return closed ? `${d} Z` : d;
}

function formatMs(ms: number): string {
  if (ms >= 1000) return `${(ms / 1000).toFixed(1)}s`;
  return `${Math.round(ms)}ms`;
}

function formatHour(iso: string): string {
  try {
    return new Date(iso).toLocaleTimeString('en-US', {
      hour:     '2-digit',
      minute:   '2-digit',
      hour12:   false,
      timeZone: 'UTC',
    });
  } catch {
    return '';
  }
}

// ── Props ────────────────────────────────────────────────────────────────────

interface LatencySparklineProps {
  buckets:  LatencyBucket[];
  loading?: boolean;
  error?:   string | null;
  /** Chart height in px. Default 40. */
  height?:  number;
}

// ── Component ────────────────────────────────────────────────────────────────

export function LatencySparkline({
  buckets,
  loading = false,
  error   = null,
  height  = 40,
}: LatencySparklineProps) {
  // ── Loading state ──────────────────────────────────────────────────────────
  if (loading) {
    return (
      <div className="flex items-center gap-2 py-1">
        <div
          className="flex-1 rounded bg-gray-100 animate-pulse"
          style={{ height }}
          aria-label="Loading latency chart…"
        />
      </div>
    );
  }

  // ── Error state ────────────────────────────────────────────────────────────
  if (error) {
    return (
      <p className="text-xs text-gray-400 py-1" role="status">
        Latency unavailable
      </p>
    );
  }

  // ── No-data state ─────────────────────────────────────────────────────────
  const validBuckets = buckets.filter(
    b => b.avgLatencyMs !== null && !b.insufficientData
  );

  if (validBuckets.length === 0) {
    return (
      <p className="text-xs text-gray-400 py-1" role="status">
        No latency history available
      </p>
    );
  }

  // ── Build chart ───────────────────────────────────────────────────────────

  // Sort chronologically
  const sorted = [...buckets].sort(
    (a, b) => new Date(a.bucketStartUtc).getTime() - new Date(b.bucketStartUtc).getTime()
  );

  const totalBuckets = sorted.length;

  // Gather all valid avg values for scale
  const allAvg = sorted.map(b => b.avgLatencyMs).filter((v): v is number => v !== null);
  const allMax = sorted.map(b => b.maxLatencyMs).filter((v): v is number => v !== null);

  const dataMax = Math.max(...allAvg, ...allMax);
  const dataMin = 0; // latency always starts from 0 baseline

  // SVG coordinate system: viewBox="0 0 240 {height}"
  const VW     = 240;
  const VH     = height;
  const PAD_X  = 0;
  const PAD_T  = 4;   // top padding
  const PAD_B  = 12;  // bottom: labels
  const chartH = VH - PAD_T - PAD_B;
  const chartW = VW - PAD_X * 2;

  function xOf(i: number): number {
    if (totalBuckets <= 1) return chartW / 2 + PAD_X;
    return PAD_X + (i / (totalBuckets - 1)) * chartW;
  }

  function yOf(ms: number | null): number | null {
    if (ms === null) return null;
    if (dataMax === dataMin) return PAD_T + chartH / 2;
    return PAD_T + (1 - (ms - dataMin) / (dataMax - dataMin)) * chartH;
  }

  // Build avg-line segments (skip gaps for null buckets)
  const avgPoints: Array<{ x: number; y: number }> = [];
  const segments: Array<Array<{ x: number; y: number }>> = [];
  let currentSeg: Array<{ x: number; y: number }> = [];

  sorted.forEach((b, i) => {
    const y = yOf(b.avgLatencyMs);
    if (y === null || b.insufficientData) {
      if (currentSeg.length > 0) { segments.push(currentSeg); currentSeg = []; }
    } else {
      const pt = { x: xOf(i), y };
      avgPoints.push(pt);
      currentSeg.push(pt);
    }
  });
  if (currentSeg.length > 0) segments.push(currentSeg);

  // Build filled area path (only for the first segment for simplicity)
  const fillPath = avgPoints.length >= 2
    ? buildPath([
        ...avgPoints,
        { x: avgPoints[avgPoints.length - 1].x, y: PAD_T + chartH },
        { x: avgPoints[0].x,                    y: PAD_T + chartH },
      ], true)
    : '';

  // Max latency horizontal reference line (single value = overall max across window)
  const peakMax = allMax.length > 0 ? Math.max(...allMax) : null;
  const maxY    = peakMax !== null ? yOf(peakMax) : null;

  // Axis label values
  const topLabel    = formatMs(dataMax);
  const bottomLabel = '0 ms';

  // ── Render ─────────────────────────────────────────────────────────────────

  return (
    <div className="w-full" aria-label="Response time trend chart">
      <svg
        viewBox={`0 0 ${VW} ${VH}`}
        width="100%"
        height={VH}
        preserveAspectRatio="none"
        role="img"
        aria-label="Average latency over last 24 hours"
      >
        {/* Axis labels (top / bottom) */}
        <text
          x={VW - 2}
          y={PAD_T + 6}
          textAnchor="end"
          fontSize="7"
          fill={LABEL_COLOR}
        >
          {topLabel}
        </text>
        <text
          x={VW - 2}
          y={PAD_T + chartH + 1}
          textAnchor="end"
          fontSize="7"
          fill={LABEL_COLOR}
        >
          {bottomLabel}
        </text>

        {/* Baseline */}
        <line
          x1={0} y1={PAD_T + chartH}
          x2={VW} y2={PAD_T + chartH}
          stroke={AXIS_COLOR}
          strokeWidth="0.5"
        />

        {/* Max latency reference line */}
        {maxY !== null && maxY < PAD_T + chartH - 2 && (
          <line
            x1={0}   y1={maxY}
            x2={VW}  y2={maxY}
            stroke={MAX_LINE_COLOR}
            strokeWidth="1"
            strokeDasharray="3 2"
          />
        )}

        {/* Filled area */}
        {fillPath && (
          <path
            d={fillPath}
            fill={FILL_COLOR}
          />
        )}

        {/* Avg latency line segments */}
        {segments.map((seg, si) =>
          seg.length >= 2 ? (
            <polyline
              key={si}
              points={seg.map(p => `${p.x},${p.y}`).join(' ')}
              fill="none"
              stroke={LINE_COLOR}
              strokeWidth="1.5"
              strokeLinejoin="round"
              strokeLinecap="round"
            />
          ) : seg.length === 1 ? (
            <circle
              key={si}
              cx={seg[0].x}
              cy={seg[0].y}
              r="1.5"
              fill={LINE_COLOR}
            />
          ) : null
        )}

        {/* Per-bucket invisible hit targets with native tooltip */}
        {sorted.map((b, i) => {
          const y = yOf(b.avgLatencyMs);
          if (y === null) return null;
          const hour  = formatHour(b.bucketStartUtc);
          const label = b.insufficientData
            ? `${hour} UTC — No data`
            : `${hour} UTC — avg ${formatMs(b.avgLatencyMs!)}${b.maxLatencyMs !== null ? ` · max ${formatMs(b.maxLatencyMs)}` : ''}`;
          return (
            <rect
              key={i}
              x={xOf(i) - (chartW / totalBuckets / 2)}
              y={PAD_T}
              width={chartW / totalBuckets}
              height={chartH}
              fill="transparent"
              aria-label={label}
            >
              <title>{label}</title>
            </rect>
          );
        })}
      </svg>

      {/* Legend row */}
      <div className="flex items-center gap-4 mt-0.5">
        <span className="flex items-center gap-1">
          <span className="inline-block w-4 h-px bg-indigo-500" />
          <span className="text-[10px] text-gray-400">Avg latency</span>
        </span>
        {peakMax !== null && maxY !== null && maxY < PAD_T + chartH - 2 && (
          <span className="flex items-center gap-1">
            <span className="inline-block w-4 h-px border-t border-dashed border-amber-400" />
            <span className="text-[10px] text-gray-400">Peak max ({formatMs(peakMax)})</span>
          </span>
        )}
        <span className="ml-auto text-[10px] text-gray-400">24 h</span>
      </div>
    </div>
  );
}
