/**
 * GET /api/monitoring/latency?window=24h|7d|30d
 *
 * Internal-only BFF route that returns per-component latency history
 * for sparkline rendering on the internal /monitoring page.
 *
 * ── Window support (MON-INT-05-001) ──────────────────────────────────────────
 *
 *   ?window=24h  (default) → 24 hourly buckets
 *   ?window=7d             → aggregated to 7 daily buckets
 *   ?window=30d            → aggregated to 30 daily buckets
 *   Invalid / missing      → falls back to 24h
 *
 * ── Source modes ─────────────────────────────────────────────────────────────
 *
 *   MONITORING_SOURCE=service → fetches rollups then per-entity history,
 *     extracts avgLatencyMs / maxLatencyMs, aggregates for 7d/30d.
 *   MONITORING_SOURCE=local   → returns { components: [] } (no fabrication).
 *
 * ── What is exposed ───────────────────────────────────────────────────────────
 *
 *   - component display name
 *   - bucketStartUtc (hour or day boundary)
 *   - avgLatencyMs   (primary chart value)
 *   - maxLatencyMs   (secondary reference)
 *   - insufficientData flag
 *
 * ── What is NOT exposed ───────────────────────────────────────────────────────
 *
 *   - entityId (internal UUID — stripped)
 *   - raw check counts
 *   - backend URLs or service tokens
 *
 * ── Security boundary ─────────────────────────────────────────────────────────
 *
 *   This route is internal-only. Never link from the public /status page.
 *   Consumed exclusively by ComponentStatusList on /monitoring.
 *   Control Center middleware enforces authentication for all /api/* routes.
 */

import { NextResponse } from 'next/server';
import { requirePlatformAdmin } from '@/lib/auth-guards';
import {
  resolveWindow,
  totalBarsForWindow,
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

interface RawHistoryBucket {
  bucketStartUtc:   string;
  avgLatencyMs:     number | null;
  maxLatencyMs:     number;
  insufficientData: boolean;
}

interface RawHistoryResponse {
  buckets: RawHistoryBucket[];
}

// ── Internal-safe output shapes ───────────────────────────────────────────────

export interface LatencyBucket {
  bucketStartUtc:   string;
  avgLatencyMs:     number | null;
  maxLatencyMs:     number | null;
  insufficientData: boolean;
}

export interface LatencyComponent {
  name:    string;
  buckets: LatencyBucket[];
}

export interface InternalLatencyResponse {
  source:     'service' | 'local';
  window:     string;
  totalBars:  number;
  components: LatencyComponent[];
}

// ── Helpers ───────────────────────────────────────────────────────────────────

async function fetchRollups(window: SupportedWindow): Promise<RollupsComponent[]> {
  const url = `${GATEWAY_URL}/monitoring/monitoring/uptime/rollups?window=${window}`;
  const res = await fetch(url, { cache: 'no-store', headers: { Accept: 'application/json' } });
  if (!res.ok) throw new Error(`Rollups returned HTTP ${res.status}`);
  const data: RollupsResponse = await res.json();
  return data.components ?? [];
}

async function fetchHistory(entityId: string, window: SupportedWindow): Promise<RawHistoryBucket[]> {
  const url = `${GATEWAY_URL}/monitoring/monitoring/uptime/history?entityId=${entityId}&window=${window}`;
  const res = await fetch(url, { cache: 'no-store', headers: { Accept: 'application/json' } });
  if (!res.ok) return [];
  const data: RawHistoryResponse = await res.json();
  return data.buckets ?? [];
}

const NO_STORE = { 'Cache-Control': 'no-store, no-cache, must-revalidate' };

// ── Handler ───────────────────────────────────────────────────────────────────

export async function GET(request: Request): Promise<NextResponse> {
  try {
    await requirePlatformAdmin();
  } catch {
    return NextResponse.json({ error: 'Unauthorized' }, { status: 401 });
  }

  const { searchParams } = new URL(request.url);
  const window     = resolveWindow(searchParams.get('window'));
  const bars       = totalBarsForWindow(window);
  const isMultiDay = window === '7d' || window === '30d';

  if (MONITORING_SOURCE !== 'service') {
    const empty: InternalLatencyResponse = { source: 'local', window, totalBars: bars, components: [] };
    return NextResponse.json(empty, { headers: NO_STORE });
  }

  try {
    const rollups = await fetchRollups(window);

    const historyResults = await Promise.allSettled(
      rollups.map(c => fetchHistory(c.entityId, window)),
    );

    const components: LatencyComponent[] = rollups.map((c, i) => {
      const result     = historyResults[i];
      const rawBuckets = result.status === 'fulfilled' ? result.value : [];

      let buckets: LatencyBucket[];

      if (isMultiDay) {
        const daily = aggregateLatencyToDaily(
          rawBuckets.map(b => ({
            bucketStartUtc:   b.bucketStartUtc,
            avgLatencyMs:     typeof b.avgLatencyMs === 'number' && isFinite(b.avgLatencyMs) ? b.avgLatencyMs : null,
            maxLatencyMs:     typeof b.maxLatencyMs === 'number' && isFinite(b.maxLatencyMs) && b.maxLatencyMs > 0 ? b.maxLatencyMs : null,
            insufficientData: b.insufficientData,
          })),
          bars,
        );
        buckets = daily;
      } else {
        buckets = rawBuckets.map(b => ({
          bucketStartUtc:  b.bucketStartUtc,
          avgLatencyMs:    typeof b.avgLatencyMs === 'number' && isFinite(b.avgLatencyMs)
                             ? Math.round(b.avgLatencyMs * 10) / 10
                             : null,
          maxLatencyMs:    typeof b.maxLatencyMs === 'number' && isFinite(b.maxLatencyMs) && b.maxLatencyMs > 0
                             ? b.maxLatencyMs
                             : null,
          insufficientData: b.insufficientData,
        }));
      }

      return { name: c.entityName, buckets };
    });

    const response: InternalLatencyResponse = { source: 'service', window, totalBars: bars, components };
    return NextResponse.json(response, { headers: NO_STORE });
  } catch {
    const empty: InternalLatencyResponse = { source: 'service', window, totalBars: bars, components: [] };
    return NextResponse.json(empty, { status: 502, headers: NO_STORE });
  }
}
