/**
 * api-mappers.ts — Backend → Frontend response normalization layer.
 *
 * The Identity/Platform services may return either camelCase or snake_case
 * field names depending on the endpoint and serialiser configuration.
 * These mappers normalise every raw API response to the strict TypeScript
 * types defined in types/control-center.ts.
 *
 * Conventions:
 *   - Every mapper accepts `unknown` input and returns a typed value.
 *   - Fields are read with snake_case priority, camelCase fallback.
 *   - Missing / null / wrong-type fields are replaced with safe defaults.
 *   - In development, console.warn fires once per malformed field so
 *     backend issues surface during integration without crashing the UI.
 *
 * TODO: replace manual mappers with generated types from OpenAPI spec
 */

import type {
  TenantSummary,
  TenantDetail,
  TenantStatus,
  TenantType,
  ProvisioningStatus,
  ProvisioningFailureStage,
  ProductEntitlementSummary,
  ProductCode,
  EntitlementStatus,
  UserSummary,
  UserDetail,
  UserStatus,
  Permission,
  RoleSummary,
  RoleDetail,
  AuditLogEntry,
  ActorType,
  PlatformSetting,
  MonitoringSummary,
  MonitoringStatus,
  AlertSeverity,
  SystemHealthSummary,
  IntegrationStatus,
  SystemAlert,
  SupportCase,
  SupportNote,
  SupportCaseDetail,
  SupportCaseStatus,
  SupportCasePriority,
  SupportProductRef,
  PagedResponse,
  OrganizationTypeItem,
  RelationshipTypeItem,
  OrgRelationship,
  OrgRelationshipStatus,
  ProductOrgTypeRule,
  ProductRelTypeRule,
  LegacyCoverageReport,
  PlatformReadinessSummary,
  CareConnectIntegrityReport,
  ScopedRoleAssignment,
  CanonicalAuditEvent,
  AuditExport,
  IntegrityCheckpoint,
  LegalHold,
  AccessGroupSummary,
  AccessGroupMember,
  GroupProductAccess,
  GroupRoleAssignment,
  PermissionCatalogItem,
  UserActivityEvent,
  RoleCapabilityItem,
  EffectivePermission,
  PermissionSource,
  EffectivePermissionsResult,
  AccessDebugResult,
  PolicySummary,
  PolicyDetail,
  PolicyRule,
  PermissionPolicyMapping,
  PermissionPolicySummary,
  SupportedFieldsResponse,
} from '@/types/control-center';

// ── Low-level helpers ─────────────────────────────────────────────────────────

/**
 * asObj — safely casts `unknown` to a plain-object record.
 * Returns {} on null / non-object input.
 */
function asObj(v: unknown): Record<string, unknown> {
  if (v !== null && typeof v === 'object' && !Array.isArray(v)) {
    return v as Record<string, unknown>;
  }
  return {};
}

/**
 * asArr — safely casts `unknown` to an array.
 * Returns [] on null / non-array input.
 */
function asArr(v: unknown): unknown[] {
  return Array.isArray(v) ? v : [];
}

/**
 * str — reads a string field; snake_case first, then camelCase.
 * Falls back to `fallback` and optionally logs a warning in dev.
 */
function str(
  raw:       Record<string, unknown>,
  snake:     string,
  camel:     string,
  fallback:  string,
  warnLabel?: string,
): string {
  const val = raw[snake] ?? raw[camel];
  if (typeof val === 'string' && val.length > 0) return val;
  if (warnLabel && process.env.NODE_ENV !== 'production') {
    const got = JSON.stringify(val ?? null);
    console.warn(`[api-mappers] ${warnLabel}: expected string at "${snake}"/"${camel}", got ${got}. Using fallback "${fallback}".`);
  }
  return fallback;
}

function strOrNull(
  raw:   Record<string, unknown>,
  snake: string,
  camel: string,
): string | null {
  const val = raw[snake] ?? raw[camel];
  if (typeof val === 'string' && val.length > 0) return val;
  return null;
}

/**
 * optStr — reads an optional string field; returns undefined when absent.
 */
function optStr(
  raw:   Record<string, unknown>,
  snake: string,
  camel: string,
): string | undefined {
  const val = raw[snake] ?? raw[camel];
  if (typeof val === 'string' && val.length > 0) return val;
  return undefined;
}

/**
 * num — reads a number field; snake_case first, then camelCase.
 * Falls back to `fallback`.
 */
function num(
  raw:      Record<string, unknown>,
  snake:    string,
  camel:    string,
  fallback: number,
  warnLabel?: string,
): number {
  const val = raw[snake] ?? raw[camel];
  if (typeof val === 'number' && isFinite(val)) return val;
  if (warnLabel && process.env.NODE_ENV !== 'production') {
    console.warn(`[api-mappers] ${warnLabel}: expected number at "${snake}"/"${camel}", got ${JSON.stringify(val ?? null)}. Using ${fallback}.`);
  }
  return fallback;
}

/**
 * bool — reads a boolean field; snake_case first, then camelCase.
 * Coerces 0/1/"true"/"false" loosely. Falls back to `fallback`.
 */
function bool(
  raw:      Record<string, unknown>,
  snake:    string,
  camel:    string,
  fallback: boolean,
): boolean {
  const val = raw[snake] ?? raw[camel];
  if (typeof val === 'boolean') return val;
  if (val === 1 || val === '1' || val === 'true')  return true;
  if (val === 0 || val === '0' || val === 'false') return false;
  return fallback;
}

/**
 * oneOf — reads a field and validates it against an allowed set.
 * Falls back to `fallback` if the value is absent or not in the set.
 */
function oneOf<T extends string>(
  raw:      Record<string, unknown>,
  snake:    string,
  camel:    string,
  allowed:  readonly T[],
  fallback: T,
  warnLabel?: string,
): T {
  const val = raw[snake] ?? raw[camel];
  if (typeof val === 'string' && (allowed as readonly string[]).includes(val)) {
    return val as T;
  }
  if (warnLabel && val !== undefined && process.env.NODE_ENV !== 'production') {
    console.warn(`[api-mappers] ${warnLabel}: unexpected value "${String(val)}" at "${snake}"/"${camel}". Expected one of [${allowed.join(', ')}]. Using "${fallback}".`);
  }
  return fallback;
}

// ── Tenant mappers ────────────────────────────────────────────────────────────

const TENANT_TYPES:   readonly TenantType[]   = ['LawFirm', 'Provider', 'Funder', 'LienOwner', 'Corporate', 'Government', 'Other'];
const TENANT_STATUSES: readonly TenantStatus[] = ['Active', 'Inactive', 'Suspended'];

const ORG_TYPE_NORMALIZE: Record<string, TenantType> = {
  LAW_FIRM:  'LawFirm',
  PROVIDER:  'Provider',
  FUNDER:    'Funder',
  LIEN_OWNER: 'LienOwner',
  INTERNAL:  'Other',
};

function normalizeTenantType(r: Record<string, unknown>): TenantType {
  const val = (r['type'] as string) ?? '';
  const normalized = ORG_TYPE_NORMALIZE[val];
  if (normalized) return normalized;
  if ((TENANT_TYPES as readonly string[]).includes(val)) return val as TenantType;
  return 'Other';
}

/**
 * mapTenantSummary — normalises a raw backend tenant list item.
 *
 * TODO: replace manual mappers with generated types from OpenAPI spec
 */
const PROVISIONING_STATUSES: readonly ProvisioningStatus[] = ['Pending', 'InProgress', 'Provisioned', 'Verifying', 'Active', 'Failed'];
const PROVISIONING_FAILURE_STAGES: readonly ProvisioningFailureStage[] = ['None', 'DnsProvisioning', 'DnsVerification', 'HttpVerification'];

export function mapTenantSummary(raw: unknown): TenantSummary {
  const r = asObj(raw);
  const id = str(r, 'id', 'id', '', 'mapTenantSummary.id');
  return {
    id,
    code:               str(r, 'code',                 'code',               '',        'mapTenantSummary.code'),
    displayName:        str(r, 'display_name',          'displayName',         '',        'mapTenantSummary.displayName'),
    type:               normalizeTenantType(r),
    status:             oneOf(r, 'status',              'status',             TENANT_STATUSES, 'Inactive', 'mapTenantSummary.status'),
    primaryContactName: str(r, 'primary_contact_name',  'primaryContactName', '',        'mapTenantSummary.primaryContactName'),
    isActive:           bool(r, 'is_active',            'isActive',           false),
    userCount:          num(r,  'user_count',            'userCount',          0),
    orgCount:           num(r,  'org_count',             'orgCount',           0),
    createdAtUtc:       str(r, 'created_at',            'createdAtUtc',       new Date().toISOString()),
    subdomain:          optStr(r, 'subdomain',          'subdomain'),
    provisioningStatus: (r['provisioning_status'] ?? r['provisioningStatus']) as ProvisioningStatus | undefined
      ? oneOf(r, 'provisioning_status', 'provisioningStatus', PROVISIONING_STATUSES, 'Pending', 'mapTenantSummary.provisioningStatus')
      : undefined,
  };
}

/**
 * mapEntitlement — normalises a single product entitlement item.
 *
 * TODO: replace manual mappers with generated types from OpenAPI spec
 */
