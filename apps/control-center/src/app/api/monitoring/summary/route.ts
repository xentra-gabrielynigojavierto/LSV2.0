/**
 * GET /api/monitoring/summary
 *
 * Returns a MonitoringSummary: overall system health, per-service integration
 * statuses, and active alerts.
 *
 * All monitoring logic lives in @/lib/monitoring-source — this route is a
 * thin HTTP adapter only. To switch from the local probe engine to the
 * Monitoring Service, set MONITORING_SOURCE=service and implement the
 * 'service' branch in monitoring-source.ts (MON-INT-01-001).
 */
import { NextResponse } from 'next/server';
import { getMonitoringSummary } from '@/lib/monitoring-source';

export async function GET() {
  try {
    const summary = await getMonitoringSummary();
    return NextResponse.json(summary, {
      headers: { 'Cache-Control': 'no-store, no-cache, must-revalidate' },
    });
  } catch (err) {
    const message = err instanceof Error ? err.message : 'Monitoring source unavailable';
    console.error('[/api/monitoring/summary]', message);
    return NextResponse.json(
      { error: 'monitoring_unavailable', message },
      {
        status: 503,
        headers: { 'Cache-Control': 'no-store, no-cache, must-revalidate' },
      },
    );
  }
}
