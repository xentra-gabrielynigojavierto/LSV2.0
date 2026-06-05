/**
 * control-center-api.ts — Control Center server-side API client.
 *
 * All methods call the real backend via the API gateway (apiFetch).
 * Every response is normalised through api-mappers.ts before being
 * returned, so the UI always receives strict-typed frontend shapes
 * regardless of whether the backend uses camelCase or snake_case.
 *
 * Server-only: Server Components, Server Actions, Route Handlers.
 * Never import into Client Components.
 *
 * Identity admin endpoints:  /identity/api/admin/...
 * Platform monitoring:       /platform/monitoring/...
 *
 * ── Cache strategy summary ───────────────────────────────────────────────────
 *
 *   Endpoint               Tag                  TTL    Rationale
 *   ─────────────────────  ───────────────────  ─────  ─────────────────────────────────
 *   tenants.list           cc:tenants           60 s   Tenant roster changes rarely
 *   tenants.getById        cc:tenants           60 s   Same lifecycle as list
 *   users.list             cc:users             30 s   User state changes more often
 *   users.getById          cc:users             30 s   Same lifecycle as list
 *   roles.list             cc:roles             300 s  Roles are near-static
 *   roles.getById          cc:roles             300 s  Same lifecycle as list
 *   audit.list             cc:audit             10 s   Near-real-time log view
 *   settings.list          cc:settings          300 s  Settings rarely change
 *   monitoring.getSummary  cc:monitoring        5 s    Live health feed
 *   support.list           cc:support           10 s   Case status changes frequently
 *   support.getById        cc:support           10 s   Same lifecycle as list
 *
 * ── Revalidation after mutations ─────────────────────────────────────────────
 *
 *   Mutation                          Invalidates
 *   ────────────────────────────────  ─────────────────
 *   tenants.updateEntitlement         cc:tenants
 *   settings.update                   cc:settings
 *   support.create                    cc:support
 *   support.addNote                   cc:support
 *   support.updateStatus              cc:support
 *
 * Error handling:
 *   - HTTP 401 is handled by apiFetch (redirects to /login)
 *   - HTTP 403/404/5xx throw ApiError — callers catch and display
 *     fetchError banners (already wired on all pages)
 *
 * TODO: add retry/backoff
 * TODO: add request tracing (correlation-id header)
 * TODO: add Redis or edge caching
 * TODO: add stale-while-revalidate strategy
 * TODO: add request deduplication
 */

import { apiClient, CACHE_TAGS }       from '@/lib/api-client';

/**
 * Dynamically loads revalidateTag so the static import never appears at
 * module level.  This keeps the file importable from the pages/ router
 * (where next/cache is not available) while still busting caches when
 * called from an App Router Server Action or Route Handler.
 */
function safeRevalidateTag(tag: string): void {
  void (import('next/cache') as Promise<{ revalidateTag: (tag: string) => void }>)
    .then(({ revalidateTag }) => revalidateTag(tag))
    .catch(() => { /* no-op when running outside App Router */ });
}
import {
  mapTenantSummary,
  mapTenantDetail,
  mapEntitlementResponse,
  mapUserSummary,
  mapUserDetail,
  mapRoleSummary,
  mapRoleDetail,
  mapAuditLog,
  mapCanonicalAuditEvent,
  mapSetting,
  mapMonitoring,
  mapSupportCase,
  mapSupportCaseDetail,
  mapSupportNote,
  mapSupportProductRef,
  mapTicketAttachment,
  mapPagedResponse,
  mapOrganizationTypeItem,
  mapRelationshipTypeItem,
  mapOrgRelationship,
  mapProductOrgTypeRule,
  mapProductRelTypeRule,
  mapLegacyCoverageReport,
  mapPlatformReadiness,
  mapCareConnectIntegrity,
  mapScopedRoleAssignment,
  mapAuditExport,
  mapIntegrityCheckpoint,
  mapLegalHold,
  mapAccessGroupSummary,
  mapAccessGroupMember,
  mapGroupProductAccess,
  mapGroupRoleAssignment,
  mapPermissionCatalogItem,
  mapRoleCapabilityItem,
  mapEffectivePermissionsResult,
  mapAccessDebugResult,
  mapAssignableRole,
  mapPolicySummary,
  mapPolicyDetail,
  mapPermissionPolicySummary,
  mapSupportedFields,
  unwrapApiResponse,
  unwrapApiResponseList,
}                                       from '@/lib/api-mappers';
import type {
  TenantSummary,
  TenantDetail,
  TenantUserSummary,
  TenantUserRoleAssignment,
  UserSummary,
  UserDetail,
  RoleSummary,
  RoleDetail,
  ProductEntitlementSummary,
  ProductCode,
  AuditLogEntry,
  CanonicalAuditEvent,
  PlatformSetting,
  MonitoringSummary,
  SupportCase,
  SupportCaseDetail,
  SupportCaseStatus,
  SupportNote,
  TicketAttachmentItem,
  PagedResponse,
  OrganizationTypeItem,
  RelationshipTypeItem,
  OrgRelationship,
  ProductOrgTypeRule,
  ProductRelTypeRule,
  LegacyCoverageReport,
  PlatformReadinessSummary,
  CareConnectIntegrityReport,
  ScopedRoleAssignment,
  AuditExport,
  IntegrityCheckpoint,
  LegalHold,
  AuditIngestPayload,
  RelatedEventsData,
  AuditAnalyticsSummary,
  AuditAnalyticsRequest,
  AuditAnomalyData,
  AuditAlertItem,
  AuditAlertListData,
  AuditEvaluateAlertsData,
  OrgSummary,
  AccessGroupSummary,
  AccessGroupMember,
  GroupProductAccess,
  GroupRoleAssignment,
  PermissionCatalogItem,
  RoleCapabilityItem,
  EffectivePermissionsResult,
  AccessDebugResult,
  PolicySummary,
  PolicyDetail,
  PermissionPolicySummary,
  SupportedFieldsResponse,
  WorkflowInstanceListItem,
  WorkflowInstanceDetail,
  WorkflowInstancePagedResponse,
  WorkflowAdminAction,
  WorkflowAdminActionResult,
  WorkflowTimelineEvent,
  WorkflowTimelineResponse,
  WorkflowTimelineSeverity,
  WorkflowTimelineActor,
  OutboxListItem,
  OutboxDetail,
  OutboxListResponse,
  OutboxSummary,
  OutboxRetryResult,
}                                       from '@/types/control-center';

// ── Helpers ───────────────────────────────────────────────────────────────────

/**
 * Build a URL query string from a params object, omitting any undefined /
 * null / empty-string values.
 */
function toQs(params: Record<string, unknown>): string {
  const pairs = Object.entries(params)
    .filter(([, v]) => v !== undefined && v !== null && v !== '')
    .map(([k, v]) => `${encodeURIComponent(k)}=${encodeURIComponent(String(v))}`);
  return pairs.length ? `?${pairs.join('&')}` : '';
}

/**
 * E9.1 — normalise a single workflow-instance row from Flow's admin
 * listing into the Control Center DTO. Tolerates missing optional fields
 * and never throws on unexpected values (best-effort string coercion).
 */
function mapWorkflowInstanceListItem(raw: unknown): WorkflowInstanceListItem {
  const r = (raw ?? {}) as Record<string, unknown>;
  const s = (v: unknown): string | null => {
    if (v === undefined || v === null) return null;
    const str = String(v);
    return str.length === 0 ? null : str;
  };
  return {
    id:                   String(r.id ?? ''),
    tenantId:             String(r.tenantId ?? '').toLowerCase(),
    productKey:           String(r.productKey ?? ''),
    workflowDefinitionId: String(r.workflowDefinitionId ?? ''),
    workflowName:         s(r.workflowName),
    status:               String(r.status ?? ''),
    currentStepKey:       s(r.currentStepKey),
    assignedToUserId:     s(r.assignedToUserId),
    correlationKey:       s(r.correlationKey),
    sourceEntityType:     s(r.sourceEntityType),
    sourceEntityId:       s(r.sourceEntityId),
    startedAt:            s(r.startedAt),
    completedAt:          s(r.completedAt),
    updatedAt:            s(r.updatedAt),
    createdAt:            String(r.createdAt ?? ''),
    lastErrorMessage:     s(r.lastErrorMessage),
    classifications:      Array.isArray(r.classifications)
      ? (r.classifications as unknown[]).map(c => String(c)).filter(c =>
          c === 'Failed' || c === 'Cancelled' || c === 'Stuck' || c === 'ErrorPresent'
        ) as WorkflowInstanceListItem['classifications']
      : [],
  };
}

/**
 * E9.2 — normalise the single-instance detail. Reuses the list-row
 * mapper for shared fields and tacks on the three detail-only fields.
 */
function mapWorkflowInstanceDetail(raw: unknown): WorkflowInstanceDetail {
  const base = mapWorkflowInstanceListItem(raw);
  const r = (raw ?? {}) as Record<string, unknown>;
  const s = (v: unknown): string | null => {
    if (v === undefined || v === null) return null;
    const str = String(v);
    return str.length === 0 ? null : str;
  };
  return {
    ...base,
    currentStageId:   s(r.currentStageId),
    currentStepName:  s(r.currentStepName),
    lastErrorMessage: s(r.lastErrorMessage),
  };
}

/**
 * E13.2 / E13.3 — normalise a single timeline event from the Flow
 * `/timeline` response. Tolerates missing optional fields and lifts
 * `severity` out of the metadata bag (where the Flow normalizer
 * places the upstream audit value) into a top-level field with a
 * constrained vocabulary. Anything unexpected falls back to `'info'`
 * so the drawer never crashes on a partial record.
 */
function mapWorkflowTimelineEvent(raw: unknown): WorkflowTimelineEvent {
  const r = (raw ?? {}) as Record<string, unknown>;
  const s = (v: unknown): string | null => {
    if (v === undefined || v === null) return null;
    const str = String(v);
    return str.length === 0 ? null : str;
  };

  const rawActor   = (r.actor ?? {}) as Record<string, unknown>;
  const actor: WorkflowTimelineActor = {
    id:   s(rawActor.id),
    name: s(rawActor.name),
    type: s(rawActor.type),
  };

  const rawMeta = (r.metadata ?? {}) as Record<string, unknown>;
  const metadata: Record<string, string> = {};
  for (const [k, v] of Object.entries(rawMeta)) {
    if (v === undefined || v === null) continue;
    metadata[k] = String(v);
  }

  // Severity may come either as a top-level field (future-proof) or
  // inside the metadata bag (current Flow normalizer behaviour). Map
  // common synonyms onto the constrained vocabulary.
  const rawSeverity = String(
    r.severity ?? metadata.severity ?? metadata.Severity ?? 'info',
  ).toLowerCase();
  let severity: WorkflowTimelineSeverity = 'info';
  if (rawSeverity === 'warning' || rawSeverity === 'warn') {
    severity = 'warning';
  } else if (rawSeverity === 'critical' || rawSeverity === 'error' || rawSeverity === 'fatal') {
    severity = 'critical';
  }

  return {
    eventId:        String(r.eventId ?? ''),
    auditId:        s(r.auditId),
    occurredAtUtc:  String(r.occurredAtUtc ?? ''),
    category:       String(r.category ?? 'other'),
    action:         String(r.action ?? ''),
    source:         String(r.source ?? 'flow'),
    severity,
    actor,
    performedBy:    s(r.performedBy) ?? actor.name ?? actor.id,
    summary:        s(r.summary),
    previousStatus: s(r.previousStatus),
    newStatus:      s(r.newStatus),
    metadata,
  };
}

function mapWorkflowTimelineResponse(
  raw: unknown,
  fallbackId: string,
): WorkflowTimelineResponse {
  const r = (raw ?? {}) as Record<string, unknown>;
  const events = Array.isArray(r.events)
    ? (r.events as unknown[]).map(mapWorkflowTimelineEvent)
    : [];
  return {
    workflowInstanceId: String(r.workflowInstanceId ?? fallbackId),
    tenantId:           String(r.tenantId ?? '').toLowerCase(),
    totalCount:         Number(r.totalCount ?? events.length) || 0,
    truncated:          Boolean(r.truncated ?? false),
    events,
  };
}

// ── E17 Outbox mappers ────────────────────────────────────────────────────────

function mapOutboxListItem(raw: unknown): OutboxListItem {
  const r = (raw ?? {}) as Record<string, unknown>;
  return {
    id:                  String(r.id                  ?? ''),
    tenantId:            String(r.tenantId            ?? '').toLowerCase(),
    workflowInstanceId:  r.workflowInstanceId != null ? String(r.workflowInstanceId) : null,
    eventType:           String(r.eventType           ?? ''),
    status:              String(r.status              ?? 'Pending') as OutboxListItem['status'],
    attemptCount:        Number(r.attemptCount        ?? 0) || 0,
    createdAt:           String(r.createdAt           ?? ''),
    updatedAt:           r.updatedAt  != null ? String(r.updatedAt)  : null,
    nextAttemptAt:       String(r.nextAttemptAt       ?? ''),
    processedAt:         r.processedAt != null ? String(r.processedAt) : null,
    lastError:           r.lastError   != null ? String(r.lastError)   : null,
  };
}

