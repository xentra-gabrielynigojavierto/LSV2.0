'use client';

import { useEffect, useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import Link from 'next/link';
import { useSession } from '@/hooks/use-session';
import { ProductRole } from '@/types';
import { lienApi } from '@/lib/lien-api';
import { ApiError } from '@/lib/api-client';
import { LienDetailPanel } from '@/components/lien/lien-detail-panel';
import { PurchaseLienPanel } from '@/components/lien/purchase-lien-panel';
import { LienOfferPanel } from '@/components/lien/lien-offer-panel';
import type { LienDetail } from '@/types/lien';

/**
 * /lien/marketplace/[id] — Marketplace lien detail for buyers.
 *
 * Role: SYNQLIEN_BUYER.
 *
 * Shows full lien detail (subject party hidden if confidential).
 * Offered liens show two action panels:
 *   - PurchaseLienPanel: buy directly at asking price
 *   - LienOfferPanel: submit a negotiated offer
 *
 * 403/409 surface gracefully with inline error messages.
 */
export default function MarketplaceLienDetailPage() {
  const params = useParams<{ id: string }>();
  const router = useRouter();
  const { session, isLoading: sessionLoading } = useSession();

  const isBuyer = session?.productRoles.includes(ProductRole.SynqLienBuyer) ?? false;

  const [lien,          setLien]          = useState<LienDetail | null>(null);
  const [loading,       setLoading]       = useState(true);
  const [error,         setError]         = useState<string | null>(null);
  const [offerSuccess,  setOfferSuccess]  = useState(false);

  useEffect(() => {
    if (sessionLoading) return;
    if (!session) { router.push('/login'); return; }

    async function load() {
      setLoading(true);
      try {
        const { data } = await lienApi.liens.getById(params?.id ?? '');
        setLien(data);
      } catch (err) {
        if (err instanceof ApiError) {
          if (err.isUnauthorized) { router.push('/login'); return; }
          if (err.isNotFound)     { setError('This lien is no longer available.'); return; }
          if (err.isForbidden)    { setError('You do not have access to this listing.'); return; }
          setError(err.message);
        } else {
          setError('Failed to load lien details.');
        }
      } finally {
        setLoading(false);
      }
    }

    load();
  }, [params?.id ?? '', session, sessionLoading, router]);

  if (sessionLoading || loading) {
    return (
      <div className="space-y-4 animate-pulse">
        <div className="h-6 w-32 bg-gray-100 rounded" />
        <div className="h-64 bg-gray-100 rounded" />
      </div>
    );
  }

  if (error) {
    return (
      <div className="space-y-4">
        <nav>
          <Link href="/lien/marketplace" className="text-sm text-gray-500 hover:text-gray-800">
            ← Back to Marketplace
          </Link>
        </nav>
        <div className="bg-red-50 border border-red-200 rounded-lg px-4 py-3 text-sm text-red-700">
          {error}
        </div>
      </div>
    );
  }

  if (!lien) return null;

  const isOffered = lien.status === 'Offered';
  const isSold    = lien.status === 'Sold';

  return (
    <div className="space-y-4">
      <nav>
        <Link href="/lien/marketplace" className="text-sm text-gray-500 hover:text-gray-800">
          ← Back to Marketplace
        </Link>
      </nav>

      <LienDetailPanel lien={lien} />

      {/* Already sold notice */}
      {isSold && (
        <div className="bg-gray-50 border border-gray-200 rounded-lg px-4 py-3 text-sm text-gray-500">
          This lien has already been sold and is no longer available for purchase.
        </div>
      )}

      {/* Buyer actions: purchase + offer panels, shown only if lien is still offered */}
      {isBuyer && isOffered && !offerSuccess && (
        <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
          <PurchaseLienPanel lien={lien} onUpdated={setLien} />
          <LienOfferPanel   lien={lien} onSuccess={() => setOfferSuccess(true)} />
        </div>
      )}

      {/* After offer submitted */}
      {isBuyer && isOffered && offerSuccess && (
        <div className="bg-green-50 border border-green-200 rounded-lg px-4 py-3 text-sm text-green-700">
          Your offer has been submitted. The seller will be in touch.
          You can still{' '}
          <button
            onClick={() => setOfferSuccess(false)}
            className="underline font-medium"
          >
            purchase directly
          </button>{' '}
          at the asking price.
        </div>
      )}

      {/* Non-buyer viewing an offered lien */}
      {!isBuyer && isOffered && (
        <div className="bg-yellow-50 border border-yellow-200 rounded-lg px-4 py-3 text-sm text-yellow-700">
          You need a Buyer role to purchase or offer on this lien.
        </div>
      )}
    </div>
  );
}
