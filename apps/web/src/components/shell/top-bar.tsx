'use client';

import Image from 'next/image';
import Link from 'next/link';
import { useState, useRef, useEffect } from 'react';
import { useSession } from '@/hooks/use-session';
import { useProduct } from '@/contexts/product-context';
import { orgTypeLabel, PRODUCT_CODE_TO_NAV_KEY } from '@/lib/nav';
import { getClientPortalConfig, type PortalConfig } from '@/lib/portal';
import { useTenantBranding } from '@/providers/tenant-branding-provider';
import { NotificationBell } from '@/components/shell/notification-bell';

// ── All platform products shown in the app switcher ──────────────────────────

const ALL_PRODUCTS = [
  {
    id:      'careconnect',
    label:   'Synq CareConnect',
    href:    '/careconnect/dashboard',
    iconSrc: '/product-icons/synqconnect.png',
    bg:      '#eff6ff',
  },
  {
    id:      'fund',
    label:   'Synq Funds',
    href:    '/fund/dashboard',
    iconSrc: '/product-icons/synqfund.png',
    bg:      '#f0fdf4',
  },
  {
    id:      'lien',
    label:   'Synq Liens',
    href:    '/lien/dashboard',
    iconSrc: '/product-icons/synqlien.png',
    bg:      '#f5f3ff',
  },
  {
    id:      'ai',
    label:   'Synq AI',
    href:    '/ai/dashboard',
    iconSrc: '/product-icons/synqai.png',
    bg:      '#fffbeb',
  },
  {
    id:      'insights',
    label:   'Synq Insights',
    href:    '/insights/dashboard',
    iconSrc: '/product-icons/synqinsight.png',
    bg:      '#ecfeff',
  },
] as const;

/**
 * Full-width navy top bar.
 * Left:  9-dot app-switcher → logo
 * Right: avatar → profile dropdown
 */
export function TopBar() {
  const { session, clearSession } = useSession();
  const branding = useTenantBranding();

  const [portalConfig, setPortalConfig] = useState<PortalConfig | null>(null);
  useEffect(() => { setPortalConfig(getClientPortalConfig()); }, []);

  const showSwitcher = portalConfig ? portalConfig.showAppSwitcher : true;

  return (
    <header
      className="flex items-center h-14 px-4 shrink-0 gap-3"
      style={{ backgroundColor: '#0f1928' }}
    >
      {/* ── App switcher (hidden on portal-specific portals) ─────────────── */}
      {showSwitcher && <AppSwitcher />}

      {/* ── Vertical divider ────────────────────────────────────────────── */}
      {showSwitcher && (
        <div className="self-center h-5 w-px shrink-0" style={{ backgroundColor: 'rgba(255,255,255,0.15)' }} />
      )}

      {/* ── Logo — portal logo or tenant branding logo ───────────────────── */}
      {portalConfig ? (
        <Link href={portalConfig.landingPath} className="flex items-center min-w-0">
          <img
            src={portalConfig.logoSrc}
            alt={portalConfig.logoLabel}
            style={{ height: 36, width: 'auto', maxWidth: 240 }}
            className="object-contain"
          />
        </Link>
      ) : (
        <Link href="/dashboard" className="flex items-center shrink-0">
          <TenantLogo branding={branding} hasSession={!!session} />
        </Link>
      )}

      {/* ── Spacer ──────────────────────────────────────────────────────── */}
      <div className="flex-1" />

      {/* ── Notification bell ──────────────────────────────────────────── */}
      <NotificationBell />

      {/* ── User menu ────────────────────────────────────────────────────────── */}
      {/* Always render something so the top-right corner never goes blank:    */}
      {/* - skeleton while loading or if session is unavailable               */}
      {/* - UserMenu once the session is confirmed                            */}
      {session ? (
        <UserMenu session={session} clearSession={clearSession} />
      ) : (
        <div className="w-8 h-8 rounded-full bg-white/15 animate-pulse shrink-0" />
      )}
    </header>
  );
}

// ── App switcher button + popout ──────────────────────────────────────────────

