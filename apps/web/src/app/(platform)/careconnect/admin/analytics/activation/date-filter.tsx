'use client';

import { useRouter, usePathname, useSearchParams } from 'next/navigation';

const PRESETS = [
  { label: 'Last 7 days',  value: '7'  },
  { label: 'Last 30 days', value: '30' },
  { label: 'Last 90 days', value: '90' },
] as const;

interface DateFilterProps {
  activeDays: string;
}

export function DateFilter({ activeDays }: DateFilterProps) {
  const router      = useRouter();
  const pathname    = usePathname();
  const searchParams = useSearchParams();

  function select(days: string) {
    const params = new URLSearchParams(searchParams?.toString());
    params.set('days', days);
    params.delete('startDate');
    params.delete('endDate');
    router.push(`${pathname}?${params.toString()}`);
  }

  return (
    <div className="flex items-center gap-1 bg-gray-100 rounded-lg p-1">
      {PRESETS.map(p => (
        <button
          key={p.value}
          onClick={() => select(p.value)}
          className={`px-3 py-1.5 rounded-md text-sm font-medium transition-colors ${
            activeDays === p.value
              ? 'bg-white text-gray-900 shadow-sm'
              : 'text-gray-600 hover:text-gray-900'
          }`}
        >
          {p.label}
        </button>
      ))}
    </div>
  );
}
