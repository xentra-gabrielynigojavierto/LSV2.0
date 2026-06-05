/**
 * Health check endpoint — GET /api/health
 *
 * Returns the current status of the Control Center process and
 * optionally validates reachability of the upstream API gateway.
 *
 * Used by:
 *   - Container orchestrators (ECS, Kubernetes) for readiness/liveness probes
 *   - Load balancers (ALB, nginx) for health gate checks
 *   - Monitoring systems (CloudWatch, Datadog, PagerDuty) for uptime alerts
 *
 * Response shape:
 *   {
 *     status:     "ok" | "degraded",   // "degraded" when gateway unreachable
 *     service:    "control-center",
 *     uptime:     number,              // process uptime in seconds
 *     gateway:    "reachable" | "unreachable" | "unknown",
 *     gatewayUrl: string,              // base URL that was checked (no path)
 *     ts:         string,              // ISO 8601 timestamp of this check
 *   }
 *
 * Status codes:
 *   200 — service is healthy (gateway may be unreachable; use "degraded" flag)
 *   200 — service is degraded (gateway probe failed); callers inspect "status"
 *   503 — reserved for future: process is alive but refusing traffic
 *
 * Design notes:
 *   - This route never returns non-2xx for a degraded gateway because the CC
 *     process itself is healthy. The caller decides whether "degraded" should
 *     trigger a downstream alert.
 *   - The gateway check is a HEAD request with a tight 2-second timeout so it
 *     never blocks the health check for more than ~2 seconds.
 *   - The HEAD target is the root of the gateway base URL, not an app path,
 *     to avoid triggering auth middleware on the downstream service.
 *   - No auth is required on this endpoint — it must be callable by the load
 *     balancer before a session exists.
 *
 * TODO: add /api/readiness that validates all required env vars are set
 * TODO: add version field (git SHA or package.json version)
 * TODO: add memory usage field (process.memoryUsage().rss)
 * TODO: cache gateway reachability result for ~5 s to avoid hammering the gateway
 *       during rapid health-check intervals
 */

import { type NextRequest, NextResponse } from 'next/server';
import { CONTROL_CENTER_API_BASE }        from '@/lib/env';

// ── Types ─────────────────────────────────────────────────────────────────────

type GatewayStatus = 'reachable' | 'unreachable' | 'unknown';
type ServiceStatus = 'ok' | 'degraded';

interface HealthPayload {
  status:     ServiceStatus;
  service:    'control-center';
  uptime:     number;
  gateway:    GatewayStatus;
  gatewayUrl: string;
  ts:         string;
}

// ── Gateway probe ─────────────────────────────────────────────────────────────

/**
 * Sends a HEAD request to the root of CONTROL_CENTER_API_BASE.
 * Returns 'reachable' if the gateway responds with any HTTP status within
 * 2 seconds (even a 401/404 proves it is alive), 'unreachable' on network
 * error or timeout, 'unknown' when the URL is clearly a localhost address
 * and we are running in a context where the gateway is expected to be absent.
 */
async function probeGateway(baseUrl: string): Promise<GatewayStatus> {
  let url: URL;
  try {
    url = new URL(baseUrl);
  } catch {
    // Not a valid URL — cannot probe
    return 'unknown';
  }

  const controller = new AbortController();
  const timer = setTimeout(() => controller.abort(), 2_000);

  try {
    await fetch(url.origin, {
      method:  'HEAD',
      signal:  controller.signal,
      cache:   'no-store',
    });
    return 'reachable';
  } catch {
    return 'unreachable';
  } finally {
    clearTimeout(timer);
  }
}

// ── Route handler ─────────────────────────────────────────────────────────────

export async function GET(_request: NextRequest): Promise<NextResponse<HealthPayload>> {
  const gateway    = await probeGateway(CONTROL_CENTER_API_BASE);
  const status: ServiceStatus = gateway === 'unreachable' ? 'degraded' : 'ok';

  // Strip any path component from the gateway URL for the response
  // so we never inadvertently log a path that includes a secret.
  let gatewayUrl = CONTROL_CENTER_API_BASE;
  try {
    gatewayUrl = new URL(CONTROL_CENTER_API_BASE).origin;
  } catch { /* leave as-is */ }

  const payload: HealthPayload = {
    status,
    service:    'control-center',
    uptime:     process.uptime(),
    gateway,
    gatewayUrl,
    ts:         new Date().toISOString(),
  };

  return NextResponse.json(payload, {
    status:  200,
    headers: {
      // Disable caching — health checks must always reflect current state.
      'Cache-Control': 'no-store, no-cache, must-revalidate',
    },
  });
}
