import type { PermissionCatalogItem } from '@/types/control-center';
import { PermissionRowActions } from './permission-row-actions';

interface PermissionCatalogTableProps {
  permissions:    PermissionCatalogItem[];
  productFilter?: string;
}

function ActiveBadge({ isActive }: { isActive: boolean }) {
  return isActive ? (
    <span className="inline-flex items-center px-2 py-0.5 rounded text-[11px] font-semibold border bg-green-50 text-green-700 border-green-200">
      Active
    </span>
  ) : (
    <span className="inline-flex items-center px-2 py-0.5 rounded text-[11px] font-semibold border bg-gray-100 text-gray-500 border-gray-200">
      Inactive
    </span>
  );
}

const PRODUCT_COLORS: Record<string, string> = {
  'CareConnect': 'bg-teal-50   text-teal-700   border-teal-100',
  'SynqLien':    'bg-amber-50  text-amber-700  border-amber-100',
  'SynqFund':    'bg-violet-50 text-violet-700 border-violet-100',
};

function ProductBadge({ name }: { name: string }) {
  const cls = PRODUCT_COLORS[name] ?? 'bg-gray-50 text-gray-600 border-gray-200';
  return (
    <span className={`inline-flex items-center px-2 py-0.5 rounded text-[11px] font-semibold border ${cls}`}>
      {name}
    </span>
  );
}

export function PermissionCatalogTable({
  permissions,
  productFilter,
}: PermissionCatalogTableProps) {
  const filtered = productFilter
    ? permissions.filter(p => p.productId === productFilter)
    : permissions;

  if (filtered.length === 0) {
    return (
      <div className="bg-white border border-gray-200 rounded-lg p-10 text-center">
        <p className="text-sm text-gray-400">No permissions found.</p>
      </div>
    );
  }

  const byProduct = filtered.reduce<Record<string, PermissionCatalogItem[]>>((acc, p) => {
    if (!acc[p.productName]) acc[p.productName] = [];
    acc[p.productName].push(p);
    return acc;
  }, {});

  const productNames = Object.keys(byProduct);

  return (
    <div className="space-y-4">
      {productNames.map((productName, gi) => {
        const items = byProduct[productName];
        return (
          <div
            key={productName}
            className="bg-white border border-gray-200 rounded-lg overflow-hidden"
          >
            <div className="flex items-center justify-between px-4 py-3 bg-gray-50 border-b border-gray-200">
              <div className="flex items-center gap-2.5">
                <ProductBadge name={productName} />
              </div>
              <span className="text-xs text-gray-400">
                {items.length} permission{items.length !== 1 ? 's' : ''}
              </span>
            </div>

            <div className="overflow-x-auto">
              <table className="min-w-full divide-y divide-gray-100">
                <thead>
                  <tr className="bg-gray-50/50">
                    <th className="px-4 py-2.5 text-left text-[11px] font-semibold text-gray-400 uppercase tracking-wide">Code</th>
                    <th className="px-4 py-2.5 text-left text-[11px] font-semibold text-gray-400 uppercase tracking-wide">Name</th>
                    <th className="px-4 py-2.5 text-left text-[11px] font-semibold text-gray-400 uppercase tracking-wide">Category</th>
                    <th className="px-4 py-2.5 text-left text-[11px] font-semibold text-gray-400 uppercase tracking-wide">Description</th>
                    <th className="px-4 py-2.5 text-left text-[11px] font-semibold text-gray-400 uppercase tracking-wide w-24">Status</th>
                    <th className="px-4 py-2.5 text-right text-[11px] font-semibold text-gray-400 uppercase tracking-wide w-20">Actions</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-50">
                  {items.map(perm => (
                    <tr key={perm.id} className="hover:bg-gray-50 transition-colors">
                      <td className="px-4 py-3">
                        <code className="text-xs bg-gray-100 px-1.5 py-0.5 rounded text-gray-700 font-mono whitespace-nowrap">
                          {perm.code}
                        </code>
                      </td>
                      <td className="px-4 py-3 text-sm font-medium text-gray-900 whitespace-nowrap">
                        {perm.name}
                      </td>
                      <td className="px-4 py-3 text-sm text-gray-500 whitespace-nowrap">
                        {perm.category ?? <span className="text-gray-300 italic">—</span>}
                      </td>
                      <td className="px-4 py-3 text-sm text-gray-500 max-w-xs">
                        {perm.description ?? <span className="text-gray-300 italic">—</span>}
                      </td>
                      <td className="px-4 py-3">
                        <ActiveBadge isActive={perm.isActive} />
                      </td>
                      <td className="px-4 py-3 text-right">
                        <PermissionRowActions permission={perm} />
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            {gi === productNames.length - 1 && !productFilter && (
              <div className="px-4 py-2 border-t border-gray-100 bg-gray-50/40">
                <p className="text-xs text-gray-400">
                  {filtered.length} total permission{filtered.length !== 1 ? 's' : ''} across {productNames.length} product{productNames.length !== 1 ? 's' : ''}
                </p>
              </div>
            )}
          </div>
        );
      })}
    </div>
  );
}
