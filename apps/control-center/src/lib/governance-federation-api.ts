const API_BASE = process.env.CONTROL_CENTER_API_BASE ?? 'http://127.0.0.1:5010';
const BASE = `${API_BASE}/notifications/v1/admin/governance`;

// ---------------------------------------------------------------------------
// Constants
// ---------------------------------------------------------------------------

export const CHANNEL_TYPES = ['sms', 'email', 'push', 'webhook', 'in_app', 'voice'] as const;
export type ChannelType = typeof CHANNEL_TYPES[number];

export const SCOPE_MODES = [
  'isolated_channel', 'inherited_channel', 'federated_shared', 'tenant_federated', 'rollout_federated',
] as const;
export type ScopeMode = typeof SCOPE_MODES[number];

export const OVERLAY_TYPES = [
  'add_rule', 'disable_rule', 'suppress_rule', 'override_severity',
  'override_pattern', 'override_metadata', 'override_classification',
  'channel_override', 'tenant_channel_override',
] as const;
export type OverlayType = typeof OVERLAY_TYPES[number];

export const OVERLAY_STATES = ['draft', 'active', 'inactive', 'expired', 'superseded'] as const;
export type OverlayState = typeof OVERLAY_STATES[number];

// ---------------------------------------------------------------------------
// DTOs
// ---------------------------------------------------------------------------

export interface ChannelScopeDto {
  id:          string;
  channelType: string;
  scopeMode:   string;
  enabled:     boolean;
  priority:    number;
  description: string | null;
  createdAt:   string;
  updatedAt:   string;
  createdBy:   string | null;
}

export interface FederatedRulePackDto {
  id:              string;
  rulePackId:      string;
  channelType:     string;
  federationGroup: string | null;
  tenantId:        string | null;
  enabled:         boolean;
  priority:        number;
  effectiveFrom:   string | null;
  effectiveTo:     string | null;
  createdAt:       string;
  createdBy:       string | null;
}

export interface FederationOverlayDto {
  id:           string;
  tenantId:     string | null;
  channelType:  string;
  rulePackId:   string | null;
  ruleId:       string | null;
  overlayType:  string;
  overlayState: string;
  priority:     number;
  enabled:      boolean;
  effectiveFrom: string | null;
  effectiveTo:   string | null;
  createdAt:    string;
  createdBy:    string | null;
}

export interface PaginatedFederationResult<T> {
  total:    number;
  page:     number;
  pageSize: number;
  items:    T[];
}

export interface ChannelPackSummary {
  rulePackId:        string;
  packName:          string;
  source:            string;
  federationGroup:   string | null;
  priority:          number;
  isGlobal:          boolean;
  isChannelFederated: boolean;
  isTenantAssigned:  boolean;
}

export interface FederationOverlaySummary {
  overlayId:    string;
  overlayType:  string;
  channelType:  string;
  tenantId:     string | null;
  rulePackId:   string | null;
  ruleId:       string | null;
  priority:     number;
}

export interface GovernanceTopologyGraph {
  channelType:       string;
  tenantId:          string | null;
  scopeMode:         string;
  globalPacks:       ChannelPackSummary[];
  channelPacks:      ChannelPackSummary[];
  tenantPacks:       ChannelPackSummary[];
  federatedPacks:    ChannelPackSummary[];
  tenantOverlays:    FederationOverlaySummary[];
  federationOverlays: FederationOverlaySummary[];
  rolloutOverrides:  string[];
  finalRuleCount:    number;
  warnings:          string[];
}

export interface TopologyExplanationStep {
  stepNumber:       number;
  stepName:         string;
  description:      string;
  rulesContributed: number;
  rulesFiltered:    number;
  details:          string[];
}

export interface TopologyExplanation {
  channelType:       string;
  tenantId:          string | null;
  steps:             TopologyExplanationStep[];
  totalFinalRules:   number;
  federationEnabled: boolean;
  channelScopeFound: boolean;
  resolutionMode:    string;
}

export interface TopologyAnalyticsResult {
  totalChannelScopes:      number;
  enabledChannelScopes:    number;
  totalFederatedPacks:     number;
  enabledFederatedPacks:   number;
  totalFederationOverlays: number;
  activeFederationOverlays: number;
  totalAuditEvents:        number;
  byChannel:               ChannelGovernanceStats[];
  warnings:                string[];
}

export interface ChannelGovernanceStats {
  channelType:        string;
  activeChannelScopes: number;
  federatedPackCount:  number;
  activeOverlayCount:  number;
  tenantCoverageCount: number;
  scopeMode:           string;
  enabled:             boolean;
}

export interface FederationAuditEvent {
  id:              string;
  tenantId:        string | null;
  channelType:     string | null;
  federationGroup: string | null;
  entityType:      string;
  entityId:        string | null;
  eventType:       string;
  previousState:   string | null;
  newState:        string | null;
  actor:           string | null;
  reason:          string | null;
  createdAt:       string;
}

// ---------------------------------------------------------------------------
// Badge helpers
// ---------------------------------------------------------------------------

