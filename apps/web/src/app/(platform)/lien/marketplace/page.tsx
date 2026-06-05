import { Suspense } from 'react';
import { requireProductRole } from '@/lib/auth-guards';
import { ProductRole } from '@/types';
import { lienServerApi } from '@/lib/lien-server-api';
import { ServerApiError } from '@/lib/server-api-client';
import { MarketplaceFilters } from '@/components/lien/marketplace-filters';
import { MarketplaceCard } from '@/components/lien/marketplace-card';

export const dynamic = 'force-dynamic';


interface MarketplacePageProps {
  searchParams: Promise<{
    lienType?:     string;
    jurisdiction?: string;
    minAmount?:    string;
    maxAmount?:    string;
    page?:         string;
  }>;
}

/**
 * /lien/marketplace — Lien marketplace browser.
 *
 * Access: SYNQLIEN_BUYER.
 * Displays offered liens with server-side filter support.
 * Filters are handled by MarketplaceFilters (client component) which
 * updates the URL; this page re-renders server-side on each navigation.
 */
export default async function MarketplacePage({ searchParams }: MarketplacePageProps) {
  const sp = await searchParams;
  await requireProductRole(ProductRole.SynqLienBuyer);

  let liens = null;
  let fetchError: string | null = null;

  try {
    liens = await lienServerApi.liens.marketplace({
      lienType:     sp.lienType     || undefined,
      jurisdiction: sp.jurisdiction || undefined,
      minAmount:    sp.minAmount    ? parseFloat(sp.minAmount) : undefined,
      maxAmount:    sp.maxAmount    ? parseFloat(sp.maxAmount) : undefined,
      page:         sp.page         ? parseInt(sp.page, 10)    : 1,
    });
  } catch (err) {
    fetchError = err instanceof ServerApiError ? err.message : 'Failed to load marketplace.';
  }

  const hasFilters = !!(sp.lienType || sp.jurisdiction || sp.minAmount || sp.maxAmount);

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold text-gray-900">Lien Marketplace</h1>
        {liens && (
          <p className="text-sm text-gray-400">
            {liens.length} lien{liens.length !== 1 ? 's' : ''} available
          </p>
        )}
      </div>

      {/* Filters — client component, updates URL params */}
      <Suspense fallback={<div className="h-16 bg-gray-50 rounded-lg animate-pulse" />}>
        <MarketplaceFilters />
      </Suspense>

      {fetchError && (
        <div className="bg-red-50 border border-red-200 rounded-lg px-4 py-3 text-sm text-red-700">
          {fetchError}
        </div>
      )}

      {liens && liens.length === 0 && (
        <div className="bg-white border border-gray-200 rounded-lg p-10 text-center">
          <p className="text-sm text-gray-400">
            {hasFilters ? 'No liens match your filters.' : 'No liens are currently listed on the marketplace.'}
          </p>
        </div>
      )}

      {liens && liens.length > 0 && (
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
          {liens.map(lien => (
            <MarketplaceCard key={lien.id} lien={lien} />
          ))}
        </div>
      )}
    </div>
  );
}
