/**
 * NoTenantContext — shown on every Notifications page when no tenant context
 * is active.  All notifications endpoints require x-tenant-id, so there is
 * nothing to display without a scoped tenant.
 */
export function NoTenantContext() {
  return (
    <div className="rounded-lg border border-amber-200 bg-amber-50 px-6 py-10 text-center">
      <i className="ri-building-2-line text-3xl text-amber-400 mb-3 block" />
      <p className="text-sm font-semibold text-amber-800 mb-1">No tenant selected</p>
      <p className="text-sm text-amber-700 max-w-sm mx-auto">
        The Notifications section is scoped to a tenant.{' '}
        <a href="/tenants" className="underline hover:text-amber-900 font-medium">
          Open the Tenants page
        </a>{' '}
        and activate a tenant context first.
      </p>
    </div>
  );
}
