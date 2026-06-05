import Link from 'next/link';
import type { LienSummary } from '@/types/lien';
import { LIEN_TYPE_LABELS } from '@/types/lien';

interface MarketplaceCardProps {
  lien: LienSummary;
}

function formatCurrency(amount?: number): string {
  if (amount == null) return '—';
  return new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD', maximumFractionDigits: 0 }).format(amount);
}

export function MarketplaceCard({ lien }: MarketplaceCardProps) {
  const subjectLine = lien.isConfidential
    ? 'Confidential'
    : lien.subjectParty
      ? `${lien.subjectParty.firstName ?? ''} ${lien.subjectParty.lastName ?? ''}`.trim()
      : null;

  return (
    <div className="bg-white border border-gray-200 rounded-lg hover:border-primary hover:shadow-sm transition-all overflow-hidden">
      {/* Type banner */}
      <div className="bg-gray-50 border-b border-gray-100 px-4 py-2 flex items-center justify-between">
        <span className="text-xs font-semibold text-gray-600 uppercase tracking-wide">
          {LIEN_TYPE_LABELS[lien.lienType] ?? lien.lienType}
        </span>
        {lien.isConfidential && (
          <span className="text-xs bg-yellow-50 text-yellow-700 border border-yellow-100 rounded-full px-2 py-0.5">
            Confidential
          </span>
        )}
      </div>

      <div className="px-4 py-4 space-y-3">
        {/* Amounts */}
        <div className="flex items-end justify-between gap-2">
          <div>
            <p className="text-xs text-gray-400">Offer price</p>
            <p className="text-xl font-bold text-gray-900">{formatCurrency(lien.offerPrice)}</p>
          </div>
          <div className="text-right">
            <p className="text-xs text-gray-400">Original</p>
            <p className="text-sm font-medium text-gray-600">{formatCurrency(lien.originalAmount)}</p>
          </div>
        </div>

        {/* Meta */}
        <div className="space-y-1 text-xs text-gray-500">
          {subjectLine && <p>Subject: <span className={lien.isConfidential ? 'italic text-gray-400' : ''}>{subjectLine}</span></p>}
          {lien.jurisdiction && <p>Jurisdiction: {lien.jurisdiction}</p>}
          {lien.caseRef      && <p>Case ref: {lien.caseRef}</p>}
          {lien.sellingOrg   && <p>Seller: {lien.sellingOrg.orgName}</p>}
        </div>

        {/* CTA */}
        <Link
          href={`/lien/marketplace/${lien.id}`}
          className="block w-full text-center bg-primary text-white text-sm font-medium py-2 rounded-md hover:opacity-90 transition-opacity"
        >
          View &amp; Purchase
        </Link>
      </div>
    </div>
  );
}
