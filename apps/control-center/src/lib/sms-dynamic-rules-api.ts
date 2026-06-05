const BASE = "/api/notifications/v1/admin/sms/governance";

// ─── Rule Pack types ──────────────────────────────────────────────────────────

export type RulePackStatus = "draft" | "active" | "inactive" | "archived";
export type InheritanceMode = "merge" | "override" | "append_only";
export type RuleType =
  | "prohibited_phrase"
  | "restricted_pattern"
  | "classification_override"
  | "variable_rule"
  | "link_rule"
  | "delivery_restriction"
  | "escalation_rule";
export type RuleSeverity = "allow" | "warn" | "review_required" | "block" | "override_allowed";
export type EnforcementMode = "permissive" | "standard" | "strict";
export type ProfileScope = "tenant" | "provider" | "template_category" | "escalation";

export interface GovernanceRulePack {
  id: string;
  tenantId: string | null;
  name: string;
  description: string | null;
  version: number;
  status: RulePackStatus;
  enabled: boolean;
  inheritanceMode: InheritanceMode;
  priority: number;
  effectiveFrom: string | null;
  effectiveTo: string | null;
  createdAt: string;
  updatedAt: string;
  createdBy: string | null;
  updatedBy: string | null;
  ruleCount?: number;
}

export interface GovernanceRule {
  id: string;
  rulePackId: string;
  name: string;
  description: string | null;
  ruleType: RuleType;
  pattern: string | null;
  severity: RuleSeverity;
  enabled: boolean;
  priority: number;
  metadataJson: string | null;
  createdAt: string;
  updatedAt: string;
  createdBy: string | null;
  updatedBy: string | null;
}

export interface ComplianceProfile {
  id: string;
  tenantId: string | null;
  name: string;
  description: string | null;
  enabled: boolean;
  defaultRulePackIdsJson: string | null;
  enforcementMode: EnforcementMode;
  createdAt: string;
  updatedAt: string;
  createdBy: string | null;
  updatedBy: string | null;
  assignmentCount?: number;
}

export interface SimulationMatchedRule {
  ruleId: string;
  rulePackId: string;
  ruleName: string;
  ruleType: RuleType;
  severity: RuleSeverity;
  matchedPatternMasked: string | null;
  reasonCode: string | null;
}

export interface SimulationTrace {
  step: string;
  decisionType: string;
  reasonCode: string;
  blocked: boolean;
}

export interface SimulationResponse {
  finalDecision: string;
  finalReasonCode: string;
  contentClassification: string | null;
  wouldBlock: boolean;
  staticDecision: string;
  staticReasonCode: string;
  dynamicDecision: string;
  dynamicReasonCode: string;
  matchedRules: SimulationMatchedRule[];
  ruleTrace: SimulationTrace[];
  warnings: string[];
  enforcementMode: EnforcementMode;
  profileAssigned: boolean;
  simulatedAt: string;
}

export interface RuleAnalytics {
  windowHours: number;
  since: string;
  tenantId: string | null;
  globalActivePacks: number;
  tenantActivePacks: number;
  totalActiveRules: number;
  rulesByType: Array<{ ruleType: string; count: number }>;
  rulesBySeverity: Array<{ severity: string; count: number }>;
  activeProfileAssignments: number;
}

export interface PaginatedResult<T> {
  total: number;
  page: number;
  pageSize: number;
  items: T[];
}

// ─── Rule Pack API ────────────────────────────────────────────────────────────

export async function listRulePacks(params?: {
  tenantId?: string;
  status?: string;
  enabled?: boolean;
  page?: number;
  pageSize?: number;
}): Promise<PaginatedResult<GovernanceRulePack>> {
  const qs = new URLSearchParams();
  if (params?.tenantId) qs.set("tenantId", params.tenantId);
  if (params?.status)   qs.set("status",   params.status);
  if (params?.enabled !== undefined) qs.set("enabled", String(params.enabled));
  if (params?.page)     qs.set("page",     String(params.page));
  if (params?.pageSize) qs.set("pageSize", String(params.pageSize));
  const res = await fetch(`${BASE}/rule-packs?${qs}`, { credentials: "include" });
  if (!res.ok) throw new Error(`listRulePacks: ${res.status}`);
  return res.json();
}

export async function getRulePack(id: string): Promise<GovernanceRulePack> {
  const res = await fetch(`${BASE}/rule-packs/${id}`, { credentials: "include" });
  if (!res.ok) throw new Error(`getRulePack: ${res.status}`);
  return res.json();
}

export async function createRulePack(body: {
  name: string;
  tenantId?: string | null;
  description?: string;
  status?: RulePackStatus;
  enabled?: boolean;
  inheritanceMode?: InheritanceMode;
  priority?: number;
  effectiveFrom?: string;
  effectiveTo?: string;
  requestedBy?: string;
}): Promise<{ id: string }> {
  const res = await fetch(`${BASE}/rule-packs`, {
    method: "POST",
    credentials: "include",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body),
  });
  if (!res.ok) throw new Error(`createRulePack: ${res.status}`);
  return res.json();
}

export async function updateRulePack(id: string, body: {
  name?: string;
  description?: string;
  status?: RulePackStatus;
  enabled?: boolean;
  inheritanceMode?: InheritanceMode;
  priority?: number;
  requestedBy?: string;
}): Promise<{ id: string; version: number; updatedAt: string }> {
  const res = await fetch(`${BASE}/rule-packs/${id}`, {
    method: "PUT",
    credentials: "include",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body),
  });
  if (!res.ok) throw new Error(`updateRulePack: ${res.status}`);
  return res.json();
}