function mapEntitlement(raw: unknown): ProductEntitlementSummary {
  const r = asObj(raw);
  const PRODUCT_CODES: readonly ProductCode[] = [
    'SynqFund', 'SynqLien', 'SynqBill', 'SynqRx', 'SynqPayout', 'CareConnect',
  ];
  const ENTITLEMENT_STATUSES: readonly EntitlementStatus[] = ['Active', 'Disabled'];
  const enabled = bool(r, 'enabled', 'enabled', false);
  return {
    productCode:  oneOf(r, 'product_code',  'productCode',  PRODUCT_CODES,        'SynqFund', 'mapEntitlement.productCode'),
    productName:  str(r,  'product_name',   'productName',  '',                   'mapEntitlement.productName'),
    enabled,
    status:       oneOf(r, 'status',        'status',       ENTITLEMENT_STATUSES, enabled ? 'Active' : 'Disabled'),
    enabledAtUtc: optStr(r, 'enabled_at',   'enabledAtUtc'),
  };
}

/**
 * mapTenantDetail — normalises a raw backend tenant detail response.
 * Extends mapTenantSummary with the extra fields on TenantDetail.
 *
 * TODO: replace manual mappers with generated types from OpenAPI spec
 */
export function mapTenantDetail(raw: unknown): TenantDetail {
  const r    = asObj(raw);
  const base = mapTenantSummary(raw);
  const rawTimeout = r['sessionTimeoutMinutes'] ?? r['session_timeout_minutes'];
  return {
    ...base,
    email:           optStr(r, 'email', 'email'),
    updatedAtUtc:    str(r, 'updated_at',        'updatedAtUtc',    new Date().toISOString()),
    activeUserCount: num(r, 'active_user_count',  'activeUserCount', 0),
    linkedOrgCount:  r['linked_org_count']  !== undefined
                       ? num(r, 'linked_org_count',  'linkedOrgCount',  0)
                       : r['linkedOrgCount'] !== undefined
                         ? num(r, 'linked_org_count', 'linkedOrgCount', 0)
                         : undefined,
    sessionTimeoutMinutes: rawTimeout != null ? Number(rawTimeout) : undefined,
    logoDocumentId: (r['logoDocumentId'] ?? r['logo_document_id']) as string | undefined,
    logoWhiteDocumentId: (r['logoWhiteDocumentId'] ?? r['logo_white_document_id']) as string | undefined,
    productEntitlements: asArr(
      r['product_entitlements'] ?? r['productEntitlements'],
    ).map(mapEntitlement),
    lastProvisioningAttemptUtc: optStr(r, 'last_provisioning_attempt_utc', 'lastProvisioningAttemptUtc'),
    provisioningFailureReason: optStr(r, 'provisioning_failure_reason', 'provisioningFailureReason'),
    provisioningFailureStage: (r['provisioning_failure_stage'] ?? r['provisioningFailureStage']) as ProvisioningFailureStage | undefined
      ? oneOf(r, 'provisioning_failure_stage', 'provisioningFailureStage', PROVISIONING_FAILURE_STAGES, 'None', 'mapTenantDetail.provisioningFailureStage')
      : undefined,
    hostname: optStr(r, 'hostname', 'hostname'),
    verificationAttemptCount: typeof (r['verification_attempt_count'] ?? r['verificationAttemptCount']) === 'number'
      ? (r['verification_attempt_count'] ?? r['verificationAttemptCount']) as number
      : undefined,
    lastVerificationAttemptUtc: optStr(r, 'last_verification_attempt_utc', 'lastVerificationAttemptUtc'),
    nextVerificationRetryAtUtc: optStr(r, 'next_verification_retry_at_utc', 'nextVerificationRetryAtUtc'),
    isVerificationRetryExhausted: typeof (r['is_verification_retry_exhausted'] ?? r['isVerificationRetryExhausted']) === 'boolean'
      ? (r['is_verification_retry_exhausted'] ?? r['isVerificationRetryExhausted']) as boolean
      : undefined,
  };
}

/**
 * mapEntitlementResponse — normalises a single entitlement response
 * (from the toggle endpoint).
 *
 * TODO: replace manual mappers with generated types from OpenAPI spec
 */
export function mapEntitlementResponse(raw: unknown): ProductEntitlementSummary {
  return mapEntitlement(raw);
}

// ── User mappers ──────────────────────────────────────────────────────────────

const USER_STATUSES: readonly UserStatus[] = ['Active', 'Inactive', 'Invited'];

/**
 * mapUserSummary — normalises a raw backend user list item.
 *
 * Handles:
 *   first_name / firstName → firstName
 *   last_name / lastName → lastName
 *   tenant_id / tenantId → tenantId
 *   tenant_code / tenantCode → tenantCode
 *   last_login_at / lastLoginAt / lastLoginAtUtc → lastLoginAtUtc
 *
 * TODO: replace manual mappers with generated types from OpenAPI spec
 */
export function mapUserSummary(raw: unknown): UserSummary {
  const r = asObj(raw);
  return {
    id:              str(r, 'id',            'id',           '',       'mapUserSummary.id'),
    firstName:       str(r, 'first_name',    'firstName',    '',       'mapUserSummary.firstName'),
    lastName:        str(r, 'last_name',     'lastName',     '',       'mapUserSummary.lastName'),
    email:           str(r, 'email',         'email',        '',       'mapUserSummary.email'),
    role:            str(r, 'role',          'role',         'User'),
    status:          oneOf(r, 'status',      'status',       USER_STATUSES, 'Inactive', 'mapUserSummary.status'),
    tenantId:        str(r, 'tenant_id',     'tenantId',     '',       'mapUserSummary.tenantId'),
    tenantCode:      str(r, 'tenant_code',   'tenantCode',   '',       'mapUserSummary.tenantCode'),
    lastLoginAtUtc:  optStr(r, 'last_login_at', 'lastLoginAtUtc')
                       ?? optStr(r, 'last_login_at_utc', 'lastLoginAtUtc'),
    primaryOrg: optStr(r, 'primary_org', 'primaryOrg'),
    groupCount: typeof (r['group_count'] ?? r['groupCount']) === 'number'
      ? (r['group_count'] ?? r['groupCount']) as number
      : undefined,
    userType: (r['user_type'] ?? r['userType']) as string | undefined,
  };
}

/**
 * mapUserDetail — normalises a raw backend user detail response.
 *
 * Handles:
 *   tenant_display_name / tenantDisplayName → tenantDisplayName
 *   created_at / createdAtUtc → createdAtUtc
 *   updated_at / updatedAtUtc → updatedAtUtc
 *   is_locked / isLocked → isLocked
 *   invite_sent_at / inviteSentAtUtc → inviteSentAtUtc
 *
 * TODO: replace manual mappers with generated types from OpenAPI spec
 */
export function mapUserDetail(raw: unknown): UserDetail {
  const r    = asObj(raw);
  const base = mapUserSummary(raw);

  const rawMemberships = asArr(r['memberships']);
  const rawGroups      = asArr(r['groups']);
  const rawRoles       = asArr(r['roles']);

  return {
    ...base,
    tenantDisplayName: str(r, 'tenant_display_name', 'tenantDisplayName', base.tenantCode || base.tenantId),
    createdAtUtc:      str(r, 'created_at',           'createdAtUtc',      new Date().toISOString()),
    updatedAtUtc:      str(r, 'updated_at',           'updatedAtUtc',      new Date().toISOString()),
    isLocked:          bool(r, 'is_locked',           'isLocked',          false),
    lockedAtUtc:       optStr(r, 'locked_at_utc',     'lockedAtUtc'),
    lastLoginAtUtc:    optStr(r, 'last_login_at_utc', 'lastLoginAtUtc')
                         ?? optStr(r, 'lastLoginAtUtc', 'last_login_at_utc'),
    sessionVersion:    typeof (r['sessionVersion'] ?? r['session_version']) === 'number'
                         ? (r['sessionVersion'] ?? r['session_version']) as number
                         : undefined,
    avatarDocumentId:  (r['avatarDocumentId'] ?? r['avatar_document_id']) as string | undefined,
    phone:             optStr(r, 'phone',             'phone'),
    inviteSentAtUtc:   optStr(r, 'invite_sent_at',    'inviteSentAtUtc'),
    memberships: rawMemberships.map(m => {
      const mo = asObj(m);
      return {
        membershipId:   str(mo, 'membership_id',   'membershipId',   ''),
        organizationId: str(mo, 'organization_id', 'organizationId', ''),
        orgName:        str(mo, 'org_name',         'orgName',        ''),
        memberRole:     str(mo, 'member_role',      'memberRole',     ''),
        isPrimary:      bool(mo, 'is_primary',      'isPrimary',      false),
        joinedAtUtc:    str(mo, 'joined_at_utc',    'joinedAtUtc',    ''),
      };
    }),
    groups: rawGroups.map(g => {
      const go = asObj(g);
      return {
        groupId:     str(go, 'group_id',    'groupId',    ''),
        groupName:   str(go, 'group_name',  'groupName',  ''),
        joinedAtUtc: str(go, 'joined_at_utc', 'joinedAtUtc', ''),
      };
    }),
    roles: rawRoles.map(ro => {
      const r2 = asObj(ro);
      return {
        roleId:       str(r2, 'role_id',       'roleId',       ''),
        roleName:     str(r2, 'role_name',      'roleName',     ''),
        assignmentId: str(r2, 'assignment_id',  'assignmentId', ''),
      };
    }),
  };
}

export function mapAccessGroupSummary(raw: unknown): AccessGroupSummary {
  const r = asObj(raw);
  return {
    id:              str(r, 'id',              'id',              ''),
    tenantId:        str(r, 'tenant_id',       'tenantId',        ''),
    name:            str(r, 'name',            'name',            ''),
    description:     optStr(r, 'description',  'description'),
    status:          str(r, 'status',          'status',          'Active') as AccessGroupSummary['status'],
    scopeType:       str(r, 'scope_type',      'scopeType',       'Tenant') as AccessGroupSummary['scopeType'],
    productCode:     optStr(r, 'product_code', 'productCode'),
    organizationId:  optStr(r, 'organization_id', 'organizationId'),
    createdAtUtc:    str(r, 'created_at_utc',  'createdAtUtc',    ''),
    updatedAtUtc:    str(r, 'updated_at_utc',  'updatedAtUtc',    ''),
  };
}

