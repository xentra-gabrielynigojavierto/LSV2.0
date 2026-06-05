import { CC_NAV } from '@/lib/nav';
import type { NavItem, NavSection } from '@/types';

/** Flatten all items in a section, including those nested inside subGroups. */
function allItems(s: NavSection): NavItem[] {
  if (s.subGroups && s.subGroups.length > 0) {
    return s.subGroups.flatMap(g => g.items);
  }
  return s.items;
}

/** Convert a section heading to a URL slug. e.g. "PRODUCT RULES" → "product-rules" */
export function slugify(heading: string): string {
  return heading.toLowerCase().replace(/[\s_]+/g, '-');
}

/** Reverse-lookup a NavSection by its slug. */
export function getSectionBySlug(slug: string): NavSection | undefined {
  return CC_NAV.find(s => s.heading && slugify(s.heading) === slug);
}

/**
 * Find the NavSection that owns the given pathname.
 * Excludes OVERVIEW (the Home entry) so the sidebar contextual area
 * only appears on actual sub-pages.
 */
export function getSectionForPathname(pathname: string): NavSection | undefined {
  return CC_NAV.find(
    s =>
      s.heading &&
      s.heading !== 'OVERVIEW' &&
      allItems(s).some(
        item =>
          item.href !== '/' &&
          (pathname === item.href || pathname.startsWith(item.href + '/')),
      ),
  );
}

/** Presentable model for a navigation group card on the dashboard hub. */
export interface NavGroupModel {
  slug:      string;
  heading:   string;
  icon:      string;
  itemCount: number;
  liveCount: number;
  items:     NavItem[];
}

/** Explicit icon per group heading. Falls back to the first item's icon. */
const GROUP_ICON_MAP: Record<string, string> = {
  'PLATFORM':      'ri-server-line',
  'IDENTITY':      'ri-shield-user-line',
  'RELATIONSHIPS': 'ri-links-line',
  'PRODUCT RULES': 'ri-shield-check-line',
  'CARECONNECT':   'ri-heart-pulse-line',
  'TENANTS':       'ri-building-2-line',
  'NOTIFICATIONS': 'ri-notification-3-line',
  'AUDIT':         'ri-file-list-3-line',
  'TRACEABILITY':  'ri-git-merge-line',
  'OPERATIONS':    'ri-flow-chart',
  'CATALOG':       'ri-apps-line',
  'SYSTEM':        'ri-settings-3-line',
};

/**
 * Returns a presentable array of nav group models for the dashboard hub.
 * Excludes OVERVIEW (Home is handled separately).
 */
export function getNavGroupModels(): NavGroupModel[] {
  return CC_NAV
    .filter(s => s.heading && s.heading !== 'OVERVIEW')
    .map(s => {
      const flat = allItems(s);
      return {
        slug:      slugify(s.heading!),
        heading:   s.heading!,
        icon:      GROUP_ICON_MAP[s.heading!] ?? flat[0]?.icon ?? 'ri-folder-line',
        itemCount: flat.length,
        liveCount: flat.filter(i => i.badge === 'LIVE').length,
        items:     flat,
      };
    });
}
