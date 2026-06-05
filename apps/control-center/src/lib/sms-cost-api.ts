/**
 * sms-cost-api.ts — LS-NOTIF-SMS-013
 *
 * TypeScript types and API client methods for the Notification Service
 * SMS Cost Analytics endpoints.
 *
 * All methods are server-side only (use notifClient which reads the
 * platform_session HttpOnly cookie).  Never use these from Client Components.
 *
 * Endpoints consumed:
 *   GET /notifications/v1/admin/sms/costs/summary
 *   GET /notifications/v1/admin/sms/costs/trends
 *   GET /notifications/v1/admin/sms/costs/providers
 *   GET /notifications/v1/admin/sms/costs/tenants
 *   GET /notifications/v1/admin/sms/costs/failures
 *   GET /notifications/v1/admin/sms/costs/export
 *
 * Security guarantees (enforced by Notification Service):
 *   - No credentials, CredentialsJson, or SettingsJson in any response.
 *   - No full phone numbers or recipient PII in any response.
 *   - All endpoints require PlatformAdmin JWT role.
 *   - All operations are read-only.
 *   - Not an invoicing engine — all amounts are operational estimates only.
 */

import { notifClient } from '@/lib/notifications-api';

// ── Query model ───────────────────────────────────────────────────────────────

export interface SmsCostQuery {
  tenantId?:              string;
  provider?:              string;
  providerConfigId?:      string;
  providerOwnershipMode?: string;
  status?:                string;
  failureCategory?:       string;
  costSource?:            string;
  currency?:              string;
  from?:                  string;   // ISO-8601 UTC
  to?:                    string;   // ISO-8601 UTC
  bucket?:                string;   // 'hour' | 'day' | 'week'
  limit?:                 number;
}

// ── Summary ───────────────────────────────────────────────────────────────────

export interface SmsCostSummary {
  totalAttempts:           number;
  costedAttempts:          number;
  uncostedAttempts:        number;
  totalEffectiveCost:      number;
  totalEstimatedCost:      number;
  totalActualCost:         number;
  deliveredCost:           number;
  sentCost:                number;
  failedCost:              number;
  deadLetterCost:          number;
  retryCost:               number;
  tenantOwnedCost:         number;
  platformOwnedCost:       number;
  costPerDeliveredMessage: number | null;
  deliveredCount:          number;
  failedCount:             number;
  currency:                string;
  estimatedCostCount:      number;
  providerReconciledCount: number;
  unavailableCount:        number;
  earliestAt:              string | null;
  latestAt:                string | null;
}

// ── Trends ────────────────────────────────────────────────────────────────────

export interface SmsCostTrendPoint {
  bucketStart:        string;
  bucketEnd:          string;
  totalAttempts:      number;
  costedAttempts:     number;
  totalEffectiveCost: number;
  deliveredCost:      number;
  failedCost:         number;
  retryCost:          number;
  currency:           string;
}

export interface SmsCostTrendResult {
  bucket:      string;
  windowFrom:  string;
  windowTo:    string;
  points:      SmsCostTrendPoint[];
  currency:    string;
}

// ── Provider breakdown ────────────────────────────────────────────────────────

export interface SmsCostProviderItem {
  provider:                string;
  providerConfigId:        string | null;
  providerOwnershipMode:   string;
  totalAttempts:           number;
  deliveredAttempts:       number;
  failedAttempts:          number;
  costedAttempts:          number;
  totalEffectiveCost:      number;
  costPerDeliveredMessage: number | null;
  currency:                string;
  latestActivityAt:        string | null;
}

export interface SmsCostProviderResult {
  items:                   SmsCostProviderItem[];
  totalProviderConfigs:    number;
  grandTotalEffectiveCost: number;
  currency:                string;
}

// ── Tenant breakdown ──────────────────────────────────────────────────────────

export interface SmsCostTenantItem {
  tenantId:                string | null;
  totalAttempts:           number;
  deliveredAttempts:       number;
  failedAttempts:          number;
  costedAttempts:          number;
  totalEffectiveCost:      number;
  costPerDeliveredMessage: number | null;
  currency:                string;
  latestActivityAt:        string | null;
}

