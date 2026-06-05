/**
 * GET /api/monitoring/uptime?window=24h|7d|30d
 *
 * Public-safe BFF route that returns sanitized uptime history data
 * for availability bar rendering on the public /status page.
 *
 * ── Window support (MON-INT-05-001) ──────────────────────────────────────────
 *
 *   ?window=24h  (default) → 24 hourly buckets → 24 bars
 *   ?window=7d             → hourly buckets aggregated to 7 daily buckets
 *   ?window=30d            → hourly buckets aggregated to 30 daily buckets
 *   Invalid / missing      → falls back to 24h
 *
 * ── Behavior by MONITORING_SOURCE ────────────────────────────────────────────
 *
 *   local   — returns { components: [], window: '24h', totalBars: 24 }
 *   service — fetches rollups then fetches per-entity history in parallel,
 *             strips all internal IDs, aggregates to daily buckets for 7d/30d.
 *
 * ── What is exposed publicly ──────────────────────────────────────────────────
 *
 *   - component name (safe display name)
 *   - uptimePercent (rounded to 2dp)
 *   - hourly/daily bucket dominant status (Healthy | Degraded | Down | Unknown)
 *   - bucket uptimePercent
 *   - bucket avgLatencyMs (aggregated trend — safe to expose publicly)
 *   - insufficientData flag
 *   - window string
 *   - totalBars (bar count for the selected window)
 *
 * ── What is NOT exposed ───────────────────────────────────────────────────────
 *
 *   - entityId (internal UUID — stripped at this layer)
 *   - raw check counts
 *   - maxLatencyMs (internal diagnostic metric)
 *   - backend URLs or service tokens
 */

import { NextResponse }  from 'next/server';
import {
  resolveWindow,
  totalBarsForWindow,
  aggregateUptimeToDaily,
  aggregateLatencyToDaily,
  type SupportedWindow,
} from '@/lib/uptime-aggregation';

export const dynamic = 'force-dynamic';

const MONITORING_SOURCE = process.env.MONITORING_SOURCE ?? 'local';
const GATEWAY_URL       = process.env.GATEWAY_URL       ?? 'http://localhost:5010';

// ── Internal Monitoring Service shapes ────────────────────────────────────────

interface RollupsComponent {
  entityId:         string;
  entityName:       string;
  uptimePercent:    number;
  insufficientData: boolean;
}

interface RollupsResponse {
  components: RollupsComponent[];
}

interface HistoryBucket {
  bucketStartUtc:   string;
  uptimePercent:    number;
  dominantStatus:   string;
  insufficientData: boolean;
  avgLatencyMs:     number | null;
}

interface HistoryResponse {
  buckets: HistoryBucket[];
}

// ── Public-safe output shapes (exposed to browser) ────────────────────────────

export interface PublicUptimeBucket {
  bucketStartUtc:   string;
  dominantStatus:   'Healthy' | 'Degraded' | 'Down' | 'Unknown';
  uptimePercent:    number;
  insufficientData: boolean;
  avgLatencyMs:     number | null;
}

export interface PublicUptimeComponent {
  name:          string;
  uptimePercent: number | null;
  buckets:       PublicUptimeBucket[];
}

export interface PublicUptimeResponse {
  window:                string;
  totalBars:             number;
  components:            PublicUptimeComponent[];
  monitoringUnavailable?: boolean;
}

// ── Helpers ───────────────────────────────────────────────────────────────────

function sanitizeStatus(raw: string): 'Healthy' | 'Degraded' | 'Down' | 'Unknown' {
  if (raw === 'Healthy' || raw === 'Degraded' || raw === 'Down') return raw;
  return 'Unknown';
}

function safeLatency(val: unknown): number | null {
  if (typeof val !== 'number' || !isFinite(val) || val < 0) return null;
  return Math.round(val * 10) / 10;
}

async function fetchRollups(window: SupportedWindow): Promise<RollupsComponent[]> {
  const url = `${GATEWAY_URL}/monitoring/monitoring/uptime/rollups?window=${window}`;
  const res = await fetch(url, { cache: 'no-store', headers: { Accept: 'application/json' } });
  if (!res.ok) throw new Error(`Rollups returned HTTP ${res.status}`);
  const data: RollupsResponse = await res.json();
  return data.components ?? [];
}

