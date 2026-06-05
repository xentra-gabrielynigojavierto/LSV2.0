/**
 * monitoring-source.ts — Central abstraction layer for all monitoring data access.
 *
 * Architecture rule: Control Center = read-only consumer.
 * All monitoring data access in the Control Center must go through this module,
 * never directly through system-health-store or probe logic.
 *
 * Source switch (env):
 *   MONITORING_SOURCE=local   → use built-in local probe engine (default, current behavior)
 *   MONITORING_SOURCE=service → delegate to Monitoring Service REST API (MON-INT-01-002)
 *
 * Migration target: MON-INT-01-002 — Monitoring Read Model Completion.
 * The Monitoring Service now exposes /monitoring/summary, /monitoring/status,
 * and /monitoring/alerts. The service branch calls /monitoring/summary directly
 * instead of deriving a summary from entity registry data.
 */

import { listServices, type ServiceDef } from '@/lib/system-health-store';
import { CONTROL_CENTER_API_BASE } from '@/lib/env';
import type {
  MonitoringSummary,
  MonitoringStatus,
  SystemHealthSummary,
  IntegrationStatus,
  SystemAlert,
  AlertSeverity,
} from '@/types/control-center';

// ── Source toggle ─────────────────────────────────────────────────────────────

const MONITORING_SOURCE = process.env.MONITORING_SOURCE ?? 'local';

// ── Local engine types (internal to this module) ──────────────────────────────

interface ProbeResult {
  name:             string;
  status:           MonitoringStatus;
  latencyMs?:       number;
  lastCheckedAtUtc: string;
  category:         string;
  detail?:          string;
}

// ── Monitoring Service types (internal to this module) ───────────────────────
// Matches MonitoringSummaryResponse / MonitoringStatusResponse / MonitoringAlertResponse
// from Monitoring.Api/Contracts/ (MON-INT-01-002).

interface ServiceStatusEntry {
  entityId:         string;
  name:             string;
  scope:            string;
  status:           string;           // "Healthy" | "Degraded" | "Down"
  lastCheckedAtUtc: string | null;
  latencyMs:        number | null;
}

interface ServiceAlertEntry {
  alertId:      string;
  entityId:     string;
  name:         string;
  severity:     string;               // "Info" | "Warning" | "Critical"
  message:      string;
  createdAtUtc: string;
  resolvedAtUtc: string | null;
}

interface ServiceSummaryResponse {
  system: {
    status:           string;
    lastCheckedAtUtc: string;
  };
  integrations: ServiceStatusEntry[];
  alerts:       ServiceAlertEntry[];
}

// ── Local engine implementation ───────────────────────────────────────────────
// TEMPORARY — local monitoring engine (to be replaced by Monitoring Service)
// MON-INT-01 migration target

/**
 * Execute a single HTTP health probe against a registered service URL.
 * TEMPORARY — this responsibility will move to the Monitoring Service.
 */
async function probeService(svc: ServiceDef): Promise<ProbeResult> {
  const start      = Date.now();
  const controller = new AbortController();
  const timer      = setTimeout(() => controller.abort(), 4000);

  try {
    const res = await fetch(svc.url, {
      signal: controller.signal,
      cache:  'no-store',
    });
    const latencyMs = Date.now() - start;

    let detail: string | undefined;
    try {
      const text = await res.text();
      try {
        const body = JSON.parse(text);
        detail = body.status ?? body.service ?? undefined;
      } catch {
        if (text) detail = text.trim();
      }
    } catch { /* ignore body-read errors */ }

    const status: MonitoringStatus = !res.ok
      ? 'Degraded'
      : latencyMs > 2000
        ? 'Degraded'
        : 'Healthy';

    return { name: svc.name, status, latencyMs, lastCheckedAtUtc: new Date().toISOString(), category: svc.category, detail };
  } catch {
    return {
      name:             svc.name,
      status:           'Down',
      latencyMs:        Date.now() - start,
      lastCheckedAtUtc: new Date().toISOString(),
      category:         svc.category,
      detail:           'Unreachable',
    };
  } finally {
    clearTimeout(timer);
  }
}

/**
 * Run probes against all registered services and aggregate results.
 * TEMPORARY — aggregation will be owned by the Monitoring Service.
 */
async function localGetMonitoringSummary(): Promise<MonitoringSummary> {
  const services = await listServices();
  const results  = await Promise.all(services.map(probeService));

  const downCount     = results.filter(r => r.status === 'Down').length;
  const degradedCount = results.filter(r => r.status === 'Degraded').length;

  const overallStatus: MonitoringStatus =
    downCount     > 0 ? 'Down' :
    degradedCount > 0 ? 'Degraded' :
                        'Healthy';

  const system: SystemHealthSummary = {
    status:           overallStatus,
    lastCheckedAtUtc: new Date().toISOString(),
  };

  const integrations: IntegrationStatus[] = results.map(r => ({
    name:             r.name,
    status:           r.status,
    latencyMs:        r.latencyMs,
    lastCheckedAtUtc: r.lastCheckedAtUtc,
    category:         r.category,
  }));

  const alerts: SystemAlert[] = results
    .filter(r => r.status !== 'Healthy')
    .map(r => ({
      id:           `alert-${r.name.toLowerCase().replace(/\s+/g, '-')}`,
      message:      `${r.name} is ${r.status.toLowerCase()}${r.detail ? `: ${r.detail}` : ''}`,
      severity:     (r.status === 'Down' ? 'Critical' : 'Warning') as AlertSeverity,
      createdAtUtc: r.lastCheckedAtUtc,
      entityName:   r.name,      // correlates to IntegrationStatus.name
      resolvedAtUtc: undefined,  // local probes don't track resolution timestamps
    }));

  return { system, integrations, alerts };
}

