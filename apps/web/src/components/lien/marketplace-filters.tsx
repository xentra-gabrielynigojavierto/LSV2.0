'use client';

import { useRouter, useSearchParams } from 'next/navigation';
import { LIEN_TYPE_LABELS } from '@/types/lien';

const US_STATES = ['AL','AK','AZ','AR','CA','CO','CT','DE','FL','GA','HI','ID','IL','IN','IA','KS','KY','LA','ME','MD','MA','MI','MN','MS','MO','MT','NE','NV','NH','NJ','NM','NY','NC','ND','OH','OK','OR','PA','RI','SC','SD','TN','TX','UT','VT','VA','WA','WV','WI','WY','DC'];

export function MarketplaceFilters() {
  const router     = useRouter();
  const sp         = useSearchParams();

  function update(key: string, value: string) {
    const params = new URLSearchParams(sp?.toString() ?? '');
    if (value) params.set(key, value);
    else        params.delete(key);
    params.delete('page');  // reset page on filter change
    router.push(`/lien/marketplace?${params.toString()}`);
  }

  const lienType   = sp?.get('lienType')    ?? '';
  const jurisdiction = sp?.get('jurisdiction') ?? '';
  const minAmount  = sp?.get('minAmount')   ?? '';
  const maxAmount  = sp?.get('maxAmount')   ?? '';

  return (
    <div className="bg-white border border-gray-200 rounded-lg px-5 py-4">
      <div className="flex flex-wrap items-end gap-3">
        <div>
          <label className="block text-xs font-medium text-gray-500 mb-1">Lien type</label>
          <select
            value={lienType}
            onChange={e => update('lienType', e.target.value)}
            className="border border-gray-300 rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-primary bg-white"
          >
            <option value="">All types</option>
            {Object.entries(LIEN_TYPE_LABELS).map(([k, v]) => (
              <option key={k} value={k}>{v}</option>
            ))}
          </select>
        </div>

        <div>
          <label className="block text-xs font-medium text-gray-500 mb-1">Jurisdiction</label>
          <select
            value={jurisdiction}
            onChange={e => update('jurisdiction', e.target.value)}
            className="border border-gray-300 rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-primary bg-white"
          >
            <option value="">All states</option>
            {US_STATES.map(s => <option key={s} value={s}>{s}</option>)}
          </select>
        </div>

        <div>
          <label className="block text-xs font-medium text-gray-500 mb-1">Min amount ($)</label>
          <input
            type="number"
            min="0"
            step="1000"
            value={minAmount}
            onChange={e => update('minAmount', e.target.value)}
            placeholder="0"
            className="w-28 border border-gray-300 rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-primary"
          />
        </div>

        <div>
          <label className="block text-xs font-medium text-gray-500 mb-1">Max amount ($)</label>
          <input
            type="number"
            min="0"
            step="1000"
            value={maxAmount}
            onChange={e => update('maxAmount', e.target.value)}
            placeholder="Any"
            className="w-28 border border-gray-300 rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-primary"
          />
        </div>

        {(lienType || jurisdiction || minAmount || maxAmount) && (
          <button
            onClick={() => router.push('/lien/marketplace')}
            className="text-xs text-gray-500 hover:text-gray-900 underline pb-1.5"
          >
            Clear filters
          </button>
        )}
      </div>
    </div>
  );
}