export function mapAccessGroupMember(raw: unknown): AccessGroupMember {
  const r = asObj(raw);
  return {
    id:               str(r, 'id',                'id',               ''),
    tenantId:         str(r, 'tenant_id',         'tenantId',         ''),
    groupId:          str(r, 'group_id',          'groupId',          ''),
    userId:           str(r, 'user_id',           'userId',           ''),
    membershipStatus: str(r, 'membership_status', 'membershipStatus', 'Active') as AccessGroupMember['membershipStatus'],
    addedAtUtc:       str(r, 'added_at_utc',      'addedAtUtc',       ''),
    removedAtUtc:     optStr(r, 'removed_at_utc',  'removedAtUtc'),
  };
}

export function mapGroupProductAccess(raw: unknown): GroupProductAccess {
  const r = asObj(raw);
  return {
    id:           str(r, 'id',            'id',           ''),
    tenantId:     str(r, 'tenant_id',     'tenantId',     ''),
    groupId:      str(r, 'group_id',      'groupId',      ''),
    productCode:  str(r, 'product_code',  'productCode',  ''),
    accessStatus: str(r, 'access_status', 'accessStatus', 'Granted') as GroupProductAccess['accessStatus'],
    grantedAtUtc: str(r, 'granted_at_utc','grantedAtUtc', ''),
    revokedAtUtc: optStr(r, 'revoked_at_utc', 'revokedAtUtc'),
  };
}

export function mapGroupRoleAssignment(raw: unknown): GroupRoleAssignment {
  const r = asObj(raw);
  return {
    id:               str(r, 'id',                'id',               ''),
    tenantId:         str(r, 'tenant_id',         'tenantId',         ''),
    groupId:          str(r, 'group_id',          'groupId',          ''),
    roleCode:         str(r, 'role_code',         'roleCode',         ''),
    productCode:      optStr(r, 'product_code',   'productCode'),
    organizationId:   optStr(r, 'organization_id','organizationId'),
    assignmentStatus: str(r, 'assignment_status', 'assignmentStatus', 'Active') as GroupRoleAssignment['assignmentStatus'],
    assignedAtUtc:    str(r, 'assigned_at_utc',   'assignedAtUtc',    ''),
    removedAtUtc:     optStr(r, 'removed_at_utc', 'removedAtUtc'),
  };
}

export function mapPermissionCatalogItem(raw: unknown): PermissionCatalogItem {
  const r = asObj(raw);
  return {
    id:          str(r, 'id',          'id',          ''),
    code:        str(r, 'code',        'code',        ''),
    name:        str(r, 'name',        'name',        ''),
    description: optStr(r, 'description', 'description'),
    category:    optStr(r, 'category',    'category'),
    productId:   str(r, 'product_id',  'productId',   ''),
    productName: str(r, 'product_name','productName',  ''),
    productCode: str(r, 'product_code','productCode',  ''),
    isActive:    bool(r, 'is_active',  'isActive',    true),
    updatedAtUtc: optStr(r, 'updated_at_utc', 'updatedAtUtc'),
  };
}

// ── Role mappers ──────────────────────────────────────────────────────────────

/**
 * mapPermission — normalises a single permission object.
 *
 * TODO: replace manual mappers with generated types from OpenAPI spec
 */
function mapPermission(raw: unknown): Permission {
  const r = asObj(raw);
  return {
    id:          str(r, 'id',          'id',          ''),
    key:         str(r, 'key',         'key',         ''),
    description: str(r, 'description', 'description', ''),
  };
}

/**
 * mapRoleSummary — normalises a raw backend role list item.
 *
 * Handles:
 *   user_count / userCount → userCount
 *   permissions (array of strings or Permission objects)
 *
 * TODO: replace manual mappers with generated types from OpenAPI spec
 */
export function mapRoleSummary(raw: unknown): RoleSummary {
  const r = asObj(raw);

  // permissions may be string[] (keys) or Permission[] (objects)
  const rawPerms = asArr(r['permissions']);
  const permissions: string[] = rawPerms.map(p => {
    if (typeof p === 'string') return p;
    const po = asObj(p);
    return str(po, 'key', 'key', '');
  }).filter(Boolean);

  const rawAllowedOrgTypes = r['allowedOrgTypes'] ?? r['allowed_org_types'];
  const allowedOrgTypes = Array.isArray(rawAllowedOrgTypes)
    ? (rawAllowedOrgTypes as unknown[]).map(v => String(v))
    : undefined;

  const scopeRaw = r['scope'] ?? r['roleScope'];
  const scope = typeof scopeRaw === 'string' && scopeRaw.length > 0 ? scopeRaw : undefined;

  return {
    id:              str(r, 'id',               'id',              '',    'mapRoleSummary.id'),
    name:            str(r, 'name',             'name',            '',    'mapRoleSummary.name'),
    description:     str(r, 'description',      'description',     ''),
    scope,
    isSystemRole:    bool(r, 'is_system_role',  'isSystemRole',    false),
    isProductRole:   bool(r, 'is_product_role', 'isProductRole',   false),
    productCode:     r['productCode'] as string | undefined ?? r['product_code'] as string | undefined,
    productName:     r['productName'] as string | undefined ?? r['product_name'] as string | undefined,
    allowedOrgTypes,
    userCount:       num(r, 'user_count',       'userCount',       0),
    capabilityCount: num(r, 'capability_count', 'capabilityCount', 0),
    permissions,
  };
}

export function mapAssignableRole(raw: unknown): import('@/types/control-center').AssignableRole {
  const r = asObj(raw);
  const rawAllowedOrgTypes = r['allowedOrgTypes'] ?? r['allowed_org_types'];
  const allowedOrgTypes = Array.isArray(rawAllowedOrgTypes)
    ? (rawAllowedOrgTypes as unknown[]).map(v => String(v))
    : null;

  return {
    id:              str(r, 'id',               'id',              ''),
    name:            str(r, 'name',             'name',            ''),
    description:     str(r, 'description',      'description',     ''),
    isSystemRole:    bool(r, 'is_system_role',  'isSystemRole',    false),
    isProductRole:   bool(r, 'is_product_role', 'isProductRole',   false),
    productCode:     (r['productCode'] ?? r['product_code'] ?? null) as string | null,
    productName:     (r['productName'] ?? r['product_name'] ?? null) as string | null,
    allowedOrgTypes,
    assignable:      bool(r, 'assignable',      'assignable',      true),
    disabledReason:  (r['disabledReason'] ?? r['disabled_reason'] ?? null) as string | null,
    isAssigned:      bool(r, 'is_assigned',     'isAssigned',      false),
  };
}

/**
 * mapRoleDetail — normalises a raw backend role detail response.
 *
 * Handles:
 *   created_at / createdAtUtc → createdAtUtc
 *   updated_at / updatedAtUtc → updatedAtUtc
 *   resolved_permissions / resolvedPermissions → resolvedPermissions
 *
 * TODO: replace manual mappers with generated types from OpenAPI spec
 */
export function mapRoleDetail(raw: unknown): RoleDetail {
  const r    = asObj(raw);
  const base = mapRoleSummary(raw);
  return {
    ...base,
    createdAtUtc: str(r, 'created_at',  'createdAtUtc', new Date().toISOString()),
    updatedAtUtc: str(r, 'updated_at',  'updatedAtUtc', new Date().toISOString()),
    resolvedPermissions: asArr(
      r['resolved_permissions'] ?? r['resolvedPermissions'],
    ).map(mapPermission),
  };
}

// ── UIX-005: Role capability assignment + effective permission mappers ─────────

/**
 * mapRoleCapabilityItem — maps a capability item returned from
 * GET /api/admin/roles/{id}/permissions.
 */
export function mapRoleCapabilityItem(raw: unknown): RoleCapabilityItem {
  const r    = asObj(raw);
  const base = mapPermissionCatalogItem(raw);
  return {
    ...base,
    assignedAtUtc:    str(r, 'assigned_at_utc',    'assignedAtUtc',    new Date().toISOString()),
    assignedByUserId: optStr(r, 'assigned_by_user_id', 'assignedByUserId') ?? null,
  };
}

/**
 * mapEffectivePermission — maps an effective permission item returned from
 * GET /api/admin/users/{id}/permissions.
 */
export function mapEffectivePermission(raw: unknown): EffectivePermission {
  const r    = asObj(raw);
  const base = mapPermissionCatalogItem(raw);

  const sources: PermissionSource[] = asArr(r['sources']).map(s => {
    const so = asObj(s);
    return {
      type: (str(so, 'type', 'type', 'role') as 'role' | 'group'),
      name: str(so, 'name', 'name', ''),
    };
  });

  return { ...base, sources };
}

/**
 * mapEffectivePermissionsResult — maps the full response from
 * GET /api/admin/users/{id}/permissions.
 */
export function mapEffectivePermissionsResult(raw: unknown): EffectivePermissionsResult {
  const r = asObj(raw);
  return {
    items:      asArr(r['items']).map(mapEffectivePermission),
    totalCount: num(r, 'total_count', 'totalCount', 0),
    roleCount:  num(r, 'role_count',  'roleCount',  0),
  };
}

// ── Access Debug mapper (LS-COR-AUT-008) ─────────────────────────────────────

