// ── Control Center domain types ────────────────────────────────────────────────
// These mirror the expected Identity service response shapes for admin endpoints.
// Keep in sync with Identity.Application DTOs as backend endpoints are confirmed.

// ── Tenants ───────────────────────────────────────────────────────────────────

export type TenantType   = 'LawFirm' | 'Provider' | 'Funder' | 'LienOwner' | 'Corporate' | 'Government' | 'Other';
export type TenantStatus = 'Active' | 'Inactive' | 'Suspended';
export type ProvisioningStatus = 'Pending' | 'InProgress' | 'Provisioned' | 'Verifying' | 'Active' | 'Failed';
export type ProvisioningFailureStage = 'None' | 'DnsProvisioning' | 'DnsVerification' | 'HttpVerification';

export interface TenantSummary {
  id:                 string;
  code:               string;
  displayName:        string;
  type:               TenantType;
  status:             TenantStatus;
  primaryContactName: string;
  isActive:           boolean;
  userCount:          number;
  orgCount:           number;
  createdAtUtc:       string;
  subdomain?:         string;
  provisioningStatus?: ProvisioningStatus;
}

/**
 * Full tenant record returned by GET /identity/api/admin/tenants/{id}.
 * Extends TenantSummary with enriched fields not present in the list view.
 */
export interface TenantDetail extends TenantSummary {
  email?:                     string;
  updatedAtUtc:               string;
  activeUserCount:            number;
  linkedOrgCount?:            number;
  sessionTimeoutMinutes?:     number;
  logoDocumentId?:            string;
  logoWhiteDocumentId?:       string;
  productEntitlements:        ProductEntitlementSummary[];
  lastProvisioningAttemptUtc?: string;
  provisioningFailureReason?: string;
  provisioningFailureStage?:  ProvisioningFailureStage;
  hostname?:                  string;
  verificationAttemptCount?:       number;
  lastVerificationAttemptUtc?:     string;
  nextVerificationRetryAtUtc?:     string;
  isVerificationRetryExhausted?:   boolean;
}

// ── Product Entitlements ──────────────────────────────────────────────────────

/**
 * Canonical product identifiers used across the LegalSynq platform.
 * Must match values emitted by Identity service entitlement endpoints.
 */
export type ProductCode =
  | 'SynqFund'
  | 'SynqLien'
  | 'SynqBill'
  | 'SynqRx'
  | 'SynqPayout'
  | 'CareConnect';

/** Live status of a product entitlement for a tenant. */
export type EntitlementStatus = 'Active' | 'Disabled';

/**
 * A single product entitlement for a tenant.
 * Used inside TenantDetail.productEntitlements.
 */
export interface ProductEntitlementSummary {
  productCode:   ProductCode;
  productName:   string;
  enabled:       boolean;
  status:        EntitlementStatus;
  enabledAtUtc?: string;
}

// ── Tenant Users (PUM-B07) ────────────────────────────────────────────────────

/**
 * A single tenant-scoped role assignment returned inline by
 * GET /identity/api/admin/tenants/{tenantId}/users.
 */
export interface TenantUserRoleAssignment {
  assignmentId:  string;
  roleId:        string;
  roleName:      string;
  roleScope:     string;
  assignedAtUtc: string;
}

/**
 * User record returned by GET /identity/api/admin/tenants/{tenantId}/users.
 * Richer than UserSummary — includes inline tenant-scoped role assignments.
 * PlatformInternal users are excluded client-side.
 */
export interface TenantUserSummary {
  userId:         string;
  email:          string;
  firstName:      string;
  lastName:       string;
  displayName:    string;
  userType:       string;
  isActive:       boolean;
  tenantId:       string;
  roles:          TenantUserRoleAssignment[];
  createdAtUtc:   string;
  updatedAtUtc:   string;
  lastLoginAtUtc?: string;
}

// ── Users ─────────────────────────────────────────────────────────────────────

export type UserStatus = 'Active' | 'Inactive' | 'Invited';

/**
 * User record returned by GET /identity/api/admin/users.
 * Represents a single user within a tenant as seen from the platform admin view.
 */
export interface UserSummary {
  id:              string;
  firstName:       string;
  lastName:        string;
  email:           string;
  role:            string;
  status:          UserStatus;
  tenantId:        string;
  tenantCode:      string;
  lastLoginAtUtc?: string;
  primaryOrg?:     string;
  groupCount?:     number;
  userType?:       string;
}

/**
 * Full user record returned by GET /identity/api/admin/users/{id}.
 * Extends UserSummary with audit timestamps and account state.
 *
 * tenantDisplayName is included so the detail page can link to the tenant
 * without requiring a second API call.
 */
export interface UserDetail extends UserSummary {
  tenantDisplayName: string;
  createdAtUtc:      string;
  updatedAtUtc:      string;
  isLocked?:         boolean;
  lockedAtUtc?:      string;
  lastLoginAtUtc?:   string;
  sessionVersion?:   number;
  avatarDocumentId?: string;
  phone?:            string;
  inviteSentAtUtc?:  string;
  memberships?:      OrgMembershipSummary[];
  groups?:           UserGroupSummary[];
  roles?:            UserRoleSummary[];
}

/**
 * UIX-003-03: Security summary for a user — returned by GET /security.
 */
export interface UserSecurity {
  userId:          string;
  email:           string;
  isLocked:        boolean;
  lockedAtUtc:     string | null;
  lastLoginAtUtc:  string | null;
  sessionVersion:  number;
  isActive:        boolean;
  hasPendingInvite: boolean;
  recentPasswordResets: PasswordResetSummary[];
}

export interface PasswordResetSummary {
  id:        string;
  status:    'PENDING' | 'USED' | 'EXPIRED' | 'REVOKED';
  createdAt: string;
  expiresAt: string;
  usedAt:    string | null;
}

export interface OrgSummary {
  id:          string;
  tenantId:    string;
  name:        string;
  displayName: string;
  orgType:     string;
  isActive:    boolean;
}

export interface OrgMembershipSummary {
  membershipId:   string;
  organizationId: string;
  orgName:        string;
  memberRole:     string;
  isPrimary:      boolean;
  joinedAtUtc:    string;
}

