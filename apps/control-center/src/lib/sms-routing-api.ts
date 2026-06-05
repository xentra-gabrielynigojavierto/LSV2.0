/**
 * LS-NOTIF-SMS-014: Control Center API client for SMS Routing admin endpoints.
 * All endpoints require PlatformAdmin role.
 * No credentials, CredentialsJson, SettingsJson, auth tokens, or raw phone numbers
 * are returned by these endpoints.
 */

const API_BASE = '/api';

// ── Types ─────────────────────────────────────────────────────────────────────

export interface SmsProviderCapability {
  providerType: string;
  displayName: string;
  supportsSend: boolean;
  supportsStatusLookup: boolean;
  supportsHealthCheck: boolean;
  supportsCostEstimate: boolean;
  supportsRegionalRouting: boolean;
  supportsTenantOwnedConfig: boolean;
  supportsPlatformConfig: boolean;
  supportedCountries: string | null;
  defaultCurrency: string | null;
  notes: string | null;
}

export interface SmsRoutingPolicy {
  id: string;
  tenantId: string | null;
  name: string;
  enabled: boolean;
  region: string | null;
  countryCode: string | null;
  routingMode: string;
  preferredProvidersJson: string | null;
  excludedProvidersJson: string | null;
  maxEstimatedCostPerMessage: number | null;
  requireHealthyProvider: boolean;
  fallbackToPlatform: boolean;
  priority: number;
  createdAt: string;
  updatedAt: string;
  createdBy: string | null;
  updatedBy: string | null;
}

export interface SmsRoutingPolicyListResult {
  items: SmsRoutingPolicy[];
  total: number;
  limit: number;
  offset: number;
}

export interface CreateSmsRoutingPolicyRequest {
  tenantId?: string;
  name: string;
  enabled: boolean;
  region?: string;
  countryCode?: string;
  routingMode: string;
  preferredProvidersJson?: string;
  excludedProvidersJson?: string;
  maxEstimatedCostPerMessage?: number;
  requireHealthyProvider: boolean;
  fallbackToPlatform: boolean;
  priority: number;
}

export interface SmsRoutingDecision {
  id: string;
  tenantId: string | null;
  notificationId: string | null;
  attemptId: string | null;
  routingPolicyId: string | null;
  routingMode: string;
  selectedProvider: string;
  selectedProviderConfigId: string | null;
  providerOwnershipMode: string | null;
  candidateProvidersJson: string | null;
  excludedProvidersJson: string | null;
  decisionReason: string;
  estimatedCostAmount: number | null;
  costCurrency: string | null;
  region: string | null;
  countryCode: string | null;
  createdAt: string;
}

export interface SmsRoutingDecisionListResult {
  items: SmsRoutingDecision[];
  total: number;
  limit: number;
  offset: number;
}

export interface SmsRoutingDecisionSummary {
  totalDecisions: number;
  byMode: Record<string, number>;
  byProvider: Record<string, number>;
  priorityModeCount: number;
  costOptimizedCount: number;
  healthOptimizedCount: number;
  hybridCount: number;
  regionalCount: number;
  noRouteCount: number;
}

export interface SmsProviderHealth {
  providerType: string;
  ownershipMode: string | null;
  providerConfigId: string | null;
  healthStatus: string;
  latencyMs: number | null;
  checkedAt: string | null;
}

// ── API functions ─────────────────────────────────────────────────────────────

async function fetchAdmin<T>(path: string, options?: RequestInit): Promise<T> {
  const res = await fetch(`${API_BASE}${path}`, {
    ...options,
    credentials: 'include',
    headers: { 'Content-Type': 'application/json', ...options?.headers },
  });
  if (!res.ok) {
    const text = await res.text().catch(() => res.statusText);
    throw new Error(`SMS Routing API error ${res.status}: ${text}`);
  }
  return res.json() as Promise<T>;
}

export async function getSmsProviderCapabilities(): Promise<{ items: SmsProviderCapability[]; total: number }> {
  return fetchAdmin('/notifications/v1/admin/sms/routing/capabilities');
}

export async function getSmsRoutingPolicies(params?: {
  tenantId?: string;
  enabled?: boolean;
  routingMode?: string;
  limit?: number;
  offset?: number;
}): Promise<SmsRoutingPolicyListResult> {
  const q = new URLSearchParams();
  if (params?.tenantId)    q.set('tenantId',    params.tenantId);
  if (params?.enabled != null) q.set('enabled', String(params.enabled));
  if (params?.routingMode) q.set('routingMode', params.routingMode);
  if (params?.limit)       q.set('limit',       String(params.limit));
  if (params?.offset)      q.set('offset',      String(params.offset));
  return fetchAdmin(`/notifications/v1/admin/sms/routing/policies?${q}`);
}