export function mapAccessDebugResult(raw: unknown): AccessDebugResult {
  const r = asObj(raw);
  return {
    userId:        str(r, 'userId', 'user_id', ''),
    tenantId:      str(r, 'tenantId', 'tenant_id', ''),
    accessVersion: num(r, 'accessVersion', 'access_version', 0),
    products: asArr(r['products']).map((p) => {
      const o = asObj(p);
      return {
        productCode: str(o, 'productCode', 'product_code', ''),
        source:      str(o, 'source', 'source', ''),
        groupId:     strOrNull(o, 'groupId', 'group_id'),
        groupName:   strOrNull(o, 'groupName', 'group_name'),
      };
    }),
    roles: asArr(r['roles']).map((p) => {
      const o = asObj(p);
      return {
        roleCode:    str(o, 'roleCode', 'role_code', ''),
        productCode: strOrNull(o, 'productCode', 'product_code'),
        source:      str(o, 'source', 'source', ''),
        groupId:     strOrNull(o, 'groupId', 'group_id'),
        groupName:   strOrNull(o, 'groupName', 'group_name'),
      };
    }),
    systemRoles: asArr(r['systemRoles'] ?? r['system_roles']).map((p) => {
      const o = asObj(p);
      return {
        roleName:  str(o, 'roleName', 'role_name', ''),
        scopeType: str(o, 'scopeType', 'scope_type', ''),
      };
    }),
    groups: asArr(r['groups']).map((p) => {
      const o = asObj(p);
      return {
        groupId:     str(o, 'groupId', 'group_id', ''),
        groupName:   str(o, 'groupName', 'group_name', ''),
        status:      str(o, 'status', 'status', ''),
        scopeType:   str(o, 'scopeType', 'scope_type', ''),
        productCode: strOrNull(o, 'productCode', 'product_code'),
      };
    }),
    entitlements: asArr(r['entitlements']).map((p) => {
      const o = asObj(p);
      return {
        productCode: str(o, 'productCode', 'product_code', ''),
        status:      str(o, 'status', 'status', ''),
      };
    }),
    productRolesFlat: asArr(r['productRolesFlat'] ?? r['product_roles_flat']).map((v) => String(v ?? '')),
    tenantRoles:      asArr(r['tenantRoles'] ?? r['tenant_roles']).map((v) => String(v ?? '')),
    permissions:      asArr(r['permissions']).map((v) => String(v ?? '')),
    permissionSources: asArr(r['permissionSources'] ?? r['permission_sources']).map((p) => {
      const o = asObj(p);
      return {
        permissionCode: str(o, 'permissionCode', 'permission_code', ''),
        productCode:    str(o, 'productCode', 'product_code', ''),
        source:         str(o, 'source', 'source', ''),
        viaRoleCode:    strOrNull(o, 'viaRoleCode', 'via_role_code') ?? undefined,
        groupId:        strOrNull(o, 'groupId', 'group_id') ?? undefined,
        groupName:      strOrNull(o, 'groupName', 'group_name') ?? undefined,
      };
    }),
  };
}

// ── Audit log mapper ──────────────────────────────────────────────────────────

const ACTOR_TYPES: readonly ActorType[] = ['Admin', 'System'];

/**
 * mapAuditLog — normalises a raw backend audit log entry.
 *
 * Handles:
 *   actor_name / actorName → actorName
 *   actor_type / actorType → actorType
 *   entity_type / entityType → entityType
 *   entity_id / entityId → entityId
 *   created_at / createdAt / createdAtUtc → createdAtUtc
 *
 * TODO: replace manual mappers with generated types from OpenAPI spec
 */
export function mapAuditLog(raw: unknown): AuditLogEntry {
  const r = asObj(raw);
  const rawMeta = r['metadata'] ?? r['meta'];
  const metadata: Record<string, unknown> | undefined =
    rawMeta !== null && typeof rawMeta === 'object' && !Array.isArray(rawMeta)
      ? (rawMeta as Record<string, unknown>)
      : undefined;
  return {
    id:          str(r, 'id',          'id',          '',      'mapAuditLog.id'),
    actorName:   str(r, 'actor_name',  'actorName',   '',      'mapAuditLog.actorName'),
    actorType:   oneOf(r, 'actor_type', 'actorType',  ACTOR_TYPES, 'Admin', 'mapAuditLog.actorType'),
    action:      str(r, 'action',      'action',      ''),
    entityType:  str(r, 'entity_type', 'entityType',  ''),
    entityId:    str(r, 'entity_id',   'entityId',    ''),
    metadata,
    createdAtUtc: str(r, 'created_at', 'createdAtUtc', new Date().toISOString()),
  };
}

// ── UIX-004: Audit event label mapping ───────────────────────────────────────

/**
 * Maps raw backend eventType codes to readable admin-facing labels.
 * Used by the global audit page and user activity panel.
 */
export const AUDIT_EVENT_LABELS: Record<string, string> = {
  // Auth / Security
  'identity.user.login':                    'Login successful',
  'identity.user.login_failed':             'Login failed',
  'identity.user.login.blocked':            'Login blocked',
  'identity.user.logout':                   'Logged out',
  'identity.user.password_changed':         'Password changed',
  'identity.user.password_reset_triggered': 'Password reset triggered',
  'identity.user.password_reset_completed': 'Password reset completed',
  // Account lifecycle
  'identity.user.invited':                  'User invited',
  'identity.user.invite_resent':            'Invite resent',
  'identity.user.invite_accepted':          'Invite accepted',
  'identity.user.activated':                'Account activated',
  'identity.user.deactivated':              'Account deactivated',
  // Security admin actions
  'identity.user.locked':                   'Account locked',
  'identity.user.unlocked':                 'Account unlocked',
  'identity.user.force_logout':             'Force logged out',
  'identity.user.session_revoked':          'Sessions revoked',
  // Access control
  'identity.user.role_assigned':            'Role assigned',
  'identity.user.role_revoked':             'Role revoked',
  'identity.user.membership_added':         'Membership added',
  'identity.user.membership_removed':       'Membership removed',
  'identity.user.primary_membership_changed': 'Primary membership changed',
  'identity.user.group_membership_added':   'Group membership added',
  'identity.user.group_membership_removed': 'Group membership removed',
  // Tenant / platform
  'platform.admin.tenant.created':          'Tenant created',
  'platform.admin.tenant.entitlement.updated': 'Product entitlement updated',
};

/** Maps an eventType code to a readable label, falling back to the raw code. */
export function mapEventLabel(eventType: string): string {
  return AUDIT_EVENT_LABELS[eventType] ?? eventType
    .replace(/^identity\.|^platform\./, '')
    .replace(/\./g, ' ')
    .replace(/_/g, ' ')
    .replace(/\b\w/g, c => c.toUpperCase());
}

/**
 * Maps a CanonicalAuditEvent to a UserActivityEvent for display in panels.
 * Handles missing fields gracefully.
 */
export function mapUserActivityEvent(raw: unknown): UserActivityEvent {
  const r = asObj(raw);
  const eventType = str(r, 'eventType', 'event_type', '');
  const actor     = asObj(r['actor'] ?? {});
  return {
    id:           str(r, 'auditId', 'id', ''),
    label:        mapEventLabel(eventType),
    eventType,
    category:     str(r, 'eventCategory', 'category', 'Administrative'),
    actorLabel:   str(actor, 'name', 'actorLabel', '') || str(r, 'actorLabel', 'actorName', 'System'),
    actorType:    str(actor, 'type', 'actorType', 'System'),
    occurredAtUtc: str(r, 'occurredAtUtc', 'createdAtUtc', new Date().toISOString()),
    description:  (r['description'] as string | undefined) || undefined,
    ipAddress:    (actor['ipAddress'] ?? r['ipAddress']) as string | undefined,
  };
}

// ── Settings mapper ───────────────────────────────────────────────────────────

const SETTING_TYPES: readonly PlatformSetting['type'][] = ['boolean', 'string', 'number'];

/**
 * mapSetting — normalises a raw backend platform setting.
 *
 * Handles:
 *   type coercion for value (string → boolean/number as needed)
 *
 * TODO: replace manual mappers with generated types from OpenAPI spec
 */
export function mapSetting(raw: unknown): PlatformSetting {
  const r = asObj(raw);
  const key      = str(r, 'key',   'key',   '',      'mapSetting.key');
  const rawType  = oneOf(r, 'type', 'type',  SETTING_TYPES, 'string', 'mapSetting.type');
  const rawValue = r['value'];

  // Coerce value to the declared type
  let value: string | number | boolean;
  if (rawType === 'boolean') {
    value = bool(r, 'value', 'value', false);
  } else if (rawType === 'number') {
    value = num(r, 'value', 'value', 0);
  } else {
    value = typeof rawValue === 'string' ? rawValue : String(rawValue ?? '');
  }

  return {
    key,
    label:       str(r, 'label',       'label',       key),
    value,
    type:        rawType,
    description: optStr(r, 'description', 'description'),
    editable:    bool(r, 'editable',    'editable',    false),
  };
}

// ── Monitoring mapper ─────────────────────────────────────────────────────────

const MONITORING_STATUSES: readonly MonitoringStatus[] = ['Healthy', 'Degraded', 'Down'];
const ALERT_SEVERITIES:    readonly AlertSeverity[]    = ['Info', 'Warning', 'Critical'];

/**
 * mapSystemHealth — normalises a raw system health object.
 */
function mapSystemHealth(raw: unknown): SystemHealthSummary {
  const r = asObj(raw);
  return {
    status:           oneOf(r, 'status',           'status',           MONITORING_STATUSES, 'Healthy'),
    lastCheckedAtUtc: str(r, 'last_checked_at',    'lastCheckedAtUtc', new Date().toISOString()),
  };
}

