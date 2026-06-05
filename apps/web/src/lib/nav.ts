import type { NavSection, PlatformSession, ProductRoleValue, OrgTypeValue } from '@/types';
import { ProductRole, OrgType } from '@/types';

// ── Per-product sidebar navigation (sections) ─────────────────────────────────

export const PRODUCT_NAV: Record<string, NavSection[]> = {
  careconnect: [
    {
      items: [
        { href: '/careconnect/dashboard',    label: 'Dashboard',    icon: 'ri-dashboard-line' },
        { href: '/careconnect/referrals',    label: 'Referrals',    icon: 'ri-file-list-3-line', badgeKey: 'newReferrals' },
        // CC-REFERRER-BROWSE: for elevated law firm referrers (tenant portal).
        // Hidden from network managers AND from lien-owner orgs (they manage their
        // own network; they never browse other networks).
        { href: '/careconnect/browse-networks', label: 'Available Networks', icon: 'ri-share-circle-line',
          requiredRoles:    [ProductRole.CareConnectReferrer],
          excludedRoles:    [ProductRole.CareConnectNetworkManager],
          hiddenForOrgTypes:[OrgType.LienOwner] },
        // Lien company network management — visible to network managers AND to
        // any TenantAdmin at a LIEN_OWNER org (covers the case where the admin
        // hasn't been explicitly granted the NetworkManager product role yet).
        { href: '/careconnect/my-network', label: 'My Network', icon: 'ri-settings-4-line',
          requiredRoles:                 [ProductRole.CareConnectNetworkManager],
          visibleForTenantAdminInOrgTypes:[OrgType.LienOwner] },
        // Multi-network admin view — internal/admin use only; hidden from lien company orgs.
        { href: '/careconnect/networks', label: 'Networks', icon: 'ri-share-forward-2-line',
          requiredRoles:    [ProductRole.CareConnectNetworkManager],
          hiddenForOrgTypes:[OrgType.LienOwner] },
      ],
    },
  ],

  fund: [
    {
      items: [
        { href: '/fund/dashboard',    label: 'Dashboard',    icon: 'ri-dashboard-line' },
        { href: '/fund/processing',   label: 'Processing',   icon: 'ri-loader-4-line' },
        { href: '/fund/underwriting', label: 'Underwriting', icon: 'ri-file-search-line' },
        { href: '/fund/payouts',      label: 'Payouts',      icon: 'ri-money-dollar-circle-line' },
        { href: '/fund/reports',      label: 'Reports',      icon: 'ri-bar-chart-2-line' },
      ],
    },
  ],

  lien: [
    {
      items: [
        { href: '/lien/home',          label: 'Home',          icon: 'ri-home-line' },
      ],
    },
    {
      heading: 'MY TASKS',
      items: [
        { href: '/lien/dashboard',     label: 'Dashboard',     icon: 'ri-dashboard-line' },
        { href: '/lien/task-manager',  label: 'Task Manager',  icon: 'ri-task-line' },
        { href: '/lien/cases',         label: 'Cases',         icon: 'ri-folder-open-line' },
        { href: '/lien/liens',         label: 'Liens',         icon: 'ri-stack-line' },
        { href: '/lien/bill-of-sales', label: 'Bill of Sales', icon: 'ri-receipt-line', sellModeOnly: true },
        { href: '/lien/servicing',     label: 'Servicing',     icon: 'ri-tools-line' },
        { href: '/lien/contacts',      label: 'Contacts',      icon: 'ri-contacts-book-line' },
      ],
    },
    {
      heading: 'MARKETPLACE',
      sellModeOnly: true,
      items: [
        { href: '/lien/my-liens',    label: 'My Liens',    icon: 'ri-price-tag-3-line',         requiredRoles: [ProductRole.SynqLienSeller] },
        { href: '/lien/marketplace', label: 'Marketplace', icon: 'ri-store-2-line',              requiredRoles: [ProductRole.SynqLienBuyer] },
        { href: '/lien/portfolio',   label: 'Portfolio',   icon: 'ri-briefcase-line',            requiredRoles: [ProductRole.SynqLienBuyer, ProductRole.SynqLienHolder] },
      ],
    },
    {
      heading: 'MY TOOLS',
      items: [
        { href: '/lien/batch-entry',       label: 'Batch Entry',       icon: 'ri-upload-2-line', requiredRoles: [ProductRole.SynqLienSeller] },
        { href: '/lien/document-handling', label: 'Document Handling', icon: 'ri-file-copy-2-line' },
      ],
    },
    {
      heading: 'SETTINGS',
      items: [
        { href: '/lien/settings/workflow',              label: 'Workflow Settings', icon: 'ri-git-branch-line'    },
        { href: '/lien/settings/task-templates',        label: 'Task Templates',    icon: 'ri-file-list-3-line'   },
        { href: '/lien/settings/task-automation',       label: 'Task Automation',   icon: 'ri-robot-line'         },
        { href: '/lien/settings/task-governance',       label: 'Task Governance',   icon: 'ri-shield-check-line'  },
      ],
    },
  ],

  ai: [
    { items: [{ href: '/ai/dashboard', label: 'Dashboard', icon: 'ri-dashboard-line' }] },
  ],

  insights: [
    {
      items: [
        { href: '/insights/dashboard', label: 'Dashboard', icon: 'ri-dashboard-line' },
        { href: '/insights/reports',   label: 'Reports',   icon: 'ri-file-chart-line' },
        { href: '/insights/schedules', label: 'Schedules', icon: 'ri-calendar-schedule-line' },
      ],
    },
  ],
};

// ── Product metadata ──────────────────────────────────────────────────────────

