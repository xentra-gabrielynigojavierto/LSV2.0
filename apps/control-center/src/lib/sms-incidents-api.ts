/**
 * sms-incidents-api.ts — LS-NOTIF-SMS-012
 *
 * Server-side TypeScript API client for the Notification Service
 * SMS Operational Alert & Escalation endpoints (LS-NOTIF-SMS-010/011).
 *
 * All methods use notifClient and are server-side only.
 * Never import from Client Components.
 *
 * Endpoints consumed:
 *   GET /notifications/v1/admin/sms/alerts/             — alert list
 *   GET /notifications/v1/admin/sms/alerts/summary      — alert aggregate
 *   GET /notifications/v1/admin/sms/alerts/escalations  — escalation history
 *   GET /notifications/v1/admin/sms/alerts/escalations/summary
 *   GET /notifications/v1/admin/sms/alerts/policies     — policy list
 *   GET /notifications/v1/admin/sms/alerts/policies/{id}
 *
 * Security:
 *   - All endpoints require PlatformAdmin JWT role (enforced by Notification Service).
 *   - No raw targets, webhook URLs, phone numbers, or credentials in any response.
 *   - TargetMasked is the only target field ever returned.
 */

import { notifClient } from '@/lib/notifications-api';

// ── Alert DTOs (LS-NOTIF-SMS-010) ─────────────────────────────────────────────

export interface SmsAlertDto {
  id:                    string;
  alertType:             string;
  severity:              string;
  tenantId:              string | null;
  provider:              string | null;
  providerConfigId:      string | null;
  metricValue:           number;
  thresholdValue:        number;
  message:               string;
  evaluationWindowStart: string;
  evaluationWindowEnd:   string;
  status:                string;
  occurrenceCount:       number;
  firstObservedAt:       string;
  lastObservedAt:        string;
  resolvedAt:            string | null;
  resolvedBy:            string | null;
  resolutionNote:        string | null;
  suppressedUntil:       string | null;
  createdAt:             string;
  updatedAt:             string;
}

export interface SmsAlertListResult {
  items:  SmsAlertDto[];
  total:  number;
  limit:  number;
  offset: number;
}

export interface SmsAlertSummaryDto {
  activeCount:         number;
  resolvedCount:       number;
  suppressedCount:     number;
  totalCount:          number;
  criticalActiveCount: number;
  warningActiveCount:  number;
  activeByType:        Record<string, number>;
}

// ── Escalation DTOs (LS-NOTIF-SMS-011) ───────────────────────────────────────

export interface SmsAlertEscalationDto {
  id:             string;
  alertId:        string;
  policyId:       string | null;
  channelType:    string;
  targetMasked:   string | null;
  severity:       string;
  status:         string;
  attemptCount:   number;
  lastAttemptAt:  string | null;
  sentAt:         string | null;
  failureReason:  string | null;
  nextRetryAt:    string | null;
  suppressedUntil: string | null;
  payloadHash:    string | null;
  createdAt:      string;
  updatedAt:      string;
}

export interface SmsAlertEscalationListResult {
  items:  SmsAlertEscalationDto[];
  total:  number;
  limit:  number;
  offset: number;
}

export interface SmsEscalationSummaryDto {
  totalCount:      number;
  sentCount:       number;
  failedCount:     number;
  pendingCount:    number;
  suppressedCount: number;
  skippedCount:    number;
  byChannel:       Record<string, number>;
  byStatus:        Record<string, number>;
}

// ── Policy DTOs (LS-NOTIF-SMS-011) ────────────────────────────────────────────

export interface SmsEscalationPolicyDto {
  id:               string;
  name:             string;
  enabled:          boolean;
  alertType:        string | null;
  severity:         string | null;
  tenantId:         string | null;
  provider:         string | null;
  providerConfigId: string | null;
  channelType:      string;
  targetMasked:     string;
  targetDisplay:    string | null;
  cooldownMinutes:  number;
  retryEnabled:     boolean;
  maxRetryCount:    number;
  createdAt:        string;
  updatedAt:        string;
  createdBy:        string | null;
  updatedBy:        string | null;
}

