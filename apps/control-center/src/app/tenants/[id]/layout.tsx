import Link                          from 'next/link';
import type { ReactNode }            from 'react';
import { requirePlatformAdmin }      from '@/lib/auth-guards';
import { getTenantContext }          from '@/lib/auth';
import { getCachedTenantById }       from '@/lib/tenant-fetch';
import { Routes }                    from '@/lib/routes';
import { CCShell }                   from '@/components/shell/cc-shell';
import { TenantActions }             from '@/components/tenants/tenant-actions';
import { TenantNavTabs }             from '@/components/tenants/tenant-nav-tabs';
import { switchTenantContextAction } from '@/app/actions/tenant-context';
import type { TenantStatus, TenantType } from '@/types/control-center';

interface TenantDetailLayoutProps {
  children: ReactNode;
  params:   Promise<{ id: string }>;
}

/**
 * Shared layout for /tenants/[id]/* pages.
 *
 * Renders the standard tenant header (breadcrumb, name, status badge,
 * code/type, action buttons, and sub-nav tabs) consistently across the
 * Overview, Users, and Notifications tabs.
 *
 * Each child page is responsible only for its body content.
 */
export default async function TenantDetailLayout({
  children,
  params,
}: TenantDetailLayoutProps) {
  const session   = await requirePlatformAdmin();
  const tenantCtx = await getTenantContext();
  const { id }    = await params;

  let tenant     = null;
  let fetchError: string | null = null;

  try {
    tenant = await getCachedTenantById(id);
  } catch (err) {
    fetchError = err instanceof Error ? err.message : 'Failed to load tenant.';
  }

  return (
    <CCShell userEmail={session.email}>
      <div className="space-y-5">

        {/* ── Breadcrumb ──────────────────────────────────────────────────── */}
        <nav className="flex items-center gap-1.5 text-sm text-gray-500">
          <Link href={Routes.tenants} className="hover:text-gray-900 transition-colors">
            Tenants
          </Link>
          <span className="text-gray-300">›</span>
          <span className="text-gray-900 font-medium">
            {tenant ? tenant.displayName : id}
          </span>
        </nav>

        {/* ── Tenant fetch error ───────────────────────────────────────────── */}
        {fetchError && (
          <div className="bg-red-50 border border-red-200 rounded-lg px-4 py-3 text-sm text-red-700">
            {fetchError}
          </div>
        )}

        {/* ── Not found state ─────────────────────────────────────────────── */}
        {!fetchError && !tenant && (
          <div className="bg-white border border-gray-200 rounded-lg p-10 text-center space-y-3">
            <p className="text-sm font-medium text-gray-700">Tenant not found</p>
            <p className="text-xs text-gray-400">
              No tenant with ID{' '}
              <code className="font-mono bg-gray-100 px-1 rounded">{id}</code> exists.
            </p>
            <Link href={Routes.tenants} className="text-xs text-indigo-600 hover:underline">
              ← Back to Tenants
            </Link>
          </div>
        )}

        {/* ── Standard tenant header ───────────────────────────────────────── */}
        {tenant && (
          <>
            <div className="flex items-start justify-between gap-4">
              <div className="space-y-1">
                <div className="flex items-center gap-3">
                  <h1 className="text-xl font-semibold text-gray-900">
                    {tenant.displayName}
                  </h1>
                  <StatusBadge status={tenant.status} />
                </div>
                <p className="text-sm text-gray-500">
                  <span className="font-mono text-xs bg-gray-100 px-1.5 py-0.5 rounded text-gray-600">
                    {tenant.code}
                  </span>
                  <span className="ml-2">{formatType(tenant.type)}</span>
                </p>
              </div>

              {/* Action buttons */}
              <div className="flex items-center gap-2 shrink-0">

                {/* Context switch — state-aware */}
                {tenantCtx?.tenantId === tenant.id ? (
                  <span className="inline-flex items-center gap-1.5 text-xs font-semibold text-amber-700 bg-amber-100 border border-amber-300 px-3 py-1.5 rounded-md">
                    <span className="h-1.5 w-1.5 rounded-full bg-amber-500" />
                    Active Context
                  </span>
                ) : (
                  (() => {
                    const switchAction = switchTenantContextAction.bind(null, {
                      tenantId:   tenant.id,
                      tenantName: tenant.displayName,
                      tenantCode: tenant.code,
                    });
                    return (
                      <form action={switchAction}>
                        <button
                          type="submit"
                          className="inline-flex items-center gap-1.5 text-sm font-medium text-amber-700 hover:text-amber-900 bg-amber-50 hover:bg-amber-100 border border-amber-300 hover:border-amber-400 px-3 py-1.5 rounded-md transition-colors"
                          title={
                            tenantCtx
                              ? `Switch context from ${tenantCtx.tenantName} to ${tenant.displayName}`
                              : `Switch admin view into ${tenant.displayName}`
                          }
                        >
                          <span aria-hidden="true">⇄</span>
                          Switch to Tenant Context
                        </button>
                      </form>
                    );
                  })()
                )}

                <TenantActions tenantId={tenant.id} currentStatus={tenant.status} />
              </div>
            </div>

            {/* Sub-navigation tabs (client component — reads pathname) */}
            <TenantNavTabs
              overviewHref={Routes.tenantDetail(id)}
              usersHref={Routes.tenantUsers_(id)}
              notificationsHref={Routes.tenantNotifications(id)}
              activityHref={Routes.tenantActivity(id)}
            />
          </>
        )}

        {/* ── Tab body ─────────────────────────────────────────────────────── */}
        {children}

      </div>
    </CCShell>
  );
}

// ── Helpers ───────────────────────────────────────────────────────────────────

function StatusBadge({ status }: { status: TenantStatus }) {
  const styles: Record<TenantStatus, string> = {
    Active:    'bg-green-50 text-green-700 border-green-200',
    Inactive:  'bg-gray-100 text-gray-500 border-gray-200',
    Suspended: 'bg-red-50 text-red-700 border-red-200',
  };
  return (
    <span className={`inline-flex items-center px-2 py-0.5 rounded text-[11px] font-semibold border ${styles[status]}`}>
      {status}
    </span>
  );
}

function formatType(type: TenantType): string {
  const labels: Record<TenantType, string> = {
    LawFirm:    'Law Firm',
    Provider:   'Provider',
    Funder:     'Funder',
    LienOwner:  'Lien Owner',
    Corporate:  'Corporate',
    Government: 'Government',
    Other:      'Other',
  };
  return labels[type] ?? type;
}
