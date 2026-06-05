'use client';

import Link from 'next/link';
import { usePathname, useSearchParams } from 'next/navigation';
import { useState, useEffect, Suspense } from 'react';
import { useSettings } from '@/contexts/settings-context';
import { getSectionForPathname, getSectionBySlug } from '@/lib/nav-utils';
import type { NavItem } from '@/types';
import { clsx } from 'clsx';

const STORAGE_KEY = 'ls_cc_sidebar_collapsed';

/**
 * Control Center compact sidebar.
 *
 * Active group resolution:
 *   • On a non-home route  → infer from pathname (getSectionForPathname)
 *   • On / with ?group=X  → resolve from query param (getSectionBySlug)
 *   • On / with no param  → undefined, sidebar shows Home only
 *
 * Home highlight:
 *   Active only when pathname === '/' AND no group is selected.
 *   When a category group is selected (even from /), Home appears unselected
 *   so the group context is the clear active state.
 *
 * Collapse toggle (220px ↔ 52px) and Ctrl+[ shortcut are preserved.
 */

// ── Inner (reads useSearchParams — must live inside a Suspense boundary) ─────

function CCSidebarInner({ collapsed, mounted, toggle }: {
  collapsed: boolean;
  mounted:   boolean;
  toggle:    () => void;
}) {
  const pathname     = usePathname();
  const searchParams = useSearchParams();
  const settings     = useSettings();
  const nav          = settings.appearance.nav;

  const isHome     = pathname === '/';
  const groupParam = isHome ? searchParams.get('group') : null;

  // Active section: param-based on home, pathname-based elsewhere
  const contextSection = groupParam
    ? getSectionBySlug(groupParam)
    : isHome
      ? undefined
      : getSectionForPathname(pathname ?? '');

  // Home is only "active" on pure / with no group selected
  const homeIsActive = isHome && !groupParam;

  const width = !mounted ? 220 : collapsed ? 52 : 220;

  return (
    <aside
      className="shrink-0 flex flex-col bg-white border-r border-gray-200 overflow-hidden"
      style={{
        width,
        transition: mounted ? 'width 200ms ease' : undefined,
        alignSelf: 'stretch',
      }}
    >
      {/* ── Header ──────────────────────────────────────────────────────────── */}
      <div className={clsx(
        'shrink-0 flex items-center border-b border-gray-100 h-12',
        collapsed ? 'justify-center' : 'justify-between px-4',
      )}>
        {!collapsed && (
          <div className="flex items-center gap-2 min-w-0">
            <i className="ri-shield-star-line text-[15px]" style={{ color: nav.activeColor }} />
            <span className="text-[12px] font-semibold text-gray-700 truncate">Control Center</span>
          </div>
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

      {/* ── Nav items ───────────────────────────────────────────────────────── */}
      <div className="flex-1 overflow-y-auto overflow-x-hidden py-2">

        {/* Home — always visible */}
        <nav className={clsx('space-y-0.5', collapsed ? 'px-1.5' : 'px-3')}>
          <SidebarItem
            item={{ href: '/', label: 'Home', icon: 'ri-home-3-line' }}
            pathname={pathname ?? ''}
            collapsed={collapsed}
            activeColor={nav.activeColor}
            activeBg={nav.activeBg}
            forceActive={homeIsActive}
          />
        </nav>

        {/* Active group section — from ?group param (home) or pathname (deep routes) */}
        {contextSection && (
          <div className="mt-3">
            {/* Section label — expanded mode only */}
            {!collapsed && contextSection.heading && (
              <div className="px-3 mx-2 mb-1">
                <span className="text-[10px] font-semibold uppercase tracking-widest text-gray-400 select-none">
                  {contextSection.heading}
                </span>
              </div>
            )}

            {/* Thin divider in icon-only mode */}
            {collapsed && (
              <div className="mx-2 mb-2 border-t border-gray-100" />
            )}

            {/* Sub-grouped layout (e.g. Notifications: Email / SMS / General Settings) */}
            {contextSection.subGroups && contextSection.subGroups.length > 0 ? (
              <div className="space-y-1">
                {contextSection.subGroups.map((group, gi) => (
                  <div key={group.label}>
                    {/* Sub-group label — expanded mode only; divider between groups */}
                    {!collapsed && (
                      <div className={clsx('px-5 flex items-center gap-2', gi > 0 ? 'pt-2 mt-1' : 'pt-0')}>
                        {gi > 0 && <div className="flex-1 border-t border-gray-100" />}
                        <span className="text-[9px] font-semibold uppercase tracking-widest text-gray-400 select-none whitespace-nowrap">
                          {group.label}
                        </span>
                        {gi === 0 && <div className="flex-1 border-t border-gray-100" />}
                      </div>
                    )}
                    {/* Collapsed: thin rule between groups */}
                    {collapsed && gi > 0 && (
                      <div className="mx-2 my-1 border-t border-gray-100" />
                    )}
                    <nav className={clsx('space-y-0.5 mt-0.5', collapsed ? 'px-1.5' : 'px-3')}>
                      {group.items.map(item => (
                        <SidebarItem
                          key={item.href}
                          item={item}
                          pathname={pathname ?? ''}
                          collapsed={collapsed}
                          activeColor={nav.activeColor}
                          activeBg={nav.activeBg}
                        />
                      ))}
                    </nav>
                  </div>
                ))}
              </div>
            ) : (
              /* Flat layout (all other sections) */
              <nav className={clsx('space-y-0.5', collapsed ? 'px-1.5' : 'px-3')}>
                {contextSection.items.map(item => (
                  <SidebarItem
                    key={item.href}
                    item={item}
                    pathname={pathname ?? ''}
                    collapsed={collapsed}
                    activeColor={nav.activeColor}
                    activeBg={nav.activeBg}
                  />
                ))}
              </nav>
            )}
          </div>
        )}

        {/* Empty state hint — home with no group, expanded only */}
        {!contextSection && isHome && !collapsed && (
          <div className="mx-3 mt-3 px-3 py-3 rounded-lg border border-dashed border-gray-200 text-center">
            <p className="text-[10px] text-gray-400 leading-relaxed">
              Select a category<br />to see its tools here
            </p>
          </div>
        )}
      </div>
    </aside>
  );
}

// ── Outer wrapper (manages collapse state, keyboard shortcut, Suspense) ───────

export function CCSidebar() {
  const [collapsed, setCollapsed] = useState(false);
  const [mounted,   setMounted]   = useState(false);

  useEffect(() => {
    const stored = localStorage.getItem(STORAGE_KEY);
    if (stored === 'true') setCollapsed(true);
    setMounted(true);
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

  return (
    <Suspense
      fallback={
        <aside
          className="shrink-0 flex flex-col bg-white border-r border-gray-200 overflow-hidden"
          style={{ width: collapsed ? 52 : 220, alignSelf: 'stretch' }}
        />
      }
    >
      <CCSidebarInner collapsed={collapsed} mounted={mounted} toggle={toggle} />
    </Suspense>
  );
}

// ── SidebarItem ───────────────────────────────────────────────────────────────

function SidebarItem({
  item, pathname, collapsed, activeColor, activeBg, forceActive,
}: {
  item:         NavItem;
  pathname:     string;
  collapsed:    boolean;
  activeColor:  string;
  activeBg:     string;
  forceActive?: boolean;
}) {
  const isActive = forceActive !== undefined
    ? forceActive
    : pathname === item.href || pathname.startsWith(item.href + '/');

  return (
    <Link
      href={item.href}
      title={collapsed ? item.label : undefined}
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
      {!collapsed && (
        <span className="flex-1 min-w-0 flex items-center gap-1.5">
          <span className="truncate">{item.label}</span>
          {item.badge && <NavBadge badge={item.badge} />}
        </span>
      )}
    </Link>
  );
}

function NavBadge({ badge }: { badge: NonNullable<NavItem['badge']> }) {
  const styles: Record<string, string> = {
    'LIVE':        'bg-emerald-100 text-emerald-700',
    'IN PROGRESS': 'bg-amber-100   text-amber-700',
    'MOCKUP':      'bg-gray-100    text-gray-500',
    'NEW':         'bg-blue-100    text-blue-700',
  };
  return (
    <span className={`shrink-0 text-[9px] font-semibold px-1.5 py-0.5 rounded-full leading-none ${styles[badge] ?? styles['MOCKUP']}`}>
      {badge}
    </span>
  );
}
