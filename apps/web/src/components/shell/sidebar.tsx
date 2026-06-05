'use client';

import Link from 'next/link';
import { usePathname } from 'next/navigation';
import { useState, useEffect } from 'react';
import { useProduct } from '@/contexts/product-context';
import { useSettings } from '@/contexts/settings-context';
import { PRODUCT_NAV, PRODUCT_META, GLOBAL_BOTTOM_NAV, buildNavGroups, filterNavByAccess } from '@/lib/nav';
import { getClientPortalConfig, type PortalConfig } from '@/lib/portal';
import { useSession } from '@/hooks/use-session';
import { useNavBadges } from '@/hooks/use-nav-badges';
import { useProviderMode } from '@/hooks/use-provider-mode';
import type { NavItem } from '@/types';
import { clsx } from 'clsx';

const STORAGE_KEY = 'ls_sidebar_collapsed';

export function Sidebar() {
  const pathname              = usePathname();
  const { selectedProductId } = useProduct();
  const settings              = useSettings();
  const nav                   = settings.appearance.nav;
  const { session }           = useSession();
  const badges                = useNavBadges();
  const { isSellMode }        = useProviderMode();
  const adminSections         = session ? buildNavGroups(session) : [];

  const [collapsed,    setCollapsed]    = useState(false);
  const [mounted,      setMounted]      = useState(false);
  const [portalConfig, setPortalConfig] = useState<PortalConfig | null>(null);

  useEffect(() => {
    const stored = localStorage.getItem(STORAGE_KEY);
    if (stored === 'true') setCollapsed(true);
    setMounted(true);
    setPortalConfig(getClientPortalConfig());
  }, []);

  function toggle() {
    setCollapsed(prev => {
      const next = !prev;
      localStorage.setItem(STORAGE_KEY, String(next));
      return next;
    });
  }

  useEffect(() => {
    function onKey(e: KeyboardEvent) {
      if ((e.ctrlKey || e.metaKey) && e.key === '[') { e.preventDefault(); toggle(); }
    }
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const width    = !mounted ? 220 : collapsed ? 52 : 220;
  const rawSections = selectedProductId ? (PRODUCT_NAV[selectedProductId] ?? []) : [];
  const sections = session
    ? filterNavByAccess(rawSections, session.productRoles, isSellMode, session.orgType, session.isTenantAdmin)
    : rawSections;
  const meta     = selectedProductId ? PRODUCT_META[selectedProductId] : null;

  const currentPathname = pathname ?? '';
  const allNavItems = [
    ...sections.flatMap(s => s.items),
    ...adminSections.flatMap(s => s.items),
    ...GLOBAL_BOTTOM_NAV.items,
  ];
  const activeNavItem = allNavItems.find(
    item => currentPathname === item.href || currentPathname.startsWith(item.href + '/'),
  ) ?? null;

  return (
    <aside
      className="shrink-0 flex flex-col bg-white border-r border-gray-200 overflow-hidden"
      style={{
        width,
        transition: mounted ? 'width 200ms ease' : undefined,
        alignSelf: 'stretch',
      }}
    >
      {/* ── Header ────────────────────────────────────────────────────────── */}
      <div className={clsx(
        'shrink-0 flex items-center border-b border-gray-100 h-12',
        collapsed ? 'justify-center' : 'justify-between px-4',
      )}>
        {!collapsed && meta && (
          <div className="flex items-center gap-2 min-w-0">
            {meta.iconSrc
              ? <img src={meta.iconSrc} alt="" aria-hidden className="w-4 h-4 shrink-0 object-contain" />
              : <i className={`${meta.icon} text-[15px]`} style={{ color: nav.activeColor }} />
            }
            <span className="text-[12px] font-semibold text-gray-700 truncate">{meta.label}</span>
          </div>
        )}
        {!collapsed && !meta && (
          <span className="text-[11px] font-semibold uppercase tracking-widest text-gray-400 select-none">
            Navigation
          </span>
        )}
        <button
          onClick={toggle}
          title={collapsed ? 'Expand sidebar (Ctrl+[)' : 'Collapse sidebar (Ctrl+[)'}
          className="flex items-center justify-center rounded-md w-7 h-7 text-gray-400 hover:bg-gray-100 hover:text-gray-700 transition-colors shrink-0"
        >
          <i className={clsx('text-[17px] leading-none',
            collapsed ? 'ri-sidebar-unfold-line' : 'ri-sidebar-fold-line',
          )} />
        </button>
      </div>

      {/* ── Nav sections ──────────────────────────────────────────────────── */}
      <div className="flex-1 overflow-y-auto overflow-x-hidden py-2">
        {sections.map((section, si) => (
          <div key={si} className={si > 0 ? 'mt-4' : ''}>
            {/* Section heading (expanded only) */}
            {section.heading && !collapsed && (
              <p className="px-5 mb-1 text-[10px] font-semibold uppercase tracking-widest text-gray-400 select-none">
                {section.heading}
              </p>
            )}
            {/* Divider between sections when collapsed */}
            {si > 0 && collapsed && (
              <div className="mx-2 mb-2 border-t border-gray-100" />
            )}
            <nav className={clsx('space-y-0.5', collapsed ? 'px-1.5' : 'px-3')}>
              {section.items.map(item => (
                <SidebarItem
                  key={item.href + item.label}
                  item={item}
                  pathname={currentPathname}
                  collapsed={collapsed}
                  activeColor={nav.activeColor}
                  activeBg={nav.activeBg}
                  badgeCount={item.badgeKey ? badges[item.badgeKey] : undefined}
                  isActive={item === activeNavItem}
                />
              ))}
            </nav>
          </div>
        ))}

        {/* ── Administration sections (PlatformAdmin / TenantAdmin only) ──── */}
        {adminSections.map((section, si) => (
          <div key={`admin-${si}`} className={sections.length > 0 || si > 0 ? 'mt-4' : ''}>
            {section.heading && !collapsed && (
              <p className="px-5 mb-1 text-[10px] font-semibold uppercase tracking-widest text-gray-400 select-none">
                {section.heading}
              </p>
            )}
            {(sections.length > 0 || si > 0) && collapsed && (
              <div className="mx-2 mb-2 border-t border-gray-100" />
            )}
            <nav className={clsx('space-y-0.5', collapsed ? 'px-1.5' : 'px-3')}>
              {section.items.map(item => (
                <SidebarItem
                  key={item.href + item.label}
                  item={item}
                  pathname={currentPathname}
                  collapsed={collapsed}
                  activeColor={nav.activeColor}
                  activeBg={nav.activeBg}
                  isActive={item === activeNavItem}
                />
              ))}
            </nav>
          </div>
        ))}
      </div>

      {/* ── Global bottom section (Account / Activity Log) ─────────────────── */}
      {(portalConfig ? portalConfig.showBottomNav : true) && (
        <div className="shrink-0 border-t border-gray-100 py-2">
          {GLOBAL_BOTTOM_NAV.heading && !collapsed && (
            <p className="px-5 mb-1 text-[10px] font-semibold uppercase tracking-widest text-gray-400 select-none">
              {GLOBAL_BOTTOM_NAV.heading}
            </p>
          )}
          <nav className={clsx('space-y-0.5', collapsed ? 'px-1.5' : 'px-3')}>
            {GLOBAL_BOTTOM_NAV.items
              .filter(item => !(selectedProductId === 'lien' && item.href === '/my-work'))
              .filter(item => !item.adminOnly || (session?.isPlatformAdmin || session?.isTenantAdmin))
              .map(item => (
              <SidebarItem
                key={item.href + item.label}
                item={item}
                pathname={currentPathname}
                collapsed={collapsed}
                activeColor={nav.activeColor}
                activeBg={nav.activeBg}
                isActive={item === activeNavItem}
              />
            ))}
          </nav>
        </div>
      )}
    </aside>
  );
}

function SidebarItem({
  item, pathname, collapsed, activeColor, activeBg, badgeCount, isActive: isActiveProp,
}: {
  item:        NavItem;
  pathname:    string;
  collapsed:   boolean;
  activeColor: string;
  activeBg:    string;
  badgeCount?: number;
  isActive?:   boolean;
}) {
  const isActive = isActiveProp ?? (pathname === item.href || pathname.startsWith(item.href + '/'));
  const showBadge = typeof badgeCount === 'number' && badgeCount > 0;

  return (
    <Link
      href={item.href}
      title={collapsed ? `${item.label}${showBadge ? ` (${badgeCount})` : ''}` : undefined}
      className={clsx(
        'relative flex items-center rounded-lg text-[12px] font-medium transition-colors',
        collapsed ? 'w-8 h-8 justify-center mx-auto' : 'gap-2.5 px-3 py-2.5',
        !isActive && 'text-gray-600 hover:bg-gray-100 hover:text-gray-900',
      )}
      style={isActive ? { backgroundColor: activeBg, color: '#0f1928' } : undefined}
    >
      {/* Left accent bar (expanded active) */}
      {isActive && !collapsed && (
        <span
          className="absolute left-0 top-1.5 bottom-1.5 w-0.5 rounded-full"
          style={{ backgroundColor: activeColor }}
        />
      )}
      {/* Right pip (collapsed active) */}
      {isActive && collapsed && (
        <span
          className="absolute -right-0.5 top-1/2 -translate-y-1/2 w-1 h-4 rounded-full"
          style={{ backgroundColor: activeColor }}
        />
      )}
      {item.icon
        ? <i
            className={`${item.icon} text-[16px] leading-none shrink-0`}
            style={{ color: isActive ? activeColor : undefined }}
          />
        : <span className="w-1.5 h-1.5 rounded-full bg-current opacity-50" />
      }
      {!collapsed && <span className="flex-1">{item.label}</span>}
      {showBadge && !collapsed && (
        <span className="ml-auto inline-flex items-center justify-center min-w-[18px] h-[18px] px-1 rounded-full bg-red-500 text-white text-[10px] font-semibold leading-none">
          {badgeCount > 99 ? '99+' : badgeCount}
        </span>
      )}
      {showBadge && collapsed && (
        <span className="absolute -top-0.5 -right-0.5 w-2.5 h-2.5 rounded-full bg-red-500 ring-2 ring-white" />
      )}
    </Link>
  );
}