export interface UserGroupSummary {
  groupId:     string;
  groupName:   string;
  joinedAtUtc: string;
}

export interface UserRoleSummary {
  roleId:       string;
  roleName:     string;
  assignmentId: string;
}

// ── Access Groups (LS-COR-AUT-004 / LS-COR-AUT-005) ─────────────────────────

export type AccessGroupStatus = 'Active' | 'Archived';
export type AccessGroupScopeType = 'Tenant' | 'Product' | 'Organization';
export type AccessGroupMembershipStatus = 'Active' | 'Removed';
export type GroupProductAccessStatus = 'Granted' | 'Revoked';
export type GroupRoleAssignmentStatus = 'Active' | 'Removed';

export interface AccessGroupSummary {
  id:              string;
  tenantId:        string;
  name:            string;
  description?:    string;
  status:          AccessGroupStatus;
  scopeType:       AccessGroupScopeType;
  productCode?:    string;
  organizationId?: string;
  createdAtUtc:    string;
  updatedAtUtc:    string;
}

export interface AccessGroupMember {
  id:               string;
  tenantId:         string;
  groupId:          string;
  userId:           string;
  membershipStatus: AccessGroupMembershipStatus;
  addedAtUtc:       string;
  removedAtUtc?:    string;
}

export interface GroupProductAccess {
  id:           string;
  tenantId:     string;
  groupId:      string;
  productCode:  string;
  accessStatus: GroupProductAccessStatus;
  grantedAtUtc: string;
  revokedAtUtc?: string;
}

export interface GroupRoleAssignment {
  id:               string;
  tenantId:         string;
  groupId:          string;
  roleCode:         string;
  productCode?:     string;
  organizationId?:  string;
  assignmentStatus: GroupRoleAssignmentStatus;
  assignedAtUtc:    string;
  removedAtUtc?:    string;
}

// ── Permissions catalog ────────────────────────────────────────────────────────

export interface PermissionCatalogItem {
  id:          string;
  code:        string;
  name:        string;
  description?: string;
  category?:   string;
  productId:   string;
  productName: string;
  productCode: string;
  isActive:    boolean;
  updatedAtUtc?: string;
}

// ── Roles & Permissions ───────────────────────────────────────────────────────

/**
 * A single granular permission in the platform RBAC model.
 * Permissions are additive — roles are a named collection of permissions.
 */
export interface Permission {
  id:          string;
  key:         string;   // e.g. "tenants.activate"
  description: string;
}

/**
 * Role record returned by GET /identity/api/admin/roles.
 * Platform-level roles only — tenant-level roles are separate.
 */
export interface RoleSummary {
  id:              string;
  name:            string;
  description:     string;
  scope?:          string;
  isSystemRole:    boolean;
  isProductRole?:  boolean;
  productCode?:    string;
  productName?:    string;
  allowedOrgTypes?: string[];
  userCount:       number;
  capabilityCount: number;
  permissions:     string[];   // permission keys
}

export interface AssignableRole {
  id:              string;
  name:            string;
  description:     string;
  isSystemRole:    boolean;
  isProductRole:   boolean;
  productCode:     string | null;
  productName:     string | null;
  allowedOrgTypes: string[] | null;
  assignable:      boolean;
  disabledReason:  string | null;
  isAssigned:      boolean;
}

export interface AssignableRolesResponse {
  items:                 AssignableRole[];
  userOrgType:           string;
  tenantEnabledProducts: number;
}

/**
 * Full role record returned by GET /identity/api/admin/roles/{id}.
 * Extends RoleSummary with audit timestamps and resolved permission objects.
 */
export interface RoleDetail extends RoleSummary {
  createdAtUtc:        string;
  updatedAtUtc:        string;
  capabilityCount:     number;
  resolvedPermissions: Permission[];
}

// ── UIX-005: Role Capability Assignment ────────────────────────────────────────

/**
 * A capability assigned to a role.
 * Returned by GET /identity/api/admin/roles/{id}/permissions.
 * Extends PermissionCatalogItem with assignment metadata.
 */
export interface RoleCapabilityItem extends PermissionCatalogItem {
  assignedAtUtc:    string;
  assignedByUserId: string | null;
}

/**
 * A permission source — which role or group grants a capability.
 */
export interface PermissionSource {
  type: 'role' | 'group';
  name: string;
}

/**
 * An effective permission for a user — the union of capabilities across
 * all their active role assignments, with attribution sources.
 * Returned by GET /identity/api/admin/users/{id}/permissions.
 */
export interface EffectivePermission extends PermissionCatalogItem {
  sources: PermissionSource[];
}

/**
 * Result shape from GET /identity/api/admin/users/{id}/permissions.
 */
export interface EffectivePermissionsResult {
  items:      EffectivePermission[];
  totalCount: number;
  roleCount:  number;
}

// ── Access Debug (LS-COR-AUT-008) ─────────────────────────────────────────────

export interface AccessDebugProductEntry {
  productCode: string;
  source:      string;
  groupId:     string | null;
  groupName:   string | null;
}

export interface AccessDebugRoleEntry {
  roleCode:    string;
  productCode: string | null;
  source:      string;
  groupId:     string | null;
  groupName:   string | null;
}

export interface AccessDebugSystemRole {
  roleName:  string;
  scopeType: string;
}

export interface AccessDebugGroup {
  groupId:     string;
  groupName:   string;
  status:      string;
  scopeType:   string;
  productCode: string | null;
}

export interface AccessDebugEntitlement {
  productCode: string;
  status:      string;
}

export interface AccessDebugPermissionEntry {
  permissionCode: string;
  productCode:    string;
  source:         string;
  viaRoleCode?:   string;
  groupId?:       string;
  groupName?:     string;
}

export interface AccessDebugResult {
  userId:            string;
  tenantId:          string;
  accessVersion:     number;
  products:          AccessDebugProductEntry[];
  roles:             AccessDebugRoleEntry[];
  systemRoles:       AccessDebugSystemRole[];
  groups:            AccessDebugGroup[];
  entitlements:      AccessDebugEntitlement[];
  productRolesFlat:  string[];
  tenantRoles:       string[];
  permissions:       string[];
  permissionSources: AccessDebugPermissionEntry[];
}

