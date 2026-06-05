/**
 * sms-dashboard-api.ts — LS-NOTIF-SMS-009
 *
 * TypeScript types and API client methods for the Notification Service
 * SMS Dashboard endpoints (LS-NOTIF-SMS-008).
 *
 * All methods are server-side only (use notifClient which reads the
 * platform_session HttpOnly cookie).  Never use these from Client Components.
 *
 * Endpoints consumed:
 *   GET /notifications/v1/admin/sms/dashboard/summary
 *   GET /notifications/v1/admin/sms/dashboard/trends
 *   GET /notifications/v1/admin/sms/dashboard/failures
 *   GET /notifications/v1/admin/sms/dashboard/tenants
 *   GET /notifications/v1/admin/sms/dashboard/providers
 *
 * Security guarantees (enforced by Notification Service):
 *   - No credentials, CredentialsJson, or SettingsJson in any response.
 *   - No full phone numbers or recipient PII in any response.
 *   - All endpoints require PlatformAdmin JWT role.
 *   - All operations are read-only.
 */

import { notifClient } from '@/lib/notifications-api';

// ── Query model ───────────────────────────────────────────────────────────────

export interface SmsDashboardQuery {
  tenantId?:             string;
  provider?:             string;
  providerConfigId?:     string;
  providerOwnershipMode?: string;
  status?:               string;
  failureCategory?:      string;
  from?:                 string;  // ISO-8601 UTC
  to?:                   string;  // ISO-8601 UTC
  bucket?:               string;  // 'hour' | 'day' | 'week'
  limit?:                number;
}

// ── Summary ───────────────────────────────────────────────────────────────────

export interface SmsDashboardSummary {
  totalAttempts:                    number;
  sentCount:                        number;
  deliveredCount:                   number;
  failedCount:                      number;
  deadLetterCount:                  number;
  pendingCount:                     number;
  processingCount:                  number;
  sendingCount:                     number;
  retryingCount:                    number;
  tenantOwnedCount:                 number;
  platformOwnedCount:               number;
  unknownOwnershipCount:            number;
  reconciledTotal:                  number;
  neverReconciled:                  number;
  reconciliationUpdated:            number;
  reconciliationNoChange:           number;
  reconciliationLookupFailed:       number;
  reconciliationSkipped:            number;
  reconciliationProviderConfigFailed: number;
  uniqueTenantCount:                number;
  uniqueProviderCount:              number;
  uniqueProviderConfigCount:        number;
  earliestAt:                       string | null;
  latestAt:                         string | null;
}

// ── Trends ────────────────────────────────────────────────────────────────────

export interface SmsDashboardTrendPoint {
  bucketStart:               string;
  bucketEnd:                 string;
  totalAttempts:             number;
  sentCount:                 number;
  deliveredCount:            number;
  failedCount:               number;
  pendingCount:              number;
  reconciledTotal:           number;
  reconciliationLookupFailed: number;
}

export interface SmsDashboardTrendResult {
  bucket:     string;
  windowFrom: string;
  windowTo:   string;
  points:     SmsDashboardTrendPoint[];
}

// ── Failure breakdown ─────────────────────────────────────────────────────────

export interface SmsDashboardFailureItem {
  failureCategory:    string;
  errorCode:          string | null;
  count:              number;
  latestOccurrenceAt: string;
}

export interface SmsDashboardFailureResult {
  items:               SmsDashboardFailureItem[];
  totalFailedAttempts: number;
}

// ── Tenant breakdown ──────────────────────────────────────────────────────────

export interface SmsDashboardTenantItem {
  tenantId:          string | null;
  totalAttempts:     number;
  sentCount:         number;
  deliveredCount:    number;
  failedCount:       number;
  pendingCount:      number;
  reconciledTotal:   number;
  neverReconciled:   number;
  tenantOwnedCount:  number;
  platformOwnedCount: number;
  latestActivityAt:  string;
}

export interface SmsDashboardTenantResult {
  items:        SmsDashboardTenantItem[];
  totalTenants: number;
}

// ── Provider breakdown ────────────────────────────────────────────────────────

export interface SmsDashboardProviderItem {
  provider:                  string;
  providerConfigId:          string | null;
  providerOwnershipMode:     string;
  totalAttempts:             number;
  sentCount:                 number;
  deliveredCount:            number;
  failedCount:               number;
  reconciledTotal:           number;
  reconciliationLookupFailed: number;
  latestActivityAt:          string;
}

export interface SmsDashboardProviderResult {
  items:               SmsDashboardProviderItem[];
  totalProviderConfigs: number;
}

// ── Query string builder ──────────────────────────────────────────────────────

function buildQs(q: SmsDashboardQuery): string {
  const p = new URLSearchParams();
  if (q.tenantId)              p.set('tenantId',             q.tenantId);
  if (q.provider)              p.set('provider',             q.provider);
  if (q.providerConfigId)      p.set('providerConfigId',     q.providerConfigId);
  if (q.providerOwnershipMode) p.set('providerOwnershipMode', q.providerOwnershipMode);
  if (q.status)                p.set('status',               q.status);
  if (q.failureCategory)       p.set('failureCategory',      q.failureCategory);
  if (q.from)                  p.set('from',                 q.from);
  if (q.to)                    p.set('to',                   q.to);
  if (q.bucket)                p.set('bucket',               q.bucket);
  if (q.limit !== undefined)   p.set('limit',                String(q.limit));
  const s = p.toString();
  return s ? `?${s}` : '';
}

// ── API client ────────────────────────────────────────────────────────────────

export const smsDashboardApi = {
  /**
   * High-level SMS delivery and reconciliation KPI aggregate.
   * Respects all filters in the query.
   */
  getSummary: (q: SmsDashboardQuery = {}) =>
    notifClient.get<SmsDashboardSummary>(`/admin/sms/dashboard/summary${buildQs(q)}`),

  /**
   * Time-series trend data bucketed by hour/day/week.
   * Default window: last 30 days when from/to are omitted.
   */
  getTrends: (q: SmsDashboardQuery = {}) =>
    notifClient.get<SmsDashboardTrendResult>(`/admin/sms/dashboard/trends${buildQs(q)}`),

  /**
   * Failure category and error code breakdown.
   * Groups rows with Status=failed/dead_letter or non-null FailureCategory.
   */
  getFailures: (q: SmsDashboardQuery = {}) =>
    notifClient.get<SmsDashboardFailureResult>(`/admin/sms/dashboard/failures${buildQs(q)}`),

  /**
   * Per-tenant SMS activity breakdown (tenantId only, no names).
   * Control Center should enrich names from Identity service in the future.
   */
  getTenants: (q: SmsDashboardQuery = {}) =>
    notifClient.get<SmsDashboardTenantResult>(`/admin/sms/dashboard/tenants${buildQs(q)}`),

  /**
   * Per-provider/config SMS activity breakdown.
   * No credentials or settings are included in responses.
   */
  getProviders: (q: SmsDashboardQuery = {}) =>
    notifClient.get<SmsDashboardProviderResult>(`/admin/sms/dashboard/providers${buildQs(q)}`),
};
