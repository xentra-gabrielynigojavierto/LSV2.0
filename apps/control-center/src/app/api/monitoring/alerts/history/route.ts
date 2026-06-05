/**
 * GET /api/monitoring/alerts/history?entityName={name}&limit={n}
 *
 * Returns the alert history for a specific monitored entity — both active
 * and resolved alerts, ordered by triggered time descending.
 *
 * ── Source modes ──────────────────────────────────────────────────────────────
 *
 *   MONITORING_SOURCE=service → proxies to Monitoring Service via gateway.
 *   MONITORING_SOURCE=local   → returns empty array; local engine is ephemeral
 *     and does not persist alert history between requests.
 *
 * ── Response shape ────────────────────────────────────────────────────────────
 *
 *   { source: 'service' | 'local', items: AlertHistoryItem[] }
 *
 *   The 'source' field lets the UI distinguish between "no data" and
 *   "history unavailable in this mode" without adding a separate status code.
 */

import { NextResponse } from 'next/server';
import { requirePlatformAdmin } from '@/lib/auth-guards';

const MONITORING_SOURCE = process.env.MONITORING_SOURCE ?? 'local';

export interface AlertHistoryItem {
  alertId:      string;
  entityName:   string;
  severity:     string;  // "Critical" | "Warning" | "Info"
  message:      string;
  createdAtUtc: string;  // TriggeredAtUtc on the Monitoring Service
  resolvedAtUtc: string | null;
}

export interface AlertHistoryResponse {
  source: 'service' | 'local';
  items:  AlertHistoryItem[];
}

export async function GET(request: Request) {
  try {
    await requirePlatformAdmin();
  } catch {
    return NextResponse.json({ error: 'Unauthorized' }, { status: 401 });
  }

  const { searchParams } = new URL(request.url);
  const entityName = searchParams.get('entityName');
  const limit      = searchParams.get('limit') ?? '10';

  if (!entityName) {
    return NextResponse.json(
      { source: 'service', items: [] } satisfies AlertHistoryResponse,
      { status: 400, headers: { 'Cache-Control': 'no-store' } },
    );
  }

  // Local mode: ephemeral engine has no persisted alert history.
  if (MONITORING_SOURCE !== 'service') {
    return NextResponse.json(
      { source: 'local', items: [] } satisfies AlertHistoryResponse,
      { headers: { 'Cache-Control': 'no-store' } },
    );
  }

  // Service mode: proxy to Monitoring Service via gateway.
  const gatewayBase = process.env.GATEWAY_URL ?? 'http://localhost:5010';
  const url = `${gatewayBase}/monitoring/monitoring/alerts/history` +
    `?entityName=${encodeURIComponent(entityName)}&limit=${encodeURIComponent(limit)}`;

  try {
    const res = await fetch(url, {
      cache:   'no-store',
      headers: { 'Accept': 'application/json' },
    });

    if (!res.ok) {
      return NextResponse.json(
        { source: 'service', items: [] } satisfies AlertHistoryResponse,
        { status: res.status, headers: { 'Cache-Control': 'no-store' } },
      );
    }

    // Monitoring Service returns MonitoringAlertResponse[]:
    //   { alertId, entityId, name, severity, message, createdAtUtc, resolvedAtUtc }
    const raw: Array<{
      alertId:      string;
      entityId:     string;
      name:         string;
      severity:     string;
      message:      string;
      createdAtUtc: string;
      resolvedAtUtc: string | null;
    }> = await res.json();

    const items: AlertHistoryItem[] = raw.map(a => ({
      alertId:      a.alertId,
      entityName:   a.name,
      severity:     a.severity,
      message:      a.message,
      createdAtUtc: a.createdAtUtc,
      resolvedAtUtc: a.resolvedAtUtc,
    }));

    return NextResponse.json(
      { source: 'service', items } satisfies AlertHistoryResponse,
      { headers: { 'Cache-Control': 'no-store' } },
    );

  } catch {
    return NextResponse.json(
      { source: 'service', items: [] } satisfies AlertHistoryResponse,
      { status: 502, headers: { 'Cache-Control': 'no-store' } },
    );
  }
}
