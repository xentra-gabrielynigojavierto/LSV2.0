'use client';

import { useCallback, useState } from 'react';
import { useRouter, usePathname, useSearchParams } from 'next/navigation';
import type { DatePreset } from '@/lib/daterange';
import { isValidIsoDate, isoDate } from '@/lib/daterange';

interface DateRangePickerProps {
  activePreset: DatePreset;
  currentFrom:  string;
  currentTo:    string;
}

const PRESETS: { id: DatePreset; label: string }[] = [
  { id: '7d',  label: 'Last 7 days' },
  { id: '30d', label: 'Last 30 days' },
  { id: 'custom', label: 'Custom' },
];

/**
 * Client-side analytics date range picker.
 *
 * Preset buttons push URL params (analyticsFrom, analyticsTo) which
 * trigger a Next.js Server Component re-render with new analytics data.
 * Custom range shows two date inputs and an Apply button.
 */
export function DateRangePicker({ activePreset, currentFrom, currentTo }: DateRangePickerProps) {
  const router      = useRouter();
  const pathname    = usePathname();
  const searchParams = useSearchParams();

  const [showCustom, setShowCustom] = useState(activePreset === 'custom');
  const [customFrom, setCustomFrom] = useState(activePreset === 'custom' ? currentFrom : '');
  const [customTo,   setCustomTo]   = useState(activePreset === 'custom' ? currentTo   : '');

  const todayStr = isoDate(new Date());

  const navigate = useCallback((from: string, to: string) => {
    const params = new URLSearchParams(searchParams?.toString());
    params.set('analyticsFrom', from);
    params.set('analyticsTo',   to);
    router.push(`${pathname}?${params.toString()}`);
  }, [router, pathname, searchParams]);

  function handlePreset(preset: DatePreset) {
    if (preset === 'custom') {
      setShowCustom(true);
      return;
    }
    setShowCustom(false);
    const now = new Date();
    const days = preset === '7d' ? 6 : 29;
    const from = new Date(now);
    from.setDate(from.getDate() - days);
    navigate(isoDate(from), isoDate(now));
  }

  function handleApplyCustom() {
    if (!isValidIsoDate(customFrom) || !isValidIsoDate(customTo)) return;
    if (customFrom > customTo) return;
    navigate(customFrom, customTo);
    setShowCustom(false);
  }

  const customApplyDisabled =
    !isValidIsoDate(customFrom) ||
    !isValidIsoDate(customTo)   ||
    customFrom > customTo;

  return (
    <div className="flex flex-wrap items-center gap-2">
      {PRESETS.map(p => (
        <button
          key={p.id}
          onClick={() => handlePreset(p.id)}
          className={`text-xs font-medium px-3 py-1.5 rounded-full border transition-colors ${
            activePreset === p.id && p.id !== 'custom'
              ? 'bg-primary text-white border-primary'
              : p.id === 'custom' && showCustom
              ? 'bg-gray-100 text-gray-800 border-gray-200'
              : 'bg-white text-gray-600 border-gray-200 hover:border-primary hover:text-primary'
          }`}
        >
          {p.label}
        </button>
      ))}

      {showCustom && (
        <div className="flex items-center gap-2 mt-1 sm:mt-0">
          <input
            type="date"
            value={customFrom}
            max={customTo || todayStr}
            onChange={e => setCustomFrom(e.target.value)}
            className="text-xs border border-gray-200 rounded px-2 py-1.5 focus:outline-none focus:ring-1 focus:ring-primary"
          />
          <span className="text-xs text-gray-400">→</span>
          <input
            type="date"
            value={customTo}
            min={customFrom}
            max={todayStr}
            onChange={e => setCustomTo(e.target.value)}
            className="text-xs border border-gray-200 rounded px-2 py-1.5 focus:outline-none focus:ring-1 focus:ring-primary"
          />
          <button
            onClick={handleApplyCustom}
            disabled={customApplyDisabled}
            className="text-xs font-medium px-3 py-1.5 bg-primary text-white rounded-full disabled:opacity-40 transition-opacity"
          >
            Apply
          </button>
        </div>
      )}
    </div>
  );
}
