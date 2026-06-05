/**
 * uptime-aggregation.ts
 *
 * BFF-side utilities for coarsening hourly uptime/latency history buckets
 * into daily buckets for 7d and 30d window views.
 *
 * RULES:
 *   - No fabrication. If a day has no hourly data, it is returned with
 *     insufficientData=true and null values.
 *   - Status severity: Down > Degraded > Healthy > Unknown.
 *   - uptimePercent, avgLatencyMs = simple mean of non-null hourly values.
 *   - maxLatencyMs = maximum of all hourly maxLatencyMs values.
 *   - insufficientData = true only if ALL hours in the day have insufficientData.
 *   - bucketStartUtc = midnight UTC of the day.
 *
 * Used by:
 *   /api/monitoring/uptime   — availability bars
 *   /api/monitoring/latency  — latency sparklines
 */

// ── Supported windows ─────────────────────────────────────────────────────────

export type SupportedWindow = '24h' | '7d' | '30d';

export const SUPPORTED_WINDOWS: SupportedWindow[] = ['24h', '7d', '30d'];

export function resolveWindow(raw: string | null | undefined): SupportedWindow {
  if (raw === '7d' || raw === '30d') return raw;
  return '24h';
}

/** Total bar count for a given window (= number of output buckets). */
export function totalBarsForWindow(window: SupportedWindow): number {
  if (window === '7d')  return 7;
  if (window === '30d') return 30;
  return 24;
}

// ── Status severity ───────────────────────────────────────────────────────────

const STATUS_SEVERITY: Record<string, number> = {
  Down:     3,
  Degraded: 2,
  Healthy:  1,
  Unknown:  0,
};

function worstStatus(statuses: string[]): 'Healthy' | 'Degraded' | 'Down' | 'Unknown' {
  let best: string = 'Unknown';
  let bestScore = -1;
  for (const s of statuses) {
    const score = STATUS_SEVERITY[s] ?? 0;
    if (score > bestScore) { bestScore = score; best = s; }
  }
  if (best === 'Down')     return 'Down';
  if (best === 'Degraded') return 'Degraded';
  if (best === 'Healthy')  return 'Healthy';
  return 'Unknown';
}

// ── Day key helper ────────────────────────────────────────────────────────────

function utcDayKey(iso: string): string {
  // Returns "YYYY-MM-DD" from an ISO timestamp
  try {
    return new Date(iso).toISOString().slice(0, 10);
  } catch {
    return '1970-01-01';
  }
}

function dayStartUtc(dayKey: string): string {
  return `${dayKey}T00:00:00.000Z`;
}

// ── Hourly bucket shapes (used by both uptime and latency routes) ─────────────

export interface HourlyUptimeBucket {
  bucketStartUtc:   string;
  uptimePercent:    number | null;
  dominantStatus:   string;
  insufficientData: boolean;
}

export interface DailyUptimeBucket {
  bucketStartUtc:   string;  // midnight UTC
  uptimePercent:    number | null;
  dominantStatus:   'Healthy' | 'Degraded' | 'Down' | 'Unknown';
  insufficientData: boolean;
}

export interface HourlyLatencyBucket {
  bucketStartUtc:   string;
  avgLatencyMs:     number | null;
  maxLatencyMs:     number | null;
  insufficientData: boolean;
}

export interface DailyLatencyBucket {
  bucketStartUtc:   string;
  avgLatencyMs:     number | null;
  maxLatencyMs:     number | null;
  insufficientData: boolean;
}

// ── Aggregation functions ─────────────────────────────────────────────────────

/**
 * Aggregate hourly uptime buckets into daily buckets.
 * Input order is irrelevant; output is sorted chronologically.
 */
export function aggregateUptimeToDaily(
  hourly:     HourlyUptimeBucket[],
  targetDays: number,
): DailyUptimeBucket[] {
  // Group by UTC day
  const groups = new Map<string, HourlyUptimeBucket[]>();
  for (const b of hourly) {
    const key = utcDayKey(b.bucketStartUtc);
    const arr = groups.get(key) ?? [];
    arr.push(b);
    groups.set(key, arr);
  }

  // Sort day keys chronologically
  const sortedKeys = [...groups.keys()].sort();

  // Limit to most recent targetDays
  const usedKeys = sortedKeys.slice(-targetDays);

  return usedKeys.map(key => {
    const hours = groups.get(key)!;
    const statuses = hours.map(h => h.dominantStatus);
    const uptimeValues = hours.map(h => h.uptimePercent).filter((v): v is number => v !== null);
    const allInsufficient = hours.every(h => h.insufficientData);

    return {
      bucketStartUtc:   dayStartUtc(key),
      dominantStatus:   worstStatus(statuses),
      uptimePercent:    uptimeValues.length > 0
        ? Math.round((uptimeValues.reduce((a, b) => a + b, 0) / uptimeValues.length) * 100) / 100
        : null,
      insufficientData: allInsufficient,
    };
  });
}

/**
 * Aggregate hourly latency buckets into daily buckets.
 * Input order is irrelevant; output is sorted chronologically.
 */
export function aggregateLatencyToDaily(
  hourly:     HourlyLatencyBucket[],
  targetDays: number,
): DailyLatencyBucket[] {
  const groups = new Map<string, HourlyLatencyBucket[]>();
  for (const b of hourly) {
    const key = utcDayKey(b.bucketStartUtc);
    const arr = groups.get(key) ?? [];
    arr.push(b);
    groups.set(key, arr);
  }

  const sortedKeys = [...groups.keys()].sort();
  const usedKeys = sortedKeys.slice(-targetDays);

  return usedKeys.map(key => {
    const hours = groups.get(key)!;
    const avgValues = hours.map(h => h.avgLatencyMs).filter((v): v is number => v !== null);
    const maxValues = hours.map(h => h.maxLatencyMs).filter((v): v is number => v !== null);
    const allInsufficient = hours.every(h => h.insufficientData);

    return {
      bucketStartUtc:   dayStartUtc(key),
      avgLatencyMs:     avgValues.length > 0
        ? Math.round((avgValues.reduce((a, b) => a + b, 0) / avgValues.length) * 10) / 10
        : null,
      maxLatencyMs:     maxValues.length > 0 ? Math.max(...maxValues) : null,
      insufficientData: allInsufficient,
    };
  });
}
