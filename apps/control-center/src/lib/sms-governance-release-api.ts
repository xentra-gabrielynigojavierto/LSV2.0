// LS-NOTIF-SMS-021: Governance release management API client
// All calls go to the Notification Service via the gateway.
// No raw phone numbers, credentials, or message content are returned or sent.

const BASE = '/api/notifications/v1/admin/sms/governance';

// ── Types ─────────────────────────────────────────────────────────────────────

export type ReleaseState =
  | 'draft' | 'pending_review' | 'approved' | 'scheduled'
  | 'active' | 'superseded' | 'rejected' | 'archived' | 'activation_failed';

export type ReleaseType =
  | 'rule_pack' | 'rule_set' | 'compliance_profile' | 'mixed_governance';

export type ReleaseEntityType =
  | 'rule_pack' | 'rule' | 'compliance_profile' | 'policy' | 'template';

export type ReleaseActionType =
  | 'create' | 'update' | 'disable' | 'rollback' | 'import' | 'activate';

export interface ReleasePackageDto {
  id: string;
  tenantId: string | null;
  name: string;
  description: string | null;
  releaseState: ReleaseState;
  releaseType: ReleaseType;
  scheduledActivationAt: string | null;
  activatedAt: string | null;
  rejectedAt: string | null;
  archivedAt: string | null;
  createdAt: string;
  updatedAt: string;
  createdBy: string | null;
  updatedBy: string | null;
  itemCount: number;
}

export interface ReleaseItemDto {
  id: string;
  releasePackageId: string;
  entityType: ReleaseEntityType;
  entityId: string;
  entityVersionNumber: number | null;
  actionType: ReleaseActionType;
  createdAt: string;
  createdBy: string | null;
}

export interface ApprovalDecisionDto {
  id: string;
  decision: 'approve' | 'reject';
  decisionReason: string | null;
  decidedBy: string | null;
  decidedByRole: string | null;
  createdAt: string;
}

export interface ApprovalRequestDto {
  id: string;
  releasePackageId: string;
  approvalStage: number;
  approverRole: string;
  requiredApprovals: number;
  status: 'pending' | 'approved' | 'rejected' | 'cancelled';
  requestedAt: string;
  resolvedAt: string | null;
  approvalCount: number;
  decisions: ApprovalDecisionDto[];
}

export interface ReleaseDetailDto {
  package: ReleasePackageDto;
  items: ReleaseItemDto[];
  approvalRequests: ApprovalRequestDto[];
}

export interface ReleaseAuditEventDto {
  id: string;
  releasePackageId: string;
  eventType: string;
  previousState: string | null;
  newState: string | null;
  actor: string | null;
  reason: string | null;
  metadataJson: string | null;
  createdAt: string;
}

export interface PaginatedReleaseResult {
  items: ReleasePackageDto[];
  total: number;
  page: number;
  pageSize: number;
}

export interface PendingApprovalDto {
  releasePackageId: string;
  releaseName: string;
  releaseDescription: string | null;
  tenantId: string | null;
  approvalStage: number;
  approverRole: string;
  requiredApprovals: number;
  approvalCount: number;
  requestedAt: string;
}

// ── Request types ─────────────────────────────────────────────────────────────

export interface CreateReleaseRequest {
  name: string;
  description?: string;
  releaseType: ReleaseType;
  tenantId?: string;
}

export interface AddReleaseItemRequest {
  entityType: ReleaseEntityType;
  entityId: string;
  entityVersionNumber?: number;
  actionType: ReleaseActionType;
  entitySnapshotJson?: string;
}

export interface ApproveRequest {
  reason?: string;
}

export interface RejectRequest {
  reason: string;
}

export interface ScheduleRequest {
  activateAtUtc: string;
}

export interface ArchiveRequest {
  reason?: string;
}

export interface ReleaseListParams {
  tenantId?: string;
  state?: ReleaseState;
  releaseType?: ReleaseType;
  page?: number;
  pageSize?: number;
}

// ── API helpers ───────────────────────────────────────────────────────────────

async function apiFetch<T>(url: string, init?: RequestInit): Promise<T> {
  const res = await fetch(url, { credentials: 'include', ...init });
  if (!res.ok) {
    let msg = `${res.status} ${res.statusText}`;
    try { const j = await res.json(); msg = j?.error ?? j?.message ?? msg; } catch {}
    throw new Error(msg);
  }
  if (res.status === 204) return undefined as T;
  return res.json();
}

function qs(params: Record<string, string | number | boolean | undefined | null>): string {
  const parts = Object.entries(params)
    .filter(([, v]) => v !== undefined && v !== null && v !== '')
    .map(([k, v]) => `${encodeURIComponent(k)}=${encodeURIComponent(String(v))}`);
  return parts.length ? '?' + parts.join('&') : '';
}

