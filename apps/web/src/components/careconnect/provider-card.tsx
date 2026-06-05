'use client';

import { useState } from 'react';
import Link from 'next/link';
import type { ProviderSummary } from '@/types/careconnect';
import { CreateReferralForm } from '@/components/careconnect/create-referral-form';
import { formatPhoneDisplay } from '@/lib/phone';

interface ProviderCardProps {
  provider:    ProviderSummary;
  /** When true and provider is accepting referrals, shows the Refer action. */
  isReferrer?: boolean;
  /** Referrer identity forwarded to the form so the backend can send notifications. */
  referrerEmail?: string;
  referrerName?:  string;
}

export function ProviderCard({
  provider,
  isReferrer    = false,
  referrerEmail,
  referrerName,
}: ProviderCardProps) {
  const isAccepting  = provider.acceptingReferrals;
  const showReferBtn = isReferrer && isAccepting;

  const [showForm, setShowForm] = useState(false);

  return (
    <>
      {/* Card */}
      <div className="relative bg-white border border-gray-200 rounded-lg hover:border-primary hover:shadow-sm transition-all">
        {/* Main clickable area — navigates to provider detail */}
        <Link
          href={`/careconnect/providers/${provider.id}`}
          className="block p-4"
        >
          <div className="flex items-start justify-between gap-4">
            {/* Name + org + location */}
            <div className="min-w-0 flex-1">
              <p className="font-medium text-gray-900 truncate">{provider.displayLabel}</p>
              {provider.organizationName && provider.organizationName !== provider.name && (
                <p className="text-sm text-gray-500 truncate">{provider.organizationName}</p>
              )}
              <p className="text-sm text-gray-500 mt-0.5">{provider.markerSubtitle}</p>
            </div>

            {/* Accepting referrals badge */}
            <span
              className={`shrink-0 inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium border ${
                isAccepting
                  ? 'bg-green-50 text-green-700 border-green-200'
                  : 'bg-gray-50 text-gray-500 border-gray-200'
              }`}
            >
              {isAccepting ? 'Accepting' : 'Not accepting'}
            </span>
          </div>

          {/* Categories */}
          {provider.categories.length > 0 && (
            <div className="mt-3 flex flex-wrap gap-1.5">
              {provider.categories.slice(0, 4).map(cat => (
                <span
                  key={cat}
                  className="inline-flex items-center rounded px-1.5 py-0.5 text-xs bg-gray-100 text-gray-600"
                >
                  {cat}
                </span>
              ))}
              {provider.categories.length > 4 && (
                <span className="inline-flex items-center rounded px-1.5 py-0.5 text-xs text-gray-400">
                  +{provider.categories.length - 4} more
                </span>
              )}
            </div>
          )}

          {/* Contact */}
          <div className="mt-3 flex items-center gap-4 text-xs text-gray-400">
            {provider.phone && <span>{formatPhoneDisplay(provider.phone)}</span>}
            {provider.email && <span className="truncate">{provider.email}</span>}
          </div>
        </Link>

        {/* Refer button — outside the Link to avoid nested-nav */}
        {showReferBtn && (
          <div className="px-4 pb-3">
            <button
              type="button"
              onClick={() => setShowForm(true)}
              className="w-full text-center text-xs font-medium text-primary border border-primary/30 rounded-md py-1.5 hover:bg-primary/5 transition-colors"
            >
              Refer Patient
            </button>
          </div>
        )}
      </div>

      {/* Referral form modal */}
      {showForm && (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/40"
          onClick={() => setShowForm(false)}
        >
          <div
            className="w-full max-w-xl bg-white rounded-xl shadow-xl overflow-y-auto max-h-[90vh]"
            onClick={e => e.stopPropagation()}
          >
            <CreateReferralForm
              providerId={provider.id}
              providerName={provider.displayLabel}
              referrerEmail={referrerEmail}
              referrerName={referrerName}
              onClose={() => setShowForm(false)}
            />
          </div>
        </div>
      )}
    </>
  );
}
