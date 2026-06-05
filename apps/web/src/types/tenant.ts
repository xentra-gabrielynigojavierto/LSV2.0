export interface TenantUser {
  id: string;
  tenantId: string;
  email: string;
  firstName: string;
  lastName: string;
  isActive: boolean;
  status?: string;
  roles: string[];
  organizationId?: string;
  orgType?: string;
  productRoles?: string[];
  groupCount?: number;
  productCount?: number;
  avatarDocumentId?: string;
}

export interface TenantUserDetail {
  id: string;
  firstName: string;
  lastName: string;
  email: string;
  role: string;
  roles: { roleId: string; roleName: string; assignmentId: string }[];
  status: string;
  tenantId: string;
  tenantCode: string;
  tenantDisplayName: string;
  createdAtUtc: string;
  updatedAtUtc: string;
  isLocked: boolean;
  lockedAtUtc?: string;
  lastLoginAtUtc?: string;
  sessionVersion: number;
  avatarDocumentId?: string;
  phone?: string | null;
  memberships: {
    membershipId: string;
    organizationId: string;
    orgName: string;
    memberRole: string;
    isPrimary: boolean;
    joinedAtUtc: string;
  }[];
  groups: { groupId: string; groupName: string; joinedAtUtc: string }[];
  groupCount: number;
}

