'use client';

/**
 * SmsTrendChart — LS-NOTIF-SMS-009
 *
 * Pure-SVG multi-line trend chart for SMS delivery and reconciliation data.
 * No charting library dependency — follows the LatencySparkline pattern.
 *
 * Lines rendered:
 *   — Gray   : Total attempts
 *   — Green  : Delivered
 *   — Red    : Failed
 *   — Indigo : Reconciled (dashed)
 *
 * States: loading | empty | data
 */

import type { SmsDashboardTrendPoint } from '@/lib/sms-dashboard-api';

const LINE = {
  total:       { color: '#9ca3af', width: 1,   dash: undefined },      // gray-400
  delivered:   { color: '#10b981', width: 1.5, dash: undefined },      // emerald-500
  failed:      { color: '#ef4444', width: 1.5, dash: undefined },      // red-500
  reconciled:  { color: '#6366f1', width: 1,   dash: '3 2'    },      // indigo-500
} as const;

const VW    = 600;
const VH    = 90;
const PAD_L = 4;
const PAD_R = 4;
const PAD_T = 6;
const PAD_B = 6;   // labels rendered below SVG in HTML
const CHART_W = VW - PAD_L - PAD_R;
const CHART_H = VH - PAD_T - PAD_B;

function formatBucketLabel(iso: string, bucket: string): string {
  try {
    const d = new Date(iso);
    if (bucket === 'hour') {
      return d.toLocaleTimeString('en-US', { hour: '2-digit', minute: '2-digit', hour12: false, timeZone: 'UTC' });
    }
    if (bucket === 'week') {
      return d.toLocaleDateString('en-US', { month: 'short', day: 'numeric', timeZone: 'UTC' });
    }
    return d.toLocaleDateString('en-US', { month: 'short', day: 'numeric', timeZone: 'UTC' });
  } catch {
    return '';
  }
}

interface SmsTrendChartProps {
  points:  SmsDashboardTrendPoint[];
  bucket:  string;
  loading?: boolean;
}

