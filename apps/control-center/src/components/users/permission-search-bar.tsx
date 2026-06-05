'use client';

import { useState, useTransition } from 'react';
import { useRouter }               from 'next/navigation';

interface PermissionSearchBarProps {
  initialSearch: string;
  productId:     string;
}

/**
 * PermissionSearchBar — client-side search input for the permissions catalog.
 * Navigates to /permissions?search=<query>[&product=<id>] on submit.
 *
 * UIX-005
 */
export function PermissionSearchBar({ initialSearch, productId }: PermissionSearchBarProps) {
  const router = useRouter();
  const [, startTransition] = useTransition();
  const [query, setQuery] = useState(initialSearch);

  function handleSearch(e: React.FormEvent) {
    e.preventDefault();
    const params = new URLSearchParams();
    if (query.trim()) params.set('search', query.trim());
    if (productId)    params.set('product', productId);
    const qs = params.toString();
    startTransition(() => {
      router.push(`/permissions${qs ? `?${qs}` : ''}`);
    });
  }

  function handleClear() {
    setQuery('');
    const params = new URLSearchParams();
    if (productId) params.set('product', productId);
    const qs = params.toString();
    startTransition(() => {
      router.push(`/permissions${qs ? `?${qs}` : ''}`);
    });
  }

  return (
    <form onSubmit={handleSearch} className="flex items-center gap-2 max-w-md">
      <div className="relative flex-1">
        <div className="absolute inset-y-0 left-0 pl-3 flex items-center pointer-events-none">
          <svg className="h-4 w-4 text-gray-400" viewBox="0 0 20 20" fill="currentColor" aria-hidden="true">
            <path
              fillRule="evenodd"
              d="M9 3.5a5.5 5.5 0 1 0 0 11 5.5 5.5 0 0 0 0-11ZM2 9a7 7 0 1 1 12.452 4.391l3.328 3.329a.75.75 0 1 1-1.06 1.06l-3.329-3.328A7 7 0 0 1 2 9Z"
              clipRule="evenodd"
            />
          </svg>
        </div>
        <input
          type="text"
          value={query}
          onChange={e => setQuery(e.target.value)}
          placeholder="Search permissions by code, name, or description…"
          className="block w-full pl-9 pr-8 py-2 text-sm border border-gray-300 rounded-md focus:outline-none focus:ring-2 focus:ring-indigo-400 focus:border-transparent bg-white"
        />
        {query && (
          <button
            type="button"
            onClick={handleClear}
            className="absolute inset-y-0 right-0 pr-2.5 flex items-center text-gray-400 hover:text-gray-600"
          >
            <svg className="h-3.5 w-3.5" viewBox="0 0 16 16" fill="currentColor">
              <path d="M3.72 3.72a.75.75 0 0 1 1.06 0L8 6.94l3.22-3.22a.75.75 0 1 1 1.06 1.06L9.06 8l3.22 3.22a.75.75 0 1 1-1.06 1.06L8 9.06l-3.22 3.22a.75.75 0 0 1-1.06-1.06L6.94 8 3.72 4.78a.75.75 0 0 1 0-1.06Z" />
            </svg>
          </button>
        )}
      </div>
      <button
        type="submit"
        className="px-3 py-2 text-sm font-medium text-white bg-indigo-600 border border-indigo-600 rounded-md hover:bg-indigo-700 transition-colors"
      >
        Search
      </button>
    </form>
  );
}
