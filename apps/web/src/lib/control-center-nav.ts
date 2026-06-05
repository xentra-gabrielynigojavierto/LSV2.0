import type { NavGroup, PlatformSession } from '@/types';
import { CCRoutes } from '@/lib/control-center-routes';

/**
 * Derives the Control Center sidebar navigation from the session.
 *
 * Only rendered for PlatformAdmin users — the (control-center) layout
 * enforces this via requireCCPlatformAdmin() before this is ever called.
 *
 * All hrefs are built through CCRoutes so they work in both:
 *   - embedded mode (/control-center/... prefix)
 *   - standalone mode (host-root paths)
 *
 * Returns a flat list of NavGroups compatible with the existing Sidebar
 * and ControlCenterShell. All items are always shown to platform admins
 * (access is binary: admin or not — no product-role filtering needed).
 */
export function buildControlCenterNav(_session: PlatformSession): NavGroup[] {
  return [
    {
      id:    'control-center-overview',
      label: 'Overview',
      icon:  'LayoutDashboard',
      items: [
        { href: CCRoutes.dashboard, label: 'Dashboard' },
      ],
    },
    {
      id:    'control-center-tenants',
      label: 'Tenants',
      icon:  'Building2',
      items: [
        { href: CCRoutes.tenants,     label: 'All Tenants' },
        { href: CCRoutes.tenantUsers, label: 'Tenant Users' },
      ],
    },
    {
      id:    'control-center-access',
      label: 'Access Control',
      icon:  'ShieldCheck',
      items: [
        { href: CCRoutes.roles,    label: 'Roles & Permissions' },
        { href: CCRoutes.products, label: 'Product Entitlements' },
      ],
    },
    {
      id:    'control-center-ops',
      label: 'Operations',
      icon:  'Wrench',
      items: [
        { href: CCRoutes.support,    label: 'Support Tools' },
        { href: CCRoutes.auditLogs,  label: 'Audit Logs' },
        { href: CCRoutes.monitoring, label: 'Monitoring' },
      ],
    },
    {
      id:    'control-center-notifications',
      label: 'Notifications',
      icon:  'Bell',
      items: [
        { href: CCRoutes.notifications,        label: 'Overview' },
        { href: CCRoutes.notifProviders,       label: 'Providers' },
        { href: CCRoutes.notifTemplates,       label: 'Templates' },
        { href: CCRoutes.notifBilling,         label: 'Billing' },
        { href: CCRoutes.notifContactPolicies, label: 'Contact Policies' },
        { href: CCRoutes.notifLog,             label: 'Delivery Log' },
      ],
    },
    {
      id:    'control-center-liens',
      label: 'Liens',
      icon:  'Scale',
      items: [
        { href: CCRoutes.liensWorkflow,        label: 'Workflow Config'  },
        { href: CCRoutes.liensTaskTemplates,   label: 'Task Templates'   },
        { href: CCRoutes.liensTaskAutomation,  label: 'Task Automation'  },
        { href: CCRoutes.liensTaskGovernance,  label: 'Task Governance'  },
      ],
    },
    {
      id:    'control-center-config',
      label: 'Configuration',
      icon:  'Settings',
      items: [
        { href: CCRoutes.settings, label: 'Platform Settings' },
      ],
    },
  ];
}