// ── Service engine implementation ─────────────────────────────────────────────
// MON-INT-01-002 — Monitoring Read Model Completion
//
// Calls GET /monitoring/summary via the YARP gateway.
// Gateway routing (double-prefix):
//   CC calls: {GATEWAY_URL}/monitoring/monitoring/summary
//   YARP strips: /monitoring prefix → cluster receives: /monitoring/summary
//   Monitoring Service handles: GET /monitoring/summary
//
// The Monitoring Service returns a MonitoringSummaryResponse that maps
// directly to the MonitoringSummary type used by the Control Center.

async function serviceGetMonitoringSummary(): Promise<MonitoringSummary> {
  const gatewayBase = CONTROL_CENTER_API_BASE;

  // Double "monitoring" is intentional: YARP strips the outer /monitoring
  // prefix for the monitoring-cluster, so the inner /monitoring/summary
  // is the actual service path.
  const url = `${gatewayBase}/monitoring/monitoring/summary`;

  const controller = new AbortController();
  const timeout    = setTimeout(() => controller.abort(), 10_000);

  let res: Response;
  try {
    res = await fetch(url, {
      cache:   'no-store',
      headers: { 'Accept': 'application/json' },
      signal:  controller.signal,
    });
  } catch (err) {
    const reason = err instanceof Error && err.name === 'AbortError'
      ? `timed out after 10 s`
      : `network error: ${err instanceof Error ? err.message : String(err)}`;
    throw new Error(
      `[monitoring-source] Cannot reach Monitoring Service at ${url} — ${reason}. ` +
      `Verify the gateway is running and ConnectionStrings__MonitoringDb is set.`,
    );
  } finally {
    clearTimeout(timeout);
  }

  if (!res.ok) {
    throw new Error(
      `[monitoring-source] Monitoring Service returned HTTP ${res.status} from ${url}. ` +
      `Verify the Monitoring Service is reachable via CONTROL_CENTER_API_BASE and ConnectionStrings__MonitoringDb is set.`,
    );
  }

  const data: ServiceSummaryResponse = await res.json();

  // Map service response types to Control Center types.
  const system: SystemHealthSummary = {
    status:           data.system.status as MonitoringStatus,
    lastCheckedAtUtc: data.system.lastCheckedAtUtc,
  };

  const integrations: IntegrationStatus[] = data.integrations.map(i => ({
    name:             i.name,
    status:           i.status as MonitoringStatus,
    latencyMs:        i.latencyMs ?? undefined,
    lastCheckedAtUtc: i.lastCheckedAtUtc ?? new Date().toISOString(),
    category:         i.scope,
  }));

  const alerts: SystemAlert[] = data.alerts.map(a => ({
    id:            a.alertId,
    message:       a.message,
    severity:      a.severity as AlertSeverity,
    createdAtUtc:  a.createdAtUtc,
    entityName:    a.name,          // correlates to IntegrationStatus.name
    resolvedAtUtc: a.resolvedAtUtc, // null = active; string = resolved at this time
  }));

  return { system, integrations, alerts };
}

// ── Public API ────────────────────────────────────────────────────────────────

/**
 * Return a complete monitoring summary: overall system status, per-service
 * integration statuses, and active alerts.
 *
 * In 'local' mode: probes registered services directly (current behavior).
 * In 'service' mode: calls GET /monitoring/summary on the Monitoring Service.
 *   If the Monitoring Service is unavailable (502/503/network error), falls
 *   back to the local probe engine so the page remains functional.
 */
export async function getMonitoringSummary(): Promise<MonitoringSummary> {
  if (MONITORING_SOURCE === 'service') {
    try {
      return await serviceGetMonitoringSummary();
    } catch (err) {
      // If the Monitoring Service is unreachable or the gateway returns 502/503,
      // fall back to the local probe engine so monitoring is still available.
      const msg = err instanceof Error ? err.message : String(err);
      const isServiceUnavailable =
        msg.includes('502') ||
        msg.includes('503') ||
        msg.includes('network error') ||
        msg.includes('timed out') ||
        msg.includes('Cannot reach');
      if (isServiceUnavailable) {
        console.warn('[monitoring-source] Monitoring Service unavailable, falling back to local engine:', msg);
        return localGetMonitoringSummary();
      }
      throw err;
    }
  }

  // Default: local engine (existing behavior, no regression).
  return localGetMonitoringSummary();
}

/**
 * Return only the top-level system health status.
 * Derived from the full summary — convenience helper for dashboard widgets.
 */
export async function getMonitoringStatus(): Promise<SystemHealthSummary> {
  const summary = await getMonitoringSummary();
  return summary.system;
}

/**
 * Return only the active alert list.
 * Derived from the full summary — convenience helper for alert-focused consumers.
 */
export async function getMonitoringAlerts(): Promise<SystemAlert[]> {
  const summary = await getMonitoringSummary();
  return summary.alerts;
}