function mapOutboxDetail(raw: unknown): OutboxDetail {
  const base = mapOutboxListItem(raw);
  const r = (raw ?? {}) as Record<string, unknown>;
  return {
    ...base,
    lastError:       r.lastError      != null ? String(r.lastError)      : null,
    payloadSummary:  r.payloadSummary  != null ? String(r.payloadSummary)  : null,
    isRetryEligible: Boolean(r.isRetryEligible ?? false),
  };
}

// ── Server-side API ───────────────────────────────────────────────────────────
// Use in Server Components and Server Actions only.

export const controlCenterServerApi = {

  // ── Tenants ──────────────────────────────────────────────────────────────

  tenants: {
    /**
     * GET /identity/api/admin/tenants
     *
     * Returns a paged list of tenants, optionally filtered by search text
     * and/or scoped to a single tenant (tenantId param).
     * Response is normalised via mapTenantSummary + mapPagedResponse.
     *
     * Cache: 60 s  Tag: cc:tenants
     *   Tenant roster changes rarely; 60 s balances freshness vs load.
     *   On-demand invalidated by tenants.updateEntitlement mutation.
     *
     * TODO: enforce tenant scoping server-side
     * TODO: validate tenant context against session
     * TODO: add Redis or edge caching
     */
    list: async (params: {
      page?:     number;
      pageSize?: number;
      search?:   string;
      tenantId?: string;
    } = {}): Promise<PagedResponse<TenantSummary>> => {
      const qs = toQs({
        page:     params.page     ?? 1,
        pageSize: params.pageSize ?? 20,
      });
      const raw = await apiClient.get<unknown>(
        `/tenant/api/v1/admin/tenants${qs}`,
        60,
        [CACHE_TAGS.tenants],
      );
      return mapPagedResponse(raw, mapTenantSummary);
    },

    /**
     * GET /tenant/api/v1/admin/tenants/{id}
     *
     * TENANT-B11: Switched from Identity to Tenant service. Returns full
     * TenantDetail including product entitlements from Tenant DB,
     * branding logos, and Identity compat fields (sessionTimeoutMinutes).
     * Response is normalised via mapTenantDetail (mapper unchanged).
     *
     * Cache: 60 s  Tag: cc:tenants
     *   Same cache lifecycle as tenants.list.
     *   On-demand invalidated by tenants.updateEntitlement mutation.
     */
    getById: async (id: string): Promise<TenantDetail | null> => {
      try {
        const raw = await apiClient.get<unknown>(
          `/tenant/api/v1/admin/tenants/${encodeURIComponent(id)}`,
          60,
          [CACHE_TAGS.tenants],
        );
        return mapTenantDetail(raw);
      } catch (err: unknown) {
        if (isNotFound(err)) return null;
        throw err;
      }
    },

    /**
     * POST /tenant/api/v1/admin/tenants/{id}/entitlements/{productCode}
     *
     * TENANT-B12: Switched to Tenant service (canonical owner of entitlements).
     * Enables or disables a product entitlement for a tenant.
     * Response is normalised via mapEntitlementResponse.
     *
     * Revalidates: cc:tenants — so the next tenants.list / getById call
     * bypasses the cache and fetches fresh data.
     */
    updateEntitlement: async (
      tenantId:    string,
      productCode: ProductCode,
      enabled:     boolean,
    ): Promise<ProductEntitlementSummary> => {
      const raw = await apiClient.post<unknown>(
        `/tenant/api/v1/admin/tenants/${encodeURIComponent(tenantId)}/entitlements/${encodeURIComponent(productCode)}`,
        { enabled },
      );
      const result = mapEntitlementResponse(raw);
      safeRevalidateTag(CACHE_TAGS.tenants);
      return result;
    },

    /**
     * PATCH /tenant/api/v1/admin/tenants/{id}/session-settings
     *
     * TENANT-STABILIZATION: Switched from Identity to Tenant service proxy.
     * The Tenant service proxies this to Identity's PATCH /api/admin/tenants/{id}/session-settings.
     * Updates the per-tenant idle session timeout.
     * Pass null to reset to the platform default (30 minutes).
     */
    updateSessionSettings: async (
      tenantId: string,
      sessionTimeoutMinutes: number | null,
    ): Promise<void> => {
      await apiClient.patch<unknown>(
        `/tenant/api/v1/admin/tenants/${encodeURIComponent(tenantId)}/session-settings`,
        { sessionTimeoutMinutes },
      );
      safeRevalidateTag(CACHE_TAGS.tenants);
    },

    /**
     * POST /tenant/api/v1/admin/tenants
     *
     * TENANT-B12: Switched to Tenant service (canonical owner of tenant creation).
     * Creates a new tenant (Tenant-first) and triggers Identity provisioning downstream.
     * Returns the new tenant's ID/code/name plus the one-time temporary password
     * from the Identity provisioning step.
     *
     * If Identity provisioning fails, identityProvisioned=false is returned and the
     * Tenant record remains (canonical). The admin can retry via retryProvisioning.
     *
     * Revalidates: cc:tenants so the list refreshes immediately.
     */
    create: async (body: {
      name:               string;
      code:               string;
      orgType:            string;
      adminEmail:         string;
      adminFirstName:     string;
      adminLastName:      string;
      addressLine1?:      string;
      city?:              string;
      state?:             string;
      postalCode?:        string;
      latitude?:          number;
      longitude?:         number;
      geoPointSource?:    string;
    }): Promise<{
      tenantId:            string;
      displayName:         string;
      code:                string;
      status:              string;
      adminUserId:         string | null;
      adminEmail:          string;
      temporaryPassword:   string | null;
      subdomain?:          string;
      provisioningStatus?: string;
      hostname?:           string;
      tenantCreated:       boolean;
      identityProvisioned: boolean;
      nextAction?:         string;
    }> => {
      const raw = await apiClient.post<{
        tenantId:            string;
        displayName:         string;
        code:                string;
        status:              string;
        adminUserId:         string | null;
        adminEmail:          string;
        temporaryPassword:   string | null;
        subdomain?:          string;
        provisioningStatus?: string;
        hostname?:           string;
        tenantCreated:       boolean;
        identityProvisioned: boolean;
        nextAction?:         string;
      }>('/tenant/api/v1/admin/tenants', body);
      safeRevalidateTag(CACHE_TAGS.tenants);
      return raw;
    },

    /**
     * TENANT-STABILIZATION: Switched from Identity to Tenant service proxy.
     * The Tenant service proxies this to Identity's POST /api/admin/tenants/{id}/provisioning/retry.
     */
    retryProvisioning: async (tenantId: string): Promise<{
      success:            boolean;
      provisioningStatus: string;
      hostname?:          string;
      error?:             string;
    }> => {
      const raw = await apiClient.post<{
        success:            boolean;
        provisioningStatus: string;
        hostname?:          string;
        error?:             string;
      }>(`/tenant/api/v1/admin/tenants/${tenantId}/provisioning/retry`, {});
      safeRevalidateTag(CACHE_TAGS.tenants);
      return raw;
    },

    /**
     * TENANT-STABILIZATION: Switched from Identity to Tenant service proxy.
     * The Tenant service proxies this to Identity's POST /api/admin/tenants/{id}/verification/retry.
     */
    retryVerification: async (tenantId: string): Promise<{
      success:            boolean;
      provisioningStatus: string;
      hostname?:          string;
      error?:             string;
      failureStage?:      string;
    }> => {
      const raw = await apiClient.post<{
        success:            boolean;
        provisioningStatus: string;
        hostname?:          string;
        error?:             string;
        failureStage?:      string;
      }>(`/tenant/api/v1/admin/tenants/${tenantId}/verification/retry`, {});
      safeRevalidateTag(CACHE_TAGS.tenants);
      return raw;
    },
  },

  // ── Users ─────────────────────────────────────────────────────────────────

  users: {
    /**
     * GET /identity/api/admin/users
     *
     * Returns a paged list of tenant users. Optionally scoped to a single
     * tenant via tenantId query param.
     * Response is normalised via mapUserSummary + mapPagedResponse.
     *
     * Cache: 30 s  Tag: cc:users
     *   User records change more often than tenant records (invites,
     *   status updates). 30 s keeps the UI reasonably live.
     *
     * TODO: enforce tenant scoping server-side
     * TODO: validate tenant context against session
     * TODO: add Redis or edge caching
     */
    list: async (params: {
      page?:     number;
      pageSize?: number;
      search?:   string;
      tenantId?: string;
      status?:   string;
      userType?: string;
    } = {}): Promise<PagedResponse<UserSummary>> => {
      const qs = toQs({
        page:     params.page     ?? 1,
        pageSize: params.pageSize ?? 20,
        search:   params.search,
        tenantId: params.tenantId,
        status:   params.status,
        userType: params.userType,
      });
      const raw = await apiClient.get<unknown>(
        `/identity/api/admin/users${qs}`,
        30,
        [CACHE_TAGS.users],
      );
      return mapPagedResponse(raw, mapUserSummary);
    },

    /**
     * GET /identity/api/admin/users/{id}
     *
     * Returns full UserDetail, or null if not found.
     * Response is normalised via mapUserDetail.
     *
     * Cache: 30 s  Tag: cc:users
     *   Same lifecycle as users.list.
     */
    getById: async (id: string): Promise<UserDetail | null> => {
      try {
        const raw = await apiClient.get<unknown>(
          `/identity/api/admin/users/${encodeURIComponent(id)}`,
          30,
          [CACHE_TAGS.users],
        );
        return mapUserDetail(raw);
      } catch (err: unknown) {
        if (isNotFound(err)) return null;
        throw err;
      }
    },

    /**
     * POST /identity/api/admin/users/{id}/activate
     * Activates an inactive user. Revalidates cc:users cache.
     */
    activate: async (id: string): Promise<void> => {
      await apiClient.post<unknown>(
        `/identity/api/admin/users/${encodeURIComponent(id)}/activate`,
        {},
      );
      safeRevalidateTag(CACHE_TAGS.users);
    },

    /**
     * POST /identity/api/admin/users/{id}/deactivate
     * Deactivates an active user. Revalidates cc:users cache.
     */
    deactivate: async (id: string): Promise<void> => {
      await apiClient.post<unknown>(
        `/identity/api/admin/users/${encodeURIComponent(id)}/deactivate`,
        {},
      );
      safeRevalidateTag(CACHE_TAGS.users);
    },

    /**
     * POST /identity/api/admin/users/invite
     * Sends an invitation to a new user. Revalidates cc:users cache.
     * Returns inviteToken in non-production environments for hand-delivery fallback.
     */
    invite: async (payload: {
      email:          string;
      firstName:      string;
      lastName:       string;
      tenantId:       string;
      organizationId?: string;
      memberRole?:    string;
    }): Promise<{ activationLink?: string }> => {
      const result = await apiClient.post<{ activationLink?: string }>(
        '/identity/api/admin/users/invite',
        payload,
      );
      safeRevalidateTag(CACHE_TAGS.users);
      return { activationLink: result?.activationLink };
    },

    /**
     * POST /identity/api/admin/users/{id}/resend-invite
     * Resends a pending invitation. Revalidates cc:users cache.
     */
    resendInvite: async (id: string): Promise<void> => {
      await apiClient.post<unknown>(
        `/identity/api/admin/users/${encodeURIComponent(id)}/resend-invite`,
        {},
      );
      safeRevalidateTag(CACHE_TAGS.users);
    },

    /**
     * POST /identity/api/admin/users/{id}/cancel-invite
     * Revokes all pending invitations for a user. Revalidates cc:users cache.
     */
    cancelInvite: async (id: string): Promise<void> => {
      await apiClient.post<unknown>(
        `/identity/api/admin/users/${encodeURIComponent(id)}/cancel-invite`,
        {},
      );
      safeRevalidateTag(CACHE_TAGS.users);
    },

    /**
     * PUM-B06: POST /identity/api/admin/platform-users/invite
     *
     * Invites a new PlatformInternal user (LegalSynq staff).
     * Unlike the tenant invite flow, no tenantId is supplied — the backend
     * resolves the platform system tenant automatically.
     * Returns activationLink in non-production environments for hand-delivery fallback.
     */
    invitePlatformUser: async (payload: {
      email:     string;
      firstName: string;
      lastName:  string;
      roleId?:   string;
    }): Promise<{ activationLink?: string }> => {
      const result = await apiClient.post<{ activationLink?: string }>(
        '/identity/api/admin/platform-users/invite',
        payload,
      );
      safeRevalidateTag(CACHE_TAGS.users);
      return { activationLink: result?.activationLink };
    },

    /**
     * UIX-003-03: POST /identity/api/admin/users/{id}/lock
     * Locks a user account. Revalidates cc:users cache.
     */
    lock: async (id: string): Promise<void> => {
      await apiClient.post<unknown>(
        `/identity/api/admin/users/${encodeURIComponent(id)}/lock`,
        {},
      );
      safeRevalidateTag(CACHE_TAGS.users);
    },

    /**
     * UIX-003-03: POST /identity/api/admin/users/{id}/unlock
     * Unlocks a user account. Revalidates cc:users cache.
     */
    unlock: async (id: string): Promise<void> => {
      await apiClient.post<unknown>(
        `/identity/api/admin/users/${encodeURIComponent(id)}/unlock`,
        {},
      );
      safeRevalidateTag(CACHE_TAGS.users);
    },

    /**
     * PATCH /identity/api/admin/users/{id}/phone
     *
     * Sets or clears the user's primary phone number. Pass null/empty to
     * clear. The identity service performs E.164 normalisation and rejects
     * malformed input with a 400; callers should surface response.error.
     * Revalidates cc:users so the user-management list reflects changes.
     */
    updatePhone: async (id: string, phone: string | null): Promise<{ phone: string | null }> => {
      const raw = await apiClient.patch<{ phone?: string | null }>(
        `/identity/api/admin/users/${encodeURIComponent(id)}/phone`,
        { phone: phone && phone.trim() !== '' ? phone.trim() : null },
      );
      safeRevalidateTag(CACHE_TAGS.users);
      return { phone: raw?.phone ?? null };
    },

    setPassword: async (id: string, newPassword: string): Promise<void> => {
      await apiClient.post<unknown>(
        `/identity/api/admin/users/${encodeURIComponent(id)}/set-password`,
        { newPassword },
      );
    },

    /**
     * UIX-003-03: POST /identity/api/admin/users/{id}/reset-password
     * Triggers an admin-initiated password reset workflow.
     */
    resetPassword: async (id: string): Promise<void> => {
      await apiClient.post<unknown>(
        `/identity/api/admin/users/${encodeURIComponent(id)}/reset-password`,
        {},
      );
    },

    /**
     * UIX-003-03: POST /identity/api/admin/users/{id}/force-logout
     * Revokes all active sessions by incrementing SessionVersion.
     */
    forceLogout: async (id: string): Promise<void> => {
      await apiClient.post<unknown>(
        `/identity/api/admin/users/${encodeURIComponent(id)}/force-logout`,
        {},
      );
    },

    /**
     * UIX-003-03: GET /identity/api/admin/users/{id}/security
     * Returns security summary: lock state, last login, session version,
     * recent password resets.
     */
    getSecurity: async (id: string): Promise<import('@/types/control-center').UserSecurity | null> => {
      try {
        return await apiClient.get<import('@/types/control-center').UserSecurity>(
          `/identity/api/admin/users/${encodeURIComponent(id)}/security`,
        );
      } catch {
        return null;
      }
    },

    /**
     * UIX-004: GET /identity/api/admin/users/{id}/activity
     *
     * Returns a paged list of local AuditLog entries for this user
     * (admin actions: lock/unlock/force-logout/role-assign/etc).
     * For richer event data (login, logout, invite) use auditCanonical.listForUser().
     * Never throws — returns null on error so the panel degrades gracefully.
     */
    getActivity: async (
      id: string,
      params: { page?: number; pageSize?: number; category?: string } = {},
    ): Promise<{ items: AuditLogEntry[]; totalCount: number } | null> => {
      const qs = toQs({
        page:     params.page     ?? 1,
        pageSize: params.pageSize ?? 20,
        category: params.category,
      });
      try {
        const raw = await apiClient.get<unknown>(
          `/identity/api/admin/users/${encodeURIComponent(id)}/activity${qs}`,
          10,
          [CACHE_TAGS.audit],
        );
        const paged = mapPagedResponse(raw, mapAuditLog);
        return { items: paged.items, totalCount: paged.totalCount };
      } catch {
        return null;
      }
    },

    /**
     * POST /identity/api/admin/users/{id}/memberships
     * Assigns the user to an organization. Revalidates cc:users cache.
     */
    assignMembership: async (id: string, payload: {
      organizationId: string;
      memberRole?:    string;
    }): Promise<void> => {
      await apiClient.post<unknown>(
        `/identity/api/admin/users/${encodeURIComponent(id)}/memberships`,
        payload,
      );
      safeRevalidateTag(CACHE_TAGS.users);
    },

    /**
     * POST /identity/api/admin/users/{id}/memberships/{membershipId}/set-primary
     * Marks an org membership as the user's primary org. Revalidates cc:users cache.
     */
    setPrimaryMembership: async (id: string, membershipId: string): Promise<void> => {
      await apiClient.post<unknown>(
        `/identity/api/admin/users/${encodeURIComponent(id)}/memberships/${encodeURIComponent(membershipId)}/set-primary`,
        {},
      );
      safeRevalidateTag(CACHE_TAGS.users);
    },

    /**
     * DELETE /identity/api/admin/users/{id}/memberships/{membershipId}
     * Removes an org membership from the user. Revalidates cc:users cache.
     */
    removeMembership: async (id: string, membershipId: string): Promise<void> => {
      await apiClient.del<unknown>(
        `/identity/api/admin/users/${encodeURIComponent(id)}/memberships/${encodeURIComponent(membershipId)}`,
      );
      safeRevalidateTag(CACHE_TAGS.users);
    },

    /**
     * POST /identity/api/admin/users/{id}/roles
     * Assigns a role to a user (GLOBAL scope). Revalidates cc:users cache.
     */
    assignRole: async (id: string, roleId: string): Promise<void> => {
      await apiClient.post<unknown>(
        `/identity/api/admin/users/${encodeURIComponent(id)}/roles`,
        { roleId },
      );
      safeRevalidateTag(CACHE_TAGS.users);
    },

    /**
     * UIX-002-C: GET /identity/api/admin/users/{id}/assignable-roles
     * Returns all roles with eligibility metadata for a specific user.
     * Cache: 10 s  Tag: cc:users
     */
    getAssignableRoles: async (id: string): Promise<import('@/types/control-center').AssignableRolesResponse> => {
      const raw = await apiClient.get<unknown>(
        `/identity/api/admin/users/${encodeURIComponent(id)}/assignable-roles`,
        10,
        [CACHE_TAGS.users],
      );
      const r = raw as Record<string, unknown>;
      const items = Array.isArray(r.items)
        ? (r.items as unknown[]).map(mapAssignableRole)
        : [];
      return {
        items,
        userOrgType: String(r.userOrgType ?? r.user_org_type ?? 'UNKNOWN'),
        tenantEnabledProducts: Number(r.tenantEnabledProducts ?? r.tenant_enabled_products ?? 0),
      };
    },

    /**
     * DELETE /identity/api/admin/users/{id}/roles/{roleId}
     * Revokes a role from a user. Revalidates cc:users cache.
     */
    revokeRole: async (id: string, roleId: string): Promise<void> => {
      await apiClient.del<unknown>(
        `/identity/api/admin/users/${encodeURIComponent(id)}/roles/${encodeURIComponent(roleId)}`,
      );
      safeRevalidateTag(CACHE_TAGS.users);
    },

    /**
     * GET /identity/api/admin/users/{id}/permissions
     *
     * Returns the effective (union) permissions for a user — all capabilities
     * derived from their active role assignments, with source attribution.
     * Cache: 30 s  Tag: cc:users
     *
     * UIX-005
     */
    getEffectivePermissions: async (id: string): Promise<EffectivePermissionsResult> => {
      const raw = await apiClient.get<unknown>(
        `/identity/api/admin/users/${encodeURIComponent(id)}/permissions`,
        30,
        [CACHE_TAGS.users],
      );
      return mapEffectivePermissionsResult(raw);
    },

    getAccessDebug: async (id: string): Promise<AccessDebugResult> => {
      const raw = await apiClient.get<unknown>(
        `/identity/api/admin/users/${encodeURIComponent(id)}/access-debug`,
        15,
        [CACHE_TAGS.users],
      );
      return mapAccessDebugResult(raw);
    },
  },

  // ── Organizations ─────────────────────────────────────────────────────────

  organizations: {
    /**
     * GET /identity/api/admin/organizations?tenantId=
     * Lists active organizations, optionally scoped to a tenant.
     * Cache: 60 s  Tag: cc:tenants (org changes follow tenant lifecycle)
     */
    listByTenant: async (tenantId: string): Promise<OrgSummary[]> => {
      const qs = tenantId ? `?tenantId=${encodeURIComponent(tenantId)}` : '';
      const raw = await apiClient.get<unknown>(
        `/identity/api/admin/organizations${qs}`,
        60,
        [CACHE_TAGS.tenants],
      );
      if (raw && typeof raw === 'object' && 'items' in raw && Array.isArray((raw as { items: unknown[] }).items)) {
        return (raw as { items: Record<string, unknown>[] }).items.map(o => ({
          id:          String(o.id ?? ''),
          tenantId:    String(o.tenantId ?? ''),
          name:        String(o.name ?? ''),
          displayName: String(o.displayName ?? o.name ?? ''),
          orgType:     String(o.orgType ?? ''),
          isActive:    Boolean(o.isActive ?? true),
        }));
      }
      return [];
    },

    update: async (orgId: string, body: { name?: string; displayName?: string; orgType?: string }): Promise<OrgSummary> => {
      const raw = await apiClient.put<Record<string, unknown>>(
        `/identity/api/admin/organizations/${encodeURIComponent(orgId)}`,
        body,
      );
      return {
        id:          String(raw.id ?? ''),
        tenantId:    String(raw.tenantId ?? ''),
        name:        String(raw.name ?? ''),
        displayName: String(raw.displayName ?? raw.name ?? ''),
        orgType:     String(raw.orgType ?? ''),
        isActive:    Boolean(raw.isActive ?? true),
      };
    },
  },

  // ── Permissions ───────────────────────────────────────────────────────────

  permissions: {
    /**
     * GET /identity/api/admin/permissions
     *
     * Returns the platform capability catalog, optionally filtered by productId
     * or searched by code/name/description.
     *
     * Cache: 300 s  Tag: cc:roles (same lifecycle as roles — permissions are static)
     */
    list: async (opts?: { productId?: string; search?: string }): Promise<PermissionCatalogItem[]> => {
      const qs = new URLSearchParams();
      if (opts?.productId) qs.set('productId', opts.productId);
      if (opts?.search)    qs.set('search',    opts.search);
      const suffix = qs.toString() ? `?${qs}` : '';

      const raw = await apiClient.get<unknown>(
        `/identity/api/admin/permissions${suffix}`,
        300,
        [CACHE_TAGS.roles],
      );
      const paged = mapPagedResponse(raw, mapPermissionCatalogItem);
      return paged.items;
    },

    create: async (payload: {
      code: string;
      name: string;
      description?: string;
      category?: string;
      productCode: string;
    }): Promise<PermissionCatalogItem> => {
      const raw = await apiClient.post<unknown>(
        '/identity/api/admin/permissions',
        payload,
      );
      safeRevalidateTag(CACHE_TAGS.roles);
      return mapPermissionCatalogItem(raw);
    },

    update: async (id: string, payload: {
      name?: string;
      description?: string;
      category?: string;
    }): Promise<PermissionCatalogItem> => {
      const raw = await apiClient.patch<unknown>(
        `/identity/api/admin/permissions/${encodeURIComponent(id)}`,
        payload,
      );
      safeRevalidateTag(CACHE_TAGS.roles);
      return mapPermissionCatalogItem(raw);
    },

    deactivate: async (id: string): Promise<void> => {
      await apiClient.del(
        `/identity/api/admin/permissions/${encodeURIComponent(id)}`,
      );
      safeRevalidateTag(CACHE_TAGS.roles);
    },
  },

  // ── Policies (LS-COR-AUT-011) ──────────────────────────────────────────────

  policies: {
    list: async (opts?: { productCode?: string; search?: string }): Promise<PolicySummary[]> => {
      const qs = new URLSearchParams();
      if (opts?.productCode) qs.set('productCode', opts.productCode);
      if (opts?.search) qs.set('search', opts.search);
      const suffix = qs.toString() ? `?${qs}` : '';

      const raw = await apiClient.get<unknown>(
        `/identity/api/admin/policies${suffix}`,
        300,
        [CACHE_TAGS.policies],
      );
      const paged = mapPagedResponse(raw, mapPolicySummary);
      return paged.items;
    },

    getById: async (id: string): Promise<PolicyDetail | null> => {
      try {
        const raw = await apiClient.get<unknown>(
          `/identity/api/admin/policies/${encodeURIComponent(id)}`,
          300,
          [CACHE_TAGS.policies],
        );
        return mapPolicyDetail(raw);
      } catch { return null; }
    },

    create: async (payload: {
      policyCode: string;
      name: string;
      productCode: string;
      description?: string;
      priority?: number;
    }): Promise<PolicySummary> => {
      const raw = await apiClient.post<unknown>(
        '/identity/api/admin/policies',
        payload,
      );
      safeRevalidateTag(CACHE_TAGS.policies);
      return mapPolicySummary(raw);
    },

    update: async (id: string, payload: {
      name: string;
      description?: string;
      priority: number;
    }): Promise<PolicySummary> => {
      const raw = await apiClient.patch<unknown>(
        `/identity/api/admin/policies/${encodeURIComponent(id)}`,
        payload,
      );
      safeRevalidateTag(CACHE_TAGS.policies);
      return mapPolicySummary(raw);
    },

    deactivate: async (id: string): Promise<void> => {
      await apiClient.del(
        `/identity/api/admin/policies/${encodeURIComponent(id)}`,
      );
      safeRevalidateTag(CACHE_TAGS.policies);
    },

    createRule: async (policyId: string, payload: {
      conditionType: string;
      field: string;
      operator: string;
      value: string;
      logicalGroup?: string;
    }): Promise<unknown> => {
      const raw = await apiClient.post<unknown>(
        `/identity/api/admin/policies/${encodeURIComponent(policyId)}/rules`,
        { ...payload, logicalGroup: payload.logicalGroup ?? 'And' },
      );
      safeRevalidateTag(CACHE_TAGS.policies);
      return raw;
    },

    deleteRule: async (policyId: string, ruleId: string): Promise<void> => {
      await apiClient.del(
        `/identity/api/admin/policies/${encodeURIComponent(policyId)}/rules/${encodeURIComponent(ruleId)}`,
      );
      safeRevalidateTag(CACHE_TAGS.policies);
    },

    getSupportedFields: async (): Promise<SupportedFieldsResponse> => {
      const raw = await apiClient.get<unknown>(
        '/identity/api/admin/policies/supported-fields',
        600,
        [CACHE_TAGS.policies],
      );
      return mapSupportedFields(raw);
    },
  },

  permissionPolicies: {
    list: async (opts?: { permissionCode?: string; policyId?: string }): Promise<PermissionPolicySummary[]> => {
      const qs = new URLSearchParams();
      if (opts?.permissionCode) qs.set('permissionCode', opts.permissionCode);
      if (opts?.policyId) qs.set('policyId', opts.policyId);
      const suffix = qs.toString() ? `?${qs}` : '';

      const raw = await apiClient.get<unknown>(
        `/identity/api/admin/permission-policies${suffix}`,
        300,
        [CACHE_TAGS.policies],
      );
      const paged = mapPagedResponse(raw, mapPermissionPolicySummary);
      return paged.items;
    },

    create: async (payload: { permissionCode: string; policyId: string }): Promise<unknown> => {
      const raw = await apiClient.post<unknown>(
        '/identity/api/admin/permission-policies',
        { permissionCode: payload.permissionCode, policyId: payload.policyId },
      );
      safeRevalidateTag(CACHE_TAGS.policies);
      return raw;
    },

    deactivate: async (id: string): Promise<void> => {
      await apiClient.del(
        `/identity/api/admin/permission-policies/${encodeURIComponent(id)}`,
      );
      safeRevalidateTag(CACHE_TAGS.policies);
    },
  },

  // ── Roles ─────────────────────────────────────────────────────────────────

  roles: {
    /**
     * GET /identity/api/admin/roles
     *
     * Returns the full list of platform roles with capability counts.
     * Response is normalised via mapRoleSummary.
     *
     * Cache: 300 s  Tag: cc:roles
     */
    list: async (params: { scope?: string } = {}): Promise<RoleSummary[]> => {
      const qs = params.scope ? `?scope=${encodeURIComponent(params.scope)}` : '';
      const raw = await apiClient.get<unknown>(
        `/identity/api/admin/roles${qs}`,
        300,
        [CACHE_TAGS.roles],
      );
      if (Array.isArray(raw)) return raw.map(mapRoleSummary);
      const paged = mapPagedResponse(raw, mapRoleSummary);
      return paged.items;
    },

    /**
     * GET /identity/api/admin/roles/{id}
     *
     * Returns full RoleDetail including resolved permissions, or null if not found.
     * Response is normalised via mapRoleDetail.
     *
     * Cache: 300 s  Tag: cc:roles
     */
    getById: async (id: string): Promise<RoleDetail | null> => {
      try {
        const raw = await apiClient.get<unknown>(
          `/identity/api/admin/roles/${encodeURIComponent(id)}`,
          300,
          [CACHE_TAGS.roles],
        );
        return mapRoleDetail(raw);
      } catch (err: unknown) {
        if (isNotFound(err)) return null;
        throw err;
      }
    },

    /**
     * GET /identity/api/admin/roles/{id}/permissions
     *
     * Returns all capabilities currently assigned to a role.
     * Cache: 60 s — assignments change less often than user data but more than catalog.
     */
    getPermissions: async (id: string): Promise<RoleCapabilityItem[]> => {
      const raw = await apiClient.get<unknown>(
        `/identity/api/admin/roles/${encodeURIComponent(id)}/permissions`,
        60,
        [CACHE_TAGS.roles],
      );
      const paged = mapPagedResponse(raw, mapRoleCapabilityItem);
      return paged.items;
    },

    /**
     * POST /identity/api/admin/roles/{id}/permissions
     *
     * Assigns a capability to a role. Revalidates cc:roles cache.
     */
    assignPermission: async (id: string, capabilityId: string): Promise<void> => {
      await apiClient.post(
        `/identity/api/admin/roles/${encodeURIComponent(id)}/permissions`,
        { permissionId: capabilityId },
      );
      safeRevalidateTag(CACHE_TAGS.roles);
    },

    /**
     * DELETE /identity/api/admin/roles/{id}/permissions/{capabilityId}
     *
     * Revokes a capability from a role. Revalidates cc:roles cache.
     */
    revokePermission: async (id: string, capabilityId: string): Promise<void> => {
      await apiClient.del(
        `/identity/api/admin/roles/${encodeURIComponent(id)}/permissions/${encodeURIComponent(capabilityId)}`,
      );
      safeRevalidateTag(CACHE_TAGS.roles);
    },
  },

  // ── Audit Logs ────────────────────────────────────────────────────────────

  audit: {
    /**
     * GET /identity/api/admin/audit
     *
     * Returns a paged, filtered list of audit log entries.
     * Response is normalised via mapAuditLog + mapPagedResponse.
     *
     * Cache: 10 s  Tag: cc:audit
     *   Admins expect near-real-time audit visibility. 10 s prevents
     *   hammering the DB on every keystroke in the search box while
     *   still showing recent entries within one page refresh.
     *
     * TODO: enforce tenant scoping server-side
     * TODO: validate tenant context against session
     * TODO: add Redis or edge caching
     */
    list: async (params: {
      page?:       number;
      pageSize?:   number;
      search?:     string;
      entityType?: string;
      actor?:      string;
      tenantId?:   string;
    } = {}): Promise<{ items: AuditLogEntry[]; totalCount: number }> => {
      const qs = toQs({
        page:       params.page     ?? 1,
        pageSize:   params.pageSize ?? 15,
        search:     params.search,
        entityType: params.entityType,
        actor:      params.actor,
        tenantId:   params.tenantId,
      });
      const raw = await apiClient.get<unknown>(
        `/identity/api/admin/audit${qs}`,
        10,
        [CACHE_TAGS.audit],
      );
      const paged = mapPagedResponse(raw, mapAuditLog);
      return { items: paged.items, totalCount: paged.totalCount };
    },
  },

  // ── Canonical Audit (Platform Audit Event Service) ────────────────────────

  auditCanonical: {
    /**
     * GET /audit-service/audit/events
     *
     * Queries the canonical Platform Audit Event Service (port 5007) via the
     * API gateway route /audit-service/...
     *
     * Supports rich filtering: tenantId, eventType, category, severity, actorId,
     * targetType, targetId, correlationId, dateFrom, dateTo, page, pageSize.
     *
     * Cache: 10 s  Tag: cc:audit-canonical
     *
     * AUDIT_READ_MODE env controls which source the audit-logs page uses:
     *   legacy    → only GET /identity/api/admin/audit   (default)
     *   canonical → only GET /audit-service/audit/events
     *   hybrid    → canonical first, fall back to legacy on error
     */
    list: async (params: {
      page?:          number;
      pageSize?:      number;
      tenantId?:      string;
      eventType?:     string;
      category?:      string;
      severity?:      string;
      actorId?:       string;
      targetType?:    string;
      targetId?:      string;
      correlationId?: string;
      dateFrom?:      string;
      dateTo?:        string;
      search?:        string;
    } = {}): Promise<{ items: CanonicalAuditEvent[]; totalCount: number }> => {
      const qs = toQs({
        page:          params.page        ?? 1,
        pageSize:      params.pageSize    ?? 15,
        tenantId:      params.tenantId,
        eventType:     params.eventType,
        category:      params.category,
        severity:      params.severity,
        actorId:       params.actorId,
        targetType:    params.targetType,
        targetId:      params.targetId,
        correlationId: params.correlationId,
        dateFrom:      params.dateFrom,
        dateTo:        params.dateTo,
        search:        params.search,
      });
      const raw = await apiClient.get<unknown>(
        `/audit-service/audit/events${qs}`,
        10,
        [CACHE_TAGS.auditCanonical],
      );
      const paged = mapPagedResponse(raw, mapCanonicalAuditEvent);
      return { items: paged.items, totalCount: paged.totalCount };
    },

    /**
     * GET /audit-service/audit/events/{auditId}
     *
     * Fetches a single canonical audit event by its stable auditId.
     * Cache: 30 s  Tag: cc:audit-canonical
     */
    getById: async (auditId: string): Promise<CanonicalAuditEvent | null> => {
      try {
        const raw = await apiClient.get<unknown>(
          `/audit-service/audit/events/${encodeURIComponent(auditId)}`,
          30,
          [CACHE_TAGS.auditCanonical],
        );
        if (!raw) return null;
        return mapCanonicalAuditEvent(unwrapApiResponse(raw));
      } catch {
        return null;
      }
    },

    /**
     * UIX-004: GET /audit-service/audit/events?targetId=&actorId=&tenantId=
     *
     * Convenience method: returns recent canonical events involving a specific user
     * (as actor or as target). Scoped to the caller's tenant.
     * Never throws — returns [] on error so the panel degrades gracefully.
     */
    listForUser: async (params: {
      userId:     string;
      tenantId?:  string;
      page?:      number;
      pageSize?:  number;
    }): Promise<{ items: CanonicalAuditEvent[]; totalCount: number }> => {
      try {
        const qs = toQs({
          targetId:   params.userId,
          targetType: 'User',
          tenantId:   params.tenantId,
          page:       params.page     ?? 1,
          pageSize:   params.pageSize ?? 15,
        });
        const raw = await apiClient.get<unknown>(
          `/audit-service/audit/events${qs}`,
          10,
          [CACHE_TAGS.auditCanonical],
        );
        const paged = mapPagedResponse(raw, mapCanonicalAuditEvent);
        return { items: paged.items, totalCount: paged.totalCount };
      } catch {
        return { items: [], totalCount: 0 };
      }
    },

    /**
     * GET /audit-service/audit/events/{auditId}/related
     *
     * Correlation engine: returns events related to the given anchor event using
     * a four-tier cascade (correlationId → sessionId → actor+entity+4h → actor+2h).
     * Each result carries a matchedBy label explaining which key linked it.
     * Never throws — returns null on error or when the anchor is not found.
     */
    relatedEvents: async (auditId: string): Promise<RelatedEventsData | null> => {
      try {
        const raw = await apiClient.get<unknown>(
          `/audit-service/audit/events/${encodeURIComponent(auditId)}/related`,
          0,
          [CACHE_TAGS.auditCanonical],
        );
        if (!raw) return null;
        const data = unwrapApiResponse(raw) as Record<string, unknown>;
        const relatedRaw = Array.isArray(data['related']) ? data['related'] as unknown[] : [];
        return {
          anchorId:        data['anchorId']        as string,
          anchorEventType: data['anchorEventType'] as string,
          strategyUsed:    (data['strategyUsed']   as RelatedEventsData['strategyUsed']) ?? 'none',
          totalRelated:    (data['totalRelated']    as number) ?? 0,
          related: relatedRaw.map((r) => {
            const item = r as Record<string, unknown>;
            return {
              matchedBy: item['matchedBy'] as RelatedEventsData['related'][0]['matchedBy'],
              matchKey:  item['matchKey']  as string,
              event:     mapCanonicalAuditEvent(item['event']),
            };
          }),
        };
      } catch {
        return null;
      }
    },
    /**
     * GET /audit-service/audit/analytics/summary
     *
     * Returns the aggregated analytics summary for the specified window.
     * Passes optional from/to/tenantId/category filters as query params.
     * Cache: short TTL (no-store in practice — analytics data changes frequently).
     */
    analyticsSummary: async (params: AuditAnalyticsRequest = {}): Promise<AuditAnalyticsSummary | null> => {
      try {
        const qs = new URLSearchParams();
        if (params.from)     qs.set('from',     params.from);
        if (params.to)       qs.set('to',       params.to);
        if (params.tenantId) qs.set('tenantId', params.tenantId);
        if (params.category) qs.set('category', params.category);

        const url = `/audit-service/audit/analytics/summary${qs.size > 0 ? `?${qs.toString()}` : ''}`;
        const raw = await apiClient.get<unknown>(url, 0, [CACHE_TAGS.auditCanonical]);
        if (!raw) return null;
        const data = unwrapApiResponse(raw) as Record<string, unknown>;

        const mapList = <T>(key: string): T[] =>
          Array.isArray(data[key]) ? (data[key] as T[]) : [];

        return {
          from:                 (data['from']  as string)  ?? '',
          to:                   (data['to']    as string)  ?? '',
          effectiveTenantId:    (data['effectiveTenantId'] as string | null) ?? null,
          totalEvents:          (data['totalEvents']        as number) ?? 0,
          securityEventCount:   (data['securityEventCount'] as number) ?? 0,
          denialEventCount:     (data['denialEventCount']   as number) ?? 0,
          governanceEventCount: (data['governanceEventCount'] as number) ?? 0,
          volumeByDay:          mapList('volumeByDay'),
          byCategory:           mapList('byCategory'),
          bySeverity:           mapList('bySeverity'),
          topEventTypes:        mapList('topEventTypes'),
          topActors:            mapList('topActors'),
          topTenants:           Array.isArray(data['topTenants'])
                                  ? (data['topTenants'] as AuditAnalyticsSummary['topTenants'])
                                  : null,
        };
      } catch {
        return null;
      }
    },

    /**
     * GET /audit-service/audit/analytics/anomalies
     *
     * Evaluates deterministic anomaly detection rules over the last 24h
     * compared to a 7-day baseline. Returns all firing anomaly items.
     * An empty anomalies array is a valid response (no anomalies detected).
     */
    anomalies: async (params: { tenantId?: string } = {}): Promise<AuditAnomalyData | null> => {
      try {
        const qs  = new URLSearchParams();
        if (params.tenantId) qs.set('tenantId', params.tenantId);
        const url = `/audit-service/audit/analytics/anomalies${qs.size > 0 ? `?${qs.toString()}` : ''}`;
        const raw = await apiClient.get<unknown>(url, 0, [CACHE_TAGS.auditCanonical]);
        if (!raw) return null;
        const data = unwrapApiResponse(raw) as Record<string, unknown>;
        return {
          evaluatedAt:        (data['evaluatedAt']        as string) ?? '',
          recentWindowFrom:   (data['recentWindowFrom']   as string) ?? '',
          recentWindowTo:     (data['recentWindowTo']     as string) ?? '',
          baselineWindowFrom: (data['baselineWindowFrom'] as string) ?? '',
          baselineWindowTo:   (data['baselineWindowTo']   as string) ?? '',
          effectiveTenantId:  (data['effectiveTenantId']  as string | null) ?? null,
          totalAnomalies:     (data['totalAnomalies']     as number) ?? 0,
          anomalies:          Array.isArray(data['anomalies'])
                                ? (data['anomalies'] as AuditAnomalyData['anomalies'])
                                : [],
        };
      } catch {
        return null;
      }
    },
  },

  // ── SynqAudit — Audit Alerts ──────────────────────────────────────────────

  auditAlerts: {
    /**
     * GET /audit-service/audit/analytics/alerts
     *
     * Returns alert records with optional status / tenantId / limit filters.
     */
    list: async (params: { status?: string; tenantId?: string; limit?: number } = {}): Promise<AuditAlertListData | null> => {
      try {
        const qs = new URLSearchParams();
        if (params.status)   qs.set('status',   params.status);
        if (params.tenantId) qs.set('tenantId', params.tenantId);
        if (params.limit)    qs.set('limit',    String(params.limit));
        const url = `/audit-service/audit/analytics/alerts${qs.size > 0 ? `?${qs.toString()}` : ''}`;
        const raw = await apiClient.get<unknown>(url, 0, [CACHE_TAGS.auditCanonical]);
        if (!raw) return null;
        const data = unwrapApiResponse(raw) as Record<string, unknown>;
        return {
          statusFilter:      (data['statusFilter']      as string | null) ?? null,
          effectiveTenantId: (data['effectiveTenantId'] as string | null) ?? null,
          totalReturned:     (data['totalReturned']     as number) ?? 0,
          openCount:         (data['openCount']         as number) ?? 0,
          acknowledgedCount: (data['acknowledgedCount'] as number) ?? 0,
          resolvedCount:     (data['resolvedCount']     as number) ?? 0,
          alerts:            Array.isArray(data['alerts']) ? (data['alerts'] as AuditAlertItem[]) : [],
        };
      } catch {
        return null;
      }
    },

    /**
     * POST /audit-service/audit/analytics/alerts/evaluate
     *
     * Runs anomaly detection and upserts alert records for all firing rules.
     * Deduplication prevents alert storms.
     */
    evaluate: async (params: { tenantId?: string } = {}): Promise<AuditEvaluateAlertsData | null> => {
      try {
        const qs = new URLSearchParams();
        if (params.tenantId) qs.set('tenantId', params.tenantId);
        const url = `/audit-service/audit/analytics/alerts/evaluate${qs.size > 0 ? `?${qs.toString()}` : ''}`;
        const raw = await apiClient.post<unknown>(url, {});
        if (!raw) return null;
        const data = unwrapApiResponse(raw) as Record<string, unknown>;
        return {
          evaluatedAt:       (data['evaluatedAt']       as string) ?? '',
          effectiveTenantId: (data['effectiveTenantId'] as string | null) ?? null,
          anomaliesDetected: (data['anomaliesDetected'] as number) ?? 0,
          alertsCreated:     (data['alertsCreated']     as number) ?? 0,
          alertsRefreshed:   (data['alertsRefreshed']   as number) ?? 0,
          alertsSuppressed:  (data['alertsSuppressed']  as number) ?? 0,
          activeAlerts:      Array.isArray(data['activeAlerts']) ? (data['activeAlerts'] as AuditAlertItem[]) : [],
        };
      } catch {
        return null;
      }
    },

    /**
     * POST /audit-service/audit/analytics/alerts/{id}/acknowledge
     */
    acknowledge: async (alertId: string): Promise<boolean> => {
      try {
        await apiClient.post<unknown>(`/audit-service/audit/analytics/alerts/${alertId}/acknowledge`, {});
        return true;
      } catch {
        return false;
      }
    },

    /**
     * POST /audit-service/audit/analytics/alerts/{id}/resolve
     */
    resolve: async (alertId: string): Promise<boolean> => {
      try {
        await apiClient.post<unknown>(`/audit-service/audit/analytics/alerts/${alertId}/resolve`, {});
        return true;
      } catch {
        return false;
      }
    },
  },

  // ── SynqAudit — Exports ───────────────────────────────────────────────────

  auditExports: {
    /**
     * POST /audit-service/audit/exports
     *
     * Submits an asynchronous export job. Returns the export status object.
     * Cache: no-store (mutations)
     */
    create: async (params: {
      format:                 'Json' | 'Csv' | 'Ndjson';
      tenantId?:              string;
      eventType?:             string;
      category?:              string;
      severity?:              string;
      correlationId?:         string;
      dateFrom?:              string;
      dateTo?:                string;
      includeStateSnapshots?: boolean;
      includeTags?:           boolean;
    }): Promise<AuditExport> => {
      const raw = await apiClient.post<unknown>(
        '/audit-service/audit/exports',
        { ...params },
      );
      return mapAuditExport(raw);
    },

    /**
     * GET /audit-service/audit/exports/{exportId}
     *
     * Polls the status of a previously submitted export job.
     * Cache: 5 s  Tag: cc:audit-exports
     */
    getById: async (exportId: string): Promise<AuditExport | null> => {
      try {
        const raw = await apiClient.get<unknown>(
          `/audit-service/audit/exports/${encodeURIComponent(exportId)}`,
          5,
          [CACHE_TAGS.auditExports],
        );
        return mapAuditExport(raw);
      } catch {
        return null;
      }
    },
  },

  // ── SynqAudit — Integrity ─────────────────────────────────────────────────

  auditIntegrity: {
    /**
     * GET /audit-service/audit/integrity/checkpoints
     *
     * Lists persisted integrity hash checkpoints.
     * Cache: 30 s  Tag: cc:audit-integrity
     */
    list: async (): Promise<IntegrityCheckpoint[]> => {
      const raw = await apiClient.get<unknown>(
        '/audit-service/audit/integrity/checkpoints',
        30,
        [CACHE_TAGS.auditIntegrity],
      );
      return unwrapApiResponseList(raw).map(mapIntegrityCheckpoint);
    },

    /**
     * POST /audit-service/audit/integrity/checkpoints/generate
     *
     * Generates a new integrity checkpoint on demand.
     */
    generate: async (params: {
      checkpointType?:    string;
      fromRecordedAtUtc?: string;
      toRecordedAtUtc?:   string;
    } = {}): Promise<IntegrityCheckpoint> => {
      const raw = await apiClient.post<unknown>(
        '/audit-service/audit/integrity/checkpoints/generate',
        params,
      );
      return mapIntegrityCheckpoint(raw);
    },
  },

  // ── SynqAudit — Legal Holds ───────────────────────────────────────────────

  auditLegalHolds: {
    /**
     * GET /audit-service/audit/legal-holds/record/{auditId}
     *
     * Lists all legal holds for a specific audit record.
     * Cache: 10 s  Tag: cc:audit-legal-holds
     */
    listForRecord: async (auditId: string): Promise<LegalHold[]> => {
      const raw = await apiClient.get<unknown>(
        `/audit-service/audit/legal-holds/record/${encodeURIComponent(auditId)}`,
        10,
        [CACHE_TAGS.auditLegalHolds],
      );
      return unwrapApiResponseList(raw).map(mapLegalHold);
    },

    /**
     * POST /audit-service/audit/legal-holds/{auditId}
     *
     * Places a legal hold on an audit record.
     */
    create: async (auditId: string, params: {
      legalAuthority: string;
      notes?:         string;
    }): Promise<LegalHold> => {
      const raw = await apiClient.post<unknown>(
        `/audit-service/audit/legal-holds/${encodeURIComponent(auditId)}`,
        params,
      );
      return mapLegalHold(raw);
    },

    /**
     * POST /audit-service/audit/legal-holds/{holdId}/release
     *
     * Releases an active legal hold.
     */
    release: async (holdId: string): Promise<LegalHold> => {
      const raw = await apiClient.post<unknown>(
        `/audit-service/audit/legal-holds/${encodeURIComponent(holdId)}/release`,
        {},
      );
      return mapLegalHold(raw);
    },
  },

  // ── Platform Settings ─────────────────────────────────────────────────────

  settings: {
    /**
     * GET /identity/api/admin/settings
     *
     * Returns all platform configuration settings.
     * Response is normalised via mapSetting.
     *
     * Cache: 300 s  Tag: cc:settings
     *   Settings rarely change; 5 min prevents re-fetching on every
     *   admin page render. On-demand invalidated after settings.update.
     *
     * TODO: integrate with Identity service settings endpoint
     * TODO: add Redis or edge caching
     */
    list: async (): Promise<PlatformSetting[]> => {
      const raw = await apiClient.get<unknown>(
        '/identity/api/admin/settings',
        300,
        [CACHE_TAGS.settings],
      );
      if (Array.isArray(raw)) return raw.map(mapSetting);
      const paged = mapPagedResponse(raw, mapSetting);
      return paged.items;
    },

    /**
     * PATCH /identity/api/admin/settings/{key}
     *
     * Updates a single setting value by key.
     * Response is normalised via mapSetting.
     *
     * Revalidates: cc:settings — so the next settings.list call
     * sees the updated value without waiting for the 300 s TTL.
     *
     * TODO: integrate with Identity service settings endpoint
     * TODO: add Redis or edge caching
     */
    update: async (key: string, value: string | number | boolean): Promise<PlatformSetting> => {
      const raw = await apiClient.patch<unknown>(
        `/identity/api/admin/settings/${encodeURIComponent(key)}`,
        { value },
      );
      const result = mapSetting(raw);
      // Purge settings cache so UI shows the new value immediately
      safeRevalidateTag(CACHE_TAGS.settings);
      return result;
    },
  },

  // ── Monitoring ────────────────────────────────────────────────────────────

  monitoring: {
    /**
     * GET /platform/monitoring/summary
     *
     * Returns system health summary, integration statuses, and active alerts.
     * Response is normalised via mapMonitoring.
     *
     * Cache: 5 s  Tag: cc:monitoring
     *   Monitoring is a live feed. 5 s gives the Next.js Data Cache just
     *   enough time to coalesce concurrent requests from multiple SSR
     *   renders (request deduplication) without staling health data.
     *
     * TODO: integrate with Platform monitoring endpoint
     * TODO: add Redis or edge caching
     * TODO: add stale-while-revalidate strategy
     */
    getSummary: async (): Promise<MonitoringSummary> => {
      const raw = await apiClient.get<unknown>(
        '/platform/monitoring/summary',
        5,
        [CACHE_TAGS.monitoring],
      );
      return mapMonitoring(raw);
    },
  },

  // ── Support ───────────────────────────────────────────────────────────────

  support: {
    /**
     * GET /support/api/tickets
     *
     * Returns a paged list of support tickets via the Support service gateway route.
     * Response is normalised via mapSupportCase + mapPagedResponse.
     *
     * Cache: 10 s  Tag: cc:support
     */
    list: async (params: {
      page?:     number;
      pageSize?: number;
      search?:   string;
      status?:   string;
      priority?: string;
      tenantId?: string;
    } = {}): Promise<{ items: SupportCase[]; totalCount: number }> => {
      const qs = toQs({
        page:     params.page     ?? 1,
        pageSize: params.pageSize ?? 10,
        search:   params.search,
        status:   params.status,
        priority: params.priority,
        tenantId: params.tenantId,
      });
      const raw = await apiClient.get<unknown>(
        `/support/api/tickets${qs}`,
        10,
        [CACHE_TAGS.support],
      );
      const paged = mapPagedResponse(raw, mapSupportCase);
      return { items: paged.items, totalCount: paged.totalCount };
    },

    /**
     * GET /support/api/tickets/{id}
     *
     * Returns full SupportCaseDetail including notes and product references,
     * or null if not found. Product refs are fetched in parallel and merged.
     * If the product-refs call fails it degrades gracefully to an empty array.
     *
     * Cache: 10 s  Tag: cc:support
     */
    getById: async (id: string): Promise<SupportCaseDetail | null> => {
      try {
        const encodedId = encodeURIComponent(id);
        const [raw, rawRefs] = await Promise.all([
          apiClient.get<unknown>(
            `/support/api/tickets/${encodedId}`,
            10,
            [CACHE_TAGS.support],
          ),
          apiClient.get<unknown>(
            `/support/api/tickets/${encodedId}/product-refs`,
            10,
            [CACHE_TAGS.support],
          ).catch(() => [] as unknown[]),
        ]);
        const refsArr = Array.isArray(rawRefs) ? rawRefs : [];
        return mapSupportCaseDetail(raw, refsArr);
      } catch (err: unknown) {
        if (isNotFound(err)) return null;
        throw err;
      }
    },

    /**
     * POST /support/api/tickets
     *
     * Creates a new support ticket.
     * Response is normalised via mapSupportCaseDetail.
     *
     * Revalidates: cc:support — the new ticket appears in support.list immediately.
     */
    create: async (data: {
      title:      string;
      tenantId:   string;
      tenantName: string;
      userId?:    string;
      userName?:  string;
      category:   string;
      priority:   SupportCase['priority'];
    }): Promise<SupportCaseDetail> => {
      const raw = await apiClient.post<unknown>('/support/api/tickets', data);
      const result = mapSupportCaseDetail(raw);
      safeRevalidateTag(CACHE_TAGS.support);
      return result;
    },

    /**
     * GET /support/api/tickets/{ticketId}/comments
     *
     * Returns all comments on a support ticket. Uses no-cache since comments
     * are frequently updated.
     */
    getComments: async (ticketId: string): Promise<SupportNote[]> => {
      try {
        const raw = await apiClient.get<unknown>(
          `/support/api/tickets/${encodeURIComponent(ticketId)}/comments`,
          0,
          [CACHE_TAGS.support],
        );
        return Array.isArray(raw) ? raw.map(mapSupportNote) : [];
      } catch {
        return [];
      }
    },

    /**
     * POST /support/api/tickets/{ticketId}/comments
     *
     * Adds a comment to an existing support ticket.
     * Sends `body` (not `message`) to match CreateCommentRequest.
     * Response is normalised via mapSupportNote.
     *
     * Revalidates: cc:support — comment count and last-updated reflect immediately.
     */
    addNote: async (
      caseId: string,
      message: string,
      options?: {
        commentType?:  string;
        visibility?:   string;
        authorUserId?: string;
        authorEmail?:  string;
      },
    ): Promise<SupportNote> => {
      const body: Record<string, unknown> = {
        body:        message,
        commentType: options?.commentType ?? 'InternalNote',
        visibility:  options?.visibility  ?? 'Internal',
      };
      if (options?.authorUserId) body.authorUserId = options.authorUserId;
      if (options?.authorEmail)  body.authorEmail  = options.authorEmail;
      const raw = await apiClient.post<unknown>(
        `/support/api/tickets/${encodeURIComponent(caseId)}/comments`,
        body,
      );
      const result = mapSupportNote(raw);
      safeRevalidateTag(CACHE_TAGS.support);
      return result;
    },

    /**
     * PUT /support/api/tickets/{ticketId}/assignment
     *
     * Assigns the ticket to a specific user and/or queue, or clears the assignment.
     * Returns the updated SupportCase.
     *
     * Revalidates: cc:support — assignee reflects immediately in list and detail.
     */
    assignTicket: async (
      ticketId: string,
      opts: {
        assignedUserId?:  string;
        assignedQueueId?: string;
        clearAssignment?: boolean;
      },
    ): Promise<SupportCase> => {
      const raw = await apiClient.put<unknown>(
        `/support/api/tickets/${encodeURIComponent(ticketId)}/assignment`,
        {
          assignedUserId:  opts.assignedUserId  ?? null,
          assignedQueueId: opts.assignedQueueId ?? null,
          clearAssignment: opts.clearAssignment ?? false,
        },
      );
      const result = mapSupportCase(raw);
      safeRevalidateTag(CACHE_TAGS.support);
      return result;
    },

    /**
     * PUT /support/api/tickets/{ticketId}
     *
     * Updates the status of a support ticket.
     * Maps the CC SupportCaseStatus back to the backend TicketStatus enum:
     *   Open          → Open
     *   Investigating → InProgress
     *   Resolved      → Resolved
     *   Closed        → Closed
     *
     * Revalidates: cc:support — new status visible in list and detail immediately.
     */
    updateStatus: async (caseId: string, status: SupportCaseStatus): Promise<SupportCase> => {
      const backendStatusMap: Record<SupportCaseStatus, string> = {
        Open:          'Open',
        Investigating: 'InProgress',
        Resolved:      'Resolved',
        Closed:        'Closed',
      };
      const raw = await apiClient.put<unknown>(
        `/support/api/tickets/${encodeURIComponent(caseId)}`,
        { status: backendStatusMap[status] ?? status },
      );
      const result = mapSupportCase(raw);
      safeRevalidateTag(CACHE_TAGS.support);
      return result;
    },

    /**
     * GET /support/api/tickets/{ticketId}/attachments
     *
     * Returns all attachments for the given ticket, ordered by upload time.
     * Cache: 0 s (real-time, attachments change immediately after upload).
     */
    listAttachments: async (ticketId: string): Promise<TicketAttachmentItem[]> => {
      try {
        const raw = await apiClient.get<unknown>(
          `/support/api/tickets/${encodeURIComponent(ticketId)}/attachments`,
          0,
          [CACHE_TAGS.support],
        );
        return Array.isArray(raw) ? raw.map(mapTicketAttachment) : [];
      } catch {
        return [];
      }
    },
  },

  // ── Organization Types (Phase E) ────────────────────────────────────────────
  /**
   * GET /identity/api/admin/organization-types
   *
   * Returns all active OrganizationType catalog entries.
   * Cache tag: cc:org-types, TTL: 300 s (near-static reference data).
   */
  organizationTypes: {
    list: async (): Promise<OrganizationTypeItem[]> => {
      const raw = await apiClient.get<unknown>(
        '/identity/api/admin/organization-types',
        300,
        [CACHE_TAGS.orgTypes],
      );
      return Array.isArray(raw) ? raw.map(mapOrganizationTypeItem) : [];
    },

    getById: async (id: string): Promise<OrganizationTypeItem | null> => {
      try {
        const raw = await apiClient.get<unknown>(
          `/identity/api/admin/organization-types/${encodeURIComponent(id)}`,
          300,
          [CACHE_TAGS.orgTypes],
        );
        return mapOrganizationTypeItem(raw);
      } catch (err) {
        if (isNotFound(err)) return null;
        throw err;
      }
    },
  },

  // ── Relationship Types (Phase E) ─────────────────────────────────────────────
  /**
   * GET /identity/api/admin/relationship-types
   *
   * Returns all active RelationshipType catalog entries.
   * Cache tag: cc:rel-types, TTL: 300 s (near-static reference data).
   */
  relationshipTypes: {
    list: async (): Promise<RelationshipTypeItem[]> => {
      const raw = await apiClient.get<unknown>(
        '/identity/api/admin/relationship-types',
        300,
        [CACHE_TAGS.relTypes],
      );
      return Array.isArray(raw) ? raw.map(mapRelationshipTypeItem) : [];
    },

    getById: async (id: string): Promise<RelationshipTypeItem | null> => {
      try {
        const raw = await apiClient.get<unknown>(
          `/identity/api/admin/relationship-types/${encodeURIComponent(id)}`,
          300,
          [CACHE_TAGS.relTypes],
        );
        return mapRelationshipTypeItem(raw);
      } catch (err) {
        if (isNotFound(err)) return null;
        throw err;
      }
    },
  },

  // ── Organization Relationships (Phase E) ─────────────────────────────────────
  /**
   * GET /identity/api/admin/organization-relationships
   *
   * Returns all OrganizationRelationship records (optionally filtered).
   * Cache tag: cc:org-relationships, TTL: 60 s.
   */
  organizationRelationships: {
    list: async (params?: {
      sourceOrgId?:       string;
      targetOrgId?:       string;
      relationshipTypeId?: string;
      activeOnly?:        boolean;
      page?:              number;
      pageSize?:          number;
    }): Promise<PagedResponse<OrgRelationship>> => {
      const qs  = toQs(params ?? {});
      const raw = await apiClient.get<unknown>(
        `/identity/api/admin/organization-relationships${qs}`,
        60,
        [CACHE_TAGS.orgRelationships],
      );
      return mapPagedResponse(raw, mapOrgRelationship);
    },

    getById: async (id: string): Promise<OrgRelationship | null> => {
      try {
        const raw = await apiClient.get<unknown>(
          `/identity/api/admin/organization-relationships/${encodeURIComponent(id)}`,
          60,
          [CACHE_TAGS.orgRelationships],
        );
        return mapOrgRelationship(raw);
      } catch (err) {
        if (isNotFound(err)) return null;
        throw err;
      }
    },
  },

  // ── Product–OrgType Rules (Phase E) ──────────────────────────────────────────
  /**
   * GET /identity/api/admin/product-org-type-rules
   *
   * Returns all ProductOrganizationTypeRule entries.
   * Cache tag: cc:product-org-type-rules, TTL: 300 s.
   */
  productOrgTypeRules: {
    list: async (params?: {
      productId?:          string;
      organizationTypeId?: string;
      activeOnly?:         boolean;
    }): Promise<ProductOrgTypeRule[]> => {
      const qs  = toQs(params ?? {});
      const raw = await apiClient.get<unknown>(
        `/identity/api/admin/product-org-type-rules${qs}`,
        300,
        [CACHE_TAGS.productOrgTypeRules],
      );
      return Array.isArray(raw) ? raw.map(mapProductOrgTypeRule) : [];
    },
  },

  // ── Product–RelType Rules (Phase E) ──────────────────────────────────────────
  /**
   * GET /identity/api/admin/product-rel-type-rules
   *
   * Returns all ProductRelationshipTypeRule entries.
   * Cache tag: cc:product-rel-type-rules, TTL: 300 s.
   */
  productRelTypeRules: {
    list: async (params?: {
      productId?:         string;
      relationshipTypeId?: string;
      activeOnly?:        boolean;
    }): Promise<ProductRelTypeRule[]> => {
      const qs  = toQs(params ?? {});
      const raw = await apiClient.get<unknown>(
        `/identity/api/admin/product-rel-type-rules${qs}`,
        300,
        [CACHE_TAGS.productRelTypeRules],
      );
      return Array.isArray(raw) ? raw.map(mapProductRelTypeRule) : [];
    },
  },

  // ── Legacy Coverage (Phase G) ──────────────────────────────────────────────
  /**
   * GET /identity/api/admin/legacy-coverage
   *
   * Returns a point-in-time snapshot of eligibility-rule migration coverage.
   * Phase G: roleAssignments now reflects the SRA-only (retired dual-write) shape.
   *
   * Short TTL (10 s) — diagnostic/admin page, not a hot-path.
   * Cache tag: cc:legacy-coverage.
   */
  legacyCoverage: {
    get: async (): Promise<LegacyCoverageReport> => {
      const raw = await apiClient.get<unknown>(
        `/identity/api/admin/legacy-coverage`,
        10,
        [CACHE_TAGS.legacyCoverage],
      );
      return mapLegacyCoverageReport(raw);
    },
  },

  // ── Platform Readiness (Phase 8) ──────────────────────────────────────────
  /**
   * GET /identity/api/admin/platform-readiness
   *
   * Returns a cross-domain readiness summary covering:
   *   • Phase G completion (UserRoles retired, SRA sole source)
   *   • OrgType consistency (OrganizationTypeId FK coverage)
   *   • ProductRole eligibility coverage (OrgTypeRule %)
   *   • Organization relationship statistics
   *
   * Short TTL (30 s) — diagnostic dashboard endpoint.
   * Cache tag: cc:platform-readiness.
   */
  platformReadiness: {
    get: async (): Promise<PlatformReadinessSummary> => {
      const raw = await apiClient.get<unknown>(
        `/identity/api/admin/platform-readiness`,
        30,
        [CACHE_TAGS.platformReadiness],
      );
      return mapPlatformReadiness(raw);
    },
  },

  // ── CareConnect Integrity ────────────────────────────────────────────────────
  /**
   * GET /careconnect/api/admin/integrity
   *
   * Returns operational integrity counters for CareConnect entities.
   * The backend never throws — query failures produce -1 for that counter.
   *
   * Cache: 10 s  Tag: cc:careconnect-integrity
   *   Short TTL — integrity issues should surface quickly in the admin dashboard.
   */
  careConnectIntegrity: {
    get: async (): Promise<CareConnectIntegrityReport> => {
      const raw = await apiClient.get<unknown>(
        '/careconnect/api/admin/integrity',
        10,
        [CACHE_TAGS.ccIntegrity],
      );
      return mapCareConnectIntegrity(raw);
    },
  },

  // ── Scoped Role Assignments (per-user) ───────────────────────────────────────
  /**
   * GET /identity/api/admin/users/{id}/scoped-roles
   *
   * Returns all active ScopedRoleAssignments for a specific user.
   * There is no global list endpoint — scoped roles are always user-scoped.
   */
  scopedRoles: {
    getByUser: async (userId: string): Promise<ScopedRoleAssignment[]> => {
      const raw = await apiClient.get<unknown>(
        `/identity/api/admin/users/${encodeURIComponent(userId)}/scoped-roles`,
        30,
        [CACHE_TAGS.users],
      );
      return Array.isArray(raw) ? raw.map(mapScopedRoleAssignment) : [];
    },
  },

  // ── SynqAudit — Event Ingest ──────────────────────────────────────────────

  auditIngest: {
    /**
     * POST /audit-service/audit/ingest
     *
     * Emits a canonical audit event directly to the Platform Audit Event Service.
     * Used by server-side actions (e.g. impersonation) that run outside the
     * Identity service and cannot use IAuditEventClient directly.
     *
     * Fire-and-observe: callers should not await the return value if they want
     * to avoid gating user-facing operations on the audit pipeline.
     */
    emit: async (payload: AuditIngestPayload): Promise<void> => {
      await apiClient.post<unknown>('/audit-service/audit/ingest', payload);
    },
  },

  // ── Access Groups (LS-COR-AUT-005) ───────────────────────────────────────

  accessGroups: {
    list: async (tenantId: string): Promise<AccessGroupSummary[]> => {
      const raw = await apiClient.get<unknown>(
        `/identity/api/tenants/${encodeURIComponent(tenantId)}/groups`,
        30,
        [CACHE_TAGS.accessGroups],
      );
      const arr = Array.isArray(raw) ? raw : [];
      return arr.map(mapAccessGroupSummary);
    },

    getById: async (tenantId: string, groupId: string): Promise<AccessGroupSummary | null> => {
      try {
        const raw = await apiClient.get<unknown>(
          `/identity/api/tenants/${encodeURIComponent(tenantId)}/groups/${encodeURIComponent(groupId)}`,
          30,
          [CACHE_TAGS.accessGroups],
        );
        return mapAccessGroupSummary(raw);
      } catch (err: unknown) {
        if (isNotFound(err)) return null;
        throw err;
      }
    },

    create: async (tenantId: string, body: {
      name:            string;
      description?:    string;
      scopeType?:      string;
      productCode?:    string;
      organizationId?: string;
    }): Promise<AccessGroupSummary> => {
      const raw = await apiClient.post<unknown>(
        `/identity/api/tenants/${encodeURIComponent(tenantId)}/groups`,
        body,
      );
      safeRevalidateTag(CACHE_TAGS.accessGroups);
      safeRevalidateTag(CACHE_TAGS.users);
      return mapAccessGroupSummary(raw);
    },

    update: async (tenantId: string, groupId: string, body: {
      name:         string;
      description?: string;
    }): Promise<AccessGroupSummary> => {
      const raw = await apiClient.patch<unknown>(
        `/identity/api/tenants/${encodeURIComponent(tenantId)}/groups/${encodeURIComponent(groupId)}`,
        body,
      );
      safeRevalidateTag(CACHE_TAGS.accessGroups);
      return mapAccessGroupSummary(raw);
    },

    archive: async (tenantId: string, groupId: string): Promise<void> => {
      await apiClient.del<unknown>(
        `/identity/api/tenants/${encodeURIComponent(tenantId)}/groups/${encodeURIComponent(groupId)}`,
      );
      safeRevalidateTag(CACHE_TAGS.accessGroups);
      safeRevalidateTag(CACHE_TAGS.users);
    },

    listMembers: async (tenantId: string, groupId: string): Promise<AccessGroupMember[]> => {
      const raw = await apiClient.get<unknown>(
        `/identity/api/tenants/${encodeURIComponent(tenantId)}/groups/${encodeURIComponent(groupId)}/members`,
        30,
        [CACHE_TAGS.accessGroups],
      );
      const arr = Array.isArray(raw) ? raw : [];
      return arr.map(mapAccessGroupMember);
    },

    addMember: async (tenantId: string, groupId: string, userId: string): Promise<void> => {
      await apiClient.post<unknown>(
        `/identity/api/tenants/${encodeURIComponent(tenantId)}/groups/${encodeURIComponent(groupId)}/members`,
        { userId },
      );
      safeRevalidateTag(CACHE_TAGS.accessGroups);
      safeRevalidateTag(CACHE_TAGS.users);
    },

    removeMember: async (tenantId: string, groupId: string, userId: string): Promise<void> => {
      await apiClient.del<unknown>(
        `/identity/api/tenants/${encodeURIComponent(tenantId)}/groups/${encodeURIComponent(groupId)}/members/${encodeURIComponent(userId)}`,
      );
      safeRevalidateTag(CACHE_TAGS.accessGroups);
      safeRevalidateTag(CACHE_TAGS.users);
    },

    listProducts: async (tenantId: string, groupId: string): Promise<GroupProductAccess[]> => {
      const raw = await apiClient.get<unknown>(
        `/identity/api/tenants/${encodeURIComponent(tenantId)}/groups/${encodeURIComponent(groupId)}/products`,
        30,
        [CACHE_TAGS.accessGroups],
      );
      const arr = Array.isArray(raw) ? raw : [];
      return arr.map(mapGroupProductAccess);
    },

    grantProduct: async (tenantId: string, groupId: string, productCode: string): Promise<void> => {
      await apiClient.put<unknown>(
        `/identity/api/tenants/${encodeURIComponent(tenantId)}/groups/${encodeURIComponent(groupId)}/products/${encodeURIComponent(productCode)}`,
        {},
      );
      safeRevalidateTag(CACHE_TAGS.accessGroups);
      safeRevalidateTag(CACHE_TAGS.users);
    },

    revokeProduct: async (tenantId: string, groupId: string, productCode: string): Promise<void> => {
      await apiClient.del<unknown>(
        `/identity/api/tenants/${encodeURIComponent(tenantId)}/groups/${encodeURIComponent(groupId)}/products/${encodeURIComponent(productCode)}`,
      );
      safeRevalidateTag(CACHE_TAGS.accessGroups);
      safeRevalidateTag(CACHE_TAGS.users);
    },

    listRoles: async (tenantId: string, groupId: string): Promise<GroupRoleAssignment[]> => {
      const raw = await apiClient.get<unknown>(
        `/identity/api/tenants/${encodeURIComponent(tenantId)}/groups/${encodeURIComponent(groupId)}/roles`,
        30,
        [CACHE_TAGS.accessGroups],
      );
      const arr = Array.isArray(raw) ? raw : [];
      return arr.map(mapGroupRoleAssignment);
    },

    assignRole: async (tenantId: string, groupId: string, body: {
      roleCode:        string;
      productCode?:    string;
      organizationId?: string;
    }): Promise<void> => {
      await apiClient.post<unknown>(
        `/identity/api/tenants/${encodeURIComponent(tenantId)}/groups/${encodeURIComponent(groupId)}/roles`,
        body,
      );
      safeRevalidateTag(CACHE_TAGS.accessGroups);
      safeRevalidateTag(CACHE_TAGS.users);
    },

    removeRole: async (tenantId: string, groupId: string, assignmentId: string): Promise<void> => {
      await apiClient.del<unknown>(
        `/identity/api/tenants/${encodeURIComponent(tenantId)}/groups/${encodeURIComponent(groupId)}/roles/${encodeURIComponent(assignmentId)}`,
      );
      safeRevalidateTag(CACHE_TAGS.accessGroups);
      safeRevalidateTag(CACHE_TAGS.users);
    },

    listUserGroups: async (tenantId: string, userId: string): Promise<AccessGroupMember[]> => {
      const raw = await apiClient.get<unknown>(
        `/identity/api/tenants/${encodeURIComponent(tenantId)}/users/${encodeURIComponent(userId)}/groups`,
        30,
        [CACHE_TAGS.accessGroups],
      );
      const arr = Array.isArray(raw) ? raw : [];
      return arr.map(mapAccessGroupMember);
    },
  },

  // ── Tenant Admin Users (PUM-B07) ─────────────────────────────────────────
  //
  // Purpose-built tenant-user management using the PUM-B03 endpoints.
  // These are separate from `users.*` which call /identity/api/admin/users.
  // All PUM-B03 endpoints live under /identity/api/admin/tenants/{tenantId}/users.
  //
  // No cache — mutations must always see fresh data.

  tenantAdminUsers: {

    /**
     * GET /identity/api/admin/tenants/{tenantId}/users
     *
     * Returns users whose primary tenant is tenantId, including their active
     * tenant-scoped role assignments inline.  PlatformInternal users are
     * excluded client-side after mapping.
     */
    list: async (
      tenantId: string,
      params: { page?: number; pageSize?: number; search?: string } = {},
    ): Promise<{ items: TenantUserSummary[]; totalCount: number; page: number; pageSize: number }> => {
      const qs = toQs({
        page:     params.page     ?? 1,
        pageSize: params.pageSize ?? 20,
        search:   params.search,
      });
      const raw = await apiClient.get<unknown>(
        `/identity/api/admin/tenants/${encodeURIComponent(tenantId)}/users${qs}`,
        0,
        [],
      );
      const r   = raw as Record<string, unknown>;
      const arr = Array.isArray(r['items']) ? (r['items'] as unknown[]) : [];

      function mapRole(rx: unknown): TenantUserRoleAssignment {
        const rv = rx as Record<string, unknown>;
        return {
          assignmentId:  String(rv['assignmentId']  ?? ''),
          roleId:        String(rv['roleId']         ?? ''),
          roleName:      String(rv['roleName']       ?? ''),
          roleScope:     String(rv['roleScope']      ?? ''),
          assignedAtUtc: String(rv['assignedAtUtc']  ?? ''),
        };
      }

      function mapUser(u: unknown): TenantUserSummary {
        const uv = u as Record<string, unknown>;
        const roles = Array.isArray(uv['roles'])
          ? (uv['roles'] as unknown[]).map(mapRole)
          : [];
        return {
          userId:        String(uv['userId']       ?? ''),
          email:         String(uv['email']        ?? ''),
          firstName:     String(uv['firstName']    ?? ''),
          lastName:      String(uv['lastName']     ?? ''),
          displayName:   String(uv['displayName']  ?? ''),
          userType:      String(uv['userType']     ?? ''),
          isActive:      uv['isActive'] === true,
          tenantId:      String(uv['tenantId']     ?? ''),
          roles,
          createdAtUtc:  String(uv['createdAtUtc']  ?? ''),
          updatedAtUtc:  String(uv['updatedAtUtc']  ?? ''),
          lastLoginAtUtc: uv['lastLoginAtUtc'] != null
            ? String(uv['lastLoginAtUtc'])
            : undefined,
        };
      }

      const items = arr.map(mapUser).filter(u => u.userType !== 'PlatformInternal');
      return {
        items,
        totalCount: typeof r['totalCount'] === 'number' ? r['totalCount'] : items.length,
        page:       typeof r['page']       === 'number' ? r['page']       : (params.page ?? 1),
        pageSize:   typeof r['pageSize']   === 'number' ? r['pageSize']   : (params.pageSize ?? 20),
      };
    },

    /**
     * POST /identity/api/admin/tenants/{tenantId}/users
     *
     * Verifies a user belongs to the given tenant and optionally assigns a
     * Tenant-scoped role. Returns 409 if the user is in a different tenant.
     */
    addToTenant: async (
      tenantId: string,
      body: { userId: string; roleKey?: string },
    ): Promise<unknown> => {
      return apiClient.post(
        `/identity/api/admin/tenants/${encodeURIComponent(tenantId)}/users`,
        body,
      );
    },

    /**
     * DELETE /identity/api/admin/tenants/{tenantId}/users/{userId}
     *
     * Soft-removes user from tenant by deactivating all active tenant-scoped
     * role assignments.  Does NOT delete the global user account.
     */
    removeFromTenant: async (tenantId: string, userId: string): Promise<void> => {
      await apiClient.del(
        `/identity/api/admin/tenants/${encodeURIComponent(tenantId)}/users/${encodeURIComponent(userId)}`,
      );
    },

    /**
     * POST /identity/api/admin/tenants/{tenantId}/users/{userId}/roles
     *
     * Assigns a Tenant-scoped role to a user.  Idempotent.
     */
    assignRole: async (
      tenantId: string,
      userId:   string,
      body:     { roleId?: string; roleKey?: string },
    ): Promise<unknown> => {
      return apiClient.post(
        `/identity/api/admin/tenants/${encodeURIComponent(tenantId)}/users/${encodeURIComponent(userId)}/roles`,
        body,
      );
    },

    /**
     * DELETE /identity/api/admin/tenants/{tenantId}/users/{userId}/roles/{assignmentId}
     *
     * Soft-deactivates a specific tenant-scoped ScopedRoleAssignment.
     */
    removeRole: async (
      tenantId:     string,
      userId:       string,
      assignmentId: string,
    ): Promise<void> => {
      await apiClient.del(
        `/identity/api/admin/tenants/${encodeURIComponent(tenantId)}/users/${encodeURIComponent(userId)}/roles/${encodeURIComponent(assignmentId)}`,
      );
    },
  },

  // ── Workflows (E9.1) ────────────────────────────────────────────────────
  //
  // Read-only cross-product workflow operations list. Backed by Flow's
  // `GET /api/v1/admin/workflow-instances` admin endpoint, which bypasses
  // the per-tenant query filter when the caller is a PlatformAdmin and
  // scopes TenantAdmin callers to their own tenant on the server side.
  //
  // Cache: 10 s  Tag: cc:workflows
  //   Workflow rows change frequently in production; a short TTL keeps the
  //   page near-real-time without pummelling Flow on every navigation.

  workflows: {
    list: async (params: {
      page?:       number;
      pageSize?:   number;
      productKey?: string;
      status?:     string;
      tenantId?:   string;
      search?:     string;
    } = {}): Promise<PagedResponse<WorkflowInstanceListItem>> => {
      const qs = toQs({
        page:       params.page     ?? 1,
        pageSize:   params.pageSize ?? 20,
        productKey: params.productKey,
        status:     params.status,
        tenantId:   params.tenantId,
        search:     params.search,
      });
      const raw = await apiClient.get<{
        items?:      unknown[];
        totalCount?: number;
        page?:       number;
        pageSize?:   number;
      }>(
        `/flow/api/v1/admin/workflow-instances${qs}`,
        10,
        [CACHE_TAGS.workflows],
      );
      return {
        items:     (raw?.items ?? []).map(mapWorkflowInstanceListItem),
        totalCount: raw?.totalCount ?? 0,
        page:       raw?.page       ?? (params.page     ?? 1),
        pageSize:   raw?.pageSize   ?? (params.pageSize ?? 20),
      };
    },

    /**
     * E9.3 — list workflow instances that match one or more
     * exception/stuck classifications. Calls the same admin endpoint as
     * `list()` with `exceptionOnly=true` so the UI can render a focused
     * triage table without duplicating the underlying handler. Returns
     * the per-row `classifications` and the server-evaluated stale
     * threshold (echoed on the response) so labels stay in sync.
     */
    listExceptions: async (params: {
      page?:                number;
      pageSize?:            number;
      productKey?:          string;
      status?:              string;
      tenantId?:            string;
      search?:              string;
      classification?:      string;
      staleThresholdHours?: number;
    }): Promise<WorkflowInstancePagedResponse> => {
      const qs = toQs({
        page:                params.page,
        pageSize:            params.pageSize,
        productKey:          params.productKey,
        status:              params.status,
        tenantId:            params.tenantId,
        search:              params.search,
        exceptionOnly:       true,
        classification:      params.classification,
        staleThresholdHours: params.staleThresholdHours,
      });
      const raw = await apiClient.get<{
        items?:               unknown[];
        totalCount?:          number;
        page?:                number;
        pageSize?:            number;
        staleThresholdHours?: number;
      }>(
        `/flow/api/v1/admin/workflow-instances${qs}`,
        10,
        [CACHE_TAGS.workflows],
      );
      return {
        items:               (raw?.items ?? []).map(mapWorkflowInstanceListItem),
        totalCount:          raw?.totalCount ?? 0,
        page:                raw?.page                ?? (params.page     ?? 1),
        pageSize:            raw?.pageSize            ?? (params.pageSize ?? 20),
        staleThresholdHours: raw?.staleThresholdHours ?? (params.staleThresholdHours ?? 24),
      };
    },

    /**
     * E10.1 — perform an admin action (retry / force-complete / cancel)
     * on a workflow instance. Calls the matching Flow admin endpoint
     * and returns the structured result. The caller is expected to
     * have already validated the operator's PlatformAdmin role at the
     * BFF route boundary (`requirePlatformAdmin`); this method is the
     * thin server-side proxy that re-uses the JWT-bearer api client so
     * the existing audit/auth path on Flow stays authoritative.
     *
     * Throws on non-2xx responses; the BFF route translates the throw
     * into a JSON error for the drawer.
     */
    adminAction: async (
      id:     string,
      action: WorkflowAdminAction,
      reason: string,
    ): Promise<WorkflowAdminActionResult> => {
      const raw = await apiClient.post<unknown>(
        `/flow/api/v1/admin/workflow-instances/${encodeURIComponent(id)}/${action}`,
        { reason },
      );
      const r = (raw ?? {}) as Record<string, unknown>;
      return {
        workflowInstanceId: String(r.workflowInstanceId ?? id),
        action:             String(r.action ?? action),
        previousStatus:     String(r.previousStatus ?? ''),
        newStatus:          String(r.newStatus ?? ''),
        performedBy:        String(r.performedBy ?? ''),
        timestamp:          String(r.timestamp ?? ''),
        reason:             String(r.reason ?? reason),
      };
    },

    /**
     * E9.2 — fetch a single workflow instance by id for the read-only
     * detail drawer. Returns null on 404 (forbidden / unknown / scoped
     * out) so the drawer can render a compact "not available" state
     * instead of breaking the parent list page.
     */
    getById: async (id: string): Promise<WorkflowInstanceDetail | null> => {
      try {
        const raw = await apiClient.get<unknown>(
          `/flow/api/v1/admin/workflow-instances/${encodeURIComponent(id)}`,
          10,
          [CACHE_TAGS.workflows],
        );
        return mapWorkflowInstanceDetail(raw);
      } catch (err) {
        if (isNotFound(err)) return null;
        throw err;
      }
    },

    /**
     * E13.1 / E13.2 / E13.3 — fetch the normalized audit timeline for a
     * single workflow instance. Backed by Flow's
     * `GET /api/v1/admin/workflow-instances/{id}/timeline`. Returns
     * null on 404 so the drawer can render an empty / not-available
     * state without breaking the parent list page.
     *
     * Cache: 5 s on `cc:workflows` — matches `getById` lifecycle, so a
     *   single `revalidateTag('cc:workflows')` after an admin action
     *   refreshes both the list row and any open timeline panel.
     */
    getTimeline: async (id: string): Promise<WorkflowTimelineResponse | null> => {
      try {
        const raw = await apiClient.get<unknown>(
          `/flow/api/v1/admin/workflow-instances/${encodeURIComponent(id)}/timeline`,
          5,
          [CACHE_TAGS.workflows],
        );
        return mapWorkflowTimelineResponse(raw, id);
      } catch (err) {
        if (isNotFound(err)) return null;
        throw err;
      }
    },
  },

  outbox: {
    /**
     * E17 — list outbox items with optional filters. Calls the Flow admin
     * endpoint and returns a normalised response.
     */
    list: async (params: {
      page?:               number;
      pageSize?:           number;
      status?:             string;
      eventType?:          string;
      tenantId?:           string;
      workflowInstanceId?: string;
      search?:             string;
    } = {}): Promise<import('@/types/control-center').OutboxListResponse> => {
      const qs = toQs({
        page:               params.page     ?? 1,
        pageSize:           params.pageSize ?? 20,
        status:             params.status,
        eventType:          params.eventType,
        tenantId:           params.tenantId,
        workflowInstanceId: params.workflowInstanceId,
        search:             params.search,
      });
      const raw = await apiClient.get<{
        items?:      unknown[];
        totalCount?: number;
        page?:       number;
        pageSize?:   number;
      }>(
        `/flow/api/v1/admin/outbox${qs}`,
        0,
        ['cc:outbox'],
      );
      return {
        items:      (raw?.items ?? []).map(mapOutboxListItem),
        totalCount: raw?.totalCount ?? 0,
        page:       raw?.page       ?? (params.page     ?? 1),
        pageSize:   raw?.pageSize   ?? (params.pageSize ?? 20),
      };
    },

    /**
     * E17 — lightweight counts by status for the summary cards.
     */
    summary: async (): Promise<import('@/types/control-center').OutboxSummary> => {
      const raw = await apiClient.get<{
        pendingCount?:      number;
        processingCount?:   number;
        failedCount?:       number;
        deadLetteredCount?: number;
        succeededCount?:    number;
      }>(
        `/flow/api/v1/admin/outbox/summary`,
        0,
        ['cc:outbox'],
      );
      return {
        pendingCount:      raw?.pendingCount      ?? 0,
        processingCount:   raw?.processingCount   ?? 0,
        failedCount:       raw?.failedCount       ?? 0,
        deadLetteredCount: raw?.deadLetteredCount ?? 0,
        succeededCount:    raw?.succeededCount    ?? 0,
      };
    },

    /**
     * E17 — fetch full detail for a single outbox item. Returns null on 404.
     */
    getById: async (id: string): Promise<import('@/types/control-center').OutboxDetail | null> => {
      try {
        const raw = await apiClient.get<unknown>(
          `/flow/api/v1/admin/outbox/${encodeURIComponent(id)}`,
          0,
          ['cc:outbox'],
        );
        return mapOutboxDetail(raw);
      } catch (err) {
        if (isNotFound(err)) return null;
        throw err;
      }
    },

    /**
     * E17 — perform a governed manual retry on an outbox item. Throws on
     * non-2xx responses; the BFF route translates the throw into a JSON
     * error for the drawer.
     */
    retry: async (
      id:     string,
      reason: string,
    ): Promise<import('@/types/control-center').OutboxRetryResult> => {
      const raw = await apiClient.post<unknown>(
        `/flow/api/v1/admin/outbox/${encodeURIComponent(id)}/retry`,
        { reason },
      );
      const r = (raw ?? {}) as Record<string, unknown>;
      return {
        outboxId:       String(r.outboxId       ?? id),
        eventType:      String(r.eventType      ?? ''),
        previousStatus: String(r.previousStatus ?? ''),
        newStatus:      String(r.newStatus      ?? ''),
        performedBy:    String(r.performedBy    ?? ''),
        timestamp:      String(r.timestamp      ?? ''),
        reason:         String(r.reason         ?? reason),
      };
    },
  },

  simulation: {
    simulate: async (payload: {
      tenantId: string;
      userId: string;
      permissionCode: string;
      resourceContext?: Record<string, unknown>;
      requestContext?: Record<string, string>;
      draftPolicy?: {
        policyCode: string;
        name: string;
        description?: string;
        priority: number;
        effect: string;
        rules: Array<{
          field: string;
          operator: string;
          value: string;
          logicalGroup: string;
        }>;
      };
      excludePolicyIds?: string[];
    }): Promise<unknown> => {
      return apiClient.post<unknown>(
        '/identity/api/admin/authorization/simulate',
        payload,
      );
    },
  },

  // ── E19 Analytics ──────────────────────────────────────────────────────────
  //
  // Read-only reporting APIs backed by Flow's /api/v1/admin/analytics/* endpoints.
  // All endpoints respect tenant isolation on the backend; the platform summary
  // requires PlatformAdmin and uses cross-tenant aggregations.
  //
  // Cache: 30 s  Tag: cc:analytics
  // Short TTL: analytics data is operational (operators need freshness).

  analytics: {
    /**
     * GET /flow/api/v1/admin/analytics/summary
     * Unified dashboard summary: SLA + queue + workflows + assignment + outbox.
     */
    getDashboardSummary: async (
      window: 'today' | '7d' | '30d' = '7d',
    ) => {
      const qs = `?window=${encodeURIComponent(window)}`;
      return apiClient.get<import('@/types/control-center').AnalyticsDashboardSummary>(
        `/flow/api/v1/admin/analytics/summary${qs}`,
        30,
        [CACHE_TAGS.analytics],
      );
    },

    /**
     * GET /flow/api/v1/admin/analytics/sla
     * SLA performance metrics for the caller's tenant scope.
     */
    getSlaSummary: async (
      window: 'today' | '7d' | '30d' = '7d',
    ) => {
      const qs = `?window=${encodeURIComponent(window)}`;
      return apiClient.get<import('@/types/control-center').SlaSummary>(
        `/flow/api/v1/admin/analytics/sla${qs}`,
        30,
        [CACHE_TAGS.analytics],
      );
    },

    /**
     * GET /flow/api/v1/admin/analytics/queues
     * Queue backlog and workload analytics (current state, no window).
     */
    getQueueSummary: async () => {
      return apiClient.get<import('@/types/control-center').QueueSummary>(
        `/flow/api/v1/admin/analytics/queues`,
        30,
        [CACHE_TAGS.analytics],
      );
    },

    /**
     * GET /flow/api/v1/admin/analytics/workflows
     * Workflow throughput analytics.
     */
    getWorkflowThroughput: async (
      window: 'today' | '7d' | '30d' = '7d',
    ) => {
      const qs = `?window=${encodeURIComponent(window)}`;
      return apiClient.get<import('@/types/control-center').WorkflowThroughput>(
        `/flow/api/v1/admin/analytics/workflows${qs}`,
        30,
        [CACHE_TAGS.analytics],
      );
    },

    /**
     * GET /flow/api/v1/admin/analytics/assignment
     * Assignment and workload fairness analytics.
     */
    getAssignmentSummary: async (
      window: 'today' | '7d' | '30d' = '7d',
    ) => {
      const qs = `?window=${encodeURIComponent(window)}`;
      return apiClient.get<import('@/types/control-center').AssignmentSummary>(
        `/flow/api/v1/admin/analytics/assignment${qs}`,
        30,
        [CACHE_TAGS.analytics],
      );
    },

    /**
     * GET /flow/api/v1/admin/analytics/outbox
     * Outbox reliability analytics: extends E17 summary with trend data.
     */
    getOutboxAnalytics: async (
      window: 'today' | '7d' | '30d' = '7d',
    ) => {
      const qs = `?window=${encodeURIComponent(window)}`;
      return apiClient.get<import('@/types/control-center').OutboxAnalyticsSummary>(
        `/flow/api/v1/admin/analytics/outbox${qs}`,
        30,
        [CACHE_TAGS.analytics],
      );
    },

    /**
     * GET /flow/api/v1/admin/analytics/platform
     * Cross-tenant platform analytics. PlatformAdmin only.
     */
    getPlatformSummary: async (
      window: 'today' | '7d' | '30d' = '7d',
    ) => {
      const qs = `?window=${encodeURIComponent(window)}`;
      return apiClient.get<import('@/types/control-center').PlatformAnalyticsSummary>(
        `/flow/api/v1/admin/analytics/platform${qs}`,
        30,
        [CACHE_TAGS.analytics],
      );
    },
  },

};

// ── Internal helpers ──────────────────────────────────────────────────────────

/**
 * isNotFound — returns true if the error is an ApiError with status 404.
 * Used to map 404 responses to null returns on getById methods.
 */
function isNotFound(err: unknown): boolean {
  return (
    typeof err === 'object' &&
    err !== null &&
    'status' in err &&
    (err as { status: number }).status === 404
  );
}