export interface SmsCostTenantResult {
  items:                   SmsCostTenantItem[];
  totalTenants:            number;
  grandTotalEffectiveCost: number;
  currency:                string;
}

// ── Failure breakdown ─────────────────────────────────────────────────────────

export interface SmsCostFailureItem {
  failureCategory:     string;
  isRetry:             boolean;
  count:               number;
  costedCount:         number;
  totalEffectiveCost:  number;
  currency:            string;
  latestOccurrenceAt:  string | null;
}

export interface SmsCostFailureResult {
  items:               SmsCostFailureItem[];
  totalFailedAttempts: number;
  totalFailedCost:     number;
  totalRetryCost:      number;
  currency:            string;
}

// ── Export ────────────────────────────────────────────────────────────────────

export interface SmsCostExportRow {
  attemptId:             string;
  notificationId:        string;
  tenantId:              string | null;
  provider:              string;
  providerConfigId:      string | null;
  providerOwnershipMode: string | null;
  status:                string;
  failureCategory:       string | null;
  attemptNumber:         number;
  isRetry:               boolean;
  estimatedCostAmount:   number | null;
  actualCostAmount:      number | null;
  effectiveCostAmount:   number | null;
  costCurrency:          string | null;
  costSource:            string | null;
  costRecordedAt:        string | null;
  createdAt:             string;
  completedAt:           string | null;
}

export interface SmsCostExportResult {
  rows:        SmsCostExportRow[];
  totalRows:   number;
  truncated:   boolean;
  limit:       number;
  currency:    string;
  generatedAt: string;
}

// ── API client helpers ────────────────────────────────────────────────────────

function buildQs(q: SmsCostQuery): string {
  const p = new URLSearchParams();
  if (q.tenantId)              p.set('tenantId',              q.tenantId);
  if (q.provider)              p.set('provider',              q.provider);
  if (q.providerConfigId)      p.set('providerConfigId',      q.providerConfigId);
  if (q.providerOwnershipMode) p.set('providerOwnershipMode', q.providerOwnershipMode);
  if (q.status)                p.set('status',                q.status);
  if (q.failureCategory)       p.set('failureCategory',       q.failureCategory);
  if (q.costSource)            p.set('costSource',            q.costSource);
  if (q.currency)              p.set('currency',              q.currency);
  if (q.from)                  p.set('from',                  q.from);
  if (q.to)                    p.set('to',                    q.to);
  if (q.bucket)                p.set('bucket',                q.bucket);
  if (q.limit != null)         p.set('limit',                 String(q.limit));
  const qs = p.toString();
  return qs ? `?${qs}` : '';
}

// ── API methods ───────────────────────────────────────────────────────────────
// All use notifClient.get<T> — server-side only (reads platform_session cookie).
// Corresponds to: GET /notifications/v1/admin/sms/costs/...

export const smsCostApi = {
  getSummary: (q: SmsCostQuery = {}) =>
    notifClient.get<SmsCostSummary>(`/admin/sms/costs/summary${buildQs(q)}`),

  getTrends: (q: SmsCostQuery = {}) =>
    notifClient.get<SmsCostTrendResult>(`/admin/sms/costs/trends${buildQs(q)}`),

  getProviders: (q: SmsCostQuery = {}) =>
    notifClient.get<SmsCostProviderResult>(`/admin/sms/costs/providers${buildQs(q)}`),

  getTenants: (q: SmsCostQuery = {}) =>
    notifClient.get<SmsCostTenantResult>(`/admin/sms/costs/tenants${buildQs(q)}`),

  getFailures: (q: SmsCostQuery = {}) =>
    notifClient.get<SmsCostFailureResult>(`/admin/sms/costs/failures${buildQs(q)}`),

  getExport: (q: SmsCostQuery = {}) =>
    notifClient.get<SmsCostExportResult>(`/admin/sms/costs/export${buildQs(q)}`),
};

// Named exports for convenience
export const getSmsCostSummary   = smsCostApi.getSummary;
export const getSmsCostTrends    = smsCostApi.getTrends;
export const getSmsCostProviders = smsCostApi.getProviders;
export const getSmsCostTenants   = smsCostApi.getTenants;
export const getSmsCostFailures  = smsCostApi.getFailures;
export const getSmsCostExport    = smsCostApi.getExport;
