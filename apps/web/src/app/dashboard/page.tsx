import { headers } from 'next/headers';
import { redirect } from 'next/navigation';
import { requireOrg } from '@/lib/auth-guards';
import { PRODUCT_META, PRODUCT_NAV, orgTypeLabel, resolveEnabledNavKeys } from '@/lib/nav';
import { getServerPortalConfig } from '@/lib/portal';
import { AppShell } from '@/components/shell/app-shell';
import Link from 'next/link';

export const dynamic = 'force-dynamic';


/**
 * Dashboard — default landing page after login.
 * Shows a welcome card and quick-access tiles for each product.
 */
export default async function DashboardPage() {
  const session = await requireOrg();

  // Portal-specific overrides: redirect to the portal's own landing page.
  const headersList  = await headers();
  const rawHost      = headersList.get('x-forwarded-host') ?? headersList.get('host') ?? '';
  const portalConfig = getServerPortalConfig(rawHost);
  if (portalConfig) redirect(portalConfig.landingPath);

  // Filter product tiles to only those enabled for this tenant.
  // resolveEnabledNavKeys falls back to ALL products when the list is empty
  // (e.g. PlatformAdmin sessions, or tenants with no entitlements configured yet).
  const enabledKeys    = resolveEnabledNavKeys(session.enabledProducts ?? []);
  const productEntries = Object.entries(PRODUCT_META).filter(([id]) => enabledKeys.has(id));

  return (
    <AppShell>
      <div className="max-w-4xl space-y-8">

        {/* Welcome header */}
        <div>
          <h1 className="text-xl font-bold text-[#0f1928]">
            Welcome back{session.orgName ? `, ${session.orgName}` : ''}
          </h1>
          <p className="text-sm text-gray-500 mt-1">
            {orgTypeLabel(session.orgType)} · {session.email}
          </p>
        </div>

        {/* Product tiles */}
        <div>
          <p className="text-xs font-semibold uppercase tracking-wider text-gray-400 mb-3">
            Your Products
          </p>
          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
            {productEntries.map(([id, meta]) => (
              <ProductCard
                key={id}
                id={id}
                meta={meta}
                items={(PRODUCT_NAV[id] ?? []).flatMap(s => s.items).slice(0, 3)}
              />
            ))}
          </div>
        </div>

        {/* Admin shortcut */}
        {(session.isTenantAdmin || session.isPlatformAdmin) && (
          <div>
            <p className="text-xs font-semibold uppercase tracking-wider text-gray-400 mb-3">
              Administration
            </p>
            <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
              <AdminCard
                href="/admin/users"
                icon="ri-group-line"
                label="Users"
                description="Manage users and their roles"
              />
              <AdminCard
                href="/admin/organizations"
                icon="ri-building-line"
                label="Organizations"
                description="View and manage organizations"
              />
            </div>
          </div>
        )}

      </div>
    </AppShell>
  );
}

// ── Product card ──────────────────────────────────────────────────────────────

function ProductCard({
  id, meta, items,
}: {
  id: string;
  meta: { label: string; icon: string; color: string; iconSrc: string };
  items: { href: string; label: string }[];
}) {
  const bgMap: Record<string, string> = {
    careconnect: '#eff6ff',
    fund:        '#f0fdf4',
    lien:        '#f5f3ff',
    ai:          '#fffbeb',
    insights:    '#ecfeff',
  };
  const bg = bgMap[id] ?? '#f9fafb';
  const primaryHref = items[0]?.href ?? '#';

  return (
    <Link
      href={primaryHref}
      className="group block rounded-xl border border-gray-200 bg-white p-5 hover:shadow-md hover:border-gray-300 transition-all"
    >
      <div
        className="inline-flex items-center justify-center w-10 h-10 rounded-lg mb-4"
        style={{ backgroundColor: bg }}
      >
        {meta.iconSrc
          ? <img src={meta.iconSrc} alt="" aria-hidden className="w-6 h-6 object-contain" />
          : <i className={`${meta.icon} text-lg`} style={{ color: meta.color }} />
        }
      </div>

      <p className="text-sm font-bold text-[#0f1928] group-hover:text-orange-600 transition-colors">
        {meta.label}
      </p>

      <ul className="mt-3 space-y-1">
        {items.map(item => (
          <li key={item.href} className="flex items-center gap-1.5 text-[11px] text-gray-500">
            <i className="ri-arrow-right-s-line text-gray-300 text-sm" />
            {item.label}
          </li>
        ))}
      </ul>
    </Link>
  );
}

// ── Admin card ────────────────────────────────────────────────────────────────

function AdminCard({ href, icon, label, description }: {
  href: string; icon: string; label: string; description: string;
}) {
  return (
    <Link
      href={href}
      className="group flex items-start gap-3 rounded-xl border border-gray-200 bg-white p-4 hover:shadow-md hover:border-gray-300 transition-all"
    >
      <div className="inline-flex items-center justify-center w-8 h-8 rounded-lg bg-gray-50 border border-gray-100 shrink-0">
        <i className={`${icon} text-sm text-gray-400`} />
      </div>
      <div>
        <p className="text-sm font-semibold text-[#0f1928] group-hover:text-orange-600 transition-colors">
          {label}
        </p>
        <p className="text-[11px] text-gray-400 mt-0.5">{description}</p>
      </div>
    </Link>
  );
}
