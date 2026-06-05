'use client';

import { useState, type FormEvent } from 'react';
import { useRouter } from 'next/navigation';
import { lienApi } from '@/lib/lien-api';
import { ApiError } from '@/lib/api-client';
import type { LienDetail } from '@/types/lien';

interface LienOfferPanelProps {
  lien:      LienDetail;
  onSuccess: () => void;
}

/**
 * SYNQLIEN_BUYER: submit a negotiated offer (below asking price).
 * Creates a LienOffer record. Seller must accept separately.
 */
export function LienOfferPanel({ lien, onSuccess }: LienOfferPanelProps) {
  const router = useRouter();
  const [offerAmount, setOfferAmount] = useState('');
  const [notes,       setNotes]       = useState('');
  const [loading,     setLoading]     = useState(false);
  const [error,       setError]       = useState<string | null>(null);
  const [submitted,   setSubmitted]   = useState(false);

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    const amount = parseFloat(offerAmount);
    if (!offerAmount || isNaN(amount) || amount <= 0) {
      setError('Please enter a valid offer amount greater than zero.');
      return;
    }
    setError(null);
    setLoading(true);
    try {
      await lienApi.liens.submitOffer(lien.id, {
        offerAmount: amount,
        notes:       notes.trim() || undefined,
      });
      setSubmitted(true);
      onSuccess();
    } catch (err) {
      if (err instanceof ApiError) {
        if (err.isUnauthorized) { router.push('/login'); return; }
        if (err.isConflict)     { setError('This lien is no longer available for offers.'); return; }
        if (err.isForbidden)    { setError('You do not have permission to submit an offer.'); return; }
        setError(err.message);
      } else {
        setError('An unexpected error occurred.');
      }
    } finally {
      setLoading(false);
    }
  }

  if (submitted) {
    return (
      <div className="bg-green-50 border border-green-200 rounded-md px-4 py-3 text-sm text-green-700">
        Your offer has been submitted. The seller will review and respond.
      </div>
    );
  }

  return (
    <div className="bg-white border border-gray-200 rounded-lg px-5 py-4">
      <h3 className="text-sm font-semibold text-gray-900 mb-1">Submit an Offer</h3>
      <p className="text-xs text-gray-400 mb-3">
        Negotiate a price below the asking rate. The seller must accept your offer before the transfer completes.
      </p>

      {error && (
        <div className="mb-3 bg-red-50 border border-red-200 rounded-md px-3 py-2 text-sm text-red-700">
          {error}
        </div>
      )}

      <form onSubmit={handleSubmit} className="space-y-3">
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1">
            Offer amount (USD) <span className="text-red-500">*</span>
          </label>
          <input
            type="number"
            min="0.01"
            step="0.01"
            value={offerAmount}
            onChange={e => setOfferAmount(e.target.value)}
            placeholder={`Up to ${new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD', maximumFractionDigits: 0 }).format(lien.offerPrice ?? 0)}`}
            className="w-full border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary"
          />
        </div>
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1">
            Notes <span className="text-gray-400 font-normal">(optional)</span>
          </label>
          <textarea
            value={notes}
            onChange={e => setNotes(e.target.value)}
            rows={2}
            placeholder="Reason for offer, conditions, etc.…"
            className="w-full border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary resize-none"
          />
        </div>
        <button
          type="submit"
          disabled={loading}
          className="bg-primary text-white text-sm font-medium px-5 py-2 rounded-md hover:opacity-90 disabled:opacity-60 transition-opacity"
        >
          {loading ? 'Submitting…' : 'Submit Offer'}
        </button>
      </form>
    </div>
  );
}
