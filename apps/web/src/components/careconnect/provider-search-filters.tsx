'use client';

import { useRouter, useSearchParams, usePathname } from 'next/navigation';
import { useState, useCallback } from 'react';

/**
 * Provider search filter bar — client component.
 *
 * Reads from / writes to URL search params so filters survive navigation
 * and the server component re-renders with new results (list mode) or
 * the client refetches markers (map mode).
 *
 * Geo controls:
 *   "Use my location" → navigator.geolocation → writes lat/lng/radius params
 *                       and switches to map view automatically.
 *   Radius selector   → updates radius param in-place (no full navigation).
 *   "Clear location"  → removes lat/lng/radius from URL.
 */
export function ProviderSearchFilters() {
  const router       = useRouter();
  const pathname     = usePathname();
  const searchParams = useSearchParams();

  const [name,               setName]               = useState(searchParams?.get('name')               ?? '');
  const [city,               setCity]               = useState(searchParams?.get('city')               ?? '');
  const [state,              setState]              = useState(searchParams?.get('state')              ?? '');
  const [categoryCode,       setCategoryCode]       = useState(searchParams?.get('categoryCode')       ?? '');
  const [acceptingReferrals, setAcceptingReferrals] = useState(searchParams?.get('acceptingReferrals') === 'true');

  const [radiusMiles, setRadiusMiles] = useState(
    searchParams?.get('radius') ? Math.min(100, Math.max(5, parseInt(searchParams?.get('radius')!))) : 25,
  );
  const [geoLoading, setGeoLoading] = useState(false);
  const [geoError,   setGeoError]   = useState<string | null>(null);

  const hasGeo = !!(searchParams?.get('lat') && searchParams?.get('lng'));

  const applyFilters = useCallback(() => {
    const params = new URLSearchParams(searchParams?.toString() ?? '');
    if (name)               params.set('name',               name);         else params.delete('name');
    if (city)               params.set('city',               city);         else params.delete('city');
    if (state)              params.set('state',              state);        else params.delete('state');
    if (categoryCode)       params.set('categoryCode',       categoryCode); else params.delete('categoryCode');
    if (acceptingReferrals) params.set('acceptingReferrals', 'true');       else params.delete('acceptingReferrals');
    params.delete('page');
    params.delete('nLat'); params.delete('sLat');
    params.delete('eLng'); params.delete('wLng');
    router.push(`${pathname}?${params.toString()}`);
  }, [name, city, state, categoryCode, acceptingReferrals, searchParams, pathname, router]);

  const clearFilters = useCallback(() => {
    setName('');
    setCity('');
    setState('');
    setCategoryCode('');
    setAcceptingReferrals(false);
    router.push(pathname ?? '/');
  }, [router, pathname]);

  const handleGeolocation = useCallback(() => {
    if (!navigator.geolocation) {
      setGeoError('Geolocation is not supported by your browser.');
      return;
    }
    setGeoLoading(true);
    setGeoError(null);

    navigator.geolocation.getCurrentPosition(
      pos => {
        const params = new URLSearchParams(searchParams?.toString() ?? '');
        params.set('lat',    pos.coords.latitude.toFixed(6));
        params.set('lng',    pos.coords.longitude.toFixed(6));
        params.set('radius', String(radiusMiles));
        params.delete('nLat'); params.delete('sLat');
        params.delete('eLng'); params.delete('wLng');
        params.set('view', 'map');
        router.push(`${pathname}?${params.toString()}`);
        setGeoLoading(false);
      },
      () => {
        setGeoError('Could not get your location. Check browser permissions.');
        setGeoLoading(false);
      },
      { timeout: 10000 },
    );
  }, [radiusMiles, searchParams, pathname, router]);

  const clearGeo = useCallback(() => {
    const params = new URLSearchParams(searchParams?.toString() ?? '');
    params.delete('lat');
    params.delete('lng');
    params.delete('radius');
    router.push(`${pathname}?${params.toString()}`);
  }, [searchParams, pathname, router]);

  const handleRadiusChange = useCallback((val: number) => {
    const clamped = Math.min(100, Math.max(5, val));
    setRadiusMiles(clamped);
    if (hasGeo) {
      const params = new URLSearchParams(searchParams?.toString() ?? '');
      params.set('radius', String(clamped));
      router.replace(`${pathname}?${params.toString()}`);
    }
  }, [hasGeo, searchParams, pathname, router]);

  return (
    <div className="bg-white border border-gray-200 rounded-lg p-4 space-y-3">
      {/* Row 1: text filters */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-3">
        <div>
          <label className="block text-xs font-medium text-gray-600 mb-1">Name</label>
          <input
            type="text"
            value={name}
            onChange={e => setName(e.target.value)}
            onKeyDown={e => e.key === 'Enter' && applyFilters()}
            placeholder="Search providers…"
            className="w-full border border-gray-300 rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-primary"
          />
        </div>

        <div>
          <label className="block text-xs font-medium text-gray-600 mb-1">City</label>
          <input
            type="text"
            value={city}
            onChange={e => setCity(e.target.value)}
            onKeyDown={e => e.key === 'Enter' && applyFilters()}
            placeholder="e.g. Chicago"
            className="w-full border border-gray-300 rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-primary"
          />
        </div>

        <div>
          <label className="block text-xs font-medium text-gray-600 mb-1">State</label>
          <input
            type="text"
            value={state}
            onChange={e => setState(e.target.value)}
            onKeyDown={e => e.key === 'Enter' && applyFilters()}
            placeholder="e.g. IL"
            maxLength={2}
            className="w-full border border-gray-300 rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-primary"
          />
        </div>

        <div>
          <label className="block text-xs font-medium text-gray-600 mb-1">Category</label>
          <input
            type="text"
            value={categoryCode}
            onChange={e => setCategoryCode(e.target.value)}
            onKeyDown={e => e.key === 'Enter' && applyFilters()}
            placeholder="Category code…"
            className="w-full border border-gray-300 rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-primary"
          />
        </div>
      </div>

      {/* Row 2: accepting referrals + geolocation + actions */}
      <div className="flex items-center justify-between flex-wrap gap-3">
        <div className="flex items-center gap-4 flex-wrap">
          {/* Accepting referrals toggle */}
          <label className="flex items-center gap-2 text-sm text-gray-700 cursor-pointer select-none">
            <input
              type="checkbox"
              checked={acceptingReferrals}
              onChange={e => setAcceptingReferrals(e.target.checked)}
              className="rounded border-gray-300 text-primary focus:ring-primary"
            />
            Accepting referrals only
          </label>

          {/* Geo controls */}
          {hasGeo ? (
            <div className="flex items-center gap-2">
              <span className="text-xs text-green-700 bg-green-50 border border-green-200 rounded-full px-2.5 py-0.5">
                📍 Near me
              </span>
              <input
                type="number"
                min={5}
                max={100}
                step={5}
                value={radiusMiles}
                onChange={e => handleRadiusChange(parseInt(e.target.value) || 25)}
                className="w-16 border border-gray-300 rounded px-2 py-0.5 text-xs focus:outline-none focus:ring-1 focus:ring-primary"
              />
              <span className="text-xs text-gray-400">mi</span>
              <button
                onClick={clearGeo}
                className="text-xs text-gray-400 hover:text-gray-600 transition-colors underline"
              >
                Clear location
              </button>
            </div>
          ) : (
            <button
              onClick={handleGeolocation}
              disabled={geoLoading}
              className="flex items-center gap-1.5 text-sm text-gray-600 hover:text-primary border border-gray-300 hover:border-primary rounded-md px-3 py-1.5 transition-colors disabled:opacity-50"
            >
              <span>📍</span>
              <span>{geoLoading ? 'Locating…' : 'Use my location'}</span>
            </button>
          )}
        </div>

        {/* Search / Clear */}
        <div className="flex items-center gap-2">
          <button
            onClick={clearFilters}
            className="text-sm text-gray-500 hover:text-gray-700 transition-colors"
          >
            Clear
          </button>
          <button
            onClick={applyFilters}
            className="bg-primary text-white text-sm font-medium px-4 py-1.5 rounded-md hover:opacity-90 transition-opacity"
          >
            Search
          </button>
        </div>
      </div>

      {geoError && (
        <p className="text-xs text-red-600">{geoError}</p>
      )}
    </div>
  );
}
