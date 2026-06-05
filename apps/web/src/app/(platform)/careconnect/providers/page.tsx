import { requireOrg } from '@/lib/auth-guards';
import { ProductRole } from '@/types';
import { careConnectServerApi } from '@/lib/careconnect-server-api';
import { ServerApiError } from '@/lib/server-api-client';
import { ProviderSearchFilters } from '@/components/careconnect/provider-search-filters';
import { ProviderMapShell } from '@/components/careconnect/provider-map-shell';

export const dynamic = 'force-dynamic';


interface ProvidersPageProps {
  searchParams: Promise<{
    name?:               string;
    city?:               string;
    state?:              string;
    categoryCode?:       string;
    acceptingReferrals?: string;
    page?:               string;
    view?:               string;
    lat?:                string;
    lng?:                string;
    radius?:             string;
    nLat?:               string;
    sLat?:               string;
    eLng?:               string;
    wLng?:               string;
  }>;
}

/**
 * /careconnect/providers — Provider search with list/map toggle.
 *
 * Access: CARECONNECT_REFERRER only. Receivers (providers) are shown an
 * inline message directing them to their Referral Inbox instead.
 *
 * Rendering: Server Component — fetches initial list data and passes it
 * to ProviderMapShell (Client Component) as a prop.
 *   - List mode uses the initial data (no extra round-trip).
 *   - Map mode fetches markers client-side via BFF proxy.
 *
 * URL params:
 *   name, city, state, categoryCode, acceptingReferrals  — text filters
 *   page                                                  — list pagination
 *   view                                                  — "list" | "map"
 *   lat, lng, radius                                      — geolocation (radius search)
 *   nLat, sLat, eLng, wLng                               — viewport bounds (map pan)
 */
export default async function ProvidersPage({ searchParams }: ProvidersPageProps) {
  const searchParamsData = await searchParams;
  const session = await requireOrg();

  const isReferrer = session.productRoles.includes(ProductRole.CareConnectReferrer);

  if (!isReferrer) {
    return (
      <div className="bg-yellow-50 border border-yellow-200 rounded-lg px-4 py-3 text-sm text-yellow-700">
        Provider search is available to referring organizations. Visit your{' '}
        <a href="/careconnect/referrals" className="text-blue-600 hover:underline font-medium">Referral Inbox</a>{' '}
        to view referrals assigned to you.
      </div>
    );
  }

  const page = Math.max(1, parseInt(searchParamsData.page ?? '1') || 1);

  const lat    = searchParamsData.lat    ? parseFloat(searchParamsData.lat)    : undefined;
  const lng    = searchParamsData.lng    ? parseFloat(searchParamsData.lng)    : undefined;
  const radius = searchParamsData.radius ? parseFloat(searchParamsData.radius) : undefined;
  const nLat   = searchParamsData.nLat   ? parseFloat(searchParamsData.nLat)   : undefined;
  const sLat   = searchParamsData.sLat   ? parseFloat(searchParamsData.sLat)   : undefined;
  const eLng   = searchParamsData.eLng   ? parseFloat(searchParamsData.eLng)   : undefined;
  const wLng   = searchParamsData.wLng   ? parseFloat(searchParamsData.wLng)   : undefined;

  const geoParams =
    lat && lng && radius
      ? { latitude: lat, longitude: lng, radiusMiles: radius }
      : nLat && sLat && eLng && wLng
      ? { northLat: nLat, southLat: sLat, eastLng: eLng, westLng: wLng }
      : {};

  let result = null;
  let fetchError: string | null = null;

  try {
    result = await careConnectServerApi.providers.search({
      name:               searchParamsData.name               || undefined,
      city:               searchParamsData.city               || undefined,
      state:              searchParamsData.state              || undefined,
      categoryCode:       searchParamsData.categoryCode       || undefined,
      acceptingReferrals: searchParamsData.acceptingReferrals === 'true' ? true : undefined,
      isActive:           true,
      page,
      pageSize:           20,
      ...geoParams,
    });
  } catch (err) {
    fetchError =
      err instanceof ServerApiError
        ? err.isNotFound
          ? 'No providers found.'
          : err.message
        : 'Failed to load providers.';
  }

  return (
    <div className="space-y-4">
      {/* Header */}
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold text-gray-900">Find Providers</h1>
        {result && (
          <span className="text-sm text-gray-400">
            {result.totalCount.toLocaleString()}{' '}
            {result.totalCount === 1 ? 'result' : 'results'}
          </span>
        )}
      </div>

      {/* Filters (client component, URL-synced) */}
      <ProviderSearchFilters />

      {/* Shell — handles list/map toggle + rendering */}
      <ProviderMapShell
        initialProviders={result}
        initialPage={page}
        isReferrer={isReferrer}
        fetchError={fetchError}
      />
    </div>
  );
}
