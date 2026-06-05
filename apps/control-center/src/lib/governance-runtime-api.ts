/**
 * LS-NOTIF-SMS-025: Governance Execution Runtime API client.
 * Calls /notifications/v1/admin/governance/runtime/* endpoints.
 */

const BASE = '/api/notifications/v1/admin/governance/runtime';

// ─── Status & channel models ──────────────────────────────────────────────

export interface GovernanceChannelRuntimeStatus {
  channelType: string;
  engineRegistered: boolean;
  supportsSimulation: boolean;
  enforcementEnabled: boolean;
  enforcementMode: string;
  notes?: string | null;
}

export interface GovernanceRuntimeStatus {
  enabled: boolean;
  failOpenOnError: boolean;
  persistAllowDecisions: boolean;
  maxEvaluationTextLength: number;
  regexTimeoutMs: number;
  registeredEngines: number;
  enforcedChannels: number;
  channelSummary: GovernanceChannelRuntimeStatus[];
}

// ─── Execution telemetry ──────────────────────────────────────────────────

export interface GovernanceExecutionRecordDto {
  id: string;
  notificationId?: string | null;
  attemptId?: string | null;
  tenantId?: string | null;
  channelType: string;
  decisionType: string;
  reasonCode: string;
  contentClassification?: string | null;
  topologyResolutionStatus?: string | null;
  engineStatus?: string | null;
  isSimulation: boolean;
  createdAt: string;
}

export interface GovernanceExecutionPageResult {
  items: GovernanceExecutionRecordDto[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface GovernanceExecutionQuery {
  channelType?: string;
  tenantId?: string;
  decisionType?: string;
  isSimulation?: boolean;
  from?: string;
  to?: string;
  page?: number;
  pageSize?: number;
}

// ─── Telemetry aggregates ─────────────────────────────────────────────────

export interface GovernanceChannelTelemetry {
  channelType: string;
  totalExecutions: number;
  allowCount: number;
  warnCount: number;
  blockCount: number;
  reviewCount: number;
  suppressCount: number;
  liveCount: number;
  simulationCount: number;
  topologyFailures: number;
  engineFailures: number;
}

export interface GovernanceRuntimeTelemetryResult {
  totalExecutions: number;
  liveExecutions: number;
  simulationExecutions: number;
  allowCount: number;
  warnCount: number;
  blockCount: number;
  reviewCount: number;
  suppressCount: number;
  topologyFailureCount: number;
  engineFailureCount: number;
  byChannel: GovernanceChannelTelemetry[];
  oldestRecord?: string | null;
  newestRecord?: string | null;
}

// ─── Simulation ───────────────────────────────────────────────────────────

export interface SimulateGovernanceRequest {
  channelType: string;
  tenantId?: string | null;
  templateId?: string | null;
  templateKey?: string | null;
  rolloutPlanId?: string | null;
  releasePackageId?: string | null;
  /** Transient only — evaluated in-memory, never persisted by the server */
  simulationPayloadText?: string | null;
  subjectText?: string | null;
  evaluationContext?: string | null;
}

export interface GovernanceSimulationResponse {
  channelType: string;
  tenantId?: string | null;
  decisionType: string;
  reasonCode: string;
  shouldProceed: boolean;
  shouldWarn: boolean;
  shouldBlock: boolean;
  requiresReview: boolean;
  contentClassification?: string | null;
  matchedRuleIds: string[];
  matchedRulePackIds: string[];
  topologyResolutionStatus?: string | null;
  engineStatus?: string | null;
  rulesEvaluated: number;
  evaluationDurationMs: number;
  simulationWarnings: string[];
  explanation?: {
    channelType: string;
    resolutionMode: string;
    federationEnabled: boolean;
    channelScopeFound: boolean;
    totalFinalRules: number;
    steps: Array<{
      stepNumber: number;
      stepName: string;
      description: string;
      rulesContributed: number;
      rulesFiltered: number;
      details: string[];
    }>;
  } | null;
}

// ─── API functions ────────────────────────────────────────────────────────

async function get<T>(path: string): Promise<T> {
  const res = await fetch(path, { credentials: 'include' });
  if (!res.ok) throw new Error(`GET ${path} failed: ${res.status}`);
  return res.json();
}

async function post<T>(path: string, body: unknown): Promise<T> {
  const res = await fetch(path, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    credentials: 'include',
    body: JSON.stringify(body),
  });
  if (!res.ok) throw new Error(`POST ${path} failed: ${res.status}`);
  return res.json();
}

function buildQs(params: Record<string, string | number | boolean | undefined | null>): string {
  const q = new URLSearchParams();
  for (const [k, v] of Object.entries(params)) {
    if (v !== undefined && v !== null && v !== '') q.set(k, String(v));
  }
  const s = q.toString();
  return s ? `?${s}` : '';
}

export const governanceRuntimeApi = {
  getStatus(): Promise<GovernanceRuntimeStatus> {
    return get(`${BASE}/status`);
  },

  getChannels(): Promise<{ channels: GovernanceChannelRuntimeStatus[] }> {
    return get(`${BASE}/channels`);
  },

  getExecutions(query: GovernanceExecutionQuery = {}): Promise<GovernanceExecutionPageResult> {
    const qs = buildQs({
      channelType:  query.channelType,
      tenantId:     query.tenantId,
      decisionType: query.decisionType,
      isSimulation: query.isSimulation,
      from:         query.from,
      to:           query.to,
      page:         query.page ?? 1,
      pageSize:     query.pageSize ?? 50,
    });
    return get(`${BASE}/executions${qs}`);
  },

  getTelemetry(opts: {
    channelType?: string;
    tenantId?: string;
    isSimulation?: boolean;
    from?: string;
    to?: string;
  } = {}): Promise<GovernanceRuntimeTelemetryResult> {
    const qs = buildQs(opts as Record<string, string | number | boolean | undefined | null>);
    return get(`${BASE}/telemetry${qs}`);
  },

  simulate(req: SimulateGovernanceRequest): Promise<GovernanceSimulationResponse> {
    return post(`${BASE}/simulate`, req);
  },
};
