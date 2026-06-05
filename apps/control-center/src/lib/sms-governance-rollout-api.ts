const NOTIFICATION_API_BASE =
  process.env.CONTROL_CENTER_API_BASE || process.env.GATEWAY_URL || 'http://127.0.0.1:5010';

const ROLLOUT_BASE = `${NOTIFICATION_API_BASE}/notifications/v1/admin/sms/governance/rollouts`;

// ── DTOs ──────────────────────────────────────────────────────────────────────

export interface RolloutPlanDto {
  id: string;
  releasePackageId: string;
  tenantId: string | null;
  name: string;
  description: string | null;
  rolloutState: string;
  rolloutStrategy: string;
  currentStageNumber: number | null;
  rollbackThresholdJson: string | null;
  startedAt: string | null;
  pausedAt: string | null;
  resumedAt: string | null;
  completedAt: string | null;
  rolledBackAt: string | null;
  failedAt: string | null;
  failureReason: string | null;
  createdAt: string;
  updatedAt: string;
  createdBy: string | null;
  updatedBy: string | null;
}

export interface RolloutStageDto {
  id: string;
  rolloutPlanId: string;
  stageNumber: number;
  stageName: string | null;
  stageState: string;
  tenantPercentage: number | null;
  durationMinutes: number | null;
  startedAt: string | null;
  completedAt: string | null;
  failedAt: string | null;
  failureReason: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface TenantCohortDto {
  id: string;
  rolloutPlanId: string;
  stageId: string | null;
  tenantId: string;
  cohortName: string;
  enabled: boolean;
  activatedAt: string | null;
  rolledBackAt: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface RolloutDetailDto {
  plan: RolloutPlanDto;
  stages: RolloutStageDto[];
  cohorts: TenantCohortDto[];
}

export interface RolloutAuditEventDto {
  id: string;
  rolloutPlanId: string;
  stageId: string | null;
  tenantId: string | null;
  eventType: string;
  previousState: string | null;
  newState: string | null;
  actor: string | null;
  reason: string | null;
  metadataJson: string | null;
  createdAt: string;
}

export interface PaginatedRolloutResult {
  items: RolloutPlanDto[];
  total: number;
  page: number;
  pageSize: number;
}

export interface RolloutAnalyticsDto {
  rolloutPlanId: string;
  rolloutState: string;
  rolloutStrategy: string;
  totalStages: number;
  completedStages: number;
  activeStages: number;
  failedStages: number;
  totalCohortTenants: number;
  activeCohortTenants: number;
  rolledBackCohortTenants: number;
  blockRate: number;
  warnRate: number;
  reviewRate: number;
  activationFailureCount: number;
  pauseEventCount: number;
  thresholdBreachCount: number;
  rolloutDuration: string | null;
  stageBreakdown: RolloutStageAnalyticsDto[];
}

export interface RolloutStageAnalyticsDto {
  stageId: string;
  stageNumber: number;
  stageName: string | null;
  stageState: string;
  cohortTenants: number;
  blockRate: number;
  warnRate: number;
  reviewRate: number;
  sampleSize: number;
  stageDuration: string | null;
}

export interface RolloutOperationResult {
  success: boolean;
  errorMessage: string | null;
}

// ── API client ────────────────────────────────────────────────────────────────

async function apiFetch<T>(
  url: string,
  options?: RequestInit,
): Promise<T> {
  const res = await fetch(url, {
    ...options,
    headers: { 'Content-Type': 'application/json', ...(options?.headers ?? {}) },
  });
  if (!res.ok) {
    const text = await res.text().catch(() => '');
    throw new Error(`Rollout API ${res.status}: ${text}`);
  }
  return res.json() as Promise<T>;
}

export const rolloutApi = {
  list: (params?: {
    releasePackageId?: string;
    tenantId?: string;
    state?: string;
    strategy?: string;
    page?: number;
    pageSize?: number;
  }) => {
    const q = new URLSearchParams();
    if (params?.releasePackageId) q.set('releasePackageId', params.releasePackageId);
    if (params?.tenantId)         q.set('tenantId', params.tenantId);
    if (params?.state)            q.set('state', params.state);
    if (params?.strategy)         q.set('strategy', params.strategy);
    if (params?.page)             q.set('page', String(params.page));
    if (params?.pageSize)         q.set('pageSize', String(params.pageSize));
    return apiFetch<PaginatedRolloutResult>(`${ROLLOUT_BASE}?${q}`);
  },

  get: (id: string) =>
    apiFetch<RolloutDetailDto>(`${ROLLOUT_BASE}/${id}`),

  create: (body: {
    releasePackageId: string;
    name: string;
    description?: string;
    rolloutStrategy: string;
    tenantId?: string;
    rollbackThresholdJson?: string;
    requestedBy?: string;
  }) =>
    apiFetch<RolloutPlanDto>(ROLLOUT_BASE, { method: 'POST', body: JSON.stringify(body) }),

  addStage: (id: string, body: {
    stageNumber: number;
    stageName?: string;
    tenantPercentage?: number;
    durationMinutes?: number;
    requestedBy?: string;
  }) =>
    apiFetch<RolloutStageDto>(`${ROLLOUT_BASE}/${id}/stages`, {
      method: 'POST',
      body: JSON.stringify(body),
    }),

  addCohort: (id: string, body: {
    stageId?: string;
    tenantId: string;
    cohortName: string;
    requestedBy?: string;
  }) =>
    apiFetch<TenantCohortDto>(`${ROLLOUT_BASE}/${id}/cohorts`, {
      method: 'POST',
      body: JSON.stringify(body),
    }),

  start:    (id: string) =>
    apiFetch<RolloutOperationResult>(`${ROLLOUT_BASE}/${id}/start`, { method: 'POST' }),

  pause:    (id: string, reason?: string) =>
    apiFetch<RolloutOperationResult>(`${ROLLOUT_BASE}/${id}/pause`, {
      method: 'POST',
      body: JSON.stringify({ reason }),
    }),

  resume:   (id: string) =>
    apiFetch<RolloutOperationResult>(`${ROLLOUT_BASE}/${id}/resume`, { method: 'POST' }),

  rollback: (id: string, reason?: string) =>
    apiFetch<RolloutOperationResult>(`${ROLLOUT_BASE}/${id}/rollback`, {
      method: 'POST',
      body: JSON.stringify({ reason }),
    }),

  advance:  (id: string) =>
    apiFetch<RolloutOperationResult>(`${ROLLOUT_BASE}/${id}/advance`, { method: 'POST' }),

  analytics: (id: string) =>
    apiFetch<RolloutAnalyticsDto>(`${ROLLOUT_BASE}/${id}/analytics`),

  auditTrail: (id: string) =>
    apiFetch<RolloutAuditEventDto[]>(`${ROLLOUT_BASE}/${id}/audit`),
};

// ── State display helpers ─────────────────────────────────────────────────────

export const ROLLOUT_STATE_LABELS: Record<string, string> = {
  draft:               'Draft',
  pending_rollout:     'Pending Rollout',
  canary_active:       'Canary Active',
  staged_rollout:      'Staged Rollout',
  rollout_paused:      'Paused',
  rollout_failed:      'Failed',
  rollout_completed:   'Completed',
  rollout_rolled_back: 'Rolled Back',
  archived:            'Archived',
};

export const ROLLOUT_STATE_COLORS: Record<string, string> = {
  draft:               'bg-slate-100 text-slate-700',
  pending_rollout:     'bg-yellow-100 text-yellow-800',
  canary_active:       'bg-blue-100 text-blue-800',
  staged_rollout:      'bg-indigo-100 text-indigo-800',
  rollout_paused:      'bg-orange-100 text-orange-800',
  rollout_failed:      'bg-red-100 text-red-800',
  rollout_completed:   'bg-green-100 text-green-800',
  rollout_rolled_back: 'bg-purple-100 text-purple-800',
  archived:            'bg-slate-100 text-slate-500',
};

export const STAGE_STATE_COLORS: Record<string, string> = {
  pending:    'bg-slate-100 text-slate-600',
  active:     'bg-blue-100 text-blue-800',
  completed:  'bg-green-100 text-green-800',
  paused:     'bg-orange-100 text-orange-800',
  failed:     'bg-red-100 text-red-800',
  rolled_back: 'bg-purple-100 text-purple-800',
};

export const STRATEGY_LABELS: Record<string, string> = {
  canary:             'Canary',
  staged_percentage:  'Staged %',
  staged_cohort:      'Staged Cohort',
  full_activation:    'Full Activation',
  manual_progression: 'Manual',
};

export function formatRate(r: number): string {
  return `${(r * 100).toFixed(1)}%`;
}