// ── Audit Logs ────────────────────────────────────────────────────────────────

/** Who originated an audited action. */
export type ActorType = 'Admin' | 'System';

/**
 * A single audit log entry.
 * Returned by GET /identity/api/admin/audit (paged).
 *
 * actorName  — display name of the actor (email for Admins, service name for System).
 * actorType  — distinguishes human admins from automated/system events.
 * entityType — the domain object affected: "Tenant", "User", "Role", "Entitlement".
 * entityId   — id or code of the affected record.
 * metadata   — arbitrary key/value context captured at event time.
 * createdAtUtc — ISO 8601 timestamp in UTC.
 */
export interface AuditLogEntry {
  id:           string;
  actorName:    string;
  actorType:    ActorType;
  action:       string;
  entityType:   string;
  entityId:     string;
  metadata?:    Record<string, unknown>;
  createdAtUtc: string;
}

/**
 * UIX-004: A normalised activity event for display in the UserActivityPanel.
 * Populated from either AuditLogEntry (identity local) or CanonicalAuditEvent.
 */
export interface UserActivityEvent {
  id:           string;
  label:        string;
  eventType:    string;
  category:     string;
  actorLabel:   string;
  actorType:    string;
  occurredAtUtc: string;
  description?: string;
  ipAddress?:   string;
}

/** Source mode for the audit log page. Set via AUDIT_READ_MODE env var. */
export type AuditReadMode = 'legacy' | 'canonical' | 'hybrid';

/** Canonical event from the Platform Audit Event Service query API. */
export interface CanonicalAuditEvent {
  id:             string;
  source:         string;
  sourceService?: string;
  eventType:      string;
  category:       string;
  severity:       string;
  tenantId?:      string;
  actorId?:       string;
  actorLabel?:    string;
  actorType?:     string;
  targetType?:    string;
  targetId?:      string;
  action?:        string;
  description:    string;
  before?:        string;
  after?:         string;
  outcome:        string;
  ipAddress?:     string;
  correlationId?: string;
  requestId?:     string;
  sessionId?:     string;
  metadata?:      string;
  tags?:          string[];
  occurredAtUtc:  string;
  ingestedAtUtc:  string;
  hash?:          string;
}

// ── SynqAudit — Correlation Engine ───────────────────────────────────────────

/**
 * A single related audit event returned by GET /audit/events/{auditId}/related.
 * Includes the match label that explains why this event was correlated.
 */
export interface RelatedAuditEvent {
  matchedBy: 'correlation_id' | 'session_id' | 'actor_entity_window' | 'actor_window';
  matchKey:  string;
  event:     CanonicalAuditEvent;
}

/**
 * Full response from GET /audit/events/{auditId}/related.
 */
export interface RelatedEventsData {
  anchorId:       string;
  anchorEventType: string;
  strategyUsed:   'correlation_id' | 'session_id' | 'actor_entity_window' | 'actor_window' | 'none';
  totalRelated:   number;
  related:        RelatedAuditEvent[];
}

// ── SynqAudit — Audit Analytics ──────────────────────────────────────────────

/** Event count for a single calendar day. */
export interface AuditVolumeByDayItem {
  date:  string; // "yyyy-MM-dd"
  count: number;
}

/** Event count for a single EventCategory. */
export interface AuditCategoryBreakdownItem {
  category:      string;
  categoryValue: number;
  count:         number;
}

/** Event count for a single SeverityLevel. */
export interface AuditSeverityBreakdownItem {
  severity:      string;
  severityValue: number;
  count:         number;
}

/** A single event type ranked by count. */
export interface AuditTopEventTypeItem {
  eventType: string;
  count:     number;
}

/** An actor ranked by event count. */
export interface AuditTopActorItem {
  actorId:   string;
  actorName: string | null;
  count:     number;
}

/** A tenant ranked by event count (platform admin only). */
export interface AuditTopTenantItem {
  tenantId: string;
  count:    number;
}

/** Full analytics summary from GET /audit/analytics/summary. */
export interface AuditAnalyticsSummary {
  from:                  string; // ISO-8601
  to:                    string;
  effectiveTenantId:     string | null;
  totalEvents:           number;
  securityEventCount:    number;
  denialEventCount:      number;
  governanceEventCount:  number;
  volumeByDay:           AuditVolumeByDayItem[];
  byCategory:            AuditCategoryBreakdownItem[];
  bySeverity:            AuditSeverityBreakdownItem[];
  topEventTypes:         AuditTopEventTypeItem[];
  topActors:             AuditTopActorItem[];
  topTenants:            AuditTopTenantItem[] | null;
}

// ── SynqAudit — Audit Anomaly Detection ──────────────────────────────────────

/** A single firing anomaly from the rule-based detection engine. */
export interface AuditAnomalyItem {
  ruleKey:           string;
  title:             string;
  description:       string;
  severity:          'High' | 'Medium' | 'Low';
  recentValue:       number;
  baselineValue:     number | null;
  threshold:         number;
  actualValue:       number;
  affectedActorId:   string | null;
  affectedActorName: string | null;
  affectedTenantId:  string | null;
  affectedEventType: string | null;
  drillDownPath:     string;
}

// ── SynqAudit — Audit Alerts ──────────────────────────────────────────────────

export type AlertStatus      = 'Open' | 'Acknowledged' | 'Resolved';
export type AuditAlertSeverity = 'High' | 'Medium' | 'Low';

/** A single alert record from the alerting engine. */
export interface AuditAlertItem {
  alertId:            string;
  ruleKey:            string;
  scopeType:          string;
  tenantId:           string | null;
  severity:           AuditAlertSeverity;
  status:             AlertStatus;
  title:              string;
  description:        string;
  drillDownPath:      string | null;
  contextJson:        string | null;
  firstDetectedAtUtc: string;
  lastDetectedAtUtc:  string;
  detectionCount:     number;
  acknowledgedAtUtc:  string | null;
  acknowledgedBy:     string | null;
  resolvedAtUtc:      string | null;
  resolvedBy:         string | null;
}

