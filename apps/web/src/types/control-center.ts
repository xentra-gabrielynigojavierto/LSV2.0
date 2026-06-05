// ── Control Center domain types ───────────────────────────────────────────────
// These mirror the expected Identity service response shapes for admin endpoints.
// Keep in sync with Identity.Application DTOs as backend endpoints are confirmed.

// ── Tenants ───────────────────────────────────────────────────────────────────

export type TenantType = 'LawFirm' | 'Provider' | 'Corporate' | 'Government' | 'Other';
export type TenantStatus = 'Active' | 'Inactive' | 'Suspended';

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
}

export interface TenantDetail {
  id:           string;
  code:         string;
  displayName:  string;
  isActive:     boolean;
  createdAtUtc: string;
  updatedAtUtc: string;
  products:     TenantProductSummary[];
  userCount:    number;
  orgCount:     number;
}

export interface TenantProductSummary {
  productId:   string;
  productCode: string;
  productName: string;
  isEnabled:   boolean;
  enabledAtUtc?: string;
}

// ── Tenant Users ──────────────────────────────────────────────────────────────

export interface TenantUserSummary {
  id:          string;
  email:       string;
  firstName:   string;
  lastName:    string;
  isActive:    boolean;
  systemRoles: string[];
  orgName?:    string;
  createdAtUtc: string;
}

// ── Roles & Permissions ───────────────────────────────────────────────────────

export interface RoleSummary {
  id:           string;
  code:         string;
  name:         string;
  productCode:  string;
  productName:  string;
  capabilities: string[];
  isActive:     boolean;
}

// ── Product Entitlements ──────────────────────────────────────────────────────

export interface ProductEntitlementSummary {
  tenantId:    string;
  tenantCode:  string;
  productId:   string;
  productCode: string;
  productName: string;
  isEnabled:   boolean;
  enabledAtUtc?: string;
}

// ── Audit Logs ────────────────────────────────────────────────────────────────

export interface AuditLogEntry {
  id:          string;
  tenantId?:   string;
  actorId?:    string;
  actorEmail?: string;
  action:      string;
  entityType:  string;
  entityId?:   string;
  detail?:     string;
  occurredAtUtc: string;
}

// ── Platform Settings ─────────────────────────────────────────────────────────

export interface PlatformSetting {
  key:         string;
  value:       string;
  description: string;
  isSecret:    boolean;
  updatedAtUtc: string;
}

// ── Monitoring ────────────────────────────────────────────────────────────────

export interface SystemHealthSummary {
  serviceName:  string;
  status:       'ok' | 'degraded' | 'down' | 'unknown';
  version?:     string;
  environment?: string;
  checkedAtUtc: string;
}

// ── Shared ────────────────────────────────────────────────────────────────────

export interface PagedResponse<T> {
  items:      T[];
  totalCount: number;
  page:       number;
  pageSize:   number;
}
