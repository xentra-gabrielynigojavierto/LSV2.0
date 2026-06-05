import type { NavSection, NavSubGroup } from '@/types';

/**
 * Control Center sidebar navigation — NavSection[] with status badges.
 *
 * Badge values:
 *   LIVE        — fully wired to a working backend endpoint
 *   IN PROGRESS — partially wired or mixed live/mock data
 *   MOCKUP      — no real backend wiring; placeholder UI only
 *
 * Sections are ordered: LIVE functionality first, MOCKUP last.
 */
export const CC_NAV: NavSection[] = [
  {
    heading: 'OVERVIEW',
    items: [
      { href: '/', label: 'Dashboard', icon: 'ri-dashboard-3-line', badge: 'LIVE' },
    ],
  },

  {
    heading: 'PLATFORM',
    items: [
      { href: '/platform-readiness', label: 'Platform Readiness', icon: 'ri-checkbox-circle-line', badge: 'LIVE' },
      { href: '/legacy-coverage',    label: 'Legacy Coverage',    icon: 'ri-history-fill'         },
    ],
  },

  {
    heading: 'PLATFORM USERS',
    items: [
      { href: '/platform-users', label: 'Platform Staff', icon: 'ri-shield-user-line', badge: 'LIVE' },
    ],
  },

  {
    heading: 'IDENTITY',
    items: [
      { href: '/tenant-users', label: 'Users',            icon: 'ri-group-line'           },
      { href: '/groups',       label: 'Groups',           icon: 'ri-team-line',   badge: 'LIVE' },
      { href: '/permissions',  label: 'Permissions',      icon: 'ri-key-2-line',  badge: 'LIVE' },
      { href: '/policies',     label: 'Policies',         icon: 'ri-git-branch-line',  badge: 'LIVE' },
      { href: '/authorization-simulator', label: 'Simulator', icon: 'ri-test-tube-line', badge: 'LIVE' },
      { href: '/roles',        label: 'Roles',            icon: 'ri-shield-keyhole-line', badge: 'LIVE' },
      { href: '/scoped-roles', label: 'Scoped Roles',     icon: 'ri-focus-3-line', badge: 'MOCKUP' },
      { href: '/org-types',    label: 'Org Types',        icon: 'ri-building-4-line'      },
    ],
  },

  {
    heading: 'RELATIONSHIPS',
    items: [
      { href: '/relationship-types', label: 'Relationship Types', icon: 'ri-links-line'         },
      { href: '/org-relationships',  label: 'Org Relationships',  icon: 'ri-share-circle-line'  },
    ],
  },

  {
    heading: 'PRODUCT RULES',
    items: [
      { href: '/product-rules', label: 'Access Rules', icon: 'ri-shield-check-line' },
    ],
  },

  {
    heading: 'CARECONNECT',
    items: [
      { href: '/careconnect-integrity', label: 'Integrity', icon: 'ri-heart-pulse-line' },
    ],
  },

  {
    heading: 'TENANTS',
    items: [
      { href: '/tenants', label: 'Tenants',       icon: 'ri-building-2-line'                    },
      { href: '/domains', label: 'Tenant Domains', icon: 'ri-global-line', badge: 'MOCKUP' },
    ],
  },

  {
    heading: 'NOTIFICATIONS',
    items: [],
    subGroups: [
      {
        label: 'Email',
        items: [
          { href: '/notifications',     label: 'Overview',     icon: 'ri-notification-3-line' },
          { href: '/notifications/log', label: 'Delivery Log', icon: 'ri-mail-send-line'      },
        ],
      },
      {
        label: 'SMS',
        items: [
          { href: '/notifications/sms-dashboard',              label: 'SMS Dashboard',       icon: 'ri-message-line',             badge: 'LIVE' },
          { href: '/notifications/sms-incidents',              label: 'SMS Incidents',       icon: 'ri-alarm-warning-line',       badge: 'LIVE' },
          { href: '/notifications/sms-incidents/alerts',       label: 'Alert List',          icon: 'ri-alert-line',               badge: 'LIVE' },
          { href: '/notifications/sms-incidents/escalations',  label: 'Escalations',         icon: 'ri-send-plane-2-line',        badge: 'LIVE' },
          { href: '/notifications/sms-incidents/policies',     label: 'Escalation Policies', icon: 'ri-settings-4-line',          badge: 'LIVE' },
          { href: '/notifications/sms-costs',                  label: 'SMS Costs',           icon: 'ri-money-dollar-circle-line', badge: 'LIVE' },
          { href: '/notifications/sms-routing',                label: 'SMS Routing',         icon: 'ri-route-line',               badge: 'LIVE' },
        ],
      },
      {
        label: 'General Settings',
        items: [
          { href: '/notifications/templates',             label: 'Templates',       icon: 'ri-file-text-line'    },
          { href: '/notifications/providers',             label: 'Providers',       icon: 'ri-plug-line'         },
          { href: '/notifications/billing',               label: 'Usage & Billing', icon: 'ri-bar-chart-2-line'  },
          { href: '/notifications/contacts/suppressions', label: 'Suppressions',    icon: 'ri-user-forbid-line'  },
          { href: '/notifications/delivery-issues',       label: 'Delivery Issues', icon: 'ri-error-warning-line'},
        ],
      },
    ] satisfies NavSubGroup[],
  },

  {
    heading: 'AUDIT',
    items: [
      { href: '/synqaudit',                  label: 'Overview',       icon: 'ri-shield-check-line',   badge: 'LIVE' },
      { href: '/synqaudit/user-activity',    label: 'User Activity',  icon: 'ri-user-heart-line',     badge: 'LIVE' },
      { href: '/synqaudit/investigation',    label: 'Investigation',  icon: 'ri-search-eye-line',     badge: 'LIVE' },
      { href: '/synqaudit/permissions',     label: 'Permissions',    icon: 'ri-key-2-line',          badge: 'LIVE' },
      { href: '/synqaudit/trace',            label: 'Trace Viewer',   icon: 'ri-git-branch-line',     badge: 'LIVE' },
      { href: '/synqaudit/exports',          label: 'Exports',        icon: 'ri-download-cloud-line', badge: 'LIVE' },
      { href: '/synqaudit/integrity',        label: 'Integrity',      icon: 'ri-fingerprint-line',    badge: 'LIVE' },
      { href: '/synqaudit/legal-holds',      label: 'Legal Holds',    icon: 'ri-scales-3-line',       badge: 'LIVE' },
      { href: '/synqaudit/analytics',        label: 'Analytics',      icon: 'ri-bar-chart-box-line',  badge: 'LIVE' },
      { href: '/synqaudit/anomalies',        label: 'Anomalies',      icon: 'ri-alarm-warning-line',  badge: 'LIVE' },
      { href: '/synqaudit/alerts',           label: 'Alerts',         icon: 'ri-notification-badge-line', badge: 'LIVE' },
    ],
  },

  {
    heading: 'TRACEABILITY',
    items: [
      { href: '/artifacts', label: 'Artifacts', icon: 'ri-git-merge-line', badge: 'LIVE' },
    ],
  },

  {
    heading: 'OPERATIONS',
    items: [
      { href: '/workflows',            label: 'Workflows',          icon: 'ri-flow-chart',              badge: 'LIVE' },
      { href: '/workflows/exceptions', label: 'Workflow exceptions', icon: 'ri-error-warning-line',     badge: 'LIVE'  },
      { href: '/operations/outbox',    label: 'Async Outbox',        icon: 'ri-inbox-archive-line',     badge: 'LIVE' },
      { href: '/analytics',            label: 'Analytics',           icon: 'ri-bar-chart-2-line',        badge: 'LIVE' },
      { href: '/support',    label: 'Support Tools', icon: 'ri-customer-service-2-line', badge: 'LIVE' },
      { href: '/audit-logs', label: 'Audit Logs',    icon: 'ri-file-list-3-line',        badge: 'LIVE' },
      { href: '/monitoring', label: 'Monitoring',    icon: 'ri-pulse-line',              badge: 'IN PROGRESS' },
      { href: '/reports',    label: 'Reports',       icon: 'ri-file-chart-line',         badge: 'IN PROGRESS' },
    ],
  },

  {
    heading: 'CATALOG',
    items: [
      { href: '/products', label: 'Products', icon: 'ri-apps-line', badge: 'IN PROGRESS' },
    ],
  },

  {
    heading: 'SYSTEM',
    items: [
      { href: '/settings', label: 'Platform Settings', icon: 'ri-settings-3-line', badge: 'IN PROGRESS' },
    ],
  },
];

/** @deprecated — kept for any existing callers; remove once all are migrated. */
export function buildCCNav() {
  return CC_NAV;
}
