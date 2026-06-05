/**
 * audit-alerts-client-api.ts — Client-safe audit alerts API.
 *
 * Uses plain fetch with credentials: 'include' so the browser's session
 * cookies are sent automatically.  The Next.js rewrite rule proxies all
 * /audit-service/* paths to the gateway, so relative URLs work in both
 * development and production.
 *
 * Safe to import from Client Components ('use client').
 */

import type {
  AuditAlertListData,
  AuditAlertItem,
  AuditEvaluateAlertsData,
} from '@/types/control-center';

async function clientFetch<T>(url: string, options?: RequestInit): Promise<T | null> {
  try {
    const res = await fetch(url, {
      ...options,
      credentials: 'include',
      headers: {
        'Content-Type': 'application/json',
        ...(options?.headers ?? {}),
      },
    });
    if (!res.ok) return null;
    const json = await res.json() as Record<string, unknown>;
    return (json['data'] ?? json) as T;
  } catch {
    return null;
  }
}

export const auditAlertsClientApi = {
  list: async (params: { status?: string; tenantId?: string; limit?: number } = {}): Promise<AuditAlertListData | null> => {
    const qs = new URLSearchParams();
    if (params.status)   qs.set('status',   params.status);
    if (params.tenantId) qs.set('tenantId', params.tenantId);
    if (params.limit)    qs.set('limit',    String(params.limit));
    const url = `/audit-service/audit/analytics/alerts${qs.size > 0 ? `?${qs.toString()}` : ''}`;
    const data = await clientFetch<Record<string, unknown>>(url);
    if (!data) return null;
    return {
      statusFilter:      (data['statusFilter']      as string | null)   ?? null,
      effectiveTenantId: (data['effectiveTenantId'] as string | null)   ?? null,
      totalReturned:     (data['totalReturned']     as number)           ?? 0,
      openCount:         (data['openCount']         as number)           ?? 0,
      acknowledgedCount: (data['acknowledgedCount'] as number)           ?? 0,
      resolvedCount:     (data['resolvedCount']     as number)           ?? 0,
      alerts:            Array.isArray(data['alerts']) ? (data['alerts'] as AuditAlertItem[]) : [],
    };
  },

  evaluate: async (params: { tenantId?: string } = {}): Promise<AuditEvaluateAlertsData | null> => {
    const qs = new URLSearchParams();
    if (params.tenantId) qs.set('tenantId', params.tenantId);
    const url = `/audit-service/audit/analytics/alerts/evaluate${qs.size > 0 ? `?${qs.toString()}` : ''}`;
    const data = await clientFetch<Record<string, unknown>>(url, { method: 'POST', body: '{}' });
    if (!data) return null;
    return {
      evaluatedAt:       (data['evaluatedAt']       as string)           ?? '',
      effectiveTenantId: (data['effectiveTenantId'] as string | null)    ?? null,
      anomaliesDetected: (data['anomaliesDetected'] as number)           ?? 0,
      alertsCreated:     (data['alertsCreated']     as number)           ?? 0,
      alertsRefreshed:   (data['alertsRefreshed']   as number)           ?? 0,
      alertsSuppressed:  (data['alertsSuppressed']  as number)           ?? 0,
      activeAlerts:      Array.isArray(data['activeAlerts']) ? (data['activeAlerts'] as AuditAlertItem[]) : [],
    };
  },

  acknowledge: async (alertId: string): Promise<boolean> => {
    const res = await fetch(
      `/audit-service/audit/analytics/alerts/${encodeURIComponent(alertId)}/acknowledge`,
      { method: 'POST', credentials: 'include', body: '{}', headers: { 'Content-Type': 'application/json' } },
    );
    return res.ok;
  },

  resolve: async (alertId: string): Promise<boolean> => {
    const res = await fetch(
      `/audit-service/audit/analytics/alerts/${encodeURIComponent(alertId)}/resolve`,
      { method: 'POST', credentials: 'include', body: '{}', headers: { 'Content-Type': 'application/json' } },
    );
    return res.ok;
  },
};