export interface SmsEscalationPolicyListResult {
  items:  SmsEscalationPolicyDto[];
  total:  number;
  limit:  number;
  offset: number;
}

// ── Create / Update request shapes (for client-side forms) ────────────────────

export interface CreateSmsEscalationPolicyRequest {
  name:             string;
  enabled:          boolean;
  channelType:      string;
  target:           string;
  targetDisplay?:   string;
  alertType?:       string;
  severity?:        string;
  tenantId?:        string;
  provider?:        string;
  providerConfigId?: string;
  cooldownMinutes:  number;
  retryEnabled:     boolean;
  maxRetryCount:    number;
}

export interface UpdateSmsEscalationPolicyRequest {
  name?:            string;
  enabled?:         boolean;
  channelType?:     string;
  target?:          string;
  targetDisplay?:   string;
  alertType?:       string;
  severity?:        string;
  tenantId?:        string;
  provider?:        string;
  providerConfigId?: string;
  cooldownMinutes?: number;
  retryEnabled?:    boolean;
  maxRetryCount?:   number;
}

// ── Query helpers ─────────────────────────────────────────────────────────────

function buildQs(params: Record<string, string | number | boolean | null | undefined>): string {
  const p = new URLSearchParams();
  for (const [k, v] of Object.entries(params)) {
    if (v !== null && v !== undefined && v !== '') p.set(k, String(v));
  }
  const s = p.toString();
  return s ? `?${s}` : '';
}

// ── Alert query params ─────────────────────────────────────────────────────────

export interface SmsAlertListParams {
  status?:           string;
  severity?:         string;
  alertType?:        string;
  tenantId?:         string;
  provider?:         string;
  providerConfigId?: string;
  limit?:            number;
  offset?:           number;
}

// ── Escalation query params ────────────────────────────────────────────────────

export interface SmsEscalationListParams {
  alertId?:    string;
  policyId?:   string;
  status?:     string;
  channelType?: string;
  severity?:   string;
  limit?:      number;
  offset?:     number;
}

// ── Policy query params ────────────────────────────────────────────────────────

export interface SmsPolicyListParams {
  enabled?:     boolean;
  channelType?: string;
  alertType?:   string;
  severity?:    string;
  limit?:       number;
  offset?:      number;
}

// ── API client ────────────────────────────────────────────────────────────────

export const smsIncidentsApi = {

  // ── Alerts ─────────────────────────────────────────────────────────────────

  listAlerts: (params: SmsAlertListParams = {}) =>
    notifClient.get<SmsAlertListResult>(
      `/admin/sms/alerts/${buildQs(params as Record<string, string | number | boolean | null | undefined>)}`
    ),

  getAlertSummary: () =>
    notifClient.get<SmsAlertSummaryDto>('/admin/sms/alerts/summary'),

  // ── Escalations ────────────────────────────────────────────────────────────

  listEscalations: (params: SmsEscalationListParams = {}) =>
    notifClient.get<SmsAlertEscalationListResult>(
      `/admin/sms/alerts/escalations${buildQs(params as Record<string, string | number | boolean | null | undefined>)}`
    ),

  getEscalationSummary: (params: { alertId?: string } = {}) =>
    notifClient.get<SmsEscalationSummaryDto>(
      `/admin/sms/alerts/escalations/summary${buildQs(params as Record<string, string | number | boolean | null | undefined>)}`
    ),

  // ── Policies ───────────────────────────────────────────────────────────────

  listPolicies: (params: SmsPolicyListParams = {}) =>
    notifClient.get<SmsEscalationPolicyListResult>(
      `/admin/sms/alerts/policies${buildQs(params as Record<string, string | number | boolean | null | undefined>)}`
    ),

  getPolicyById: (id: string) =>
    notifClient.get<SmsEscalationPolicyDto>(`/admin/sms/alerts/policies/${encodeURIComponent(id)}`),
};