export const PRODUCT_META: Record<string, { label: string; icon: string; color: string; iconSrc: string }> = {
  careconnect: { label: 'Synq CareConnect', icon: 'ri-shield-cross-line',  color: '#2563eb', iconSrc: '/product-icons/synqconnect.png' },
  fund:        { label: 'Synq Funds',        icon: 'ri-bank-line',           color: '#16a34a', iconSrc: '/product-icons/synqfund.png'    },
  lien:        { label: 'Synq Liens',        icon: 'ri-stack-line',          color: '#7c3aed', iconSrc: '/product-icons/synqlien.png'    },
  ai:          { label: 'Synq AI',           icon: 'ri-robot-line',          color: '#d97706', iconSrc: '/product-icons/synqai.png'      },
  insights:    { label: 'Synq Insights',     icon: 'ri-bar-chart-2-line',    color: '#0891b2', iconSrc: '/product-icons/synqinsight.png' },
};

/**
 * Maps backend product codes (as returned by auth/me `enabledProducts`) to the
 * PRODUCT_META key used in the tenant portal.
 * Values on the left are the frontend-friendly codes emitted by the Identity service
 * (e.g. "CareConnect", "SynqFund"). Values on the right are PRODUCT_META keys.
 */
export const PRODUCT_CODE_TO_NAV_KEY: Record<string, string> = {
  CareConnect:  'careconnect',
  SynqFund:     'fund',
  SynqLien:     'lien',
  SynqAI:       'ai',
  SynqInsights: 'insights',
  SynqBill:     'bill',
  SynqRx:       'rx',
  SynqPayout:   'payout',
};

/**
 * Converts a list of backend enabledProducts codes into the set of PRODUCT_META
 * keys that should be shown on the dashboard.
 * Falls back to showing ALL products when the list is empty (e.g. during
 * onboarding, or for PlatformAdmin users whose tokens predate this feature).
 */
export function resolveEnabledNavKeys(enabledProducts: string[]): Set<string> {
  if (enabledProducts.length === 0) return new Set(Object.keys(PRODUCT_META));
  const keys = new Set<string>();
  for (const code of enabledProducts) {
    const key = PRODUCT_CODE_TO_NAV_KEY[code];
    if (key && key in PRODUCT_META) keys.add(key);
  }
  return keys;
}

export function filterNavByRoles(
  sections:     NavSection[],
  userRoles:    ProductRoleValue[],
  isTenantAdmin = false,
  orgType?:     OrgTypeValue | null,
): NavSection[] {
  return sections
    .map(section => ({
      ...section,
      items: section.items.filter(item => {
        // Hide immediately if the user holds any excluded role.
        if (item.excludedRoles?.some(role => userRoles.includes(role))) return false;

        // TenantAdmin override: show this item if the user is a tenant admin in
        // a matching org type, regardless of product-role state.
        if (
          item.visibleForTenantAdminInOrgTypes &&
          isTenantAdmin &&
          orgType &&
          item.visibleForTenantAdminInOrgTypes.includes(orgType)
        ) return true;

        if (!item.requiredRoles || item.requiredRoles.length === 0) return true;
        return item.requiredRoles.some(role => userRoles.includes(role));
      }),
    }))
    .filter(section => section.items.length > 0);
}

export function filterNavByAccess(
  sections:     NavSection[],
  userRoles:    ProductRoleValue[],
  isSellMode:   boolean,
  orgType?:     OrgTypeValue | null,
  isTenantAdmin = false,
): NavSection[] {
  return filterNavByRoles(sections, userRoles, isTenantAdmin, orgType)
    .filter((s) => !s.sellModeOnly || isSellMode)
    .map((s) => ({
      ...s,
      items: s.items.filter((item) => {
        if (item.sellModeOnly && !isSellMode) return false;
        if (item.hiddenForOrgTypes && orgType && item.hiddenForOrgTypes.includes(orgType)) return false;
        return true;
      }),
    }))
    .filter((s) => s.items.length > 0);
}

// ── Infer product from pathname ───────────────────────────────────────────────

export function inferProductFromPath(pathname: string): string | null {
  if (pathname.startsWith('/careconnect')) return 'careconnect';
  if (pathname.startsWith('/fund'))        return 'fund';
  if (pathname.startsWith('/lien'))        return 'lien';
  if (pathname.startsWith('/ai'))          return 'ai';
  if (pathname.startsWith('/insights'))    return 'insights';
  return null;
}

// ── Org type label ────────────────────────────────────────────────────────────

export function orgTypeLabel(orgType: string | undefined): string {
  const labels: Record<string, string> = {
    LAW_FIRM:   'Law Firm',
    PROVIDER:   'Provider',
    FUNDER:     'Funder',
    LIEN_OWNER: 'Lien Owner',
    INTERNAL:   'Internal',
  };
  return orgType ? (labels[orgType] ?? orgType) : 'No Organization';
}

// ── Global bottom nav (always shown at the foot of every product sidebar) ─────

export const GLOBAL_BOTTOM_NAV: NavSection = {
  heading: 'ACCOUNT',
  items: [
    { href: '/my-work',                         label: 'My Work',         icon: 'ri-task-line'                       },
    { href: '/notifications',                   label: 'Notifications',   icon: 'ri-mail-send-line'                  },
    { href: '/activity',                        label: 'Activity Log',    icon: 'ri-history-line'                    },
    { href: '/support',                         label: 'Support',         icon: 'ri-customer-service-2-line', adminOnly: true },
    { href: '/tenant/authorization/users',      label: 'User Management', icon: 'ri-shield-user-line',        adminOnly: true },
  ],
};

// ── Admin nav sections (shown when session has admin role) ────────────────────

/**
 * Returns the Administration NavSection[] for sidebar rendering.
 * Returns an empty array for standard users — they see nothing.
 */
export function buildNavGroups(_session: PlatformSession): NavSection[] {
  return [];
}