export function channelBadgeColor(channel: string): string {
  switch (channel) {
    case 'sms':     return 'bg-blue-100 text-blue-700';
    case 'email':   return 'bg-purple-100 text-purple-700';
    case 'push':    return 'bg-amber-100 text-amber-700';
    case 'webhook': return 'bg-rose-100 text-rose-700';
    case 'in_app':  return 'bg-teal-100 text-teal-700';
    case 'voice':   return 'bg-indigo-100 text-indigo-700';
    default:        return 'bg-slate-100 text-slate-700';
  }
}

export function scopeModeBadgeColor(mode: string): string {
  switch (mode) {
    case 'isolated_channel':  return 'bg-slate-100 text-slate-700';
    case 'inherited_channel': return 'bg-blue-100 text-blue-700';
    case 'federated_shared':  return 'bg-green-100 text-green-700';
    case 'tenant_federated':  return 'bg-purple-100 text-purple-700';
    case 'rollout_federated': return 'bg-amber-100 text-amber-700';
    default:                  return 'bg-slate-100 text-slate-700';
  }
}

export function overlayStateBadgeColor(state: string): string {
  switch (state) {
    case 'active':    return 'bg-green-100 text-green-700';
    case 'draft':     return 'bg-slate-100 text-slate-600';
    case 'inactive':  return 'bg-red-100 text-red-700';
    case 'expired':   return 'bg-orange-100 text-orange-700';
    case 'superseded': return 'bg-gray-100 text-gray-600';
    default:          return 'bg-slate-100 text-slate-700';
  }
}

// ---------------------------------------------------------------------------
// API client
// ---------------------------------------------------------------------------

async function fetchJson<T>(url: string, token: string, options?: RequestInit): Promise<T> {
  const res = await fetch(url, {
    ...options,
    headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json', ...options?.headers },
    cache: 'no-store',
  });
  if (!res.ok) throw new Error(`${url}: ${res.status}`);
  return res.json();
}

export function buildGovernanceFederationApi(token: string) {
  return {
    // Channel Scopes
    listChannelScopes: (params?: Record<string, string>) =>
      fetchJson<PaginatedFederationResult<ChannelScopeDto>>(
        `${BASE}/channel-scopes?${new URLSearchParams({ page: '1', pageSize: '100', ...params })}`, token),

    createChannelScope: (body: object) =>
      fetchJson<ChannelScopeDto>(`${BASE}/channel-scopes`, token, { method: 'POST', body: JSON.stringify(body) }),

    updateChannelScope: (id: string, body: object) =>
      fetchJson<ChannelScopeDto>(`${BASE}/channel-scopes/${id}`, token, { method: 'PUT', body: JSON.stringify(body) }),

    // Federated Rule Packs
    listFederatedRulePacks: (params?: Record<string, string>) =>
      fetchJson<PaginatedFederationResult<FederatedRulePackDto>>(
        `${BASE}/federated-rule-packs?${new URLSearchParams({ page: '1', pageSize: '100', ...params })}`, token),

    federateRulePack: (body: object) =>
      fetchJson<FederatedRulePackDto>(`${BASE}/federated-rule-packs`, token, { method: 'POST', body: JSON.stringify(body) }),

    disableFederatedRulePack: (id: string, reason?: string) =>
      fetchJson<{ success: boolean }>(`${BASE}/federated-rule-packs/${id}/disable`, token,
        { method: 'POST', body: JSON.stringify({ requestedBy: 'admin', reason }) }),

    // Federation Overlays
    listFederationOverlays: (params?: Record<string, string>) =>
      fetchJson<PaginatedFederationResult<FederationOverlayDto>>(
        `${BASE}/federation-overlays?${new URLSearchParams({ page: '1', pageSize: '100', ...params })}`, token),

    createFederationOverlay: (body: object) =>
      fetchJson<FederationOverlayDto>(`${BASE}/federation-overlays`, token, { method: 'POST', body: JSON.stringify(body) }),

    activateFederationOverlay: (id: string) =>
      fetchJson<{ success: boolean }>(`${BASE}/federation-overlays/${id}/activate`, token,
        { method: 'POST', body: JSON.stringify({ requestedBy: 'admin' }) }),

    disableFederationOverlay: (id: string, reason?: string) =>
      fetchJson<{ success: boolean }>(`${BASE}/federation-overlays/${id}/disable`, token,
        { method: 'POST', body: JSON.stringify({ requestedBy: 'admin', reason }) }),

    // Topology
    getTopology: (channelType: string, tenantId?: string) =>
      fetchJson<GovernanceTopologyGraph>(
        `${BASE}/topology?channelType=${channelType}${tenantId ? `&tenantId=${tenantId}` : ''}`, token),

    explainTopology: (channelType: string, tenantId?: string) =>
      fetchJson<TopologyExplanation>(
        `${BASE}/topology/explain?channelType=${channelType}${tenantId ? `&tenantId=${tenantId}` : ''}`, token),

    // Audit
    getAuditTrail: (params?: Record<string, string>) =>
      fetchJson<{ total: number; page: number; pageSize: number; items: FederationAuditEvent[] }>(
        `${BASE}/federation/audit?${new URLSearchParams({ page: '1', pageSize: '50', ...params })}`, token),

    // Analytics
    getAnalytics: (channelType?: string) =>
      fetchJson<{ topology: TopologyAnalyticsResult; channel: unknown; rollout: unknown }>(
        `${BASE}/federation/analytics${channelType ? `?channelType=${channelType}` : ''}`, token),
  };
}