/** Response from GET /audit/analytics/alerts. */
export interface AuditAlertListData {
  statusFilter:      string | null;
  effectiveTenantId: string | null;
  totalReturned:     number;
  openCount:         number;
  acknowledgedCount: number;
  resolvedCount:     number;
  alerts:            AuditAlertItem[];
}

/** Response from POST /audit/analytics/alerts/evaluate. */
export interface AuditEvaluateAlertsData {
  evaluatedAt:        string;
  effectiveTenantId:  string | null;
  anomaliesDetected:  number;
  alertsCreated:      number;
  alertsRefreshed:    number;
  alertsSuppressed:   number;
  activeAlerts:       AuditAlertItem[];
}

/** Full response from GET /audit/analytics/anomalies. */
export interface AuditAnomalyData {
  evaluatedAt:        string;
  recentWindowFrom:   string;
  recentWindowTo:     string;
  baselineWindowFrom: string;
  baselineWindowTo:   string;
  effectiveTenantId:  string | null;
  totalAnomalies:     number;
  anomalies:          AuditAnomalyItem[];
}

/** Filter params for the analytics summary request. */
export interface AuditAnalyticsRequest {
  from?:     string;
  to?:       string;
  tenantId?: string;
  category?: string;
}

// ── SynqAudit — Event Ingest (server-side canonical emission) ─────────────────

/** Payload for POST /audit-service/audit/ingest — used by CC server actions. */
export interface AuditIngestPayload {
  eventType:     string;
  eventCategory: string;
  sourceSystem:  string;
  sourceService: string;
  visibility:    string;
  severity:      string;
  occurredAtUtc: string;
  scope: {
    scopeType: string;
    tenantId?: string;
  };
  actor: {
    id?:    string;
    type:   string;
    label?: string;
  };
  entity?: {
    type: string;
    id?:  string;
  };
  action?:        string;
  description?:   string;
  before?:        string;
  after?:         string;
  idempotencyKey?: string;
  tags?:          string[];
}

// ── SynqAudit — Exports ───────────────────────────────────────────────────────

export type AuditExportStatus = 'Pending' | 'Processing' | 'Completed' | 'Failed';
export type AuditExportFormat = 'Json' | 'Csv' | 'Ndjson';

export interface AuditExport {
  exportId:       string;
  status:         AuditExportStatus;
  format:         string;
  recordCount?:   number;
  downloadUrl?:   string;
  createdAtUtc:   string;
  completedAtUtc?: string;
  errorMessage?:  string;
}

// ── SynqAudit — Integrity ─────────────────────────────────────────────────────

export interface IntegrityCheckpoint {
  checkpointId:     string;
  checkpointType:   string;
  aggregateHash:    string;
  recordCount:      number;
  isValid?:         boolean;
  fromRecordedAtUtc: string;
  toRecordedAtUtc:  string;
  createdAtUtc:     string;
}

// ── SynqAudit — Legal Holds ───────────────────────────────────────────────────

export interface LegalHold {
  holdId:           string;
  auditId:          string;
  legalAuthority:   string;
  notes?:           string;
  heldByUserId?:    string;
  heldAtUtc:        string;
  isActive:         boolean;
  releasedAtUtc?:   string;
  releasedByUserId?: string;
}

// ── Platform Settings ─────────────────────────────────────────────────────────

export interface PlatformSetting {
  key:          string;
  label:        string;
  value:        string | number | boolean;
  type:         'boolean' | 'string' | 'number';
  description?: string;
  editable:     boolean;
}

// ── Monitoring ────────────────────────────────────────────────────────────────

export type MonitoringStatus = 'Healthy' | 'Degraded' | 'Down';
export type AlertSeverity    = 'Info' | 'Warning' | 'Critical';

export interface SystemHealthSummary {
  status:           MonitoringStatus;
  lastCheckedAtUtc: string;
}

export interface IntegrationStatus {
  name:             string;
  status:           MonitoringStatus;
  latencyMs?:       number;
  lastCheckedAtUtc: string;
  category?:        string;
}

export interface SystemAlert {
  id:            string;
  message:       string;
  severity:      AlertSeverity;
  createdAtUtc:  string;
  entityName?:   string;        // component display name; used for correlation with integrations
  resolvedAtUtc?: string | null; // null or absent = still active
}

export interface MonitoringSummary {
  system:       SystemHealthSummary;
  integrations: IntegrationStatus[];
  alerts:       SystemAlert[];
}

// ── Support Tools ─────────────────────────────────────────────────────────────

export type SupportCaseStatus   = 'Open' | 'Investigating' | 'Resolved' | 'Closed';
export type SupportCasePriority = 'Low' | 'Medium' | 'High';

export interface SupportCase {
  id:               string;
  title:            string;
  tenantId:         string;
  tenantName:       string;
  userId?:          string;
  userName?:        string;
  requesterEmail?:  string;
  status:           SupportCaseStatus;
  category:         string;
  priority:         SupportCasePriority;
  assignedUserId?:  string;
  createdAtUtc:     string;
  updatedAtUtc:     string;
  updatedByUserId?: string;
}

export interface SupportNote {
  id:            string;
  caseId:        string;
  message:       string;
  createdBy:     string;
  authorUserId?: string;
  authorEmail?:  string;
  createdAtUtc:  string;
  visibility:    string;
  commentType:   string;
}

export interface SupportProductRef {
  id:            string;
  ticketId:      string;
  productCode:   string;
  entityType:    string;
  entityId:      string;
  displayLabel?: string;
  metadataJson?: string;
  createdByUserId?: string;
  createdAt:     string;
}

export interface TicketAttachmentItem {
  id:                string;
  ticketId:          string;
  documentId:        string;
  fileName:          string;
  contentType?:      string;
  fileSizeBytes?:    number;
  uploadedByUserId?: string;
  createdAt:         string;
}

export interface SupportCaseDetail extends SupportCase {
  notes:       SupportNote[];
  productRefs: SupportProductRef[];
}

// ── Tenant Context / Impersonation ────────────────────────────────────────────