/**
 * mapIntegration — normalises a single integration status row.
 */
function mapIntegration(raw: unknown): IntegrationStatus {
  const r         = asObj(raw);
  const latencyRaw = r['latency_ms'] ?? r['latencyMs'];
  return {
    name:             str(r, 'name',            'name',            ''),
    status:           oneOf(r, 'status',        'status',          MONITORING_STATUSES, 'Healthy'),
    latencyMs:        typeof latencyRaw === 'number' && isFinite(latencyRaw) ? latencyRaw : undefined,
    lastCheckedAtUtc: str(r, 'last_checked_at', 'lastCheckedAtUtc', new Date().toISOString()),
  };
}

/**
 * mapAlert — normalises a single system alert.
 */
function mapAlert(raw: unknown): SystemAlert {
  const r = asObj(raw);
  return {
    id:           str(r, 'id',          'id',           ''),
    message:      str(r, 'message',     'message',      ''),
    severity:     oneOf(r, 'severity',  'severity',     ALERT_SEVERITIES, 'Info'),
    createdAtUtc: str(r, 'created_at',  'createdAtUtc', new Date().toISOString()),
  };
}

/**
 * mapMonitoring — normalises a raw backend monitoring summary response.
 *
 * Handles:
 *   system.last_checked_at / lastCheckedAtUtc
 *   integrations[].latency_ms / latencyMs
 *   alerts[].created_at / createdAtUtc
 *
 * TODO: replace manual mappers with generated types from OpenAPI spec
 */
export function mapMonitoring(raw: unknown): MonitoringSummary {
  const r = asObj(raw);
  return {
    system:       mapSystemHealth(r['system'] ?? {}),
    integrations: asArr(r['integrations']).map(mapIntegration),
    alerts:       asArr(r['alerts']).map(mapAlert),
  };
}

// ── Support mappers ───────────────────────────────────────────────────────────

const SUPPORT_STATUSES:   readonly SupportCaseStatus[]   = ['Open', 'Investigating', 'Resolved', 'Closed'];
const SUPPORT_PRIORITIES: readonly SupportCasePriority[] = ['Low', 'Medium', 'High'];

/**
 * Maps a raw TicketStatus value from the Support service to the CC's SupportCaseStatus.
 *
 * Support service statuses: Open, Pending, InProgress, Resolved, Closed, Cancelled
 * CC SupportCaseStatus:     Open, Investigating, Resolved, Closed
 */
function mapTicketStatus(raw: unknown): SupportCaseStatus {
  switch (raw) {
    case 'Open':        return 'Open';
    case 'Pending':     return 'Open';
    case 'InProgress':  return 'Investigating';
    case 'Resolved':    return 'Resolved';
    case 'Closed':      return 'Closed';
    case 'Cancelled':   return 'Closed';
    default:
      if (SUPPORT_STATUSES.includes(raw as SupportCaseStatus)) return raw as SupportCaseStatus;
      return 'Open';
  }
}

/**
 * Maps a raw TicketPriority value from the Support service to the CC's SupportCasePriority.
 *
 * Support service priorities: Low, Normal, High, Urgent
 * CC SupportCasePriority:     Low, Medium, High
 */
function mapTicketPriority(raw: unknown): SupportCasePriority {
  switch (raw) {
    case 'Low':    return 'Low';
    case 'Normal': return 'Medium';
    case 'High':   return 'High';
    case 'Urgent': return 'High';
    default:
      if (SUPPORT_PRIORITIES.includes(raw as SupportCasePriority)) return raw as SupportCasePriority;
      return 'Medium';
  }
}

/**
 * mapSupportCase — normalises a raw backend support ticket (list item).
 *
 * Handles both the legacy identity-service shape and the real Support service shape:
 *   id → id
 *   title → title
 *   tenant_id / tenantId → tenantId
 *   tenant_name / tenantName → tenantName   (Support service has no tenantName — defaults to '')
 *   requester_user_id / requesterUserId / user_id / userId → userId
 *   requester_name / requesterName / user_name / userName → userName
 *   status (mapped via mapTicketStatus)
 *   category → category
 *   priority (mapped via mapTicketPriority)
 *   created_at / createdAt / createdAtUtc → createdAtUtc
 *   updated_at / updatedAt / updatedAtUtc → updatedAtUtc
 *
 * TODO: replace manual mappers with generated types from OpenAPI spec
 */
export function mapSupportCase(raw: unknown): SupportCase {
  const r   = asObj(raw);
  const now = new Date().toISOString();

  const userId = (
    r['requesterUserId'] ?? r['requester_user_id'] ?? r['userId'] ?? r['user_id']
  );
  const userName = (
    r['requesterName'] ?? r['requester_name'] ?? r['userName'] ?? r['user_name']
  );
  const createdRaw = (
    r['createdAt'] ?? r['created_at'] ?? r['createdAtUtc']
  );
  const updatedRaw = (
    r['updatedAt'] ?? r['updated_at'] ?? r['updatedAtUtc']
  );

  const requesterEmail = (
    r['requesterEmail'] ?? r['requester_email']
  );
  const assignedUserId = (
    r['assignedUserId'] ?? r['assigned_user_id']
  );
  const updatedByUserId = (
    r['updatedByUserId'] ?? r['updated_by_user_id']
  );

  return {
    id:               str(r, 'id',          'id',         '',  'mapSupportCase.id'),
    title:            str(r, 'title',       'title',      ''),
    tenantId:         str(r, 'tenant_id',   'tenantId',   ''),
    tenantName:       str(r, 'tenant_name', 'tenantName', ''),
    userId:           typeof userId   === 'string' && userId.length   > 0 ? userId   : undefined,
    userName:         typeof userName === 'string' && userName.length > 0 ? userName : undefined,
    requesterEmail:   typeof requesterEmail   === 'string' && requesterEmail.length   > 0 ? requesterEmail   : undefined,
    status:           mapTicketStatus(r['status']),
    category:         str(r, 'category', 'category', ''),
    priority:         mapTicketPriority(r['priority']),
    assignedUserId:   typeof assignedUserId   === 'string' && assignedUserId.length   > 0 ? assignedUserId   : undefined,
    createdAtUtc:     typeof createdRaw === 'string' && createdRaw.length > 0 ? createdRaw : now,
    updatedAtUtc:     typeof updatedRaw === 'string' && updatedRaw.length > 0 ? updatedRaw : now,
    updatedByUserId:  typeof updatedByUserId === 'string' && updatedByUserId.length > 0 ? updatedByUserId : undefined,
  };
}

/**
 * mapSupportNote — normalises a single support case note / comment.
 *
 * Handles both the legacy note shape and the real CommentResponse shape:
 *   id → id
 *   ticketId / ticket_id / caseId / case_id → caseId
 *   body / message → message   (backend CommentResponse uses `body`)
 *   authorName / author_name / createdBy / created_by → createdBy
 *   createdAt / created_at / createdAtUtc → createdAtUtc
 *
 * TODO: replace manual mappers with generated types from OpenAPI spec
 */
export function mapSupportNote(raw: unknown): SupportNote {
  const r = asObj(raw);

  const caseId =
    (typeof r['ticketId']  === 'string' && r['ticketId'].length  > 0) ? (r['ticketId']  as string) :
    (typeof r['ticket_id'] === 'string' && r['ticket_id'].length > 0) ? (r['ticket_id'] as string) :
    str(r, 'case_id', 'caseId', '');

  const message =
    (typeof r['body']    === 'string' && r['body'].length    > 0) ? (r['body']    as string) :
    (typeof r['message'] === 'string' && r['message'].length > 0) ? (r['message'] as string) :
    '';

  const createdBy =
    (typeof r['authorName']  === 'string' && r['authorName'].length  > 0) ? (r['authorName']  as string) :
    (typeof r['author_name'] === 'string' && r['author_name'].length > 0) ? (r['author_name'] as string) :
    (typeof r['authorEmail'] === 'string' && r['authorEmail'].length > 0) ? (r['authorEmail'] as string) :
    str(r, 'created_by', 'createdBy', 'Platform Admin');

  const authorUserId =
    (typeof r['authorUserId']  === 'string' && r['authorUserId'].length  > 0) ? (r['authorUserId']  as string) :
    (typeof r['author_user_id'] === 'string' && r['author_user_id'].length > 0) ? (r['author_user_id'] as string) :
    undefined;

  const authorEmail =
    (typeof r['authorEmail']  === 'string' && r['authorEmail'].length  > 0) ? (r['authorEmail']  as string) :
    (typeof r['author_email'] === 'string' && r['author_email'].length > 0) ? (r['author_email'] as string) :
    undefined;

  return {
    id:           str(r, 'id',         'id',          ''),
    caseId,
    message,
    createdBy,
    authorUserId,
    authorEmail,
    createdAtUtc: str(r, 'created_at', 'createdAt', new Date().toISOString()),
    visibility:   str(r, 'visibility',   'visibility',   'Internal'),
    commentType:  str(r, 'commentType',  'comment_type', 'Internal'),
  };
}

/**
 * mapSupportProductRef — normalises a raw product reference entry.
 *
 * Handles:
 *   ticket_id / ticketId → ticketId
 *   product_code / productCode → productCode
 *   entity_type / entityType → entityType
 *   entity_id / entityId → entityId
 *   display_label / displayLabel → displayLabel
 *   metadata_json / metadataJson → metadataJson
 *   created_by_user_id / createdByUserId → createdByUserId
 *   created_at / createdAt → createdAt
 *
 * TODO: replace manual mappers with generated types from OpenAPI spec
 */