function AppSwitcher() {
  const [open, setOpen] = useState(false);
  const ref = useRef<HTMLDivElement>(null);
  const { setSelectedProductId } = useProduct();
  const { session, isLoading } = useSession();

  // Compute visible products only once the session is confirmed loaded.
  // LS-ID-TNT-009: prefer userProducts (user-level effective access from JWT product_codes)
  // over enabledProducts (tenant-level) so the switcher shows only products the user can
  // actually use. Both empty → PlatformAdmin / unconfigured; show all.
  // Note: portal-level restriction is enforced at the TopBar level — AppSwitcher is hidden
  // entirely on restricted portals, so no portal filtering is needed here.
  const visibleProducts: typeof ALL_PRODUCTS[number][] = (() => {
    if (isLoading || !session) return [];                   // not ready yet
    const up = session.userProducts ?? [];
    const ep = session.enabledProducts ?? [];
    const productList = up.length > 0 ? up : ep;           // user-level beats tenant-level
    if (productList.length === 0) return [...ALL_PRODUCTS]; // PlatformAdmin / unconfigured
    const ids = new Set(productList.map(code => PRODUCT_CODE_TO_NAV_KEY[code]).filter(Boolean));
    return ALL_PRODUCTS.filter(p => ids.has(p.id));
  })();

  useEffect(() => {
    if (!open) return;
    function handler(e: MouseEvent) {
      if (ref.current && !ref.current.contains(e.target as Node)) setOpen(false);
    }
    document.addEventListener('mousedown', handler);
    return () => document.removeEventListener('mousedown', handler);
  }, [open]);

  useEffect(() => {
    if (!open) return;
    function handler(e: KeyboardEvent) { if (e.key === 'Escape') setOpen(false); }
    document.addEventListener('keydown', handler);
    return () => document.removeEventListener('keydown', handler);
  }, [open]);

  return (
    <div ref={ref} className="relative flex items-center shrink-0">
      <button
        onClick={() => setOpen(p => !p)}
        title="Switch product"
        aria-haspopup="true"
        aria-expanded={open}
        className={[
          'w-8 h-8 flex items-center justify-center rounded-lg transition-colors',
          open
            ? 'bg-white/15 text-white'
            : 'text-slate-400 hover:bg-white/10 hover:text-white',
        ].join(' ')}
      >
        <i className="ri-apps-2-line text-[18px] leading-none" />
      </button>

      {open && (
        <div className="absolute left-0 top-[calc(100%+10px)] w-64 rounded-xl bg-white shadow-2xl border border-gray-200 overflow-hidden z-50">
          <div className="px-4 py-3 border-b border-gray-100">
            <p className="text-[11px] font-semibold uppercase tracking-widest text-gray-400">
              LegalSynq Products
            </p>
          </div>

          <div className="py-2">
            {visibleProducts.map(product => (
              <Link
                key={product.id}
                href={product.href}
                onClick={() => { setSelectedProductId(product.id); setOpen(false); }}
                className="flex items-center gap-3 px-4 py-2.5 hover:bg-gray-50 transition-colors group"
              >
                <div
                  className="w-9 h-9 rounded-lg flex items-center justify-center shrink-0"
                  style={{ backgroundColor: product.bg }}
                >
                  <img
                    src={product.iconSrc}
                    alt=""
                    aria-hidden
                    className="w-5 h-5 object-contain"
                  />
                </div>
                <span className="text-sm font-medium text-gray-700 group-hover:text-gray-900 transition-colors">
                  {product.label}
                </span>
              </Link>
            ))}

            {visibleProducts.length === 0 && (
              <p className="px-4 py-3 text-xs text-gray-400">
                No products enabled for your account.
              </p>
            )}
          </div>
        </div>
      )}
    </div>
  );
}

// ── Profile dropdown ──────────────────────────────────────────────────────────

interface UserMenuProps {
  session: NonNullable<ReturnType<typeof useSession>['session']>;
  clearSession: () => void;
}

