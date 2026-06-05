const BASE = "/api/notifications/v1/admin/sms/governance";

// ─── Types ────────────────────────────────────────────────────────────────────

export type GovernancePolicyType =
  | "quiet_hours"
  | "geographic_restriction"
  | "rate_limit"
  | "provider_governance"
  | "retry_governance"
  | "escalation_guardrail";

export type GovernanceDecisionType =
  | "allow"
  | "delay"
  | "throttle"
  | "block"
  | "review_required"
  | "override_allowed";

export interface GovernancePolicy {
  id: string;
  tenantId: string | null;
  name: string;
  policyType: GovernancePolicyType;
  enabled: boolean;
  priority: number;
  policyJson: string;
  emergencyOverrideAllowed: boolean;
  createdAt: string;
  updatedAt: string;
  createdBy: string | null;
  updatedBy: string | null;
}

export interface GovernanceDecision {
  id: string;
  notificationId: string | null;
  attemptId: string | null;
  tenantId: string | null;
  policyId: string | null;
  policyType: string;
  decisionType: GovernanceDecisionType;
  reasonCode: string;
  providerType: string | null;
  providerConfigId: string | null;
  countryCode: string | null;
  region: string | null;
  effectiveAt: string | null;
  decisionMetadataJson: string | null;
  createdAt: string;
}

export interface GovernanceSummary {
  windowHours: number;
  since: string;
  totalDecisions: number;
  activePolicies: number;
  byDecisionType: Array<{ decisionType: string; count: number }>;
  byPolicyType: Array<{ policyType: string; count: number }>;
  topReasonCodes: Array<{ reasonCode: string; count: number }>;
}

export interface RateLimitStatus {
  windowMinutes: number;
  rateLimitPolicies: Array<{ id: string; name: string; tenantId: string | null; priority: number; policyJson: string }>;
  recentThrottling: Array<{ tenantId: string | null; decisionsCount: number; lastDecisionAt: string }>;
}

export interface GeoStatus {
  windowHours: number;
  geoPolicies: Array<{ id: string; name: string; tenantId: string | null; priority: number; policyJson: string }>;
  blockedByCountry: Array<{ countryCode: string | null; count: number }>;
}

export interface PaginatedResult<T> {
  total: number;
  page: number;
  pageSize: number;
  items: T[];
}

export interface CreatePolicyRequest {
  policyType: GovernancePolicyType;
  name: string;
  tenantId?: string | null;
  enabled?: boolean;
  priority?: number;
  policyJson?: string;
  emergencyOverrideAllowed?: boolean;
  requestedBy?: string;
}

export interface UpdatePolicyRequest {
  name?: string;
  policyJson?: string;
  enabled?: boolean;
  priority?: number;
  emergencyOverrideAllowed?: boolean;
  requestedBy?: string;
}

// ─── API functions ────────────────────────────────────────────────────────────

export async function listGovernancePolicies(params?: {
  policyType?: string;
  tenantId?: string;
  enabled?: boolean;
  page?: number;
  pageSize?: number;
}): Promise<PaginatedResult<GovernancePolicy>> {
  const qs = new URLSearchParams();
  if (params?.policyType) qs.set("policyType", params.policyType);
  if (params?.tenantId)   qs.set("tenantId",   params.tenantId);
  if (params?.enabled !== undefined) qs.set("enabled", String(params.enabled));
  if (params?.page)       qs.set("page",       String(params.page));
  if (params?.pageSize)   qs.set("pageSize",   String(params.pageSize));
  const res = await fetch(`${BASE}/policies?${qs}`, { credentials: "include" });
  if (!res.ok) throw new Error(`listGovernancePolicies: ${res.status}`);
  return res.json();
}

export async function createGovernancePolicy(body: CreatePolicyRequest): Promise<{ id: string }> {
  const res = await fetch(`${BASE}/policies`, {
    method: "POST",
    credentials: "include",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body),
  });
  if (!res.ok) throw new Error(`createGovernancePolicy: ${res.status}`);
  return res.json();
}

export async function updateGovernancePolicy(id: string, body: UpdatePolicyRequest): Promise<{ id: string; updatedAt: string }> {
  const res = await fetch(`${BASE}/policies/${id}`, {
    method: "PUT",
    credentials: "include",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body),
  });
  if (!res.ok) throw new Error(`updateGovernancePolicy: ${res.status}`);
  return res.json();
}

export async function disableGovernancePolicy(id: string): Promise<void> {
  const res = await fetch(`${BASE}/policies/${id}/disable`, {
    method: "POST",
    credentials: "include",
  });
  if (!res.ok) throw new Error(`disableGovernancePolicy: ${res.status}`);
}

export async function listGovernanceDecisions(params?: {
  tenantId?: string;
  decisionType?: string;
  policyType?: string;
  reasonCode?: string;
  from?: string;
  to?: string;
  page?: number;
  pageSize?: number;
}): Promise<PaginatedResult<GovernanceDecision>> {
  const qs = new URLSearchParams();
  if (params?.tenantId)     qs.set("tenantId",     params.tenantId);
  if (params?.decisionType) qs.set("decisionType", params.decisionType);
  if (params?.policyType)   qs.set("policyType",   params.policyType);
  if (params?.reasonCode)   qs.set("reasonCode",   params.reasonCode);
  if (params?.from)         qs.set("from",         params.from);
  if (params?.to)           qs.set("to",           params.to);
  if (params?.page)         qs.set("page",         String(params.page));
  if (params?.pageSize)     qs.set("pageSize",     String(params.pageSize));
  const res = await fetch(`${BASE}/decisions?${qs}`, { credentials: "include" });
  if (!res.ok) throw new Error(`listGovernanceDecisions: ${res.status}`);
  return res.json();
}

export async function getGovernanceSummary(hours = 24): Promise<GovernanceSummary> {
  const res = await fetch(`${BASE}/summary?hours=${hours}`, { credentials: "include" });
  if (!res.ok) throw new Error(`getGovernanceSummary: ${res.status}`);
  return res.json();
}

export async function getRateLimitStatus(windowMinutes = 60): Promise<RateLimitStatus> {
  const res = await fetch(`${BASE}/rate-limits?windowMinutes=${windowMinutes}`, { credentials: "include" });
  if (!res.ok) throw new Error(`getRateLimitStatus: ${res.status}`);
  return res.json();
}

export async function getGeoStatus(hours = 24): Promise<GeoStatus> {
  const res = await fetch(`${BASE}/geo?hours=${hours}`, { credentials: "include" });
  if (!res.ok) throw new Error(`getGeoStatus: ${res.status}`);
  return res.json();
}