// ── Release packages ──────────────────────────────────────────────────────────

export async function listReleases(
  params: ReleaseListParams = {},
): Promise<PaginatedReleaseResult> {
  return apiFetch(`${BASE}/releases${qs({
    tenantId: params.tenantId,
    state: params.state,
    releaseType: params.releaseType,
    page: params.page ?? 1,
    pageSize: params.pageSize ?? 50,
  })}`);
}

export async function getRelease(id: string): Promise<ReleaseDetailDto> {
  return apiFetch(`${BASE}/releases/${id}`);
}

export async function createRelease(body: CreateReleaseRequest): Promise<ReleasePackageDto> {
  return apiFetch(`${BASE}/releases`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  });
}

// ── Release items ─────────────────────────────────────────────────────────────

export async function addReleaseItem(
  releaseId: string, body: AddReleaseItemRequest,
): Promise<ReleaseItemDto> {
  return apiFetch(`${BASE}/releases/${releaseId}/items`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  });
}

export async function removeReleaseItem(releaseId: string, itemId: string): Promise<void> {
  return apiFetch(`${BASE}/releases/${releaseId}/items/${itemId}`, { method: 'DELETE' });
}

// ── State transitions ─────────────────────────────────────────────────────────

export async function submitForReview(releaseId: string): Promise<{ message: string }> {
  return apiFetch(`${BASE}/releases/${releaseId}/submit-review`, { method: 'POST' });
}

export async function approveRelease(releaseId: string, body: ApproveRequest = {}): Promise<{ message: string }> {
  return apiFetch(`${BASE}/releases/${releaseId}/approve`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  });
}

export async function rejectRelease(releaseId: string, body: RejectRequest): Promise<{ message: string }> {
  return apiFetch(`${BASE}/releases/${releaseId}/reject`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  });
}

export async function scheduleRelease(releaseId: string, body: ScheduleRequest): Promise<{ message: string }> {
  return apiFetch(`${BASE}/releases/${releaseId}/schedule`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  });
}

export async function activateRelease(releaseId: string): Promise<{ message: string }> {
  return apiFetch(`${BASE}/releases/${releaseId}/activate`, { method: 'POST' });
}

export async function archiveRelease(releaseId: string, body: ArchiveRequest = {}): Promise<{ message: string }> {
  return apiFetch(`${BASE}/releases/${releaseId}/archive`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  });
}

// ── Audit + approvals ─────────────────────────────────────────────────────────

export async function getReleaseAudit(releaseId: string): Promise<{ releaseId: string; events: ReleaseAuditEventDto[] }> {
  return apiFetch(`${BASE}/releases/${releaseId}/audit`);
}

export async function getPendingApprovals(params: { approverRole?: string; page?: number; pageSize?: number } = {}): Promise<PendingApprovalDto[]> {
  return apiFetch(`${BASE}/approvals/pending${qs(params)}`);
}

// ── UI helpers ────────────────────────────────────────────────────────────────

export const STATE_LABELS: Record<ReleaseState, string> = {
  draft:             'Draft',
  pending_review:    'Pending Review',
  approved:          'Approved',
  scheduled:         'Scheduled',
  active:            'Active',
  superseded:        'Superseded',
  rejected:          'Rejected',
  archived:          'Archived',
  activation_failed: 'Activation Failed',
};

export const STATE_COLORS: Record<ReleaseState, string> = {
  draft:             'bg-slate-100 text-slate-600 border-slate-200',
  pending_review:    'bg-yellow-100 text-yellow-700 border-yellow-200',
  approved:          'bg-blue-100 text-blue-700 border-blue-200',
  scheduled:         'bg-indigo-100 text-indigo-700 border-indigo-200',
  active:            'bg-green-100 text-green-700 border-green-200',
  superseded:        'bg-slate-100 text-slate-500 border-slate-200',
  rejected:          'bg-red-100 text-red-700 border-red-200',
  archived:          'bg-slate-100 text-slate-400 border-slate-200',
  activation_failed: 'bg-red-100 text-red-800 border-red-300',
};

export const ACTION_LABELS: Record<ReleaseActionType, string> = {
  create:   'Create',
  update:   'Update',
  disable:  'Disable',
  rollback: 'Rollback',
  import:   'Import',
  activate: 'Activate',
};

export const ENTITY_LABELS: Record<ReleaseEntityType, string> = {
  rule_pack:          'Rule Pack',
  rule:               'Rule',
  compliance_profile: 'Compliance Profile',
  policy:             'Policy',
  template:           'Template',
};