export async function getSmsRoutingPolicy(id: string): Promise<SmsRoutingPolicy> {
  return fetchAdmin(`/notifications/v1/admin/sms/routing/policies/${id}`);
}

export async function createSmsRoutingPolicy(body: CreateSmsRoutingPolicyRequest): Promise<SmsRoutingPolicy> {
  return fetchAdmin('/notifications/v1/admin/sms/routing/policies', {
    method: 'POST',
    body: JSON.stringify(body),
  });
}

export async function updateSmsRoutingPolicy(id: string, body: Omit<CreateSmsRoutingPolicyRequest, 'tenantId'>): Promise<SmsRoutingPolicy> {
  return fetchAdmin(`/notifications/v1/admin/sms/routing/policies/${id}`, {
    method: 'PUT',
    body: JSON.stringify(body),
  });
}

export async function disableSmsRoutingPolicy(id: string): Promise<{ message: string; policy: SmsRoutingPolicy }> {
  return fetchAdmin(`/notifications/v1/admin/sms/routing/policies/${id}/disable`, { method: 'POST' });
}

export async function getSmsRoutingDecisions(params?: {
  tenantId?: string;
  provider?: string;
  routingMode?: string;
  policyId?: string;
  limit?: number;
  offset?: number;
}): Promise<SmsRoutingDecisionListResult> {
  const q = new URLSearchParams();
  if (params?.tenantId)    q.set('tenantId',    params.tenantId);
  if (params?.provider)    q.set('provider',    params.provider);
  if (params?.routingMode) q.set('routingMode', params.routingMode);
  if (params?.policyId)    q.set('policyId',    params.policyId);
  if (params?.limit)       q.set('limit',       String(params.limit));
  if (params?.offset)      q.set('offset',      String(params.offset));
  return fetchAdmin(`/notifications/v1/admin/sms/routing/decisions?${q}`);
}

export async function getSmsRoutingDecisionSummary(params?: {
  tenantId?: string;
  provider?: string;
}): Promise<SmsRoutingDecisionSummary> {
  const q = new URLSearchParams();
  if (params?.tenantId) q.set('tenantId', params.tenantId);
  if (params?.provider) q.set('provider', params.provider);
  return fetchAdmin(`/notifications/v1/admin/sms/routing/decisions/summary?${q}`);
}

export async function getSmsProviderHealth(): Promise<{ items: SmsProviderHealth[]; total: number }> {
  return fetchAdmin('/notifications/v1/admin/sms/routing/providers/health');
}

// ── LS-NOTIF-SMS-015: Optimization / Quality types ───────────────────────────

export interface SmsProviderQualityDto {
  providerType: string;
  providerOwnershipMode: string | null;
  countryCode: string | null;
  region: string | null;
  qualityScore: number;
  costEfficiencyScore: number | null;
  deliverySuccessRate: number;
  failureRate: number;
  retryRate: number;
  reconciliationFailureRate: number;
  averageLatencyMs: number | null;
  averageEffectiveCost: number | null;
  costPerDeliveredMessage: number | null;
  totalAttempts: number;
  deliveredAttempts: number;
  hasSufficientData: boolean;
  windowStart: string;
  windowEnd: string;
  calculatedAt: string;
}

export interface SmsQualityListResponse {
  items: SmsProviderQualityDto[];
  total: number;
}

export interface SmsQualityTrendPoint {
  providerType: string;
  countryCode: string | null;
  qualityScore: number;
  calculatedAt: string;
  totalAttempts: number;
}

export interface SmsQualityTrendResponse {
  items: SmsQualityTrendPoint[];
  total: number;
}

export interface SmsOptimizationInsight {
  providerType: string;
  qualityScore: number;
  costEfficiencyScore: number | null;
  deliverySuccessRate: number;
  averageLatencyMs: number | null;
  costPerDeliveredMessage: number | null;
  totalAttempts: number;
  hasSufficientData: boolean;
  recommendation: string;
}

export interface SmsOptimizationResponse {
  providers: SmsOptimizationInsight[];
  topQualityProvider: string | null;
  topCostEfficiencyProvider: string | null;
  topBalancedProvider: string | null;
  generatedAt: string;
  dataSummary: string;
}