/**
 * TenantContext — identifies which tenant the platform admin is currently
 * "scoped to" for context-switching and impersonation flows.
 *
 * Stored in the cc_tenant_context cookie and consumed by pages/actions that
 * need to filter data by a specific tenant without a full login as that tenant.
 */
export interface TenantContext {
  tenantId:   string;
  tenantName: string;
  tenantCode: string;
}

/**
 * ImpersonationSession — records a live impersonation started by a platform
 * admin acting as a tenant.
 *
 * originalAdminId       — userId of the PlatformAdmin who started impersonation.
 * impersonatedTenantId  — id of the tenant whose context is active.
 * impersonatedTenantName — display name kept for UI banners.
 * startedAtUtc          — ISO 8601 timestamp in UTC.
 *
 * TODO: persist impersonation session in backend and emit audit log entry
 */
export interface ImpersonationSession {
  originalAdminId:        string;
  impersonatedTenantId:   string;
  impersonatedTenantName: string;
  startedAtUtc:           string;
}

/**
 * UserImpersonationSession — records a live user-level impersonation started
 * by a platform admin acting as a specific tenant user.
 *
 * adminId               — userId of the PlatformAdmin performing impersonation.
 * impersonatedUserId    — id of the user being impersonated.
 * impersonatedUserEmail — email address for banner display.
 * tenantId              — tenant the impersonated user belongs to.
 * tenantName            — tenant display name for banner (not in minimal spec
 *                         but always available at impersonation start time).
 * startedAtUtc          — ISO 8601 timestamp in UTC.
 *
 * TODO: integrate with Identity service impersonation endpoint
 * TODO: issue temporary impersonation token
 * TODO: audit log impersonation events
 */
export interface UserImpersonationSession {
  adminId:               string;
  impersonatedUserId:    string;
  impersonatedUserEmail: string;
  tenantId:              string;
  tenantName:            string;
  startedAtUtc:          string;
}

// ── Organization Types (Phase E) ──────────────────────────────────────────────

/**
 * A catalog entry for an organization type in the Identity service.
 * Returned by GET /identity/api/admin/organization-types.
 */
export interface OrganizationTypeItem {
  id:          string;
  code:        string;
  name:        string;
  description: string;
  isActive:    boolean;
  createdAtUtc: string;
}

// ── Relationship Types (Phase E) ──────────────────────────────────────────────

/**
 * A catalog entry for a relationship type (e.g. Referral, Partnership).
 * Returned by GET /identity/api/admin/relationship-types.
 */
export interface RelationshipTypeItem {
  id:          string;
  code:        string;
  name:        string;
  description: string;
  isActive:    boolean;
  createdAtUtc: string;
}

// ── Organization Relationships (Phase E) ──────────────────────────────────────

/** Live status of an organization-to-organization relationship. */
export type OrgRelationshipStatus = 'Active' | 'Inactive' | 'Pending';

/**
 * A directed relationship between two organizations in the Identity graph.
 * Returned by GET /identity/api/admin/organization-relationships.
 */
export interface OrgRelationship {
  id:                   string;
  sourceOrganizationId: string;
  targetOrganizationId: string;
  relationshipTypeId:   string;
  relationshipTypeCode: string;
  status:               OrgRelationshipStatus;
  effectiveFromUtc?:    string;
  effectiveToUtc?:      string;
  createdAtUtc:         string;
  updatedAtUtc:         string;
}

// ── Product–OrgType Rules (Phase E) ──────────────────────────────────────────

/**
 * A rule that permits a given OrganizationType to access a Product.
 * Returned by GET /identity/api/admin/product-org-type-rules.
 */
export interface ProductOrgTypeRule {
  id:                  string;
  productId:           string;
  productCode:         string;
  /** Role code within the product (e.g. CARECONNECT_REFERRER). */
  productRoleId:       string;
  productRoleCode:     string;
  productRoleName:     string;
  organizationTypeId:  string;
  organizationTypeCode: string;
  organizationTypeName: string;
  isActive:            boolean;
  createdAtUtc:        string;
}

// ── Product–RelType Rules (Phase E) ──────────────────────────────────────────

/**
 * A rule that permits a given RelationshipType to be used for a Product.
 * Returned by GET /identity/api/admin/product-rel-type-rules.
 */
export interface ProductRelTypeRule {
  id:                  string;
  productId:           string;
  productCode:         string;
  relationshipTypeId:  string;
  relationshipTypeCode: string;
  relationshipTypeName: string;
  isActive:            boolean;
  createdAtUtc:        string;
}

// ── Legacy coverage (Step 4) ──────────────────────────────────────────────────

/** One uncovered ProductRole — has EligibleOrgType but no active OrgTypeRule. */
export interface UncoveredRole {
  code:            string;
  eligibleOrgType: string;
}

/** Breakdown of eligibility-rule migration paths. */
export interface EligibilityRulesCoverage {
  totalActiveProductRoles: number;
  withDbRuleOnly:          number;   // modern path: OrgTypeRule only (Phase F goal)
  withBothPaths:           number;   // Phase F: always 0 — EligibleOrgType column dropped
  legacyStringOnly:        number;   // Phase F: always 0 — EligibleOrgType column dropped
  unrestricted:            number;   // no restriction at all (intentional)
  dbCoveragePct:           number;
  uncoveredRoles:          UncoveredRole[];
}

/**
 * Phase G shape — UserRoles / UserRoleAssignments tables are retired.
 * Legacy dual-write fields (usersWithLegacyRoles, usersWithGapCount,
 * dualWriteCoveragePct) have been removed; SRA is the sole role source.
 */
export interface RoleAssignmentsCoverage {
  /** Phase G: UserRoles and UserRoleAssignments tables have been dropped. */
  userRolesRetired:             boolean;
  usersWithScopedRoles:         number;
  totalActiveScopedAssignments: number;
}

/**
 * Point-in-time legacy migration snapshot.
 * Returned by GET /identity/api/admin/legacy-coverage.
 */
export interface LegacyCoverageReport {
  generatedAtUtc:  string;
  eligibilityRules: EligibilityRulesCoverage;
  roleAssignments:  RoleAssignmentsCoverage;
}