export async function disableRulePack(id: string): Promise<void> {
  const res = await fetch(`${BASE}/rule-packs/${id}/disable`, {
    method: "POST",
    credentials: "include",
  });
  if (!res.ok) throw new Error(`disableRulePack: ${res.status}`);
}

// ─── Rules API ────────────────────────────────────────────────────────────────

export async function listRules(params?: {
  rulePackId?: string;
  ruleType?: string;
  severity?: string;
  enabled?: boolean;
  page?: number;
  pageSize?: number;
}): Promise<PaginatedResult<GovernanceRule>> {
  const qs = new URLSearchParams();
  if (params?.rulePackId) qs.set("rulePackId", params.rulePackId);
  if (params?.ruleType)   qs.set("ruleType",   params.ruleType);
  if (params?.severity)   qs.set("severity",   params.severity);
  if (params?.enabled !== undefined) qs.set("enabled", String(params.enabled));
  if (params?.page)       qs.set("page",       String(params.page));
  if (params?.pageSize)   qs.set("pageSize",   String(params.pageSize));
  const res = await fetch(`${BASE}/rules?${qs}`, { credentials: "include" });
  if (!res.ok) throw new Error(`listRules: ${res.status}`);
  return res.json();
}

export async function createRule(body: {
  rulePackId: string;
  name: string;
  ruleType: RuleType;
  severity: RuleSeverity;
  description?: string;
  pattern?: string;
  enabled?: boolean;
  priority?: number;
  metadataJson?: string;
  requestedBy?: string;
}): Promise<{ id: string }> {
  const res = await fetch(`${BASE}/rules`, {
    method: "POST",
    credentials: "include",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body),
  });
  if (!res.ok) {
    const err = await res.json().catch(() => ({ error: res.statusText }));
    throw new Error(err.error ?? `createRule: ${res.status}`);
  }
  return res.json();
}

export async function updateRule(id: string, body: {
  name?: string;
  description?: string;
  pattern?: string;
  severity?: RuleSeverity;
  enabled?: boolean;
  priority?: number;
  metadataJson?: string;
  requestedBy?: string;
}): Promise<{ id: string; updatedAt: string }> {
  const res = await fetch(`${BASE}/rules/${id}`, {
    method: "PUT",
    credentials: "include",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body),
  });
  if (!res.ok) throw new Error(`updateRule: ${res.status}`);
  return res.json();
}

export async function disableRule(id: string): Promise<void> {
  const res = await fetch(`${BASE}/rules/${id}/disable`, {
    method: "POST",
    credentials: "include",
  });
  if (!res.ok) throw new Error(`disableRule: ${res.status}`);
}

// ─── Profiles API ─────────────────────────────────────────────────────────────

export async function listProfiles(params?: {
  tenantId?: string;
  enabled?: boolean;
  page?: number;
  pageSize?: number;
}): Promise<PaginatedResult<ComplianceProfile>> {
  const qs = new URLSearchParams();
  if (params?.tenantId) qs.set("tenantId", params.tenantId);
  if (params?.enabled !== undefined) qs.set("enabled", String(params.enabled));
  if (params?.page)     qs.set("page",     String(params.page));
  if (params?.pageSize) qs.set("pageSize", String(params.pageSize));
  const res = await fetch(`${BASE}/profiles?${qs}`, { credentials: "include" });
  if (!res.ok) throw new Error(`listProfiles: ${res.status}`);
  return res.json();
}

export async function createProfile(body: {
  name: string;
  tenantId?: string | null;
  description?: string;
  enabled?: boolean;
  enforcementMode?: EnforcementMode;
  defaultRulePackIdsJson?: string;
  requestedBy?: string;
}): Promise<{ id: string }> {
  const res = await fetch(`${BASE}/profiles`, {
    method: "POST",
    credentials: "include",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body),
  });
  if (!res.ok) throw new Error(`createProfile: ${res.status}`);
  return res.json();
}

export async function updateProfile(id: string, body: {
  name?: string;
  description?: string;
  enabled?: boolean;
  enforcementMode?: EnforcementMode;
  defaultRulePackIdsJson?: string;
  requestedBy?: string;
}): Promise<{ id: string; updatedAt: string }> {
  const res = await fetch(`${BASE}/profiles/${id}`, {
    method: "PUT",
    credentials: "include",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body),
  });
  if (!res.ok) throw new Error(`updateProfile: ${res.status}`);
  return res.json();
}

// ─── Simulation API ───────────────────────────────────────────────────────────

export async function simulateGovernance(body: {
  renderedBody: string;
  tenantId?: string | null;
  templateKey?: string;
  templateBody?: string;
  variables?: Record<string, string>;
  contentClassification?: string;
  context?: string;
  includeRuleTrace?: boolean;
}): Promise<SimulationResponse> {
  const res = await fetch(`${BASE}/simulate`, {
    method: "POST",
    credentials: "include",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body),
  });
  if (!res.ok) throw new Error(`simulateGovernance: ${res.status}`);
  return res.json();
}

// ─── Analytics API ────────────────────────────────────────────────────────────

export async function getRuleAnalytics(params?: {
  tenantId?: string;
  ruleType?: string;
  windowHours?: number;
}): Promise<RuleAnalytics> {
  const qs = new URLSearchParams();
  if (params?.tenantId)    qs.set("tenantId",    params.tenantId);
  if (params?.ruleType)    qs.set("ruleType",    params.ruleType);
  if (params?.windowHours) qs.set("windowHours", String(params.windowHours));
  const res = await fetch(`${BASE}/rule-analytics?${qs}`, { credentials: "include" });
  if (!res.ok) throw new Error(`getRuleAnalytics: ${res.status}`);
  return res.json();
}
