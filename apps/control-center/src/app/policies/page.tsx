import { requirePlatformAdmin }       from '@/lib/auth-guards';
import { controlCenterServerApi }     from '@/lib/control-center-api';
import { CCShell }                    from '@/components/shell/cc-shell';
import { PolicyListTable }           from '@/components/policies/policy-list-table';
import { PolicyCreateDialog }        from '@/components/policies/policy-create-dialog';

export const dynamic = 'force-dynamic';

interface PoliciesPageProps {
  searchParams?: Promise<{ search?: string; product?: string }>;
}

export default async function PoliciesPage(props: PoliciesPageProps) {
  const searchParams = await props.searchParams;
  const session      = await requirePlatformAdmin();
  const search       = searchParams?.search ?? '';
  const productCode  = searchParams?.product ?? '';

  let policies = null;
  let fetchError: string | null = null;

  try {
    policies = await controlCenterServerApi.policies.list({
      search:      search      || undefined,
      productCode: productCode || undefined,
    });
  } catch (err) {
    fetchError = err instanceof Error ? err.message : 'Failed to load policies.';
  }

  const productCodes = policies
    ? [...new Set(policies.map(p => p.productCode))].sort()
    : [];

  return (
    <CCShell userEmail={session.email}>
      <div className="space-y-4">

        <div className="flex items-center justify-between">
          <div>
            <h1 className="text-xl font-semibold text-gray-900">Authorization Policies</h1>
            <p className="text-sm text-gray-500 mt-0.5">
              ABAC policies for context-aware permission enforcement
            </p>
          </div>
          <PolicyCreateDialog products={productCodes} />
        </div>

        <div className="bg-indigo-50 border border-indigo-100 rounded-lg px-4 py-3 text-sm text-indigo-700">
          Policies add conditional rules to permissions. When enabled, the system evaluates
          attribute-based conditions (amount limits, region restrictions, org boundaries)
          before allowing access.
        </div>

        {productCodes.length > 0 && (
          <div className="flex flex-wrap gap-2 items-center">
            <a
              href={`/policies${search ? `?search=${encodeURIComponent(search)}` : ''}`}
              className={`inline-flex items-center px-2.5 py-1 rounded-full text-xs font-medium border transition-colors ${
                !productCode
                  ? 'bg-indigo-600 text-white border-indigo-600'
                  : 'bg-white text-gray-600 border-gray-300 hover:border-indigo-300 hover:bg-indigo-50'
              }`}
            >
              All
            </a>
            {productCodes.map(pc => (
              <a
                key={pc}
                href={`/policies?product=${encodeURIComponent(pc)}${search ? `&search=${encodeURIComponent(search)}` : ''}`}
                className={`inline-flex items-center px-2.5 py-1 rounded-full text-xs font-medium border transition-colors ${
                  productCode === pc
                    ? 'bg-indigo-600 text-white border-indigo-600'
                    : 'bg-white text-gray-600 border-gray-300 hover:border-indigo-300 hover:bg-indigo-50'
                }`}
              >
                {pc}
              </a>
            ))}
          </div>
        )}

        {fetchError && (
          <div className="bg-red-50 border border-red-200 rounded-lg px-4 py-3 text-sm text-red-700">
            {fetchError}
          </div>
        )}

        {policies && (
          <>
            <p className="text-xs text-gray-400">
              {policies.length} polic{policies.length !== 1 ? 'ies' : 'y'}
              {search || productCode ? ' matching filters' : ' total'}
            </p>
            <PolicyListTable policies={policies} />
          </>
        )}
      </div>
    </CCShell>
  );
}
