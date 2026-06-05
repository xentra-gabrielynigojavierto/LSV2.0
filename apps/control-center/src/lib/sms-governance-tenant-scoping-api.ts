const NOTIFICATION_API_BASE =
  process.env.CONTROL_CENTER_API_BASE || process.env.GATEWAY_URL || 'http://127.0.0.1:5010';

const SCOPING_BASE = `${NOTIFICATION_API_BASE}/notifications/v1/admin/sms/governance/tenant-scoping`;

// ── DTOs ──────────────────────────────────────────────────────────────────────

export interface TenantAssignmentDto {
  id: string;
  tenantId: string;
  rulePackId: string;
  assignmentState: string;
  assignmentMode: string;
  priority: number;
  effectiveFrom: string | null;
  effectiveTo: string | null;
  rolloutPlanId: string | null;
  rolloutStageId: string | null;
  releasePackageId: string | null;
  assignedBy: string | null;
  activatedAt: string | null;
  deactivatedAt: string | null;
  supersededAt: string | null;
  deactivationReason: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface TenantOverlayDto {
  id: string;
  tenantId: string;
  rulePackId: string | null;
  ruleId: string | null;
  overlayType: string;
  overlayState: string;
  overrideJson: string | null;
  priority: number;
  enabled: boolean;
  effectiveFrom: string | null;
  effectiveTo: string | null;
  createdAt: string;
  updatedAt: string;
  createdBy: string | null;
  updatedBy: string | null;
}

export interface TenantAssignmentAuditEventDto {
  id: string;
  tenantId: string;
  assignmentId: string | null;
  overlayId: string | null;
  eventType: string;
  previousState: string | null;
  newState: string | null;
  actor: string | null;
  reason: string | null;
  metadataJson: string | null;
  createdAt: string;
}

export interface PaginatedAssignmentResult {
  items: TenantAssignmentDto[];
  total: number;
  page: number;
  pageSize: number;
}

export interface PaginatedOverlayResult {
  items: TenantOverlayDto[];
  total: number;
  page: number;
  pageSize: number;
}

export interface AssignmentOperationResult {
  success: boolean;
  assignmentId: string | null;
  errorMessage: string | null;
  errorCode: string | null;
}

export interface OverlayOperationResult {
  success: boolean;
  overlayId: string | null;
  errorMessage: string | null;
  errorCode: string | null;
}

export interface PackSummaryDto {
  id: string;
  name: string;
  mode: string | null;
  ruleCount: number;
  priority: number;
}

export interface OverlaySummaryDto {
  id: string;
  overlayType: string;
  ruleId: string | null;
  rulePackId: string | null;
  priority: number;
  description: string;
}

export interface EffectiveGovernanceGraphDto {
  tenantId: string;
  resolutionMode: string;
  inheritedGlobalPacks: PackSummaryDto[];
  tenantAssignedPacks: PackSummaryDto[];
  rolloutAssignedPacks: PackSummaryDto[];
  overlaysApplied: OverlaySummaryDto[];
  disabledRuleIds: string[];
  finalRuleCount: number;
  warnings: string[];
}

export interface CheckResult {
  checkName: string;
  passed: boolean;
  detail: string;
}

export interface IsolationValidationResult {
  isValid: boolean;
  errors: string[];
  warnings: string[];
  checks: CheckResult[];
}

export interface ResolutionExplanationDto {
  tenantId: string;
  resolutionMode: string;
  totalAssignments: number;
  activeAssignments: number;
  totalOverlays: number;
  activeOverlays: number;
  disabledRulesCount: number;
  finalRuleCount: number;
  hasTenantAssignments: boolean;
  usesGlobalFallback: boolean;
  steps: string[];
  warnings: string[];
}

// ── Assignment state / mode constants ─────────────────────────────────────────

export const ASSIGNMENT_STATES = ['draft', 'active', 'inactive', 'rolled_back', 'superseded'] as const;
export const ASSIGNMENT_MODES  = ['inherited', 'isolated', 'rollout_canary', 'rollout_stage'] as const;
export const OVERLAY_TYPES     = ['disable_rule', 'suppress_rule', 'override_severity', 'override_pattern', 'override_metadata', 'add_rule'] as const;
export const OVERLAY_STATES    = ['draft', 'active', 'inactive'] as const;

export function assignmentStateBadge(state: string): string {
  switch (state) {
    case 'active':      return 'bg-emerald-100 text-emerald-800';
    case 'draft':       return 'bg-slate-100 text-slate-700';
    case 'inactive':    return 'bg-amber-100 text-amber-800';
    case 'rolled_back': return 'bg-red-100 text-red-800';
    case 'superseded':  return 'bg-purple-100 text-purple-800';
    default:            return 'bg-slate-100 text-slate-700';
  }
}

export function overlayTypeBadge(type: string): string {
  switch (type) {
    case 'disable_rule':
    case 'suppress_rule':     return 'bg-red-100 text-red-800';
    case 'override_severity': return 'bg-amber-100 text-amber-800';
    case 'override_pattern':  return 'bg-blue-100 text-blue-800';
    case 'override_metadata': return 'bg-purple-100 text-purple-800';
    case 'add_rule':          return 'bg-emerald-100 text-emerald-800';
    default:                  return 'bg-slate-100 text-slate-700';
  }
}

// ── API client ────────────────────────────────────────────────────────────────

async function apiFetch<T>(url: string, init?: RequestInit): Promise<T> {
  const res = await fetch(url, {
    ...init,
    headers: { 'Content-Type': 'application/json', ...(init?.headers ?? {}) },
  });
  if (!res.ok) {
    const body = await res.text().catch(() => '');
    throw new Error(`${res.status} ${res.statusText}: ${body}`);
  }
  return res.json() as Promise<T>;
}

// ── Assignments ───────────────────────────────────────────────────────────────

export async function listTenantAssignments(params?: {
  tenantId?: string; rulePackId?: string; state?: string;
  mode?: string; rolloutPlanId?: string; page?: number; pageSize?: number;
}): Promise<PaginatedAssignmentResult> {
  const q = new URLSearchParams();
  if (params?.tenantId)     q.set('tenantId', params.tenantId);
  if (params?.rulePackId)   q.set('rulePackId', params.rulePackId);
  if (params?.state)        q.set('state', params.state);
  if (params?.mode)         q.set('mode', params.mode);
  if (params?.rolloutPlanId) q.set('rolloutPlanId', params.rolloutPlanId);
  if (params?.page)         q.set('page', String(params.page));
  if (params?.pageSize)     q.set('pageSize', String(params.pageSize));
  return apiFetch(`${SCOPING_BASE}/tenant-assignments?${q}`);
}

export async function createTenantAssignment(body: {
  tenantId: string; rulePackId: string; assignmentMode?: string;
  priority?: number; effectiveFrom?: string; effectiveTo?: string;
  rolloutPlanId?: string; rolloutStageId?: string; releasePackageId?: string;
  assignedBy?: string;
}): Promise<AssignmentOperationResult> {
  return apiFetch(`${SCOPING_BASE}/tenant-assignments`, {
    method: 'POST', body: JSON.stringify(body),
  });
}

export async function activateTenantAssignment(id: string, requestedBy?: string): Promise<AssignmentOperationResult> {
  return apiFetch(`${SCOPING_BASE}/tenant-assignments/${id}/activate`, {
    method: 'POST', body: JSON.stringify({ requestedBy }),
  });
}

export async function deactivateTenantAssignment(id: string, requestedBy?: string, reason?: string): Promise<AssignmentOperationResult> {
  return apiFetch(`${SCOPING_BASE}/tenant-assignments/${id}/deactivate`, {
    method: 'POST', body: JSON.stringify({ requestedBy, reason }),
  });
}

export async function rollbackTenantAssignment(id: string, requestedBy?: string, reason?: string): Promise<AssignmentOperationResult> {
  return apiFetch(`${SCOPING_BASE}/tenant-assignments/${id}/rollback`, {
    method: 'POST', body: JSON.stringify({ requestedBy, reason }),
  });
}

// ── Overlays ──────────────────────────────────────────────────────────────────

export async function listTenantOverlays(params?: {
  tenantId?: string; rulePackId?: string; ruleId?: string;
  overlayType?: string; overlayState?: string; enabled?: boolean;
  page?: number; pageSize?: number;
}): Promise<PaginatedOverlayResult> {
  const q = new URLSearchParams();
  if (params?.tenantId)    q.set('tenantId', params.tenantId);
  if (params?.rulePackId)  q.set('rulePackId', params.rulePackId);
  if (params?.ruleId)      q.set('ruleId', params.ruleId);
  if (params?.overlayType) q.set('overlayType', params.overlayType);
  if (params?.overlayState) q.set('overlayState', params.overlayState);
  if (params?.enabled !== undefined) q.set('enabled', String(params.enabled));
  if (params?.page)        q.set('page', String(params.page));
  if (params?.pageSize)    q.set('pageSize', String(params.pageSize));
  return apiFetch(`${SCOPING_BASE}/tenant-overlays?${q}`);
}

export async function createTenantOverlay(body: {
  tenantId: string; overlayType?: string; rulePackId?: string;
  ruleId?: string; overrideJson?: string; priority?: number;
  effectiveFrom?: string; effectiveTo?: string; createdBy?: string;
}): Promise<OverlayOperationResult> {
  return apiFetch(`${SCOPING_BASE}/tenant-overlays`, {
    method: 'POST', body: JSON.stringify(body),
  });
}

export async function activateTenantOverlay(id: string, requestedBy?: string): Promise<OverlayOperationResult> {
  return apiFetch(`${SCOPING_BASE}/tenant-overlays/${id}/activate`, {
    method: 'POST', body: JSON.stringify({ requestedBy }),
  });
}

export async function disableTenantOverlay(id: string, requestedBy?: string, reason?: string): Promise<OverlayOperationResult> {
  return apiFetch(`${SCOPING_BASE}/tenant-overlays/${id}/disable`, {
    method: 'POST', body: JSON.stringify({ requestedBy, reason }),
  });
}

// ── Resolution / graph / explain / isolation ──────────────────────────────────

export async function getTenantGovernanceGraph(tenantId: string): Promise<EffectiveGovernanceGraphDto> {
  return apiFetch(`${SCOPING_BASE}/tenant-resolution/${tenantId}`);
}

export async function explainTenantResolution(tenantId: string): Promise<ResolutionExplanationDto> {
  return apiFetch(`${SCOPING_BASE}/tenant-resolution/${tenantId}/explain`);
}

export async function validateTenantIsolation(tenantId: string): Promise<IsolationValidationResult> {
  return apiFetch(`${SCOPING_BASE}/tenant-isolation/${tenantId}`);
}

// ── Audit ─────────────────────────────────────────────────────────────────────

export async function getTenantAssignmentAudit(params?: {
  tenantId?: string; assignmentId?: string; overlayId?: string;
  eventType?: string; page?: number; pageSize?: number;
}): Promise<TenantAssignmentAuditEventDto[]> {
  const q = new URLSearchParams();
  if (params?.tenantId)     q.set('tenantId', params.tenantId);
  if (params?.assignmentId) q.set('assignmentId', params.assignmentId);
  if (params?.overlayId)    q.set('overlayId', params.overlayId);
  if (params?.eventType)    q.set('eventType', params.eventType);
  if (params?.page)         q.set('page', String(params.page));
  if (params?.pageSize)     q.set('pageSize', String(params.pageSize));
  return apiFetch(`${SCOPING_BASE}/tenant-assignment-audit?${q}`);
}
