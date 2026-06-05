/**
 * LS-NOTIF-SMS-020: Governance versioning, import/export, and analytics API client.
 *
 * Endpoints exposed by SmsGovernanceLifecycleEndpoints on the Notifications service.
 * All calls go through the BFF → gateway → notifications service.
 * No phone numbers, raw message bodies, or credentials are sent or returned.
 */

const BASE = "/api/notifications/v1/admin/sms/governance";

// ─── Version history types ────────────────────────────────────────────────────

export interface RuleVersion {
  id: string;
  ruleId: string;
  rulePackId: string | null;
  versionNumber: number;
  ruleSnapshotJson: string;
  changeType: string;
  changeReason: string | null;
  createdAt: string;
  createdBy: string | null;
}

export interface RulePackVersion {
  id: string;
  rulePackId: string;
  versionNumber: number;
  packSnapshotJson: string;
  includedRulesSnapshotJson: string | null;
  changeType: string;
  changeReason: string | null;
  createdAt: string;
  createdBy: string | null;
}

export interface RuleVersionHistory {
  ruleId: string;
  total: number;
  versions: RuleVersion[];
}

export interface RulePackVersionHistory {
  rulePackId: string;
  total: number;
  versions: RulePackVersion[];
}

// ─── Rollback types ───────────────────────────────────────────────────────────

export interface RollbackResult {
  ruleId?: string;
  rulePackId?: string;
  restoredToVersion: number;
  newVersionNumber: number;
  message: string;
}

// ─── Import / export types ────────────────────────────────────────────────────

export interface ImportRuleEntry {
  name: string;
  ruleType: string;
  severity: string;
  description?: string | null;
  pattern?: string | null;
  enabled?: boolean;
  priority?: number;
  metadataJson?: string | null;
}

export interface ImportRulePackEntry {
  name: string;
  tenantId?: string | null;
  description?: string | null;
  status?: string;
  enabled?: boolean;
  inheritanceMode?: string;
  priority?: number;
  effectiveFrom?: string | null;
  effectiveTo?: string | null;
}

export interface ImportBundle {
  rulePack: ImportRulePackEntry;
  rules: ImportRuleEntry[];
}

export interface GovernanceImportRequest {
  bundles: ImportBundle[];
  dryRun?: boolean;
  requestedBy?: string;
}

export interface ImportValidationError {
  bundleIndex: number;
  ruleIndex: number;
  field: string;
  message: string;
}

export interface GovernanceImportResult {
  isValid: boolean;
  persisted: boolean;
  bundlesImported?: number;
  rulesImported?: number;
  errors?: ImportValidationError[];
  message?: string;
}

// ─── Analytics types ──────────────────────────────────────────────────────────

export interface RuleEffectivenessRow {
  ruleId: string | null;
  rulePackId: string | null;
  ruleName: string | null;
  ruleType: string | null;
  severity: string | null;
  totalMatches: number;
  blockCount: number;
  warnCount: number;
  reviewCount: number;
  allowCount: number;
  simulationCount: number;
  liveCount: number;
  blockRate: number;
  reviewRate: number;
  lastMatchedAt: string | null;
}

export interface MatchAnalyticsRow {
  windowStart: string;
  ruleId: string | null;
  rulePackId: string | null;
  ruleType: string | null;
  severity: string | null;
  decisionType: string;
  matchCount: number;
  simulationCount: number;
  liveCount: number;
}

export interface FalsePositiveCandidateRow {
  ruleId: string | null;
  rulePackId: string | null;
  ruleName: string | null;
  ruleType: string | null;
  severity: string | null;
  totalMatches: number;
  warnCount: number;
  simulationCount: number;
  liveCount: number;
  heuristic: string;
  fpScore: number;
}

export interface PackEffectivenessRow {
  rulePackId: string | null;
  packName: string | null;
  activeRules: number;
  totalMatches: number;
  blockCount: number;
  warnCount: number;
  reviewCount: number;
  allowCount: number;
  blockRate: number;
  lastMatchedAt: string | null;
}

export interface EffectivenessPaginatedResult<T> {
  total: number;
  page: number;
  pageSize: number;
  rows: T[];
}

// ─── Version history API ──────────────────────────────────────────────────────

export async function getRuleVersionHistory(ruleId: string): Promise<RuleVersionHistory> {
  const res = await fetch(`${BASE}/rules/${ruleId}/versions`, { credentials: "include" });
  if (!res.ok) throw new Error(`getRuleVersionHistory: ${res.status}`);
  return res.json();
}

export async function getRulePackVersionHistory(rulePackId: string): Promise<RulePackVersionHistory> {
  const res = await fetch(`${BASE}/rule-packs/${rulePackId}/versions`, { credentials: "include" });
  if (!res.ok) throw new Error(`getRulePackVersionHistory: ${res.status}`);
  return res.json();
}

// ─── Rollback API ─────────────────────────────────────────────────────────────

export async function rollbackRule(
  ruleId: string,
  versionNumber: number,
  requestedBy?: string,
  reason?: string,
): Promise<RollbackResult> {
  const res = await fetch(`${BASE}/rules/${ruleId}/rollback`, {
    method: "POST",
    credentials: "include",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ versionNumber, requestedBy, reason }),
  });
  if (!res.ok) {
    const err = await res.json().catch(() => ({ error: `${res.status}` }));
    throw new Error(err.error ?? `rollbackRule: ${res.status}`);
  }
  return res.json();
}

