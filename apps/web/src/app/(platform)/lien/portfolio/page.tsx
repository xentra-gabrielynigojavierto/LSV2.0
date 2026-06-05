import { redirect } from 'next/navigation';
import { requireOrg } from '@/lib/auth-guards';
import { ProductRole } from '@/types';
import { lienServerApi } from '@/lib/lien-server-api';
import { ServerApiError } from '@/lib/server-api-client';
import { PortfolioTable } from '@/components/lien/portfolio-table';

export const dynamic = 'force-dynamic';


/**
 * /lien/portfolio — Purchased / held lien portfolio.
 *
 * Access: SYNQLIEN_BUYER or SYNQLIEN_HOLDER.
 * Backend scopes results to the caller's org.
 */
export default async function PortfolioPage() {
  const session = await requireOrg();

  const isBuyer  = session.productRoles.includes(ProductRole.SynqLienBuyer);
  const isHolder = session.productRoles.includes(ProductRole.SynqLienHolder);

  if (!isBuyer && !isHolder) redirect('/dashboard');

  let liens = null;
  let fetchError: string | null = null;

  try {
    liens = await lienServerApi.liens.portfolio();
  } catch (err) {
    fetchError = err instanceof ServerApiError ? err.message : 'Failed to load portfolio.';
  }

  const heading = isHolder && !isBuyer ? 'Held Liens' : 'Portfolio';

  // Portfolio summary stats
  const stats = liens ? {
    total:         liens.length,
    totalOriginal: liens.reduce((sum, l) => sum + l.originalAmount, 0),
    totalPurchased: liens.reduce((sum, l) => sum + (l.purchasePrice ?? 0), 0),
  } : null;

  function formatCurrency(n: number) {
    return new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD', maximumFractionDigits: 0 }).format(n);
  }

  return (
    <div className="space-y-4">
      <h1 className="text-xl font-semibold text-gray-900">{heading}</h1>

      {/* Summary cards */}
      {stats && (
        <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
          {[
            { label: 'Total liens',           value: String(stats.total) },
            { label: 'Total lien value',       value: formatCurrency(stats.totalOriginal) },
            { label: 'Total acquisition cost', value: formatCurrency(stats.totalPurchased) },
          ].map(s => (
            <div key={s.label} className="bg-white border border-gray-200 rounded-lg px-4 py-3">
              <p className="text-xs text-gray-400">{s.label}</p>
              <p className="text-xl font-bold text-gray-900 mt-0.5">{s.value}</p>
            </div>
          ))}
        </div>
      )}

      {fetchError && (
        <div className="bg-red-50 border border-red-200 rounded-lg px-4 py-3 text-sm text-red-700">
          {fetchError}
        </div>
      )}

      {liens && <PortfolioTable liens={liens} />}
    </div>
  );
}
