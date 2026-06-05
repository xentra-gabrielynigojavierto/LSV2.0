import { requirePlatformAdmin }        from '@/lib/auth-guards';
import { controlCenterServerApi }     from '@/lib/control-center-api';
import { CCShell }                    from '@/components/shell/cc-shell';
import { PermissionCatalogTable }     from '@/components/users/permission-catalog-table';
import { PermissionSearchBar }        from '@/components/users/permission-search-bar';
import { PermissionCreateDialog }     from '@/components/users/permission-create-dialog';

export const dynamic = 'force-dynamic';

interface PermissionsPageProps {
  searchParams?: Promise<{ search?: string; product?: string }>;
}

export default async function PermissionsPage(props: PermissionsPageProps) {
  const searchParams = await props.searchParams;
  const session    = await requirePlatformAdmin();
  const search     = searchParams?.search ?? '';
  const productId  = searchParams?.product ?? '';

  let permissions = null;
  let fetchError: string | null = null;

  try {
    permissions = await controlCenterServerApi.permissions.list({
      search:    search    || undefined,
      productId: productId || undefined,
    });
  } catch (err) {
    fetchError = err instanceof Error ? err.message : 'Failed to load permissions.';
  }

  let allProducts: Array<{ id: string; name: string; code: string }> = [];
  try {
    const all = await controlCenterServerApi.permissions.list();
    allProducts = [...new Map(
      all.map(p => [p.productId, { id: p.productId, name: p.productName, code: p.productCode }])
    ).values()];
  } catch {
  }

  const activeProduct = allProducts.find(p => p.id === productId);

  return (
    <CCShell userEmail={session.email}>
      <div className="space-y-4">

        <div className="flex items-center justify-between">
          <div>
            <h1 className="text-xl font-semibold text-gray-900">Permission Catalog</h1>
            <p className="text-sm text-gray-500 mt-0.5">
              Product and platform permissions — platform governance only
            </p>
          </div>
          <PermissionCreateDialog
            products={allProducts.map(p => ({ code: p.code, name: p.name }))}
          />
        </div>

        {/* Governance boundary notice */}
        <div className="bg-indigo-50 border border-indigo-100 rounded-lg px-4 py-3 text-sm text-indigo-700">
          This catalog covers product and platform permissions only. Tenant-level permissions (TENANT.*)
          are managed per-tenant in the Tenant Portal — they are not editable from Control Center.
        </div>

        {allProducts.length > 0 && (
          <div className="flex flex-wrap gap-2 items-center">
            <a
              href={`/permissions${search ? `?search=${encodeURIComponent(search)}` : ''}`}
              className={`inline-flex items-center px-2.5 py-1 rounded-full text-xs font-medium border transition-colors ${
                !productId
                  ? 'bg-indigo-600 text-white border-indigo-600'
                  : 'bg-white text-gray-600 border-gray-300 hover:border-indigo-300 hover:bg-indigo-50'
              }`}
            >
              All
            </a>
            {allProducts.map(p => (
              <a
                key={p.id}
                href={`/permissions?product=${p.id}${search ? `&search=${encodeURIComponent(search)}` : ''}`}
                className={`inline-flex items-center px-2.5 py-1 rounded-full text-xs font-medium border transition-colors ${
                  productId === p.id
                    ? 'bg-indigo-600 text-white border-indigo-600'
                    : 'bg-white text-gray-600 border-gray-300 hover:border-indigo-300 hover:bg-indigo-50'
                }`}
              >
                {p.name}
              </a>
            ))}
          </div>
        )}

        <PermissionSearchBar initialSearch={search} productId={productId} />

        {(search || activeProduct) && (
          <div className="flex items-center gap-2 text-xs text-gray-500">
            <span>Filtering by</span>
            {activeProduct && (
              <span className="bg-indigo-50 text-indigo-700 border border-indigo-100 px-2 py-0.5 rounded">
                {activeProduct.name}
              </span>
            )}
            {search && (
              <span className="bg-gray-100 text-gray-700 border border-gray-200 px-2 py-0.5 rounded font-mono">
                &quot;{search}&quot;
              </span>
            )}
            <a
              href={`/permissions${productId ? `?product=${productId}` : ''}`}
              className="text-indigo-600 hover:underline ml-1"
            >
              Clear search
            </a>
          </div>
        )}

        {permissions && !fetchError && (
          <p className="text-xs text-gray-400">
            {permissions.length} permission{permissions.length !== 1 ? 's' : ''}
            {search || productId ? ' matching filters' : ' total'}
          </p>
        )}

        {fetchError && (
          <div className="bg-red-50 border border-red-200 rounded-lg px-4 py-3 text-sm text-red-700">
            {fetchError}
          </div>
        )}

        {permissions && (
          <PermissionCatalogTable permissions={permissions} />
        )}
      </div>
    </CCShell>
  );
}
