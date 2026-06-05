/**
 * Control Center route constants and builders.
 *
 * All internal links MUST use these helpers — never hardcode paths directly
 * in components or pages. This keeps path changes isolated to one file.
 *
 * All routes are host-root paths (no path prefix — this is a standalone app).
 */
export const Routes = {
  // ── Platform diagnostics ──────────────────────────────────────────────────

  /** /platform-readiness — Platform readiness summary */
  platformReadiness: '/platform-readiness',

  /** /legacy-coverage — Legacy migration coverage report */
  legacyCoverage: '/legacy-coverage',

  // ── Identity ──────────────────────────────────────────────────────────────

  /** /tenants — Tenants list */
  tenants: '/tenants',

  /** /tenant-users — Users across all tenants */
  tenantUsers: '/tenant-users',

  /** /platform-users — PlatformInternal staff user list */
  platformUsers: '/platform-users',

  /** /roles — Roles & permissions list */
  roles: '/roles',

  /** /scoped-roles — Scoped role assignments (Phase G) */
  scopedRoles: '/scoped-roles',

  /** /org-types — Organization type catalog */
  orgTypes: '/org-types',

  // ── Relationships ─────────────────────────────────────────────────────────

  /** /relationship-types — Relationship type catalog */
  relationshipTypes: '/relationship-types',

  /** /org-relationships — Organization relationship graph */
  orgRelationships: '/org-relationships',

  // ── Product rules ─────────────────────────────────────────────────────────

  /** /product-rules — Product access rules (org-type + rel-type) */
  productRules: '/product-rules',

  // ── CareConnect ───────────────────────────────────────────────────────────

  /** /careconnect-integrity — CareConnect entity integrity report */
  careConnectIntegrity: '/careconnect-integrity',

  // ── Operations ────────────────────────────────────────────────────────────

  /** /workflows — Cross-product workflow operations list (E9.1) */
  workflows: '/workflows',

  /** /support — Support tools */
  support: '/support',

  /** /audit-logs — Audit logs */
  auditLogs: '/audit-logs',

  /** /monitoring — Service health */
  monitoring: '/monitoring',

  // ── Catalog (mockup) ──────────────────────────────────────────────────────

  /** /products — Product entitlements */
  products: '/products',

  /** /domains — Tenant domain management (mockup) */
  domains: '/domains',

  // ── System ────────────────────────────────────────────────────────────────

  /** /settings — Platform settings */
  settings: '/settings',

  // ── Dynamic route builders ────────────────────────────────────────────────

  /** /tenants/:id — Tenant detail */
  tenantDetail: (id: string) => `/tenants/${id}`,

  /** /tenants/:id/users — Users for a specific tenant */
  tenantUsers_: (tenantId: string) => `/tenants/${tenantId}/users`,

  /** /tenant-users/:id — User detail */
  userDetail: (id: string) => `/tenant-users/${id}`,

  /** /platform-users/:id — Platform user detail */
  platformUserDetail: (id: string) => `/platform-users/${id}`,

  /** /roles/:id — Role detail */
  roleDetail: (id: string) => `/roles/${id}`,

  /** /groups — Access groups list */
  groups: '/groups',

  /** /groups/:id — Group detail (legacy) */
  groupDetail: (id: string) => `/groups/${id}`,

  /** /access-groups/:tenantId/:groupId — Access group detail */
  accessGroupDetail: (tenantId: string, groupId: string) => `/access-groups/${tenantId}/${groupId}`,

  /** /tenants/:id/notifications — Notification activity for a specific tenant */
  tenantNotifications: (id: string) => `/tenants/${id}/notifications`,

  /** /tenants/:id/activity — User activity audit log scoped to a specific tenant */
  tenantActivity: (id: string) => `/tenants/${id}/activity`,

  /** /permissions — Platform permission catalog */
  permissions: '/permissions',

  /** /authorization-simulator — Authorization simulation console */
  authorizationSimulator: '/authorization-simulator',
} as const;
