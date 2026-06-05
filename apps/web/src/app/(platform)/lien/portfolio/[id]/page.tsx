'use client';

import { useEffect, useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import Link from 'next/link';
import { useSession } from '@/hooks/use-session';
import { ProductRole } from '@/types';
import { lienApi } from '@/lib/lien-api';
import { ApiError } from '@/lib/api-client';
import { LienDetailPanel } from '@/components/lien/lien-detail-panel';
import type { LienDetail } from '@/types/lien';

/**
 * /lien/portfolio/[id] — Held lien detail for buyers and holders.
 *
 * Shows full detail (subject identity revealed post-purchase, even if
 * the original listing was confidential).
 *
 * Phase 1: read-only. Phase 2 will add management actions (e.g. transfer).
 */
export default function PortfolioLienDetailPage() {
  const params = useParams<{ id: string }>();
  const router = useRouter();
  const { session, isLoading: sessionLoading } = useSession();

  const isBuyer  = session?.productRoles.includes(ProductRole.SynqLienBuyer)  ?? false;
  const isHolder = session?.productRoles.includes(ProductRole.SynqLienHolder) ?? false;

  const [lien,    setLien]    = useState<LienDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [error,   setError]   = useState<string | null>(null);

  useEffect(() => {
    if (sessionLoading) return;
    if (!session) { router.push('/login'); return; }
    if (!isBuyer && !isHolder) { router.push('/dashboard'); return; }

    async function load() {
      setLoading(true);
      try {
        const { data } = await lienApi.liens.getById(params?.id ?? '');
        setLien(data);
      } catch (err) {
        if (err instanceof ApiError) {
          if (err.isUnauthorized) { router.push('/login'); return; }
          if (err.isNotFound)     { setError('Lien not found in your portfolio.'); return; }
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
  }, [params?.id ?? '', session, sessionLoading, isBuyer, isHolder, router]);

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
      <div className="space-y-4">
        <nav>
          <Link href="/lien/portfolio" className="text-sm text-gray-500 hover:text-gray-800">
            ← Back to Portfolio
          </Link>
        </nav>
        <div className="bg-red-50 border border-red-200 rounded-lg px-4 py-3 text-sm text-red-700">
          {error}
        </div>
      </div>
    );
  }

  if (!lien) return null;

  return (
    <div className="space-y-4">
      <nav>
        <Link href="/lien/portfolio" className="text-sm text-gray-500 hover:text-gray-800">
          ← Back to Portfolio
        </Link>
      </nav>

      <LienDetailPanel lien={lien} />

      {/* Phase 1: no management actions. Phase 2 will add transfer/manage panel. */}
      <div className="bg-gray-50 border border-gray-200 rounded-lg px-4 py-3 text-xs text-gray-400">
        Management actions (transfer, assign holder) coming in Phase 2.
      </div>
    </div>
  );
}