export function mapSupportProductRef(raw: unknown): SupportProductRef {
  const r = asObj(raw);
  return {
    id:              str(r, 'id',                   'id',                '',  'mapSupportProductRef.id'),
    ticketId:        str(r, 'ticket_id',             'ticketId',          ''),
    productCode:     str(r, 'product_code',          'productCode',       ''),
    entityType:      str(r, 'entity_type',           'entityType',        ''),
    entityId:        str(r, 'entity_id',             'entityId',          ''),
    displayLabel:    optStr(r, 'display_label',      'displayLabel'),
    metadataJson:    optStr(r, 'metadata_json',      'metadataJson'),
    createdByUserId: optStr(r, 'created_by_user_id', 'createdByUserId'),
    createdAt:       str(r, 'created_at',            'createdAt',         new Date().toISOString()),
  };
}

/**
 * mapTicketAttachment — normalises a raw ticket attachment entry.
 */
export function mapTicketAttachment(raw: unknown): import('@/types/control-center').TicketAttachmentItem {
  const r = asObj(raw);
  return {
    id:                str(r, 'id',                   'id',                '',  'mapTicketAttachment.id'),
    ticketId:          str(r, 'ticket_id',             'ticketId',          ''),
    documentId:        str(r, 'document_id',           'documentId',        ''),
    fileName:          str(r, 'file_name',             'fileName',          ''),
    contentType:       optStr(r, 'content_type',       'contentType'),
    fileSizeBytes:     typeof r['file_size_bytes'] === 'number' ? r['file_size_bytes'] as number
                       : typeof r['fileSizeBytes'] === 'number' ? r['fileSizeBytes'] as number
                       : undefined,
    uploadedByUserId:  optStr(r, 'uploaded_by_user_id', 'uploadedByUserId'),
    createdAt:         str(r, 'created_at',            'createdAt',         new Date().toISOString()),
  };
}

/**
 * mapSupportCaseDetail — normalises a full support case detail response.
 * Extends mapSupportCase with the notes and productRefs arrays.
 *
 * productRefs are optionally provided (pre-fetched in parallel by the API client).
 *
 * TODO: replace manual mappers with generated types from OpenAPI spec
 */
export function mapSupportCaseDetail(raw: unknown, rawProductRefs?: unknown[]): SupportCaseDetail {
  const r    = asObj(raw);
  const base = mapSupportCase(raw);
  return {
    ...base,
    notes:       asArr(r['notes']).map(mapSupportNote),
    productRefs: (rawProductRefs ?? []).map(mapSupportProductRef),
  };
}

// ── Organization Type mapper (Phase E) ───────────────────────────────────────

/**
 * mapOrganizationTypeItem — normalises a raw backend OrganizationType catalog entry.
 * Handles: is_active/isActive, created_at/createdAtUtc
 */
export function mapOrganizationTypeItem(raw: unknown): OrganizationTypeItem {
  const r = asObj(raw);
  return {
    id:          str(r, 'id',           'id',           '', 'mapOrganizationTypeItem.id'),
    code:        str(r, 'code',         'code',         ''),
    name:        str(r, 'name',         'name',         ''),
    description: str(r, 'description',  'description',  ''),
    isActive:    bool(r, 'is_active',   'isActive',     true),
    createdAtUtc: str(r, 'created_at',  'createdAtUtc', new Date().toISOString()),
  };
}

// ── Relationship Type mapper (Phase E) ────────────────────────────────────────

/**
 * mapRelationshipTypeItem — normalises a raw backend RelationshipType catalog entry.
 */
export function mapRelationshipTypeItem(raw: unknown): RelationshipTypeItem {
  const r = asObj(raw);
  return {
    id:          str(r, 'id',           'id',           '', 'mapRelationshipTypeItem.id'),
    code:        str(r, 'code',         'code',         ''),
    name:        str(r, 'name',         'name',         ''),
    description: str(r, 'description',  'description',  ''),
    isActive:    bool(r, 'is_active',   'isActive',     true),
    createdAtUtc: str(r, 'created_at',  'createdAtUtc', new Date().toISOString()),
  };
}

// ── Organization Relationship mapper (Phase E) ────────────────────────────────

const ORG_REL_STATUSES: readonly OrgRelationshipStatus[] = ['Active', 'Inactive', 'Pending'];

/**
 * mapOrgRelationship — normalises a raw backend OrganizationRelationship entry.
 * Handles snake_case/camelCase for all FK and timestamp fields.
 */
export function mapOrgRelationship(raw: unknown): OrgRelationship {
  const r = asObj(raw);
  return {
    id:                   str(r, 'id',                      'id',                   '', 'mapOrgRelationship.id'),
    sourceOrganizationId: str(r, 'source_organization_id',  'sourceOrganizationId', ''),
    targetOrganizationId: str(r, 'target_organization_id',  'targetOrganizationId', ''),
    relationshipTypeId:   str(r, 'relationship_type_id',    'relationshipTypeId',   ''),
    relationshipTypeCode: str(r, 'relationship_type_code',  'relationshipTypeCode', ''),
    status:               oneOf(r, 'status', 'status', ORG_REL_STATUSES, 'Inactive', 'mapOrgRelationship.status'),
    effectiveFromUtc:     optStr(r, 'effective_from', 'effectiveFromUtc'),
    effectiveToUtc:       optStr(r, 'effective_to',   'effectiveToUtc'),
    createdAtUtc:         str(r, 'created_at',  'createdAtUtc', new Date().toISOString()),
    updatedAtUtc:         str(r, 'updated_at',  'updatedAtUtc', new Date().toISOString()),
  };
}

// ── Product–OrgType Rule mapper (Phase E) ────────────────────────────────────

/**
 * mapProductOrgTypeRule — normalises a raw backend ProductOrganizationTypeRule entry.
 */
export function mapProductOrgTypeRule(raw: unknown): ProductOrgTypeRule {
  const r = asObj(raw);
  return {
    id:                   str(r, 'id',                     'id',                   '', 'mapProductOrgTypeRule.id'),
    productId:            str(r, 'product_id',             'productId',            ''),
    productCode:          str(r, 'product_code',           'productCode',          ''),
    productRoleId:        str(r, 'product_role_id',        'productRoleId',        ''),
    productRoleCode:      str(r, 'product_role_code',      'productRoleCode',      ''),
    productRoleName:      str(r, 'product_role_name',      'productRoleName',      ''),
    organizationTypeId:   str(r, 'organization_type_id',   'organizationTypeId',   ''),
    organizationTypeCode: str(r, 'organization_type_code', 'organizationTypeCode', ''),
    organizationTypeName: str(r, 'organization_type_name', 'organizationTypeName', ''),
    isActive:             bool(r, 'is_active',             'isActive',             true),
    createdAtUtc:         str(r, 'created_at',             'createdAtUtc',         new Date().toISOString()),
  };
}

// ── Product–RelType Rule mapper (Phase E) ─────────────────────────────────────

/**
 * mapProductRelTypeRule — normalises a raw backend ProductRelationshipTypeRule entry.
 */
export function mapProductRelTypeRule(raw: unknown): ProductRelTypeRule {
  const r = asObj(raw);
  return {
    id:                   str(r, 'id',                     'id',                   '', 'mapProductRelTypeRule.id'),
    productId:            str(r, 'product_id',             'productId',            ''),
    productCode:          str(r, 'product_code',           'productCode',          ''),
    relationshipTypeId:   str(r, 'relationship_type_id',   'relationshipTypeId',   ''),
    relationshipTypeCode: str(r, 'relationship_type_code', 'relationshipTypeCode', ''),
    relationshipTypeName: str(r, 'relationship_type_name', 'relationshipTypeName', ''),
    isActive:             bool(r, 'is_active',             'isActive',             true),
    createdAtUtc:         str(r, 'created_at',             'createdAtUtc',         new Date().toISOString()),
  };
}

// ── Legacy Coverage mapper (Step 4) ───────────────────────────────────────────

/**
 * mapLegacyCoverageReport — normalises a raw backend legacy-coverage response.
 * Returned by GET /identity/api/admin/legacy-coverage.
 *
 * Phase G update: roleAssignments now uses the retired dual-write shape.
 * Legacy fields (usersWithLegacyRoles, usersWithGapCount, dualWriteCoveragePct)
 * are no longer emitted by the backend; Phase G fields are read instead.
 */
export function mapLegacyCoverageReport(raw: unknown): LegacyCoverageReport {
  const r = asObj(raw);

  const erRaw = asObj(r['eligibilityRules'] ?? r['eligibility_rules'] ?? {});
  const raRaw = asObj(r['roleAssignments']  ?? r['role_assignments']  ?? {});

  const uncoveredRaw = Array.isArray(erRaw['uncoveredRoles'] ?? erRaw['uncovered_roles'])
    ? (erRaw['uncoveredRoles'] ?? erRaw['uncovered_roles']) as unknown[]
    : [];

  return {
    generatedAtUtc: str(r, 'generated_at_utc', 'generatedAtUtc', new Date().toISOString()),

    eligibilityRules: {
      totalActiveProductRoles: num(erRaw, 'total_active_product_roles', 'totalActiveProductRoles', 0),
      withDbRuleOnly:          num(erRaw, 'with_db_rule_only',          'withDbRuleOnly',          0),
      withBothPaths:           num(erRaw, 'with_both_paths',            'withBothPaths',            0),
      legacyStringOnly:        num(erRaw, 'legacy_string_only',         'legacyStringOnly',         0),
      unrestricted:            num(erRaw, 'unrestricted',               'unrestricted',             0),
      dbCoveragePct:           num(erRaw, 'db_coverage_pct',            'dbCoveragePct',            100),
      uncoveredRoles: uncoveredRaw.map(u => {
        const ur = asObj(u);
        return {
          code:            str(ur, 'code',              'code',            ''),
          eligibleOrgType: str(ur, 'eligible_org_type', 'eligibleOrgType', ''),
        };
      }),
    },

    // Phase G shape — dual-write fields retired; SRA is sole role source.
    roleAssignments: {
      userRolesRetired:             bool(raRaw, 'user_roles_retired',               'userRolesRetired',             true),
      usersWithScopedRoles:         num(raRaw,  'users_with_scoped_roles',          'usersWithScopedRoles',         0),
      totalActiveScopedAssignments: num(raRaw,  'total_active_scoped_assignments',  'totalActiveScopedAssignments', 0),
    },
  };
}

