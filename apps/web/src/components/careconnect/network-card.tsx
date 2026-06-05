'use client';

import Link    from 'next/link';
import { useState } from 'react';
import type { NetworkSummary } from '@/types/careconnect';

export function NetworkCard({
  network,
  tenantLogoUrl,
}: {
  network:       NetworkSummary;
  tenantLogoUrl: string;
}) {
  const [logoFailed, setLogoFailed] = useState(false);

  const initials = network.name
    .split(' ')
    .slice(0, 2)
    .map(w => w[0] ?? '')
    .join('')
    .toUpperCase();

  return (
    <Link
      href={`/careconnect/browse-networks/${network.id}`}
      className="group flex flex-col rounded-xl border border-gray-200 bg-white p-5 shadow-sm transition-all hover:border-blue-300 hover:shadow-md"
    >
      {/* Tenant logo with initials fallback */}
      <div className="mb-4 flex h-14 w-14 shrink-0 items-center justify-center rounded-xl overflow-hidden bg-blue-50 group-hover:bg-blue-100 transition-colors">
        {!logoFailed ? (
          <img
            src={tenantLogoUrl}
            alt=""
            className="h-full w-full object-contain p-1"
            onError={() => setLogoFailed(true)}
          />
        ) : (
          <span className="text-blue-600 text-lg font-bold select-none">
            {initials || <i className="ri-share-circle-line text-2xl" />}
          </span>
        )}
      </div>

      {/* Name */}
      <p className="text-sm font-semibold text-gray-900 group-hover:text-blue-700 transition-colors line-clamp-2">
        {network.name}
      </p>

      {/* Description */}
      {network.description && (
        <p className="mt-1 text-xs text-gray-500 line-clamp-2">{network.description}</p>
      )}

      {/* Provider count */}
      <div className="mt-3 flex items-center gap-1.5 text-xs text-gray-400">
        <i className="ri-hospital-line" />
        <span>{network.providerCount} provider{network.providerCount !== 1 ? 's' : ''}</span>
      </div>

      {/* CTA */}
      <div className="mt-4 flex items-center gap-1 text-xs font-medium text-blue-600 group-hover:text-blue-700">
        View providers & refer
        <i className="ri-arrow-right-line" />
      </div>
    </Link>
  );
}
