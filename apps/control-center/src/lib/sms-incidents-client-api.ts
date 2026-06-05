/**
 * sms-incidents-client-api.ts — LS-NOTIF-SMS-012
 *
 * Browser-safe mutation client for SMS incident management.
 *
 * Uses plain fetch with credentials: 'include' so the browser session
 * cookie is sent automatically. Paths go through the Next.js BFF fallback
 * rewrite:  /api/:path* → gateway/:path*
 *
 * Safe to import from Client Components ('use client').
 *
 * Security:
 *   - Never logs or returns raw webhook URLs, emails, or phone numbers.
 *   - All routes require PlatformAdmin (enforced server-side by gateway + Notification Service).
 *   - The `target` field in createPolicy is write-only — never pre-filled, never displayed.
 */

import type {
  SmsEscalationPolicyDto,
  CreateSmsEscalationPolicyRequest,
  UpdateSmsEscalationPolicyRequest,
} from '@/lib/sms-incidents-api';

const BASE = '/api/notifications/v1/admin/sms/alerts';

// ── Internal helpers ──────────────────────────────────────────────────────────

async function clientPost(url: string, body?: unknown): Promise<boolean> {
  try {
    const res = await fetch(url, {
      method:      'POST',
      credentials: 'include',
      headers:     { 'Content-Type': 'application/json' },
      body:        JSON.stringify(body ?? {}),
    });
    return res.ok;
  } catch {
    return false;
  }
}

async function clientPostJson<T>(url: string, body: unknown): Promise<T | null> {
  try {
    const res = await fetch(url, {
      method:      'POST',
      credentials: 'include',
      headers:     { 'Content-Type': 'application/json' },
      body:        JSON.stringify(body),
    });
    if (!res.ok) return null;
    const text = await res.text();
    if (!text) return null;
    return JSON.parse(text) as T;
  } catch {
    return null;
  }
}

async function clientPutJson<T>(url: string, body: unknown): Promise<T | null> {
  try {
    const res = await fetch(url, {
      method:      'PUT',
      credentials: 'include',
      headers:     { 'Content-Type': 'application/json' },
      body:        JSON.stringify(body),
    });
    if (!res.ok) return null;
    const text = await res.text();
    if (!text) return null;
    return JSON.parse(text) as T;
  } catch {
    return null;
  }
}

// ── Public API ────────────────────────────────────────────────────────────────

export const smsIncidentsClientApi = {

  // ── Alert actions ───────────────────────────────────────────────────────────

  /**
   * Resolve an active alert. Optionally attach a resolution note (max 1000 chars).
   * Idempotent: resolving an already-resolved alert returns 404 (treated as false).
   */
  resolveAlert: (alertId: string, resolutionNote?: string): Promise<boolean> =>
    clientPost(`${BASE}/${encodeURIComponent(alertId)}/resolve`, {
      resolutionNote: resolutionNote ?? null,
    }),

  /**
   * Suppress an alert for a configurable duration.
   * suppressForMinutes: 1–10080 (7 days). Default 60.
   */
  suppressAlert: (alertId: string, suppressForMinutes: number): Promise<boolean> =>
    clientPost(`${BASE}/${encodeURIComponent(alertId)}/suppress`, { suppressForMinutes }),

  /**
   * Manually trigger one SMS alert evaluation cycle.
   * This runs the rule engine once — it does NOT send any notifications or SMS messages.
   * Creates/updates alerts based on current delivery data.
   */
  evaluateAlerts: (): Promise<boolean> =>
    clientPost(`${BASE}/evaluate`),

  // ── Escalation actions ──────────────────────────────────────────────────────

  /**
   * Retry a failed or errored escalation attempt.
   * Only eligible if status is failed or has retry configured.
   */
  retryEscalation: (escalationId: string): Promise<boolean> =>
    clientPost(`${BASE}/escalations/${encodeURIComponent(escalationId)}/retry`),

  // ── Policy actions ──────────────────────────────────────────────────────────

  /**
   * Soft-disable an escalation policy (sets Enabled = false).
   * The policy remains in the database and can be re-enabled via PUT.
   */
  disablePolicy: (policyId: string): Promise<boolean> =>
    clientPost(`${BASE}/policies/${encodeURIComponent(policyId)}/disable`),

  /**
   * Create a new escalation policy.
   * The `target` field is write-only — it is stored by the backend but never returned.
   * Returns the created policy DTO (with targetMasked instead of raw target).
   */
  createPolicy: (body: CreateSmsEscalationPolicyRequest): Promise<SmsEscalationPolicyDto | null> =>
    clientPostJson<SmsEscalationPolicyDto>(`${BASE}/policies`, body),

  /**
   * Update an existing escalation policy (partial patch via PUT).
   * If `target` is omitted, the existing target is preserved.
   * Returns the updated policy DTO (with targetMasked).
   */
  updatePolicy: (policyId: string, body: UpdateSmsEscalationPolicyRequest): Promise<SmsEscalationPolicyDto | null> =>
    clientPutJson<SmsEscalationPolicyDto>(`${BASE}/policies/${encodeURIComponent(policyId)}`, body),
};