function UserMenu({ session, clearSession }: UserMenuProps) {
  const [open, setOpen] = useState(false);
  const ref = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!open) return;
    function handler(e: MouseEvent) {
      if (ref.current && !ref.current.contains(e.target as Node)) setOpen(false);
    }
    document.addEventListener('mousedown', handler);
    return () => document.removeEventListener('mousedown', handler);
  }, [open]);

  useEffect(() => {
    if (!open) return;
    function handler(e: KeyboardEvent) { if (e.key === 'Escape') setOpen(false); }
    document.addEventListener('keydown', handler);
    return () => document.removeEventListener('keydown', handler);
  }, [open]);

  async function handleSignOut() {
    setOpen(false);
    await fetch('/api/auth/logout', { method: 'POST' });
    clearSession();
    window.location.href = '/login';
  }

  const initials = session.orgName
    ? session.orgName.split(' ').slice(0, 2).map(w => w[0]).join('').toUpperCase()
    : session.email.slice(0, 2).toUpperCase();

  const avatarSrc = session.avatarDocumentId
    ? `/api/profile/avatar/${session.avatarDocumentId}`
    : null;

  return (
    <div ref={ref} className="relative flex items-center shrink-0">
      <button
        onClick={() => setOpen(p => !p)}
        className="flex items-center focus:outline-none group"
        aria-haspopup="true"
        aria-expanded={open}
      >
        {avatarSrc ? (
          <img
            src={avatarSrc}
            alt="Profile"
            className="w-8 h-8 rounded-full object-cover shrink-0 ring-2 ring-transparent group-hover:ring-white/20 transition-all"
          />
        ) : (
          <div
            className="w-8 h-8 rounded-full flex items-center justify-center text-[11px] font-bold text-white shrink-0 ring-2 ring-transparent group-hover:ring-white/20 transition-all"
            style={{ backgroundColor: '#f97316' }}
          >
            {initials}
          </div>
        )}
      </button>

      {open && (
        <div
          className="absolute right-0 top-[calc(100%+10px)] w-64 rounded-xl bg-white shadow-xl border border-gray-200 overflow-hidden z-50"
          role="menu"
        >
          <div className="flex items-center gap-3 px-4 py-3.5 bg-gray-50 border-b border-gray-100">
            {avatarSrc ? (
              <img
                src={avatarSrc}
                alt="Profile"
                className="w-10 h-10 rounded-full object-cover shrink-0"
              />
            ) : (
              <div
                className="w-10 h-10 rounded-full flex items-center justify-center text-sm font-bold text-white shrink-0"
                style={{ backgroundColor: '#f97316' }}
              >
                {initials}
              </div>
            )}
            <div className="min-w-0">
              <p className="text-sm font-semibold text-gray-900 truncate">
                {session.orgName ?? session.email}
              </p>
              <p className="text-xs text-gray-500 truncate">{session.email}</p>
              <p className="text-[10px] text-gray-400 mt-0.5">{orgTypeLabel(session.orgType)}</p>
            </div>
          </div>

          <div className="py-1.5">
            <ProfileMenuItem href="/profile"  icon="ri-user-3-line"     label="Profile"           onClick={() => setOpen(false)} />
            <ProfileMenuItem href="/settings" icon="ri-settings-3-line"  label="Account Settings" onClick={() => setOpen(false)} />
            <ProfileMenuItem href="/activity" icon="ri-history-line"     label="Activity Log"      onClick={() => setOpen(false)} />
          </div>

          <div className="border-t border-gray-100" />

          <div className="py-1.5">
            <button
              onClick={handleSignOut}
              role="menuitem"
              className="flex w-full items-center gap-3 px-4 py-2.5 text-sm text-red-600 hover:bg-red-50 transition-colors"
            >
              <i className="ri-logout-box-r-line text-base leading-none" />
              <span>Log out</span>
            </button>
          </div>
        </div>
      )}
    </div>
  );
}

function TenantLogo({ branding, hasSession }: { branding: ReturnType<typeof useTenantBranding>; hasSession: boolean }) {
  const sources: string[] = [];
  // Authenticated: prefer white logo first, then regular logo
  if (branding.logoWhiteDocumentId && hasSession)
    sources.push(`/api/branding/logo/${branding.logoWhiteDocumentId}`);
  if (branding.logoDocumentId && hasSession)
    sources.push(`/api/branding/logo/${branding.logoDocumentId}`);
  // Direct CDN/URL logo (no auth required)
  if (branding.logoUrl)
    sources.push(branding.logoUrl);
  // Non-authenticated public fallback (e.g. login page): the BFF will attempt
  // /public/logo/{id} which serves scan-clean, IsPublishedAsLogo=true documents
  // without requiring a session cookie.  Only add when there is an actual GUID —
  // the old fallback of '/api/branding/logo/public' passed the literal string
  // "public" which is never a valid GUID and always returned 404.
  if (!hasSession) {
    if (branding.logoWhiteDocumentId)
      sources.push(`/api/branding/logo/${branding.logoWhiteDocumentId}`);
    if (branding.logoDocumentId)
      sources.push(`/api/branding/logo/${branding.logoDocumentId}`);
  }

  const [srcIndex, setSrcIndex] = useState(0);
  const [exhausted, setExhausted] = useState(false);

  const sourcesKey = sources.join('|');
  useEffect(() => {
    setSrcIndex(0);
    setExhausted(false);
  }, [sourcesKey]);

  function handleError() {
    const next = srcIndex + 1;
    if (next < sources.length) {
      setSrcIndex(next);
    } else {
      setExhausted(true);
    }
  }

  if (exhausted || sources.length === 0) {
    return (
      <Image
        src="/legalsynq-logo-white.png"
        alt="LegalSynq"
        width={130}
        height={32}
        priority
        unoptimized
        className="h-8 w-auto"
      />
    );
  }

  const currentSrc  = sources[srcIndex] ?? '';
  const isWhiteSrc  = !!branding.logoWhiteDocumentId && currentSrc.includes(branding.logoWhiteDocumentId);

  return (
    <img
      src={currentSrc}
      alt={branding.displayName || 'Tenant logo'}
      className="w-auto object-contain max-w-[180px]"
      style={{
        height: 32,
        ...(!isWhiteSrc
          ? { filter: 'brightness(0) invert(1)', opacity: 0.9 }
          : {}),
      }}
      onError={handleError}
    />
  );
}

function ProfileMenuItem({
  href, icon, label, onClick,
}: { href: string; icon: string; label: string; onClick: () => void }) {
  return (
    <Link
      href={href}
      role="menuitem"
      onClick={onClick}
      className="flex items-center gap-3 px-4 py-2.5 text-sm text-gray-700 hover:bg-gray-50 transition-colors"
    >
      <i className={`${icon} text-base leading-none text-gray-400`} />
      <span>{label}</span>
    </Link>
  );
}
