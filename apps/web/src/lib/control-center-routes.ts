import { CC_BASE_PATH } from '@/lib/control-center-config';

/**
 * Control Center route builder.
 *
 * All internal Control Center links MUST be built through this module.
 * Never hardcode '/control-center/...' strings in components or pages.
 *
 * Usage:
 *   import { CCRoutes } from '@/lib/control-center-routes';
 *   <Link href={CCRoutes.tenants}>All Tenants</Link>
 *
 * In embedded mode  (CC_BASE_PATH = '/control-center'):
 *   CCRoutes.dashboard  → '/control-center'
 *   CCRoutes.tenants    → '/control-center/tenants'
 *
 * In standalone mode (CC_BASE_PATH = ''):
 *   CCRoutes.dashboard  → '/'
 *   CCRoutes.tenants    → '/tenants'
 */
function cc(path: string): string {
  // When base is empty and path is also empty, resolve to site root '/'
  if (!CC_BASE_PATH && !path) return '/';
  return `${CC_BASE_PATH}${path}`;
}

// ── Named routes ──────────────────────────────────────────────────────────────

export const CCRoutes = {
  /** /control-center (embedded) or / (standalone) */
  dashboard:   cc(''),

  /** /control-center/tenants or /tenants */
  tenants:     cc('/tenants'),

  /** /control-center/tenant-users or /tenant-users */
  tenantUsers: cc('/tenant-users'),

  /** /control-center/roles or /roles */
  roles:       cc('/roles'),

  /** /control-center/products or /products */
  products:    cc('/products'),

  /** /control-center/support or /support */
  support:     cc('/support'),

  /** /control-center/audit-logs or /audit-logs */
  auditLogs:   cc('/audit-logs'),

  /** /control-center/monitoring or /monitoring */
  monitoring:  cc('/monitoring'),

  /** /control-center/settings or /settings */
  settings:    cc('/settings'),

  // ── Notifications ────────────────────────────────────────────────────────

  /** /control-center/notifications or /notifications */
  notifications:            cc('/notifications'),

  /** /control-center/notifications/providers */
  notifProviders:           cc('/notifications/providers'),

  /** /control-center/notifications/templates */
  notifTemplates:           cc('/notifications/templates'),

  /** /control-center/notifications/billing */
  notifBilling:             cc('/notifications/billing'),

  /** /control-center/notifications/contacts/policies */
  notifContactPolicies:     cc('/notifications/contacts/policies'),

  /** /control-center/notifications/log */
  notifLog:                 cc('/notifications/log'),

  // ── Liens ──────────────────────────────────────────────────────────────────

  /** /control-center/liens/workflow */
  liensWorkflow:            cc('/liens/workflow'),

  /** /control-center/liens/task-templates */
  liensTaskTemplates:       cc('/liens/task-templates'),

  /** /control-center/liens/task-automation */
  liensTaskAutomation:      cc('/liens/task-automation'),

  /** /control-center/liens/task-governance */
  liensTaskGovernance:      cc('/liens/task-governance'),
} as const;

export type CCRoutePath = typeof CCRoutes[keyof typeof CCRoutes];

// ── Dynamic route builders ────────────────────────────────────────────────────

export const CCRouteBuilders = {
  /** /control-center/tenants/:id or /tenants/:id */
  tenantDetail: (id: string) => cc(`/tenants/${id}`),

  /** /control-center/tenants/:id/users or /tenants/:id/users */
  tenantUsers: (tenantId: string) => cc(`/tenants/${tenantId}/users`),
};