// ── Platform Readiness mapper (Phase 8) ───────────────────────────────────────

/**
 * mapPlatformReadiness — normalises a raw platform-readiness response.
 * Returned by GET /identity/api/admin/platform-readiness.
 */
export function mapPlatformReadiness(raw: unknown): PlatformReadinessSummary {
  const r    = asObj(raw);
  const pgRaw = asObj(r['phaseGCompletion']       ?? r['phase_g_completion']       ?? {});
  const otRaw = asObj(r['orgTypeCoverage']         ?? r['org_type_coverage']        ?? {});
  const prRaw = asObj(r['productRoleEligibility']  ?? r['product_role_eligibility'] ?? {});
  const orRaw = asObj(r['orgRelationships']        ?? r['org_relationships']        ?? {});

  return {
    generatedAtUtc: str(r, 'generated_at_utc', 'generatedAtUtc', new Date().toISOString()),

    phaseGCompletion: {
      userRolesRetired:             bool(pgRaw, 'user_roles_retired',               'userRolesRetired',             true),
      soleRoleSourceIsSra:          bool(pgRaw, 'sole_role_source_is_sra',          'soleRoleSourceIsSra',          true),
      totalActiveScopedAssignments: num(pgRaw,  'total_active_scoped_assignments',  'totalActiveScopedAssignments', 0),
      globalScopedAssignments:      num(pgRaw,  'global_scoped_assignments',        'globalScopedAssignments',      0),
      usersWithScopedRole:          num(pgRaw,  'users_with_scoped_role',           'usersWithScopedRole',          0),
    },

    orgTypeCoverage: {
      totalActiveOrgs:            num(otRaw,  'total_active_orgs',             'totalActiveOrgs',            0),
      orgsWithOrganizationTypeId: num(otRaw,  'orgs_with_organization_type_id','orgsWithOrganizationTypeId', 0),
      orgsWithMissingTypeId:      num(otRaw,  'orgs_with_missing_type_id',     'orgsWithMissingTypeId',      0),
      orgsWithCodeMismatch:       num(otRaw,  'orgs_with_code_mismatch',       'orgsWithCodeMismatch',       0),
      consistent:                 bool(otRaw, 'consistent',                    'consistent',                 true),
      coveragePct:                num(otRaw,  'coverage_pct',                  'coveragePct',                100),
    },

    productRoleEligibility: {
      totalActiveProductRoles: num(prRaw, 'total_active_product_roles', 'totalActiveProductRoles', 0),
      withOrgTypeRule:         num(prRaw, 'with_org_type_rule',         'withOrgTypeRule',         0),
      unrestricted:            num(prRaw, 'unrestricted',               'unrestricted',            0),
      coveragePct:             num(prRaw, 'coverage_pct',               'coveragePct',             100),
    },

    orgRelationships: {
      total:  num(orRaw, 'total',  'total',  0),
      active: num(orRaw, 'active', 'active', 0),
    },

    // Phase I: scoped assignment counts by scope type
    scopedAssignmentsByScope: (() => {
      const sb = asObj(r['scopedAssignmentsByScope'] ?? r['scoped_assignments_by_scope'] ?? {});
      return {
        global:       num(sb, 'global',       'global',       0),
        organization: num(sb, 'organization',  'organization', 0),
        product:      num(sb, 'product',       'product',      0),
        relationship: num(sb, 'relationship',  'relationship', 0),
        tenant:       num(sb, 'tenant',        'tenant',       0),
      };
    })(),
  };
}

// ── CareConnect Integrity mapper ──────────────────────────────────────────────

/**
 * mapCareConnectIntegrity — normalises the raw GET /careconnect/api/admin/integrity response.
 *
 * The backend never throws — failing queries produce -1 for that counter.
 * The mapper preserves -1 values so the UI can distinguish "0 issues" from
 * "query failed" and render an appropriate warning.
 */
export function mapCareConnectIntegrity(raw: unknown): CareConnectIntegrityReport {
  const r    = asObj(raw);
  const refs = asObj(r['referrals']    ?? {});
  const apps = asObj(r['appointments'] ?? {});
  const prov = asObj(r['providers']    ?? {});
  const facs = asObj(r['facilities']   ?? {});

  return {
    generatedAtUtc: str(r, 'generated_at_utc', 'generatedAtUtc', new Date().toISOString()),
    clean:          bool(r, 'clean', 'clean', false),

    referrals: {
      withOrgPairButNullRelationship: num(refs, 'with_org_pair_but_null_relationship',
                                          'withOrgPairButNullRelationship', -1),
    },

    appointments: {
      missingRelationshipWhereReferralHasOne: num(apps,
        'missing_relationship_where_referral_has_one',
        'missingRelationshipWhereReferralHasOne', -1),
    },

    providers: {
      withoutOrganizationId: num(prov, 'without_organization_id', 'withoutOrganizationId', -1),
    },

    facilities: {
      withoutOrganizationId: num(facs, 'without_organization_id', 'withoutOrganizationId', -1),
    },
  };
}

// ── ScopedRoleAssignment mapper ───────────────────────────────────────────────

/**
 * mapScopedRoleAssignment — normalises a single ScopedRoleAssignment record.
 * Returned per-item by GET /identity/api/admin/users/{id}/scoped-roles.
 */
export function mapScopedRoleAssignment(raw: unknown): ScopedRoleAssignment {
  const r = asObj(raw);
  return {
    id:             str(r, 'id',              'id',             ''),
    userId:         str(r, 'user_id',         'userId',         ''),
    roleId:         str(r, 'role_id',         'roleId',         ''),
    roleName:       str(r, 'role_name',       'roleName',       ''),
    scopeType:      str(r, 'scope_type',      'scopeType',      'Global'),
    scopeEntityId:  r['scope_entity_id'] as string | undefined
                    ?? r['scopeEntityId'] as string | undefined,
    isActive:       bool(r, 'is_active',      'isActive',       true),
    createdAtUtc:   str(r, 'created_at_utc',  'createdAtUtc',   new Date().toISOString()),
  };
}

// ── PagedResponse mapper ──────────────────────────────────────────────────────

/**
 * mapPagedResponse<T> — normalises a paged list response, applying `mapItem`
 * to each element in the `items` array.
 *
 * Handles:
 *   total_count / totalCount → totalCount
 *   page_size / pageSize → pageSize
 *
 * TODO: replace manual mappers with generated types from OpenAPI spec
 */
export function mapPagedResponse<T>(
  raw:     unknown,
  mapItem: (item: unknown) => T,
): PagedResponse<T> {
  const r = asObj(raw);
  // Unwrap ApiResponse<T> envelope ({ success, data }) if present
  const payload = (typeof r['success'] === 'boolean' && r['data'] !== undefined)
    ? asObj(r['data'])
    : r;
  // Support service returns `total`; Identity service returns `total_count`/`totalCount`
  const rawTotal = payload['total'] ?? payload['total_count'] ?? payload['totalCount'];
  const totalCount = typeof rawTotal === 'number' && isFinite(rawTotal) ? rawTotal : 0;
  return {
    items:      asArr(payload['items']).map(mapItem),
    totalCount,
    page:       num(payload, 'page',      'page',     1),
    pageSize:   num(payload, 'page_size', 'pageSize', 20),
  };
}

/**
 * unwrapApiResponse — extracts the `data` field from an ApiResponse envelope.
 * If the input is not an envelope, returns it as-is.
 */
export function unwrapApiResponse(raw: unknown): unknown {
  const r = asObj(raw);
  if (typeof r['success'] === 'boolean' && r['data'] !== undefined) {
    return r['data'];
  }
  return raw;
}

/**
 * unwrapApiResponseList — extracts a top-level array from an ApiResponse envelope.
 * Tries `data.items`, then `data` (if array), then `items`, then raw array.
 */
export function unwrapApiResponseList(raw: unknown): unknown[] {
  const r = asObj(raw);
  const data = r['success'] !== undefined ? asObj(r['data'] ?? {}) : r;
  if (Array.isArray(data['items'])) return data['items'] as unknown[];
  if (Array.isArray(r['data']))     return r['data'] as unknown[];
  if (Array.isArray(r['items']))    return r['items'] as unknown[];
  if (Array.isArray(raw))           return raw as unknown[];
  return [];
}

// ── CanonicalAuditEvent mapper ────────────────────────────────────────────────

/**
 * mapCanonicalAuditEvent — normalises a raw record from the Platform Audit Event
 * Service query API (GET /audit/events).
 *
 * Maps the AuditEventRecordResponse wire shape to the CanonicalAuditEvent frontend type.
 * Field name conventions: camelCase from the service (JsonNamingPolicy.CamelCase).
 */