// ── Platform Readiness (Phase 8) ──────────────────────────────────────────────

export interface PhaseGCompletion {
  userRolesRetired:             boolean;
  soleRoleSourceIsSra:          boolean;
  totalActiveScopedAssignments: number;
  globalScopedAssignments:      number;
  usersWithScopedRole:          number;
}

export interface OrgTypeCoverage {
  totalActiveOrgs:            number;
  orgsWithOrganizationTypeId: number;
  orgsWithMissingTypeId:      number;
  orgsWithCodeMismatch:       number;
  consistent:                 boolean;
  coveragePct:                number;
}

export interface ProductRoleEligibilityCoverage {
  totalActiveProductRoles: number;
  withOrgTypeRule:         number;
  unrestricted:            number;
  coveragePct:             number;
}

export interface OrgRelationshipCoverage {
  total:  number;
  active: number;
}

/**
 * Phase I: active ScopedRoleAssignments broken down by scope type.
 * Non-zero organization/product/relationship values confirm real non-global
 * scope enforcement is in use at runtime.
 */
export interface ScopedAssignmentsByScope {
  global:       number;
  organization: number;
  product:      number;
  relationship: number;
  tenant:       number;
}

/**
 * Full platform readiness summary.
 * Returned by GET /identity/api/admin/platform-readiness.
 */
export interface PlatformReadinessSummary {
  generatedAtUtc:          string;
  phaseGCompletion:        PhaseGCompletion;
  orgTypeCoverage:         OrgTypeCoverage;
  productRoleEligibility:  ProductRoleEligibilityCoverage;
  orgRelationships:        OrgRelationshipCoverage;
  /** Phase I: SRA counts by scope type. */
  scopedAssignmentsByScope: ScopedAssignmentsByScope;
}

// ── CareConnect Integrity Report ──────────────────────────────────────────────

/**
 * Integrity counters for the CareConnect service.
 * Returned by GET /careconnect/api/admin/integrity.
 *
 * Counts of -1 indicate a query failure for that counter (the backend never
 * throws — it sets -1 so the dashboard always renders).
 */
export interface CareConnectIntegrityReport {
  generatedAtUtc: string;
  /** True when all four counters are 0. */
  clean: boolean;

  referrals: {
    /** Referrals where both org IDs are set but OrganizationRelationshipId is null. */
    withOrgPairButNullRelationship: number;
  };

  appointments: {
    /** Appointments missing a relationship ID when the linked referral has one. */
    missingRelationshipWhereReferralHasOne: number;
  };

  providers: {
    /** Active providers without an Identity OrganizationId link. */
    withoutOrganizationId: number;
  };

  facilities: {
    /** Active facilities without an Identity OrganizationId link. */
    withoutOrganizationId: number;
  };
}

// ── Scoped Role Assignment (Phase G) ──────────────────────────────────────────

/**
 * A scoped role assignment for a user.
 * Returned by GET /identity/api/admin/users/{id}/scoped-roles.
 */
export interface ScopedRoleAssignment {
  id:             string;
  userId:         string;
  roleId:         string;
  roleName:       string;
  scopeType:      string;
  scopeEntityId?: string;
  isActive:       boolean;
  createdAtUtc:   string;
}

// ── LS-COR-AUT-011: ABAC Policies ────────────────────────────────────────────

export interface PolicySummary {
  id:              string;
  policyCode:      string;
  name:            string;
  description?:    string;
  productCode:     string;
  isActive:        boolean;
  priority:        number;
  effect:          string;
  rulesCount:      number;
  permissionCount: number;
  createdAtUtc:    string;
  updatedAtUtc?:   string;
}

export interface PolicyRule {
  id:            string;
  conditionType: string;
  field:         string;
  op:            string;
  value:         string;
  logicalGroup:  string;
  createdAtUtc:  string;
}

export interface PermissionPolicyMapping {
  id:             string;
  permissionCode: string;
  isActive:       boolean;
  createdAtUtc:   string;
}

export interface PolicyDetail extends PolicySummary {
  createdBy?:          string;
  updatedBy?:          string;
  rules:               PolicyRule[];
  permissionMappings:  PermissionPolicyMapping[];
}

export interface PermissionPolicySummary {
  id:             string;
  permissionCode: string;
  policyId:       string;
  policyCode:     string;
  policyName:     string;
  isActive:       boolean;
  createdAtUtc:   string;
}

export interface SupportedFieldsResponse {
  fields:         string[];
  operators:      string[];
  conditionTypes: string[];
  logicalGroups:  string[];
  effects:        string[];
}

// ── Reports ──────────────────────────────────────────────────────────────────

export interface ReportTemplate {
  id:               string;
  code:             string;
  name:             string;
  description?:     string;
  productCode:      string;
  organizationType: string;
  isActive:         boolean;
  currentVersion:   number;
  createdAtUtc:     string;
  updatedAtUtc:     string;
}

export interface ReportTemplateVersion {
  id:              string;
  templateId:      string;
  versionNumber:   number;
  templateBody?:   string;
  outputFormat:    string;
  changeNotes?:    string;
  isActive:        boolean;
  isPublished:     boolean;
  publishedAtUtc?: string;
  createdAtUtc:    string;
  createdByUserId: string;
}

export type ReportsServiceStatus = 'online' | 'degraded' | 'offline';

export interface ReportsReadinessCheck {
  name:   string;
  status: 'ok' | 'fail' | 'mock';
}

export interface ReportsSummary {
  serviceStatus:    ReportsServiceStatus;
  serviceLatencyMs?: number;
  lastCheckedAtUtc: string;
  readinessChecks:  ReportsReadinessCheck[];
  templates:        ReportTemplate[];
  templateCount:    number;
}

// ── Shared ────────────────────────────────────────────────────────────────────

export interface PagedResponse<T> {
  items:      T[];
  totalCount: number;
  page:       number;
  pageSize:   number;
}

// ── E9.1: Cross-product workflow operations list ─────────────────────────────