export function SmsTrendChart({ points, bucket, loading = false }: SmsTrendChartProps) {
  if (loading) {
    return (
      <div className="flex items-center gap-2">
        <div className="flex-1 rounded bg-gray-100 animate-pulse" style={{ height: VH }} aria-label="Loading trend chart…" />
      </div>
    );
  }

  if (points.length === 0) {
    return (
      <p className="text-xs text-gray-400 py-6 text-center" role="status">
        No trend data for this period
      </p>
    );
  }

  const sorted = [...points].sort(
    (a, b) => new Date(a.bucketStart).getTime() - new Date(b.bucketStart).getTime(),
  );

  const n      = sorted.length;
  const maxVal = Math.max(...sorted.map(p => p.totalAttempts), 1);

  const xOf = (i: number): number =>
    n <= 1 ? PAD_L + CHART_W / 2 : PAD_L + (i / (n - 1)) * CHART_W;

  const yOf = (v: number): number =>
    PAD_T + (1 - v / maxVal) * CHART_H;

  const toPolyline = (vals: number[]): string =>
    vals.map((v, i) => `${xOf(i).toFixed(1)},${yOf(v).toFixed(1)}`).join(' ');

  // Fill for total area
  const areaD = [
    ...sorted.map((p, i) => `${i === 0 ? 'M' : 'L'}${xOf(i).toFixed(1)},${yOf(p.totalAttempts).toFixed(1)}`),
    `L${xOf(n - 1).toFixed(1)},${(PAD_T + CHART_H).toFixed(1)}`,
    `L${xOf(0).toFixed(1)},${(PAD_T + CHART_H).toFixed(1)}`,
    'Z',
  ].join(' ');

  // X-axis tick labels (max 8)
  const labelStep = Math.max(1, Math.ceil(n / 8));
  const labelIdxs = sorted.reduce<number[]>((acc, _, i) => {
    if (i % labelStep === 0) acc.push(i);
    return acc;
  }, []);
  if (labelIdxs[labelIdxs.length - 1] !== n - 1) labelIdxs.push(n - 1);

  return (
    <div className="w-full" aria-label="SMS delivery trend chart">
      <svg
        viewBox={`0 0 ${VW} ${VH}`}
        width="100%"
        height={VH}
        preserveAspectRatio="none"
        role="img"
        aria-label="SMS delivery trends over selected period"
      >
        {/* Baseline */}
        <line
          x1={PAD_L} y1={PAD_T + CHART_H}
          x2={VW - PAD_R} y2={PAD_T + CHART_H}
          stroke="#e5e7eb" strokeWidth="0.5"
        />

        {/* Total fill area */}
        <path d={areaD} fill="#9ca3af18" />

        {/* Total line */}
        <polyline
          points={toPolyline(sorted.map(p => p.totalAttempts))}
          fill="none" stroke={LINE.total.color} strokeWidth={LINE.total.width}
          strokeLinejoin="round" strokeLinecap="round"
        />

        {/* Delivered line */}
        <polyline
          points={toPolyline(sorted.map(p => p.deliveredCount))}
          fill="none" stroke={LINE.delivered.color} strokeWidth={LINE.delivered.width}
          strokeLinejoin="round" strokeLinecap="round"
        />

        {/* Failed line */}
        <polyline
          points={toPolyline(sorted.map(p => p.failedCount))}
          fill="none" stroke={LINE.failed.color} strokeWidth={LINE.failed.width}
          strokeLinejoin="round" strokeLinecap="round"
        />

        {/* Reconciled line (dashed) */}
        <polyline
          points={toPolyline(sorted.map(p => p.reconciledTotal))}
          fill="none" stroke={LINE.reconciled.color} strokeWidth={LINE.reconciled.width}
          strokeDasharray={LINE.reconciled.dash}
          strokeLinejoin="round" strokeLinecap="round"
        />

        {/* Invisible hit areas with tooltips */}
        {sorted.map((p, i) => (
          <rect
            key={i}
            x={xOf(i) - CHART_W / n / 2}
            y={PAD_T}
            width={CHART_W / n}
            height={CHART_H}
            fill="transparent"
          >
            <title>
              {`${formatBucketLabel(p.bucketStart, bucket)}\nTotal: ${p.totalAttempts.toLocaleString()} · Delivered: ${p.deliveredCount.toLocaleString()} · Failed: ${p.failedCount.toLocaleString()} · Reconciled: ${p.reconciledTotal.toLocaleString()}`}
            </title>
          </rect>
        ))}
      </svg>

      {/* X-axis labels */}
      <div className="relative w-full" style={{ height: 14 }}>
        {labelIdxs.map(i => {
          const pct = n <= 1 ? 50 : (i / (n - 1)) * 100;
          return (
            <span
              key={i}
              className="absolute text-[9px] text-gray-400 -translate-x-1/2 whitespace-nowrap"
              style={{ left: `${pct}%`, top: 0 }}
            >
              {formatBucketLabel(sorted[i].bucketStart, bucket)}
            </span>
          );
        })}
      </div>

      {/* Legend */}
      <div className="flex flex-wrap items-center gap-4 mt-3">
        {([
          { color: LINE.total.color,     label: 'Total',       dashed: false },
          { color: LINE.delivered.color, label: 'Delivered',   dashed: false },
          { color: LINE.failed.color,    label: 'Failed',      dashed: false },
          { color: LINE.reconciled.color, label: 'Reconciled', dashed: true  },
        ] as const).map(item => (
          <span key={item.label} className="flex items-center gap-1">
            {item.dashed
              ? <span className="inline-block w-4 h-px border-t border-dashed" style={{ borderColor: item.color }} />
              : <span className="inline-block w-4 h-0.5 rounded" style={{ backgroundColor: item.color }} />
            }
            <span className="text-[10px] text-gray-400">{item.label}</span>
          </span>
        ))}
      </div>
    </div>
  );
}