export function mapCanonicalAuditEvent(raw: unknown): CanonicalAuditEvent {
  const r = asObj(raw);

  // Nested objects (camelCase from .NET JsonNamingPolicy.CamelCase)
  const actor  = asObj(r['actor']  ?? {});
  const entity = asObj(r['entity'] ?? {});
  const scope  = asObj(r['scope']  ?? {});

  // Actor fields: nested actor.id / actor.name take precedence, fallback to flat
  const actorId    = (str(actor, 'id',   'id',   '') || r['actorId']    as string | undefined) as string | undefined;
  const actorLabel = (str(actor, 'name', 'name', '') || r['actorLabel'] as string | undefined) as string | undefined;
  const actorType  = str(actor, 'type', 'actorType', '') || undefined;

  // Entity fields: nested entity.type / entity.id take precedence
  const targetType = (str(entity, 'type', 'type', '') || r['targetType'] as string | undefined) as string | undefined;
  const targetId   = (str(entity, 'id',   'id',   '') || r['targetId']   as string | undefined) as string | undefined;

  // Tenant: nested scope.tenantId takes precedence
  const tenantId = (str(scope, 'tenantId', 'tenantId', '') || r['tenantId'] as string | undefined) as string | undefined;

  // Severity and category may be enums — coerce to lowercase string
  const severityRaw  = str(r, 'severity',      'severity',      'info');
  const categoryRaw  = str(r, 'eventCategory', 'category',      '');

  // Primary id: prefer auditId (canonical backend field), fallback to id
  const id = str(r, 'auditId', 'id', '');

  // Tags may be an array
  const tagsRaw = r['tags'];
  const tags: string[] | undefined = Array.isArray(tagsRaw) ? (tagsRaw as string[]) : undefined;

  return {
    id,
    source:        str(r, 'sourceSystem', 'source', ''),
    sourceService: r['sourceService'] as string | undefined,
    eventType:     str(r, 'eventType', 'event_type', ''),
    category:      categoryRaw,
    severity:      severityRaw.toLowerCase(),
    tenantId:      tenantId || undefined,
    actorId:       actorId  || undefined,
    actorLabel:    actorLabel || undefined,
    actorType:     actorType,
    targetType:    targetType || undefined,
    targetId:      targetId  || undefined,
    action:        r['action']        as string | undefined,
    description:   str(r, 'description', 'description', ''),
    before:        r['before']        as string | undefined,
    after:         r['after']         as string | undefined,
    outcome:       str(r, 'outcome',  'outcome', ''),
    ipAddress:     (actor['ipAddress'] ?? r['ipAddress']) as string | undefined,
    correlationId: r['correlationId'] as string | undefined,
    requestId:     r['requestId']     as string | undefined,
    sessionId:     r['sessionId']     as string | undefined,
    metadata:      r['metadata']      as string | undefined,
    tags,
    occurredAtUtc: str(r, 'occurredAtUtc', 'occurred_at_utc', new Date().toISOString()),
    ingestedAtUtc: str(r, 'recordedAtUtc', 'ingestedAtUtc',   new Date().toISOString()),
    hash:          r['hash']          as string | undefined,
  };
}

// ── SynqAudit — Exports mapper ────────────────────────────────────────────────

export function mapAuditExport(raw: unknown): AuditExport {
  const outer = asObj(raw);
  const r = (typeof outer['success'] === 'boolean' && outer['data'] !== undefined)
    ? asObj(outer['data'])
    : outer;
  return {
    exportId:       str(r, 'exportId',       'export_id',        ''),
    status:         str(r, 'status',         'status',           'Pending') as AuditExport['status'],
    format:         str(r, 'format',         'format',           'Json'),
    recordCount:    r['recordCount']    as number | undefined,
    downloadUrl:    r['downloadUrl']    as string | undefined,
    createdAtUtc:   str(r, 'createdAtUtc',   'created_at_utc',   new Date().toISOString()),
    completedAtUtc: r['completedAtUtc'] as string | undefined,
    errorMessage:   r['errorMessage']   as string | undefined,
  };
}

// ── SynqAudit — Integrity Checkpoint mapper ───────────────────────────────────

export function mapIntegrityCheckpoint(raw: unknown): IntegrityCheckpoint {
  const outer = asObj(raw);
  const r = (typeof outer['success'] === 'boolean' && outer['data'] !== undefined)
    ? asObj(outer['data'])
    : outer;
  return {
    checkpointId:      str(r, 'checkpointId',      'checkpoint_id',        ''),
    checkpointType:    str(r, 'checkpointType',    'checkpoint_type',      ''),
    aggregateHash:     str(r, 'aggregateHash',     'aggregate_hash',       ''),
    recordCount:       (r['recordCount'] as number) ?? 0,
    isValid:           r['isValid']            as boolean | undefined,
    fromRecordedAtUtc: str(r, 'fromRecordedAtUtc', 'from_recorded_at_utc', new Date().toISOString()),
    toRecordedAtUtc:   str(r, 'toRecordedAtUtc',   'to_recorded_at_utc',   new Date().toISOString()),
    createdAtUtc:      str(r, 'createdAtUtc',      'created_at_utc',       new Date().toISOString()),
  };
}

// ── SynqAudit — Legal Hold mapper ─────────────────────────────────────────────

export function mapLegalHold(raw: unknown): LegalHold {
  const outer = asObj(raw);
  const r = (typeof outer['success'] === 'boolean' && outer['data'] !== undefined)
    ? asObj(outer['data'])
    : outer;
  return {
    holdId:           str(r, 'holdId',           'hold_id',            ''),
    auditId:          str(r, 'auditId',           'audit_id',           ''),
    legalAuthority:   str(r, 'legalAuthority',   'legal_authority',    ''),
    notes:            r['notes']            as string | undefined,
    heldByUserId:     r['heldByUserId']     as string | undefined,
    heldAtUtc:        str(r, 'heldAtUtc',         'held_at_utc',        new Date().toISOString()),
    isActive:         (r['isActive'] as boolean) ?? true,
    releasedAtUtc:    r['releasedAtUtc']    as string | undefined,
    releasedByUserId: r['releasedByUserId'] as string | undefined,
  };
}

// ── LS-COR-AUT-011: ABAC Policy mappers ────────────────────────────────────

export function mapPolicySummary(raw: unknown): PolicySummary {
  const r = asObj(raw);
  return {
    id:              str(r, 'id', 'id', ''),
    policyCode:      str(r, 'policyCode', 'policy_code', ''),
    name:            str(r, 'name', 'name', ''),
    description:     r['description'] as string | undefined,
    productCode:     str(r, 'productCode', 'product_code', ''),
    isActive:        (r['isActive'] as boolean) ?? true,
    priority:        (r['priority'] as number) ?? 0,
    effect:          str(r, 'effect', 'effect', 'Allow'),
    rulesCount:      (r['rulesCount'] as number) ?? 0,
    permissionCount: (r['permissionCount'] as number) ?? 0,
    createdAtUtc:    str(r, 'createdAtUtc', 'created_at_utc', ''),
    updatedAtUtc:    r['updatedAtUtc'] as string | undefined,
  };
}

export function mapPolicyRule(raw: unknown): PolicyRule {
  const r = asObj(raw);
  return {
    id:            str(r, 'id', 'id', ''),
    conditionType: str(r, 'conditionType', 'condition_type', ''),
    field:         str(r, 'field', 'field', ''),
    op:            str(r, 'op', 'operator', ''),
    value:         str(r, 'value', 'value', ''),
    logicalGroup:  str(r, 'logicalGroup', 'logical_group', 'And'),
    createdAtUtc:  str(r, 'createdAtUtc', 'created_at_utc', ''),
  };
}

export function mapPermissionPolicyMapping(raw: unknown): PermissionPolicyMapping {
  const r = asObj(raw);
  return {
    id:             str(r, 'id', 'id', ''),
    permissionCode: str(r, 'permissionCode', 'permission_code', ''),
    isActive:       (r['isActive'] as boolean) ?? true,
    createdAtUtc:   str(r, 'createdAtUtc', 'created_at_utc', ''),
  };
}

export function mapPolicyDetail(raw: unknown): PolicyDetail {
  const r = asObj(raw);
  return {
    ...mapPolicySummary(raw),
    createdBy:          r['createdBy'] as string | undefined,
    updatedBy:          r['updatedBy'] as string | undefined,
    rules:              asArr(r['rules']).map(mapPolicyRule),
    permissionMappings: asArr(r['permissionMappings']).map(mapPermissionPolicyMapping),
  };
}

export function mapPermissionPolicySummary(raw: unknown): PermissionPolicySummary {
  const r = asObj(raw);
  return {
    id:             str(r, 'id', 'id', ''),
    permissionCode: str(r, 'permissionCode', 'permission_code', ''),
    policyId:       str(r, 'policyId', 'policy_id', ''),
    policyCode:     str(r, 'policyCode', 'policy_code', ''),
    policyName:     str(r, 'policyName', 'policy_name', ''),
    isActive:       (r['isActive'] as boolean) ?? true,
    createdAtUtc:   str(r, 'createdAtUtc', 'created_at_utc', ''),
  };
}

export function mapSupportedFields(raw: unknown): SupportedFieldsResponse {
  const r = asObj(raw);
  return {
    fields:         asArr(r['fields']).map(v => String(v)),
    operators:      asArr(r['operators']).map(v => String(v)),
    conditionTypes: asArr(r['conditionTypes']).map(v => String(v)),
    logicalGroups:  asArr(r['logicalGroups']).map(v => String(v)),
    effects:        asArr(r['effects']).map(v => String(v)),
  };
}
