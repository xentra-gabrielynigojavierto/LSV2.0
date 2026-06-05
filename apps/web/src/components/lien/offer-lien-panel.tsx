'use client';

import { useState, type FormEvent } from 'react';
import { useRouter } from 'next/navigation';
import { lienApi } from '@/lib/lien-api';
import { ApiError } from '@/lib/api-client';
import type { LienDetail } from '@/types/lien';

interface OfferLienPanelProps {
  lien:      LienDetail;
  onUpdated: (updated: LienDetail) => void;
}

type Mode = 'offer' | 'withdraw';

/**
 * Seller panel shown on a Draft or Offered lien.
 *
 * - Draft   → shows "List on Marketplace" form (set offer price)
 * - Offered → shows current offer price + "Withdraw" button
 */
export function OfferLienPanel({ lien, onUpdated }: OfferLienPanelProps) {
  const router = useRouter();
  const [mode,       setMode]       = useState<Mode>('offer');
  const [offerPrice, setOfferPrice] = useState(
    lien.offerPrice != null ? String(lien.offerPrice) : ''
  );
  const [offerNotes,   setOfferNotes]   = useState(lien.offerNotes ?? '');
  const [expiresAtUtc, setExpiresAtUtc] = useState('');
  const [loading,      setLoading]      = useState(false);
  const [error,        setError]        = useState<string | null>(null);

  async function handleOffer(e: FormEvent) {
    e.preventDefault();
    const price = parseFloat(offerPrice);
    if (!offerPrice || isNaN(price) || price <= 0) {
      setError('Please enter a valid offer price greater than zero.');
      return;
    }
    setError(null);
    setLoading(true);
    try {
      const { data } = await lienApi.liens.offer(lien.id, {
        offerPrice:  price,
        offerNotes:  offerNotes.trim() || undefined,
        expiresAtUtc: expiresAtUtc || undefined,
      });
      onUpdated(data);
    } catch (err) {
      handleApiError(err);
    } finally {
      setLoading(false);
    }
  }

  async function handleWithdraw() {
    if (!confirm('Withdraw this lien from the marketplace?')) return;
    setError(null);
    setLoading(true);
    try {
      const { data } = await lienApi.liens.withdraw(lien.id);
      onUpdated(data);
    } catch (err) {
      handleApiError(err);
    } finally {
      setLoading(false);
    }
  }

  function handleApiError(err: unknown) {
    if (err instanceof ApiError) {
      if (err.isUnauthorized) { router.push('/login'); return; }
      if (err.isConflict)     { setError('This action is not valid for the lien\'s current state.'); return; }
      if (err.isForbidden)    { setError('You do not have permission to perform this action.'); return; }
      setError(err.message);
    } else {
      setError('An unexpected error occurred.');
    }
  }

  return (
    <div className="bg-white border border-gray-200 rounded-lg px-5 py-4">
      <h3 className="text-sm font-semibold text-gray-900 mb-3">Seller Actions</h3>

      {error && (
        <div className="mb-3 bg-red-50 border border-red-200 rounded-md px-3 py-2 text-sm text-red-700">
          {error}
        </div>
      )}

      {/* Draft: list on marketplace */}
      {lien.status === 'Draft' && (
        <form onSubmit={handleOffer} className="space-y-3">
          <p className="text-sm text-gray-500">
            Set an asking price and list this lien on the marketplace.
          </p>
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">
                Offer price (USD) <span className="text-red-500">*</span>
              </label>
              <input
                type="number"
                min="0.01"
                step="0.01"
                value={offerPrice}
                onChange={e => setOfferPrice(e.target.value)}
                placeholder="e.g. 45000"
                className="w-full border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary"
              />
            </div>
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">
                Offer expires <span className="text-gray-400 font-normal">(optional)</span>
              </label>
              <input
                type="date"
                value={expiresAtUtc}
                onChange={e => setExpiresAtUtc(e.target.value)}
                className="w-full border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary"
              />
            </div>
            <div className="sm:col-span-2">
              <label className="block text-sm font-medium text-gray-700 mb-1">
                Offer notes <span className="text-gray-400 font-normal">(optional)</span>
              </label>
              <textarea
                value={offerNotes}
                onChange={e => setOfferNotes(e.target.value)}
                rows={2}
                placeholder="Any additional context for buyers…"
                className="w-full border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary resize-none"
              />
            </div>
          </div>
          <button
            type="submit"
            disabled={loading}
            className="bg-primary text-white text-sm font-medium px-5 py-2 rounded-md hover:opacity-90 disabled:opacity-60 transition-opacity"
          >
            {loading ? 'Listing…' : 'List on Marketplace'}
          </button>
        </form>
      )}

      {/* Offered: show current offer + withdraw */}
      {lien.status === 'Offered' && (
        <div className="space-y-3">
          <div className="bg-blue-50 border border-blue-200 rounded-md px-3 py-2 text-sm text-blue-700">
            Listed at{' '}
            <span className="font-semibold">
              {new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD', maximumFractionDigits: 0 }).format(lien.offerPrice ?? 0)}
            </span>
            {lien.offerExpiresAtUtc && (
              <span className="ml-1">
                — expires {new Date(lien.offerExpiresAtUtc).toLocaleDateString()}
              </span>
            )}
          </div>
          <button
            onClick={handleWithdraw}
            disabled={loading}
            className="text-sm text-red-600 border border-red-300 px-4 py-2 rounded-md hover:bg-red-50 disabled:opacity-60 transition-colors"
          >
            {loading ? 'Withdrawing…' : 'Withdraw from Marketplace'}
          </button>
        </div>
      )}
    </div>
  );
}
