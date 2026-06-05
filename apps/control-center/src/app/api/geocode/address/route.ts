import { NextResponse, type NextRequest } from 'next/server';

/**
 * GET /api/geocode/address?q={query}
 *
 * BFF proxy for OpenStreetMap Nominatim address autocomplete.
 * Required so we can add the mandatory User-Agent header and avoid CORS.
 * Results are cached for 60 s on the CDN edge.
 *
 * Nominatim usage policy: https://operations.osmfoundation.org/policies/nominatim/
 * Rate-limited to 1 req/s — the 300 ms client debounce keeps us within that.
 */
const NOMINATIM = 'https://nominatim.openstreetmap.org';
const USER_AGENT = 'LegalSynq/2.0 contact@legalsynq.com';

export interface AddressSuggestion {
  displayName:  string;
  addressLine1: string;
  city:         string;
  state:        string;
  postalCode:   string;
  latitude:     number;
  longitude:    number;
}

const STATE_ABBR: Record<string, string> = {
  Alabama: 'AL', Alaska: 'AK', Arizona: 'AZ', Arkansas: 'AR', California: 'CA',
  Colorado: 'CO', Connecticut: 'CT', Delaware: 'DE', Florida: 'FL', Georgia: 'GA',
  Hawaii: 'HI', Idaho: 'ID', Illinois: 'IL', Indiana: 'IN', Iowa: 'IA',
  Kansas: 'KS', Kentucky: 'KY', Louisiana: 'LA', Maine: 'ME', Maryland: 'MD',
  Massachusetts: 'MA', Michigan: 'MI', Minnesota: 'MN', Mississippi: 'MS', Missouri: 'MO',
  Montana: 'MT', Nebraska: 'NE', Nevada: 'NV', 'New Hampshire': 'NH', 'New Jersey': 'NJ',
  'New Mexico': 'NM', 'New York': 'NY', 'North Carolina': 'NC', 'North Dakota': 'ND',
  Ohio: 'OH', Oklahoma: 'OK', Oregon: 'OR', Pennsylvania: 'PA', 'Rhode Island': 'RI',
  'South Carolina': 'SC', 'South Dakota': 'SD', Tennessee: 'TN', Texas: 'TX',
  Utah: 'UT', Vermont: 'VT', Virginia: 'VA', Washington: 'WA', 'West Virginia': 'WV',
  Wisconsin: 'WI', Wyoming: 'WY',
};

export async function GET(request: NextRequest): Promise<NextResponse> {
  const q = request.nextUrl.searchParams.get('q') ?? '';
  if (q.trim().length < 3) {
    return NextResponse.json([] as AddressSuggestion[]);
  }

  const url = new URL(`${NOMINATIM}/search`);
  url.searchParams.set('q', `${q}, USA`);
  url.searchParams.set('format', 'json');
  url.searchParams.set('limit', '6');
  url.searchParams.set('addressdetails', '1');
  url.searchParams.set('countrycodes', 'us');

  let raw: Record<string, unknown>[];
  try {
    const res = await fetch(url.toString(), {
      headers: { 'User-Agent': USER_AGENT, 'Accept-Language': 'en-US,en' },
      next: { revalidate: 60 },
    });
    if (!res.ok) return NextResponse.json([] as AddressSuggestion[]);
    raw = await res.json();
  } catch {
    return NextResponse.json([] as AddressSuggestion[]);
  }

  const suggestions: AddressSuggestion[] = [];

  for (const item of raw) {
    const addr = item.address as Record<string, string> | undefined;
    if (!addr) continue;

    const lat = parseFloat(item.lat as string);
    const lon = parseFloat(item.lon as string);
    if (isNaN(lat) || isNaN(lon)) continue;

    const houseNumber = addr.house_number ?? '';
    const road = addr.road ?? '';
    const addressLine1 = houseNumber ? `${houseNumber} ${road}` : road;

    if (!addressLine1) continue;

    const city =
      addr.city ??
      addr.town ??
      addr.village ??
      addr.municipality ??
      addr.county ??
      '';

    const stateFull = addr.state ?? '';
    const state = STATE_ABBR[stateFull] ?? stateFull.slice(0, 2).toUpperCase();
    const postalCode = addr.postcode?.slice(0, 5) ?? '';

    if (!city || !state) continue;

    const displayName = [
      addressLine1,
      city,
      postalCode ? `${state} ${postalCode}` : state,
    ].filter(Boolean).join(', ');

    if (!suggestions.some(s => s.displayName === displayName)) {
      suggestions.push({ displayName, addressLine1, city, state, postalCode, latitude: lat, longitude: lon });
    }
    if (suggestions.length >= 5) break;
  }

  return NextResponse.json(suggestions, {
    headers: { 'Cache-Control': 'public, s-maxage=60, stale-while-revalidate=120' },
  });
}