/**
 * Workflow lifecycle status as exposed by the Flow service. Matches the
 * canonical engine values (Active / Pending / Completed / Cancelled / Failed).
 * `Pending` is rare (instance row exists but no current stage yet) and is
 * treated as actionable for filter/UI purposes.
 */
export type WorkflowInstanceStatus =
  | 'Active'
  | 'Pending'
  | 'Completed'
  | 'Cancelled'
  | 'Failed';

/**
 * One row in the Control Center workflow operations list. Mirrors
 * `AdminWorkflowInstanceListItem` returned by Flow's
 * `GET /api/v1/admin/workflow-instances`.
 *
 * `tenantId` is normalised to lowercase Guid string (matches how
 * Flow.Infrastructure stores it).
 */
export interface WorkflowInstanceListItem {
  id:                   string;
  tenantId:             string;
  productKey:           string;
  workflowDefinitionId: string;
  workflowName:         string | null;
  status:               WorkflowInstanceStatus | string;
  currentStepKey:       string | null;
  assignedToUserId:     string | null;
  correlationKey:       string | null;
  sourceEntityType:     string | null;
  sourceEntityId:       string | null;
  startedAt:            string | null;
  completedAt:          string | null;
  updatedAt:            string | null;
  createdAt:            string;
  /**
   * E9.3 — last engine error preview surfaced on the list row so the
   * exception view can show a truncated message without a second call.
   * Always present (null when none).
   */
  lastErrorMessage?:    string | null;
  /**
   * E9.3 — server-evaluated classification labels for this row. Empty
   * array means "no current exception". May contain multiple values
   * (e.g. `['Failed','ErrorPresent']`).
   */
  classifications?:     WorkflowClassification[];
}

/**
 * E9.3 — supported exception/stuck classification labels. Kept in sync
 * with `AdminWorkflowInstancesController` constants on the Flow side.
 */
export type WorkflowClassification =
  | 'Failed'
  | 'Cancelled'
  | 'Stuck'
  | 'ErrorPresent';

/**
 * E9.3 — paged response shape returned by the admin list endpoint when
 * exception filters are in play. Identical to the E9.1 paged response
 * with one additional field surfacing the stale threshold the server
 * used so the UI can label things like "Stuck >24h" without guessing.
 */
export interface WorkflowInstancePagedResponse {
  items:               WorkflowInstanceListItem[];
  totalCount:          number;
  page:                number;
  pageSize:            number;
  staleThresholdHours: number;
}

/**
 * E9.2 — single-workflow detail returned by Flow's
 * `GET /api/v1/admin/workflow-instances/{id}` admin endpoint. Superset
 * of `WorkflowInstanceListItem` plus the current step's display name and
 * the engine's `lastErrorMessage` for diagnostics.
 */
export interface WorkflowInstanceDetail extends WorkflowInstanceListItem {
  currentStageId:   string | null;
  currentStepName:  string | null;
  lastErrorMessage: string | null;
}

/**
 * E13.1 / E13.2 — one normalized event from the Flow workflow timeline
 * endpoint (`GET /flow/api/v1/admin/workflow-instances/{id}/timeline`).
 *
 * Shape mirrors the DTO produced by `AuditTimelineNormalizer` on the
 * Flow side. Every optional field tolerates missing data so the UI
 * never breaks on a partially-populated record.
 *
 * `severity` is consumed by the E13.3 visual indicator. The Flow
 * normalizer surfaces the upstream audit `severity` inside the
 * metadata bag; we lift it to a top-level field with a constrained
 * vocabulary and fall back to `'info'` when missing or unrecognised.
 */
export type WorkflowTimelineSeverity = 'info' | 'warning' | 'critical';

export interface WorkflowTimelineActor {
  id:   string | null;
  name: string | null;
  type: string | null;
}

export interface WorkflowTimelineEvent {
  eventId:        string;
  /**
   * Canonical AuditId of the underlying audit record, when known.
   * Used by the drawer to deep-link a timeline row to the matching
   * entry on the central Audit Logs page. Null when the upstream
   * audit service did not surface an id (degraded / unconfigured).
   */
  auditId:        string | null;
  occurredAtUtc:  string;
  category:       string;
  action:         string;
  source:         string;
  severity:       WorkflowTimelineSeverity;
  actor:          WorkflowTimelineActor;
  performedBy:    string | null;
  summary:        string | null;
  previousStatus: string | null;
  newStatus:      string | null;
  metadata:       Record<string, string>;
}

export interface WorkflowTimelineResponse {
  workflowInstanceId: string;
  tenantId:           string;
  totalCount:         number;
  truncated:          boolean;
  events:             WorkflowTimelineEvent[];
}

/**
 * E10.1 — admin action verbs supported by the Control Center drawer
 * and the matching Flow admin endpoints.
 */
export type WorkflowAdminAction = 'retry' | 'force-complete' | 'cancel';

/**
 * E10.1 — structured result returned by every admin action endpoint
 * (`POST /api/v1/admin/workflow-instances/{id}/{action}`). Mirrors
 * `AdminWorkflowActionResult` on the Flow side.
 */
export interface WorkflowAdminActionResult {
  workflowInstanceId: string;
  action:             string;
  previousStatus:     string;
  newStatus:          string;
  performedBy:        string;
  timestamp:          string;
  reason:             string;
}

/**
 * Bucketed category used by the timeline UI for color/iconography.
 * Derived at render time from the raw `category` string via the
 * drawer's `bucketFromCategory()` helper.
 *   'AdminAction'      — operator-initiated (retry/force-complete/cancel)
 *   'EngineTransition' — workflow.state_changed
 *   'Lifecycle'        — workflow.created / workflow.completed / etc.
 *   'Task'             — task.assigned / task.completed
 *   'Other'            — anything not matched above
 */
export type WorkflowTimelineEventCategory =
  | 'AdminAction'
  | 'EngineTransition'
  | 'Lifecycle'
  | 'Task'
  | 'Other';

// ── Outbox (E17) ──────────────────────────────────────────────────────────────

/**
 * E17 — outbox item status values, mirroring OutboxStatus constants on the
 * Flow backend.
 */
