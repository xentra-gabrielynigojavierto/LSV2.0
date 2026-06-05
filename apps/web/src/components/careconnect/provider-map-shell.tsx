'use client';

import dynamic from 'next/dynamic';
import { useRouter, useSearchParams, usePathname } from 'next/navigation';
import { useState, useEffect, useCallback } from 'react';
import { apiClient } from '@/lib/api-client';
import { useSession } from '@/hooks/use-session';
import { ProviderCard } from '@/components/careconnect/provider-card';
import type {
  ProviderMarker,
  ProviderSummary,
  PagedResponse,
  ProviderSearchParams,
} from '@/types/careconnect';
import type { ViewportBounds } from './provider-map';

const ProviderMap = dynamic(
  () => import('./provider-map').then(m => m.ProviderMap),
  { ssr: false, loading: () => <div className="h-full w-full bg-gray-100 animate-pulse" /> },
);

interface ProviderMapShellProps {
  initialProviders: PagedResponse<ProviderSummary> | null;
  initialPage:      number;
  isReferrer:       boolean;
  fetchError:       string | null;
}

export function ProviderMapShell({
  initialProviders,
  initialPage,
  isReferrer,
  fetchError,
}: ProviderMapShellProps) {
  const router       = useRouter();
  const { session }  = useSession();
  const pathname     = usePathname();
  const searchParams = useSearchParams();

  const view = searchParams?.get('view') === 'map' ? 'map' : 'list';

  const [markers,          setMarkers]          = useState<ProviderMarker[]>([]);
  const [markersLoading,   setMarkersLoading]   = useState(false);
  const [markersError,     setMarkersError]     = useState<string | null>(null);
  const [selectedMarkerId, setSelectedMarkerId] = useState<string | null>(null);

  const buildFilterParams = useCallback((): ProviderSearchParams => {
    const p: ProviderSearchParams = { isActive: true };

    const name             = searchParams?.get('name');
    const city             = searchParams?.get('city');
    const state            = searchParams?.get('state');
    const categoryCode     = searchParams?.get('categoryCode');
    const accepting        = searchParams?.get('acceptingReferrals');
    const lat              = searchParams?.get('lat');
    const lng              = searchParams?.get('lng');
    const radius           = searchParams?.get('radius');
    const nLat             = searchParams?.get('nLat');
    const sLat             = searchParams?.get('sLat');
    const eLng             = searchParams?.get('eLng');
    const wLng             = searchParams?.get('wLng');

    if (name)         p.name               = name;
    if (city)         p.city               = city;
    if (state)        p.state              = state;
    if (categoryCode) p.categoryCode       = categoryCode;
    if (accepting === 'true') p.acceptingReferrals = true;

    if (lat && lng && radius) {
      p.latitude    = parseFloat(lat);
      p.longitude   = parseFloat(lng);
      p.radiusMiles = parseFloat(radius);
    } else if (nLat && sLat && eLng && wLng) {
      p.northLat = parseFloat(nLat);
      p.southLat = parseFloat(sLat);
      p.eastLng  = parseFloat(eLng);
      p.westLng  = parseFloat(wLng);
    }

    return p;
  }, [searchParams]);

  useEffect(() => {
    if (view !== 'map') return;

    let cancelled = false;
    setMarkersLoading(true);
    setMarkersError(null);

    const params = buildFilterParams();
    const qs = new URLSearchParams(
      Object.fromEntries(
        Object.entries(params)
          .filter(([, v]) => v !== undefined && v !== null)
          .map(([k, v]) => [k, String(v)]),
      ),
    ).toString();
    const path = `/careconnect/api/providers/map${qs ? `?${qs}` : ''}`;

    apiClient.get<ProviderMarker[]>(path)
      .then(({ data }) => { if (!cancelled) setMarkers(data); })
      .catch(() => { if (!cancelled) setMarkersError('Failed to load map markers.'); })
      .finally(() => { if (!cancelled) setMarkersLoading(false); });

    return () => { cancelled = true; };
  }, [view, searchParams, buildFilterParams]);

  const handleViewportChange = useCallback((bounds: ViewportBounds) => {
    const params = new URLSearchParams(searchParams?.toString() ?? '');
    params.delete('lat');
    params.delete('lng');
    params.delete('radius');
    params.set('nLat', bounds.northLat.toFixed(6));
    params.set('sLat', bounds.southLat.toFixed(6));
    params.set('eLng', bounds.eastLng.toFixed(6));
    params.set('wLng', bounds.westLng.toFixed(6));
    router.replace(`${pathname}?${params.toString()}`);
  }, [searchParams, pathname, router]);

  const handleViewToggle = useCallback((newView: 'list' | 'map') => {
    const params = new URLSearchParams(searchParams?.toString() ?? '');
    params.set('view', newView);
    if (newView === 'list') {
      params.delete('nLat');
      params.delete('sLat');
      params.delete('eLng');
      params.delete('wLng');
    }
    router.push(`${pathname}?${params.toString()}`);
  }, [searchParams, pathname, router]);

  const centerLat = searchParams?.get('lat') ? parseFloat(searchParams?.get('lat')!) : undefined;
  const centerLng = searchParams?.get('lng') ? parseFloat(searchParams?.get('lng')!) : undefined;

  const hasFilters = !!(
    searchParams?.get('name')     || searchParams?.get('city')         ||
    searchParams?.get('state')    || searchParams?.get('categoryCode') ||
    searchParams?.get('acceptingReferrals')
  );

  const result = initialProviders;
  const page   = initialPage;

  return (
    <div className="space-y-4">
      {/* List / Map toggle */}
      <div className="flex items-center gap-1 bg-gray-100 rounded-lg p-1 w-fit">
        <button
          onClick={() => handleViewToggle('list')}
          className={`px-4 py-1.5 rounded-md text-sm font-medium transition-colors ${
            view === 'list'
              ? 'bg-white text-gray-900 shadow-sm'
              : 'text-gray-500 hover:text-gray-700'
          }`}
        >
          List
        </button>
        <button
          onClick={() => handleViewToggle('map')}
          className={`px-4 py-1.5 rounded-md text-sm font-medium transition-colors ${
            view === 'map'
              ? 'bg-white text-gray-900 shadow-sm'
              : 'text-gray-500 hover:text-gray-700'
          }`}
        >
          Map
        </button>
      </div>

      {/* ── LIST VIEW ─────────────────────────────────────────────────────── */}
      {view === 'list' && (
        <>
          {fetchError && (
            <div className="bg-red-50 border border-red-200 rounded-lg px-4 py-3 text-sm text-red-700">
              {fetchError}
            </div>
          )}

          {result && result.items.length === 0 && (
            <div className="bg-white border border-gray-200 rounded-lg p-10 text-center">
              <p className="text-sm text-gray-400">
                {hasFilters
                  ? 'No providers match your filters. Try adjusting your search.'
                  : 'No providers available.'}
              </p>
            </div>
          )}

          {result && result.items.length > 0 && (
            <>
              <div className="grid gap-3">
                {result.items.map(provider => (
                  <ProviderCard
                    key={provider.id}
                    provider={provider}
                    isReferrer={isReferrer}
                    referrerEmail={session?.email}
                    referrerName={session?.orgName}
                  />
                ))}
              </div>

              {result.totalCount > 20 && (
                <div className="flex items-center justify-between">
                  <p className="text-xs text-gray-400">
                    Page {page} of {Math.ceil(result.totalCount / 20)}
                  </p>
                  <div className="flex items-center gap-3">
                    {page > 1 && (
                      <a
                        href={`?${new URLSearchParams({ ...Object.fromEntries(searchParams ?? []), page: String(page - 1) })}`}
                        className="text-sm text-primary hover:underline"
                      >
                        ← Previous
                      </a>
                    )}
                    {page * 20 < result.totalCount && (
                      <a
                        href={`?${new URLSearchParams({ ...Object.fromEntries(searchParams ?? []), page: String(page + 1) })}`}
                        className="text-sm text-primary hover:underline"
                      >
                        Next →
                      </a>
                    )}
                  </div>
                </div>
              )}
            </>
          )}
        </>
      )}

      {/* ── MAP VIEW ──────────────────────────────────────────────────────── */}
      {view === 'map' && (
        <div
          className="relative rounded-lg overflow-hidden border border-gray-200"
          style={{ height: 560 }}
        >
          {markersLoading && (
            <div className="absolute inset-0 bg-white/60 z-[9999] flex items-center justify-center pointer-events-none">
              <span className="text-sm text-gray-500 bg-white px-4 py-2 rounded-md shadow-sm border border-gray-200">
                Loading markers…
              </span>
            </div>
          )}

          {markersError && (
            <div className="absolute top-4 left-1/2 -translate-x-1/2 z-[9999] bg-red-50 border border-red-200 rounded-md px-4 py-2 text-sm text-red-700 shadow">
              {markersError}
            </div>
          )}

          {!markersLoading && markers.length === 0 && !markersError && (
            <div className="absolute bottom-5 left-1/2 -translate-x-1/2 z-[9999] bg-white border border-gray-200 rounded-md px-4 py-2 text-sm text-gray-500 shadow-sm">
              No providers with location data match these filters.
            </div>
          )}

          <div className="absolute top-3 right-3 z-[9999] bg-white/90 border border-gray-200 rounded-md px-2.5 py-1 text-xs text-gray-500 shadow-sm">
            {markers.length} {markers.length === 1 ? 'provider' : 'providers'}
          </div>

          <ProviderMap
            markers={markers}
            selectedId={selectedMarkerId}
            onSelect={setSelectedMarkerId}
            onViewportChange={handleViewportChange}
            isReferrer={isReferrer}
            centerLat={centerLat}
            centerLng={centerLng}
          />
        </div>
      )}
    </div>
  );
}
