'use client';

import { useState, type FormEvent } from 'react';
import { useRouter } from 'next/navigation';
import { lienApi } from '@/lib/lien-api';
import { ApiError } from '@/lib/api-client';
import type { LienDetail } from '@/types/lien';

interface PurchaseLienPanelProps {
  lien:      LienDetail;
  onUpdated: (updated: LienDetail) => void;
}

/**
 * SYNQLIEN_BUYER: purchase a lien directly at the asking price.
 * Requires two-step confirmation to prevent accidental purchases.
 */
export function PurchaseLienPanel({ lien, onUpdated }: PurchaseLienPanelProps) {
  const router = useRouter();
  const [confirmed, setConfirmed] = useState(false);
  const [notes,     setNotes]     = useState('');
  const [loading,   setLoading]   = useState(false);
  const [error,     setError]     = useState<string | null>(null);

  const askingPrice = lien.offerPrice ?? 0;
  const fmt = (n: number) =>
    new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD', maximumFractionDigits: 0 }).format(n);

  async function handlePurchase(e: FormEvent) {
    e.preventDefault();
    setError(null);
    setLoading(true);
    try {
      const { data } = await lienApi.liens.purchase(lien.id, {
        purchaseAmount: askingPrice,
        notes:          notes.trim() || undefined,
      });
      onUpdated(data);
    } catch (err) {
      if (err instanceof ApiError) {
        if (err.isUnauthorized) { router.push('/login'); return; }
        if (err.isConflict)     { setError('This lien is no longer available. It may have been sold or withdrawn.'); return; }
        if (err.isForbidden)    { setError('You do not have permission to purchase this lien.'); return; }
        setError(err.message);
      } else {
        setError('An unexpected error occurred.');
      }
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="bg-white border border-gray-200 rounded-lg px-5 py-4">
      <h3 className="text-sm font-semibold text-gray-900 mb-3">Purchase Lien</h3>

      {error && (
        <div className="mb-3 bg-red-50 border border-red-200 rounded-md px-3 py-2 text-sm text-red-700">
          {error}
        </div>
      )}

      {!confirmed ? (
        <div className="space-y-3">
          <div className="bg-gray-50 border border-gray-200 rounded-md px-4 py-3">
            <p className="text-xs text-gray-500">Purchase price</p>
            <p className="text-2xl font-bold text-gray-900">{fmt(askingPrice)}</p>
            <p className="text-xs text-gray-400 mt-1">
              Original lien value: {fmt(lien.originalAmount)}
            </p>
          </div>
          <p className="text-xs text-gray-500">
            By purchasing, your organisation will be recorded as the buyer and, where applicable, the holder of this lien.
            This action cannot be undone.
          </p>
          <button
            onClick={() => setConfirmed(true)}
            className="bg-green-600 text-white text-sm font-medium px-5 py-2 rounded-md hover:opacity-90 transition-opacity"
          >
            Purchase at {fmt(askingPrice)}
          </button>
        </div>
      ) : (
        <form onSubmit={handlePurchase} className="space-y-3">
          <div className="bg-green-50 border border-green-200 rounded-md px-3 py-2 text-sm text-green-800">
            Confirm purchase of <strong>{fmt(askingPrice)}</strong>.
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">
              Notes <span className="text-gray-400 font-normal">(optional)</span>
            </label>
            <textarea
              value={notes}
              onChange={e => setNotes(e.target.value)}
              rows={2}
              placeholder="Internal reference, notes for your records…"
              className="w-full border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-green-500 resize-none"
            />
          </div>
          <div className="flex items-center gap-3">
            <button
              type="submit"
              disabled={loading}
              className="bg-green-600 text-white text-sm font-medium px-5 py-2 rounded-md hover:opacity-90 disabled:opacity-60 transition-opacity"
            >
              {loading ? 'Processing…' : 'Confirm Purchase'}
            </button>
            <button
              type="button"
              onClick={() => { setConfirmed(false); setError(null); }}
              className="text-sm text-gray-500 hover:text-gray-800"
            >
              Cancel
            </button>
          </div>
        </form>
      )}
    </div>
  );
}
