import Link from 'next/link';
import type { SupportProductRef } from '@/types/control-center';
import { resolveDeepLink, getProductDisplayName } from '@/lib/product-deep-links';

interface ProductRefListProps {
  refs: SupportProductRef[];
}

/**
 * ProductRefList — renders linked product references for a support case.
 *
 * Each ref displays the product name, entity type, display label (or entity ID),
 * and a deep link to the corresponding product page (relative URL, same platform session).
 *
 * Deep links rely on existing platform auth — no cross-service calls are made.
 * Tenant isolation is preserved: the target page enforces its own access controls.
 *
 * If no deep link template exists for a productCode+entityType, the ref is still
 * shown as read-only text without a link.
 */
export function ProductRefList({ refs }: ProductRefListProps) {
  if (refs.length === 0) {
    return (
      <p className="px-5 py-4 text-sm text-gray-400">No product references linked.</p>
    );
  }

  return (
    <ul className="divide-y divide-gray-100">
      {refs.map(ref => {
        const productName = getProductDisplayName(ref.productCode);
        const deepLink    = resolveDeepLink(ref.productCode, ref.entityType, ref.entityId);
        const label       = ref.displayLabel || ref.entityId;

        return (
          <li key={ref.id} className="px-5 py-3 flex items-start gap-3">
            <ProductBadge productCode={ref.productCode} />
            <div className="min-w-0 flex-1">
              <div className="flex items-center gap-1.5 flex-wrap">
                <span className="text-xs font-semibold text-gray-700">{productName}</span>
                <span className="text-gray-300 text-xs">·</span>
                <span className="text-xs text-gray-500 capitalize">{ref.entityType}</span>
              </div>
              <div className="mt-0.5">
                {deepLink ? (
                  <Link
                    href={deepLink}
                    className="text-sm text-indigo-700 hover:text-indigo-900 hover:underline font-medium truncate block"
                    target="_blank"
                    rel="noopener noreferrer"
                    title={`Open ${productName} ${ref.entityType} in new tab`}
                  >
                    {label}
                    <span className="ml-1 text-indigo-400 text-[10px]" aria-hidden="true">↗</span>
                  </Link>
                ) : (
                  <span className="text-sm text-gray-600 font-medium">{label}</span>
                )}
              </div>
              {ref.displayLabel && ref.displayLabel !== ref.entityId && (
                <p className="text-[11px] text-gray-400 mt-0.5 font-mono">{ref.entityId}</p>
              )}
            </div>
          </li>
        );
      })}
    </ul>
  );
}

function ProductBadge({ productCode }: { productCode: string }) {
  const code = productCode.toLowerCase();
  const colourClass =
    code === 'careconnect'  ? 'bg-teal-100   text-teal-700'  :
    code === 'liens'        ? 'bg-blue-100   text-blue-700'  :
    code === 'fund'         ? 'bg-violet-100 text-violet-700' :
    code === 'synqlien'     ? 'bg-blue-100   text-blue-700'  :
    code === 'synqfund'     ? 'bg-violet-100 text-violet-700' :
                              'bg-gray-100   text-gray-600';

  return (
    <span className={`shrink-0 mt-0.5 inline-flex items-center px-2 py-0.5 rounded text-[10px] font-bold uppercase tracking-wide ${colourClass}`}>
      {productCode}
    </span>
  );
}
