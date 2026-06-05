'use client';

import { useEffect, useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import Link from 'next/link';
import { useSession } from '@/hooks/use-session';
import { ProductRole } from '@/types';
import { lienApi } from '@/lib/lien-api';
import { ApiError } from '@/lib/api-client';
import { LienDetailPanel } from '@/components/lien/lien-detail-panel';
import { OfferLienPanel } from '@/components/lien/offer-lien-panel';
import type { LienDetail } from '@/types/lien';

/**
 * /lien/my-liens/[id] — Seller's lien detail.
 *
 * Role: SYNQLIEN_SELLER.
 * - Draft   → OfferLienPanel (list on marketplace)
 * - Offered → OfferLienPanel (withdraw option)
 * - Sold / Withdrawn → read-only
 */
export default function MyLienDetailPage() {
  const params = useParams<{ id: string }>();
  const router = useRouter();
  const { session, isLoading: sessionLoading } = useSession();

  const isSeller = session?.productRoles.includes(ProductRole.SynqLienSeller) ?? false;

  const [lien,    setLien]    = useState<LienDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [error,   setError]   = useState<string | null>(null);

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
          if (err.isNotFound)     { setError('Lien not found.'); return; }
          if (err.isForbidden)    { setError('You do not have access to this lien.'); return; }
          setError(err.message);
        } else {
          setError('Failed to load lien.');
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
        <div className="h-48 bg-gray-100 rounded" />
      </div>
    );
  }

  if (error) {
    return (
      <div className="bg-red-50 border border-red-200 rounded-lg px-4 py-3 text-sm text-red-700">
        {error}
      </div>
    );
  }

  if (!lien) return null;

  const isTerminal = lien.status === 'Sold' || lien.status === 'Withdrawn';
  const showOfferPanel = isSeller && (lien.status === 'Draft' || lien.status === 'Offered');

  return (
    <div className="space-y-4">
      <nav>
        <Link href="/lien/my-liens" className="text-sm text-gray-500 hover:text-gray-800 transition-colors">
          ← Back to My Liens
        </Link>
      </nav>

      <LienDetailPanel lien={lien} />

      {showOfferPanel && (
        <OfferLienPanel lien={lien} onUpdated={setLien} />
      )}

      {isSeller && isTerminal && (
        <div className="bg-gray-50 border border-gray-200 rounded-lg px-4 py-3 text-xs text-gray-400">
          This lien is {lien.status === 'Sold' ? 'sold' : 'withdrawn'} — no further actions are available.
        </div>
      )}
    </div>
  );
}