export type OutboxStatus =
  | 'Pending'
  | 'Processing'
  | 'Succeeded'
  | 'Failed'
  | 'DeadLettered';

/**
 * E17 — single row from the outbox list endpoint. LastError is truncated to
 * 200 chars server-side; use OutboxDetail for the full error.
 */
export interface OutboxListItem {
  id:                  string;
  tenantId:            string;
  workflowInstanceId?: string | null;
  eventType:           string;
  status:              OutboxStatus;
  attemptCount:        number;
  createdAt:           string;
  updatedAt?:          string | null;
  nextAttemptAt:       string;
  processedAt?:        string | null;
  lastError?:          string | null;
}

/**
 * E17 — full detail for a single outbox item including the complete last
 * error, a truncated payload summary, and the retry eligibility flag.
 */
export interface OutboxDetail extends OutboxListItem {
  payloadSummary?:  string | null;
  isRetryEligible:  boolean;
}

/**
 * E17 — lightweight grouped counts for the summary cards on the outbox page.
 */
export interface OutboxSummary {
  pendingCount:      number;
  processingCount:   number;
  failedCount:       number;
  deadLetteredCount: number;
  succeededCount:    number;
}

/**
 * E17 — paged list response envelope for the outbox list endpoint.
 */
export interface OutboxListResponse {
  items:      OutboxListItem[];
  totalCount: number;
  page:       number;
  pageSize:   number;
}

/**
 * E17 — structured result returned by the manual retry endpoint. Mirrors
 * AdminOutboxRetryResult on the Flow side.
 */
export interface OutboxRetryResult {
  outboxId:       string;
  eventType:      string;
  previousStatus: string;
  newStatus:      string;
  performedBy:    string;
  timestamp:      string;
  reason:         string;
}

// ── E19 Analytics Types ───────────────────────────────────────────────────────

export type AnalyticsWindow = 'today' | '7d' | '30d';

export interface QueueOverdueBreakdown {
  queueKey:    string;
  queueType:   string;
  overdueCount: number;
}

export interface SlaSummary {
  activeOnTrackCount:      number;
  activeAtRiskCount:       number;
  activeOverdueCount:      number;
  totalActiveCount:        number;
  overduePercentage:       number;
  breachedInWindow:        number;
  completedOnTimeInWindow: number;
  completedInWindow:       number;
  avgOverdueAgeDays:       number | null;
  windowStart:             string;
  windowEnd:               string;
  windowLabel:             string;
  topOverdueQueues:        QueueOverdueBreakdown[];
}

export interface RoleQueueBacklog {
  role:            string;
  openCount:       number;
  inProgressCount: number;
  totalCount:      number;
  overdueCount:    number;
}

export interface OrgQueueBacklog {
  orgId:           string;
  openCount:       number;
  inProgressCount: number;
  totalCount:      number;
  overdueCount:    number;
}

export interface QueueSummary {
  roleQueueBacklog:         number;
  orgQueueBacklog:          number;
  unassignedBacklog:        number;
  oldestQueuedTaskAgeHours: number | null;
  medianQueueAgeHours:      number | null;
  activeUserCount:          number;
  overloadedUserCount:      number;
  overloadThreshold:        number;
  roleQueueBreakdown:       RoleQueueBacklog[];
  orgQueueBreakdown:        OrgQueueBacklog[];
  asOf:                     string;
}

export interface WorkflowProductBreakdown {
  productKey:    string;
  startedCount:  number;
  completedCount: number;
  activeCount:   number;
}

export interface WorkflowThroughput {
  startedInWindow:      number;
  completedInWindow:    number;
  cancelledInWindow:    number;
  failedInWindow:       number;
  currentlyActiveCount: number;
  avgCycleTimeHours:    number | null;
  medianCycleTimeHours: number | null;
  byProduct:            WorkflowProductBreakdown[];
  windowStart:          string;
  windowEnd:            string;
  windowLabel:          string;
}

export interface UserWorkload {
  userId:          string;
  activeTaskCount: number;
  openCount:       number;
  inProgressCount: number;
}

export interface AssignmentSummary {
  directUserCount:          number;
  roleQueueCount:           number;
  orgQueueCount:            number;
  unassignedCount:          number;
  assignedInWindow:         number;
  topAssigneesByActiveLoad: UserWorkload[];
  assumptionNote:           string;
  windowStart:              string;
  windowEnd:                string;
  windowLabel:              string;
}

export interface OutboxEventTypeBreakdown {
  eventType:      string;
  failedCount:    number;
  deadLettered:   number;
  totalUnhealthy: number;
}

export interface OutboxAnalyticsSummary {
  pendingCount:        number;
  processingCount:     number;
  failedCount:         number;
  deadLetteredCount:   number;
  succeededCount:      number;
  unhealthyCount:      number;
  createdInWindow:     number;
  succeededInWindow:   number;
  failedInWindow:      number;
  deadLetteredInWindow: number;
  failedByEventType:   OutboxEventTypeBreakdown[];
  windowStart:         string;
  windowEnd:           string;
  windowLabel:         string;
  asOf:                string;
}

export interface AnalyticsDashboardSummary {
  sla:        SlaSummary;
  queue:       QueueSummary;
  workflows:   WorkflowThroughput;
  assignment:  AssignmentSummary;
  outbox:      OutboxAnalyticsSummary;
  generatedAt: string;
  windowLabel: string;
}

export interface TenantOverdueRank {
  tenantId:    string;
  overdueCount: number;
  overdueRate: number;
}

export interface TenantWorkflowRank {
  tenantId:    string;
  activeCount: number;
}

export interface TenantOutboxHealth {
  tenantId:    string;
  failedCount: number;
  deadLettered: number;
}

export interface PlatformAnalyticsSummary {
  totalActiveWorkflows:        number;
  totalActiveTasks:            number;
  totalOverdueTasks:           number;
  totalDeadLettered:           number;
  totalFailedOutbox:           number;
  topTenantsByOverdue:         TenantOverdueRank[];
  topTenantsByActiveWorkflows: TenantWorkflowRank[];
  outboxHealthByTenant:        TenantOutboxHealth[];
  asOf:                        string;
  windowLabel:                 string;
}