// ── LS-NOTIF-SMS-015: Optimization API functions ─────────────────────────────

export async function getSmsProviderQuality(params?: {
  provider?: string;
  providerOwnershipMode?: string;
  countryCode?: string;
  region?: string;
  tenantId?: string;
  providerConfigId?: string;
  from?: string;
  to?: string;
  limit?: number;
  offset?: number;
}): Promise<SmsQualityListResponse> {
  const q = new URLSearchParams();
  if (params?.provider)              q.set('provider',              params.provider);
  if (params?.providerOwnershipMode) q.set('providerOwnershipMode', params.providerOwnershipMode);
  if (params?.countryCode)           q.set('countryCode',           params.countryCode);
  if (params?.region)                q.set('region',                params.region);
  if (params?.tenantId)              q.set('tenantId',              params.tenantId);
  if (params?.providerConfigId)      q.set('providerConfigId',      params.providerConfigId);
  if (params?.from)                  q.set('from',                  params.from);
  if (params?.to)                    q.set('to',                    params.to);
  if (params?.limit != null)         q.set('limit',                 String(params.limit));
  if (params?.offset != null)        q.set('offset',                String(params.offset));
  return fetchAdmin(`/notifications/v1/admin/sms/routing/quality?${q}`);
}

export async function getSmsQualityTrends(params?: {
  provider?: string;
  countryCode?: string;
  tenantId?: string;
  from?: string;
  to?: string;
  limit?: number;
}): Promise<SmsQualityTrendResponse> {
  const q = new URLSearchParams();
  if (params?.provider)    q.set('provider',    params.provider);
  if (params?.countryCode) q.set('countryCode', params.countryCode);
  if (params?.tenantId)    q.set('tenantId',    params.tenantId);
  if (params?.from)        q.set('from',        params.from);
  if (params?.to)          q.set('to',          params.to);
  if (params?.limit)       q.set('limit',       String(params.limit));
  return fetchAdmin(`/notifications/v1/admin/sms/routing/quality/trends?${q}`);
}

export async function getSmsOptimizationSummary(params?: {
  tenantId?: string;
  countryCode?: string;
}): Promise<SmsOptimizationResponse> {
  const q = new URLSearchParams();
  if (params?.tenantId)    q.set('tenantId',    params.tenantId);
  if (params?.countryCode) q.set('countryCode', params.countryCode);
  return fetchAdmin(`/notifications/v1/admin/sms/routing/optimization?${q}`);
}

// ── LS-NOTIF-SMS-016: Recipient Intelligence Types ────────────────────────────

export interface SmsRecipientReputation {
  id: string;
  recipientHash: string;
  tenantId: string | null;
  providerType: string | null;
  countryCode: string | null;
  region: string | null;
  totalAttempts: number;
  deliveredAttempts: number;
  failedAttempts: number;
  retryAttempts: number;
  deadLetterAttempts: number;
  carrierRejectedAttempts: number;
  invalidDestinationAttempts: number;
  deliverySuccessRate: number;
  failureRate: number;
  retryRate: number;
  deadLetterRate: number;
  carrierFailureRate: number;
  invalidNumberRisk: number;
  retrySuppressionRisk: number;
  qualityScore: number;
  destinationRiskLevel: string;
  lastAttemptAt: string | null;
  calculatedAt: string;
}

export interface SmsRecipientReputationListResult {
  items: SmsRecipientReputation[];
  total: number;
  limit: number;
  offset: number;
}

export interface SmsSuppressionDecision {
  id: string;
  recipientHash: string;
  tenantId: string | null;
  notificationId: string | null;
  attemptId: string | null;
  decisionType: string;
  reasonCode: string;
  riskScore: number | null;
  qualityScore: number | null;
  retryCount: number;
  providerType: string | null;
  countryCode: string | null;
  region: string | null;
  createdAt: string;
}

export interface SmsSuppressionDecisionListResult {
  items: SmsSuppressionDecision[];
  total: number;
  limit: number;
  offset: number;
}

export interface SmsDestinationRiskSummary {
  lowRiskCount: number;
  mediumRiskCount: number;
  highRiskCount: number;
  suppressedCount: number;
  totalRecipients: number;
  generatedAt: string;
}

