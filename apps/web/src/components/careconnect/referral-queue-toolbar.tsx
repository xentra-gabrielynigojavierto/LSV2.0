'use client';

import { useCallback, useEffect, useRef, useState } from 'react';
import { useRouter, useSearchParams, usePathname } from 'next/navigation';

interface ReferralQueueToolbarProps {
  currentSearch?: string;
  currentStatus?: string;
}

const STATUS_OPTIONS: { value: string; label: string }[] = [
  { value: '',           label: 'All'         },
  { value: 'New',        label: 'Unopened'    },
  { value: 'NewOpened',  label: 'Opened'      },
  { value: 'Accepted',   label: 'Accepted'    },
  { value: 'InProgress', label: 'In Progress' },
  { value: 'Declined',   label: 'Declined'    },
  { value: 'Completed',  label: 'Completed'   },
  { value: 'Cancelled',  label: 'Cancelled'   },
];

export function ReferralQueueToolbar({ currentSearch = '', currentStatus = '' }: ReferralQueueToolbarProps) {
  const router      = useRouter();
  const pathname    = usePathname();
  const searchParams = useSearchParams();

  const [searchValue, setSearchValue] = useState(currentSearch);
  const debounceRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  function buildUrl(overrides: Record<string, string>): string {
    const params = new URLSearchParams(searchParams?.toString() ?? '');
    for (const [k, v] of Object.entries(overrides)) {
      if (v) {
        params.set(k, v);
      } else {
        params.delete(k);
      }
    }
    params.delete('page');
    const qs = params.toString();
    return qs ? `${pathname}?${qs}` : pathname ?? '/';
  }

  const pushSearch = useCallback(
    (value: string) => {
      router.push(buildUrl({ search: value }));
    },
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [searchParams, pathname],
  );

  function handleSearchChange(e: React.ChangeEvent<HTMLInputElement>) {
    const v = e.target.value;
    setSearchValue(v);
    if (debounceRef.current) clearTimeout(debounceRef.current);
    debounceRef.current = setTimeout(() => pushSearch(v), 320);
  }

  function handleStatusChange(value: string) {
    router.push(buildUrl({ status: value }));
  }

  function handleClearSearch() {
    setSearchValue('');
    pushSearch('');
  }

  useEffect(() => () => {
    if (debounceRef.current) clearTimeout(debounceRef.current);
  }, []);

  return (
    <div className="space-y-3">
      {/* Search */}
      <div className="relative">
        <span className="absolute left-3 top-1/2 -translate-y-1/2 text-gray-400 pointer-events-none select-none text-sm">
          &#x1F50D;
        </span>
        <input
          type="search"
          value={searchValue}
          onChange={handleSearchChange}
          placeholder="Search by client name or case number…"
          className="w-full pl-9 pr-9 py-2 text-sm border border-gray-200 rounded-lg bg-white focus:outline-none focus:ring-2 focus:ring-primary/40 focus:border-primary placeholder-gray-400"
        />
        {searchValue && (
          <button
            onClick={handleClearSearch}
            className="absolute right-3 top-1/2 -translate-y-1/2 text-gray-400 hover:text-gray-600 text-lg leading-none"
            aria-label="Clear search"
          >
            ×
          </button>
        )}
      </div>

      {/* Status filter pills */}
      <div className="flex items-center gap-2 flex-wrap">
        {STATUS_OPTIONS.map(opt => (
          <button
            key={opt.value}
            onClick={() => handleStatusChange(opt.value)}
            className={`text-sm px-3 py-1 rounded-full border transition-colors ${
              currentStatus === opt.value
                ? 'bg-primary text-white border-primary'
                : 'bg-white text-gray-600 border-gray-200 hover:border-gray-400'
            }`}
          >
            {opt.label}
          </button>
        ))}
      </div>
    </div>
  );
}
