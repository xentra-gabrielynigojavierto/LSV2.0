'use client';

import { PRODUCT_TYPE_LABELS, type ProductType } from '@/lib/notifications-shared';

const PRODUCT_COLORS: Record<ProductType, string> = {
  careconnect: 'bg-blue-50 text-blue-700 border-blue-200',
  synqlien:    'bg-violet-50 text-violet-700 border-violet-200',
  synqfund:    'bg-emerald-50 text-emerald-700 border-emerald-200',
  synqrx:      'bg-orange-50 text-orange-700 border-orange-200',
  synqpayout:  'bg-teal-50 text-teal-700 border-teal-200',
};

export function ProductTypeBadge({ productType }: { productType: ProductType }) {
  const cls = PRODUCT_COLORS[productType] ?? 'bg-gray-50 text-gray-700 border-gray-200';
  const label = PRODUCT_TYPE_LABELS[productType] ?? productType;
  return (
    <span className={`inline-flex items-center px-2 py-0.5 rounded-full text-[10px] font-semibold uppercase tracking-wide border ${cls}`}>
      {label}
    </span>
  );
}