export async function rollbackRulePack(
  rulePackId: string,
  versionNumber: number,
  requestedBy?: string,
  reason?: string,
): Promise<RollbackResult> {
  const res = await fetch(`${BASE}/rule-packs/${rulePackId}/rollback`, {
    method: "POST",
    credentials: "include",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ versionNumber, requestedBy, reason }),
  });
  if (!res.ok) {
    const err = await res.json().catch(() => ({ error: `${res.status}` }));
    throw new Error(err.error ?? `rollbackRulePack: ${res.status}`);
  }
  return res.json();
}

// ─── Import / export API ──────────────────────────────────────────────────────

export async function validateGovernanceImport(
  request: GovernanceImportRequest,
): Promise<GovernanceImportResult> {
  const res = await fetch(`${BASE}/import/validate`, {
    method: "POST",
    credentials: "include",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request),
  });
  const data = await res.json().catch(() => ({}));
  if (res.status === 422) return data as GovernanceImportResult;
  if (!res.ok) throw new Error(`validateGovernanceImport: ${res.status}`);
  return data;
}

export async function importGovernanceRules(
  request: GovernanceImportRequest,
): Promise<GovernanceImportResult> {
  const res = await fetch(`${BASE}/import`, {
    method: "POST",
    credentials: "include",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request),
  });
  const data = await res.json().catch(() => ({}));
  if (res.status === 422) return data as GovernanceImportResult;
  if (!res.ok) throw new Error(`importGovernanceRules: ${res.status}`);
  return data;
}

export async function exportGovernanceRules(params?: {
  tenantId?: string;
  rulePackId?: string;
  status?: string;
  ruleType?: string;
  severity?: string;
  includeProfiles?: boolean;
}): Promise<object> {
  const qs = new URLSearchParams();
  if (params?.tenantId)       qs.set("tenantId",        params.tenantId);
  if (params?.rulePackId)     qs.set("rulePackId",      params.rulePackId);
  if (params?.status)         qs.set("status",          params.status);
  if (params?.ruleType)       qs.set("ruleType",        params.ruleType);
  if (params?.severity)       qs.set("severity",        params.severity);
  if (params?.includeProfiles !== undefined)
    qs.set("includeProfiles", String(params.includeProfiles));
  const res = await fetch(`${BASE}/export?${qs}`, { credentials: "include" });
  if (!res.ok) throw new Error(`exportGovernanceRules: ${res.status}`);
  return res.json();
}

// ─── Analytics API ────────────────────────────────────────────────────────────

export async function getRuleEffectiveness(params?: {
  tenantId?: string;
  rulePackId?: string;
  ruleId?: string;
  ruleType?: string;
  severity?: string;
  from?: string;
  to?: string;
  page?: number;
  pageSize?: number;
}): Promise<EffectivenessPaginatedResult<RuleEffectivenessRow>> {
  const qs = new URLSearchParams();
  if (params?.tenantId)   qs.set("tenantId",  params.tenantId);
  if (params?.rulePackId) qs.set("rulePackId", params.rulePackId);
  if (params?.ruleId)     qs.set("ruleId",     params.ruleId);
  if (params?.ruleType)   qs.set("ruleType",   params.ruleType);
  if (params?.severity)   qs.set("severity",   params.severity);
  if (params?.from)       qs.set("from",       params.from);
  if (params?.to)         qs.set("to",         params.to);
  if (params?.page)       qs.set("page",       String(params.page));
  if (params?.pageSize)   qs.set("pageSize",   String(params.pageSize));
  const res = await fetch(`${BASE}/effectiveness?${qs}`, { credentials: "include" });
  if (!res.ok) throw new Error(`getRuleEffectiveness: ${res.status}`);
  return res.json();
}

export async function getFalsePositiveCandidates(params?: {
  tenantId?: string;
  rulePackId?: string;
  from?: string;
  to?: string;
}): Promise<{ total: number; candidates: FalsePositiveCandidateRow[] }> {
  const qs = new URLSearchParams();
  if (params?.tenantId)   qs.set("tenantId",  params.tenantId);
  if (params?.rulePackId) qs.set("rulePackId", params.rulePackId);
  if (params?.from)       qs.set("from",       params.from);
  if (params?.to)         qs.set("to",         params.to);
  const res = await fetch(`${BASE}/false-positive-candidates?${qs}`, { credentials: "include" });
  if (!res.ok) throw new Error(`getFalsePositiveCandidates: ${res.status}`);
  return res.json();
}

export async function getPackEffectiveness(params?: {
  tenantId?: string;
  from?: string;
  to?: string;
  page?: number;
  pageSize?: number;
}): Promise<EffectivenessPaginatedResult<PackEffectivenessRow>> {
  const qs = new URLSearchParams();
  if (params?.tenantId) qs.set("tenantId", params.tenantId);
  if (params?.from)     qs.set("from",     params.from);
  if (params?.to)       qs.set("to",       params.to);
  if (params?.page)     qs.set("page",     String(params.page));
  if (params?.pageSize) qs.set("pageSize", String(params.pageSize));
  const res = await fetch(`${BASE}/pack-effectiveness?${qs}`, { credentials: "include" });
  if (!res.ok) throw new Error(`getPackEffectiveness: ${res.status}`);
  return res.json();
}