async function fetchHistory(entityId: string, window: SupportedWindow): Promise<HistoryBucket[]> {
  const url = `${GATEWAY_URL}/monitoring/monitoring/uptime/history?entityId=${entityId}&window=${window}`;
  const res = await fetch(url, { cache: 'no-store', headers: { Accept: 'application/json' } });
  if (!res.ok) return [];
  const data: HistoryResponse = await res.json();
  return data.buckets ?? [];
}

const NO_STORE = { 'Cache-Control': 'no-store, no-cache, must-revalidate' };

// ── Handler ───────────────────────────────────────────────────────────────────

export async function GET(request: Request): Promise<NextResponse> {
  const { searchParams } = new URL(request.url);
  const window     = resolveWindow(searchParams.get('window'));
  const bars       = totalBarsForWindow(window);
  const isMultiDay = window === '7d' || window === '30d';

  if (MONITORING_SOURCE !== 'service') {
    const empty: PublicUptimeResponse = { window, totalBars: bars, components: [] };
    return NextResponse.json(empty, { headers: NO_STORE });
  }

  try {
    const rollupComponents = await fetchRollups(window);

    const historyResults = await Promise.allSettled(
      rollupComponents.map(c => fetchHistory(c.entityId, window)),
    );

    const components: PublicUptimeComponent[] = rollupComponents.map((c, i) => {
      const histResult = historyResults[i];
      const rawBuckets = histResult.status === 'fulfilled' ? histResult.value : [];

      let buckets: PublicUptimeBucket[];

      if (isMultiDay) {
        // Aggregate hourly → daily uptime
        const daily = aggregateUptimeToDaily(rawBuckets, bars);

        // Aggregate hourly → daily latency (separate pass, same raw data)
        const dailyLatency = aggregateLatencyToDaily(
          rawBuckets.map(b => ({
            bucketStartUtc:   b.bucketStartUtc,
            avgLatencyMs:     safeLatency(b.avgLatencyMs),
            maxLatencyMs:     null,
            insufficientData: b.insufficientData,
          })),
          bars,
        );
        const latencyByDay = new Map(
          dailyLatency.map(d => [d.bucketStartUtc, d.avgLatencyMs]),
        );

        buckets = daily.map(d => ({
          bucketStartUtc:   d.bucketStartUtc,
          dominantStatus:   d.dominantStatus,
          uptimePercent:    d.uptimePercent ?? 0,
          insufficientData: d.insufficientData,
          avgLatencyMs:     latencyByDay.get(d.bucketStartUtc) ?? null,
        }));
      } else {
        // 24h: pass hourly buckets through as before
        buckets = rawBuckets.map(b => ({
          bucketStartUtc:   b.bucketStartUtc,
          dominantStatus:   sanitizeStatus(b.dominantStatus),
          uptimePercent:    Math.round(b.uptimePercent * 100) / 100,
          insufficientData: b.insufficientData,
          avgLatencyMs:     safeLatency(b.avgLatencyMs),
        }));
      }

      return {
        name:          c.entityName,
        uptimePercent: c.insufficientData ? null : Math.round(c.uptimePercent * 100) / 100,
        buckets,
      };
    });

    const response: PublicUptimeResponse = { window, totalBars: bars, components };
    return NextResponse.json(response, { headers: NO_STORE });
  } catch (err) {
    const reason = err instanceof Error ? err.message : String(err);
    console.error(
      `[uptime-bff] Failed to fetch uptime data from monitoring service via ${GATEWAY_URL}. ` +
      `Verify Monitoring.Api is running and ConnectionStrings__MonitoringDb is set. Reason: ${reason}`,
    );
    const unavailable: PublicUptimeResponse = {
      window,
      totalBars:             bars,
      components:            [],
      monitoringUnavailable: true,
    };
    return NextResponse.json(unavailable, { headers: NO_STORE });
  }
}
