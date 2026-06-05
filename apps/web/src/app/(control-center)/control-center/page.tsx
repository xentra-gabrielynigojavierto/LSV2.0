import Link from 'next/link';
import { requireCCPlatformAdmin } from '@/lib/auth-guards';
import { CCRoutes } from '@/lib/control-center-routes';

export const dynamic = 'force-dynamic';


/**
 * Control Center Dashboard
 *
 * Minimal async Server Component — renders a module card grid linking to
 * all future Control Center sections. No backend calls yet.
 *
 * All hrefs use CCRoutes so they work in both embedded and standalone deployment.
 */

interface ModuleCard {
  href:        string;
  title:       string;
  description: string;
  badge?:      string;
}

const MODULES: ModuleCard[] = [
  {
    href:        CCRoutes.tenants,
    title:       'Tenants',
    description: 'View and manage all tenants on the platform.',
    badge:       'Core',
  },
  {
    href:        CCRoutes.tenantUsers,
    title:       'Tenant Users',
    description: 'Search and administer users across all tenants.',
    badge:       'Core',
  },
  {
    href:        CCRoutes.roles,
    title:       'Roles & Permissions',
    description: 'Inspect product roles and their permission assignments.',
  },
  {
    href:        CCRoutes.products,
    title:       'Product Entitlements',
    description: 'Enable or disable product access per tenant.',
  },
  {
    href:        CCRoutes.support,
    title:       'Support Tools',
    description: 'Lookup sessions, reset credentials, and assist tenants.',
  },
  {
    href:        CCRoutes.notifications,
    title:       'Notifications',
    description: 'Manage provider configs, templates, billing plans, and contact policies.',
    badge:       'Admin',
  },
  {
    href:        CCRoutes.auditLogs,
    title:       'Audit Logs',
    description: 'Browse platform-wide administrative event history.',
    badge:       'Coming soon',
  },
  {
    href:        CCRoutes.monitoring,
    title:       'Monitoring',
    description: 'Check live health status of all platform services.',
  },
  {
    href:        CCRoutes.settings,
    title:       'Platform Settings',
    description: 'Manage global platform configuration and feature flags.',
  },
];

export default async function ControlCenterDashboardPage() {
  const session = await requireCCPlatformAdmin();

  return (
    <div className="space-y-6">
      {/* Header */}
      <div>
        <h1 className="text-xl font-semibold text-gray-900">Control Center</h1>
        <p className="mt-1 text-sm text-gray-500">
          Platform administration — signed in as{' '}
          <span className="font-medium text-gray-700">{session.email}</span>
        </p>
      </div>

      {/* Module grid */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-4">
        {MODULES.map(module => (
          <ModuleCard key={module.href} module={module} />
        ))}
      </div>
    </div>
  );
}

function ModuleCard({ module }: { module: ModuleCard }) {
  return (
    <Link
      href={module.href}
      className="group block bg-white border border-gray-200 rounded-lg p-5 hover:border-indigo-300 hover:shadow-sm transition-all"
    >
      <div className="flex items-start justify-between gap-2">
        <h2 className="text-sm font-semibold text-gray-900 group-hover:text-indigo-700 transition-colors">
          {module.title}
        </h2>
        {module.badge && (
          <span className={`shrink-0 text-[10px] font-semibold px-1.5 py-0.5 rounded uppercase tracking-wide ${
            module.badge === 'Coming soon'
              ? 'bg-gray-100 text-gray-400 border border-gray-200'
              : 'bg-indigo-50 text-indigo-600 border border-indigo-200'
          }`}>
            {module.badge}
          </span>
        )}
      </div>
      <p className="mt-2 text-xs text-gray-500 leading-relaxed">
        {module.description}
      </p>
      <p className="mt-3 text-xs text-indigo-600 font-medium opacity-0 group-hover:opacity-100 transition-opacity">
        Open →
      </p>
    </Link>
  );
}