export interface SmsRecipientTrendPoint {
  windowDate: string;
  totalRecipients: number;
  averageDeliveryRate: number;
  averageFailureRate: number;
  averageQualityScore: number;
  suppressedCount: number;
  highRiskCount: number;
}

export interface SmsRecipientTrendResult {
  points: SmsRecipientTrendPoint[];
  generatedAt: string;
}

// ── LS-NOTIF-SMS-016: Recipient Intelligence API ─────────────────────────────

export async function getSmsRecipientQuality(params?: {
  tenantId?: string;
  provider?: string;
  countryCode?: string;
  region?: string;
  riskLevel?: string;
  from?: string;
  to?: string;
  limit?: number;
  offset?: number;
}): Promise<SmsRecipientReputationListResult> {
  const q = new URLSearchParams();
  if (params?.tenantId)    q.set('tenantId',    params.tenantId);
  if (params?.provider)    q.set('provider',    params.provider);
  if (params?.countryCode) q.set('countryCode', params.countryCode);
  if (params?.region)      q.set('region',      params.region);
  if (params?.riskLevel)   q.set('riskLevel',   params.riskLevel);
  if (params?.from)        q.set('from',        params.from);
  if (params?.to)          q.set('to',          params.to);
  if (params?.limit != null)  q.set('limit',  String(params.limit));
  if (params?.offset != null) q.set('offset', String(params.offset));
  return fetchAdmin(`/notifications/v1/admin/sms/recipients/quality?${q}`);
}

export async function getSmsRecipientFailures(params?: {
  tenantId?: string;
  provider?: string;
  countryCode?: string;
  riskLevel?: string;
  from?: string;
  to?: string;
  limit?: number;
  offset?: number;
}): Promise<SmsRecipientReputationListResult> {
  const q = new URLSearchParams();
  if (params?.tenantId)    q.set('tenantId',    params.tenantId);
  if (params?.provider)    q.set('provider',    params.provider);
  if (params?.countryCode) q.set('countryCode', params.countryCode);
  if (params?.riskLevel)   q.set('riskLevel',   params.riskLevel);
  if (params?.from)        q.set('from',        params.from);
  if (params?.to)          q.set('to',          params.to);
  if (params?.limit != null)  q.set('limit',  String(params.limit));
  if (params?.offset != null) q.set('offset', String(params.offset));
  return fetchAdmin(`/notifications/v1/admin/sms/recipients/failures?${q}`);
}

export async function getSmsSuppressionDecisions(params?: {
  tenantId?: string;
  decisionType?: string;
  reasonCode?: string;
  provider?: string;
  countryCode?: string;
  from?: string;
  to?: string;
  limit?: number;
  offset?: number;
}): Promise<SmsSuppressionDecisionListResult> {
  const q = new URLSearchParams();
  if (params?.tenantId)     q.set('tenantId',     params.tenantId);
  if (params?.decisionType) q.set('decisionType', params.decisionType);
  if (params?.reasonCode)   q.set('reasonCode',   params.reasonCode);
  if (params?.provider)     q.set('provider',     params.provider);
  if (params?.countryCode)  q.set('countryCode',  params.countryCode);
  if (params?.from)         q.set('from',         params.from);
  if (params?.to)           q.set('to',           params.to);
  if (params?.limit != null)  q.set('limit',  String(params.limit));
  if (params?.offset != null) q.set('offset', String(params.offset));
  return fetchAdmin(`/notifications/v1/admin/sms/recipients/suppressions?${q}`);
}

export async function getSmsRecipientRiskSummary(params?: {
  tenantId?: string;
  countryCode?: string;
}): Promise<SmsDestinationRiskSummary> {
  const q = new URLSearchParams();
  if (params?.tenantId)    q.set('tenantId',    params.tenantId);
  if (params?.countryCode) q.set('countryCode', params.countryCode);
  return fetchAdmin(`/notifications/v1/admin/sms/recipients/risk?${q}`);
}

export async function getSmsRecipientTrends(params?: {
  tenantId?: string;
  countryCode?: string;
  from?: string;
  to?: string;
}): Promise<SmsRecipientTrendResult> {
  const q = new URLSearchParams();
  if (params?.tenantId)    q.set('tenantId',    params.tenantId);
  if (params?.countryCode) q.set('countryCode', params.countryCode);
  if (params?.from)        q.set('from',        params.from);
  if (params?.to)          q.set('to',          params.to);
  return fetchAdmin(`/notifications/v1/admin/sms/recipients/trends?${q}`);
}
