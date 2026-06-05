'use server';

/**
 * monitoring.ts — Server Actions for Monitoring admin operations.
 *
 * resolveAlertAction(alertId) — resolves an active alert through the Monitoring
 *   Service admin API (`POST /monitoring/admin/alerts/{id}/resolve`).
 *
 * ── Security ─────────────────────────────────────────────────────────────────
 *
 *   The Monitoring Service admin endpoint is protected by MonitoringAdmin policy.
 *   Satisfied by a service token (HMAC-HS256) with:
 *     iss: "legalsynq-service-tokens"
 *     aud: "monitoring-service"
 *     sub: "service:control-center"
 *   Minted here server-side using FLOW_SERVICE_TOKEN_SECRET.
 *   Token TTL is 60 seconds — single-use action token.
 *
 *   The secret NEVER reaches the browser. This action runs only on the server.
 *
 * ── Idempotency ──────────────────────────────────────────────────────────────
 *
 *   Monitoring Service returns HTTP 200 with status="already_resolved" if the
 *   alert is already resolved. The CC treats this as success (no error shown).
 *
 * ── Audit ────────────────────────────────────────────────────────────────────
 *
 *   The Monitoring Service does not yet emit a dedicated audit event for manual
 *   resolve. The action is recorded via the UpdatedAtUtc timestamp on the alert
 *   row (via IAuditableEntity / SaveChanges interceptor). A structured audit
 *   event can be added to this action once the platform Audit Event bus is
 *   available to the CC server layer (MON-INT-03-002 §9).
 */

import crypto from 'crypto';

export interface ResolveAlertResult {
  ok:     boolean;
  error?: string;
}

function mintServiceToken(): string {
  const secret = process.env.FLOW_SERVICE_TOKEN_SECRET;
  if (!secret) {
    throw new Error('FLOW_SERVICE_TOKEN_SECRET is not set — cannot mint service token.');
  }

  const b64url = (data: string) =>
    Buffer.from(data).toString('base64url');

  const header  = b64url(JSON.stringify({ alg: 'HS256', typ: 'JWT' }));
  const now     = Math.floor(Date.now() / 1000);
  const payload = b64url(JSON.stringify({
    iss: 'legalsynq-service-tokens',
    aud: 'monitoring-service',
    sub: 'service:control-center',
    iat: now,
    exp: now + 60,
  }));

  const sig = Buffer.from(
    crypto.createHmac('sha256', secret)
      .update(`${header}.${payload}`)
      .digest()
  ).toString('base64url');

  return `${header}.${payload}.${sig}`;
}

export async function resolveAlertAction(alertId: string): Promise<ResolveAlertResult> {
  if (!alertId || typeof alertId !== 'string') {
    return { ok: false, error: 'Invalid alert ID.' };
  }

  try {
    const gatewayBase = process.env.GATEWAY_URL ?? 'http://localhost:5010';
    const token       = mintServiceToken();
    const url         = `${gatewayBase}/monitoring/monitoring/admin/alerts/${alertId}/resolve`;

    const res = await fetch(url, {
      method:  'POST',
      headers: {
        'Authorization': `Bearer ${token}`,
        'Content-Type':  'application/json',
      },
      cache: 'no-store',
    });

    if (res.status === 404) {
      return { ok: false, error: 'Alert not found — it may have already been removed.' };
    }

    if (!res.ok) {
      const text = await res.text().catch(() => res.statusText);
      return { ok: false, error: `Monitoring Service error (HTTP ${res.status}): ${text}` };
    }

    return { ok: true };

  } catch (err) {
    const message = err instanceof Error ? err.message : 'Unknown error contacting Monitoring Service.';
    return { ok: false, error: message };
  }
}