export interface TenantGroup {
  id: string;
  tenantId: string;
  name: string;
  description?: string;
  status: string;
  scopeType: string;
  productCode?: string;
  organizationId?: string;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface GroupMember {
  id: string;
  tenantId: string;
  groupId: string;
  userId: string;
  membershipStatus: string;
  addedAtUtc: string;
  removedAtUtc?: string;
}

export interface GroupProductAccess {
  id: string;
  tenantId: string;
  groupId: string;
  productCode: string;
  accessStatus: string;
  grantedAtUtc: string;
  revokedAtUtc?: string;
}

export interface GroupRoleAssignment {
  id: string;
  tenantId: string;
  groupId: string;
  roleCode: string;
  productCode?: string;
  organizationId?: string;
  assignmentStatus: string;
  assignedAtUtc: string;
  removedAtUtc?: string;
}

export interface AccessDebugProductSource {
  productCode: string;
  source: string;
  groupId?: string;
  groupName?: string;
}

export interface AccessDebugRoleSource {
  roleCode: string;
  productCode?: string;
  source: string;
  groupId?: string;
  groupName?: string;
}

export interface AccessDebugPermissionSource {
  permissionCode: string;
  productCode?: string;
  source: string;
  viaRoleCode: string;
  groupId?: string;
  groupName?: string;
}

export interface AccessDebugGroup {
  groupId: string;
  groupName: string;
  status: string;
  scopeType: string;
  productCode?: string;
}

export interface AccessDebugResponse {
  userId: string;
  tenantId: string;
  accessVersion?: number;
  products: AccessDebugProductSource[];
  roles: AccessDebugRoleSource[];
  systemRoles: { roleName: string; scopeType: string }[];
  groups: AccessDebugGroup[];
  entitlements: { productCode: string; status: string }[];
  productRolesFlat: string[];
  tenantRoles: string[];
  permissions: string[];
  permissionSources: AccessDebugPermissionSource[];
  policies?: {
    permission: string;
    linkedPolicies: {
      policyCode: string;
      policyName: string;
      priority: number;
      rulesCount: number;
      rules: { field: string; op: string; value: string; conditionType: string; logicalGroup: string }[];
    }[];
  }[];
}

export interface PermissionItem {
  id: string;
  code: string;
  name: string;
  description?: string;
  category?: string;
  productId?: string;
  productCode: string;
  productName: string;
  isActive: boolean;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface AdminUserItem {
  id: string;
  firstName: string;
  lastName: string;
  email: string;
  role: string;
  status: string;
  primaryOrg?: string;
  groupCount: number;
  tenantId: string;
  tenantCode: string;
  createdAtUtc: string;
}

export interface AdminUsersResponse {
  items: AdminUserItem[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface AssignableRoleItem {
  id: string;
  name: string;
  description: string;
  isSystemRole: boolean;
  isProductRole: boolean;
  productCode?: string;
  productName?: string;
  allowedOrgTypes?: string[];
  assignable: boolean;
  disabledReason?: string;
  isAssigned: boolean;
}

export interface AssignableRolesResponse {
  items: AssignableRoleItem[];
  userOrgType: string;
  tenantEnabledProducts: number;
}

export interface SimulationRequest {
  tenantId: string;
  userId: string;
  permissionCode: string;
  resourceContext?: Record<string, unknown>;
  requestContext?: Record<string, string>;
  draftPolicy?: DraftPolicyInput;
  excludePolicyIds?: string[];
}

export interface DraftPolicyInput {
  policyCode: string;
  name: string;
  description?: string;
  priority: number;
  effect: 'Allow' | 'Deny';
  rules: DraftRuleInput[];
}

export interface DraftRuleInput {
  field: string;
  operator: string;
  value: string;
  logicalGroup?: string;
}

export interface SimulationResult {
  allowed: boolean;
  permissionPresent: boolean;
  roleFallbackUsed: boolean;
  permissionCode: string;
  policyDecision: PolicyDecisionResult;
  reason: string;
  mode: 'Live' | 'Draft' | string;
  user: UserIdentitySummary;
  permissionSources: SimPermissionSourceEntry[];
  evaluationElapsedMs: number;
}

export interface PolicyDecisionResult {
  evaluated: boolean;
  policyVersion: number;
  denyOverrideApplied: boolean;
  denyOverridePolicyCode?: string;
  matchedPolicies: SimulatedMatchedPolicy[];
}

export interface SimulatedMatchedPolicy {
  policyCode: string;
  policyName?: string;
  effect: string;
  priority: number;
  evaluationOrder: number;
  result: string;
  isDraft: boolean;
  ruleResults: SimulatedRuleResult[];
}

export interface SimulatedRuleResult {
  field: string;
  operator: string;
  expected: string;
  actual?: string;
  passed: boolean;
}

export interface UserIdentitySummary {
  userId: string;
  tenantId: string;
  email: string;
  displayName: string;
  roles: string[];
  permissions: string[];
}

export interface SimPermissionSourceEntry {
  permissionCode: string;
  source: string;
  viaRole?: string;
  groupId?: string;
  groupName?: string;
}

// ── E19 Flow Analytics (tenant-scoped) ───────────────────────────────────────

export interface TenantSlaSummary {
  windowLabel:          string;
  totalTasks:           number;
  onTimeCount:          number;
  atRiskCount:          number;
  breachedCount:        number;
  onTimePct:            number;
  atRiskPct:            number;
  breachedPct:          number;
  avgTimeToBreachHours: number | null;
}

export interface TenantQueueRow {
  queueName:       string;
  pendingCount:    number;
  inProgressCount: number;
  overdueCount:    number;
  unassignedCount: number;
  avgAgeHours:     number;
}

export interface TenantQueueSummary {
  rows:         TenantQueueRow[];
  totalPending: number;
  totalOverdue: number;
}

export interface TenantWorkflowThroughput {
  windowLabel:      string;
  startedCount:     number;
  completedCount:   number;
  cancelledCount:   number;
  avgDurationHours: number | null;
  completionRate:   number;
}

// ── LS-ID-TNT-013 Permission Management ─────────────────────────────────────

export interface TenantPermissionCatalogItem {
  id:           string;
  code:         string;
  name:         string;
  description?: string;
  category?:    string;
}

export interface TenantPermissionCatalogResponse {
  tenantId:    string;
  permissions: TenantPermissionCatalogItem[];
  totalCount:  number;
}

export interface RolePermissionEntry {
  id:          string;
  code:        string;
  name:        string;
  productCode: string;
}

export interface RolePermissionsResponse {
  roleId:      string;
  roleName:    string;
  permissions: RolePermissionEntry[];
}

export interface TenantRoleItem {
  id:            string;
  name:          string;
  isSystemRole:  boolean;
  isProductRole: boolean;
  productCode?:  string;
  productName?:  string;
}

export interface TenantRolesListResponse {
  items:      TenantRoleItem[];
  totalCount: number;
  page:       number;
  pageSize:   number;
}
