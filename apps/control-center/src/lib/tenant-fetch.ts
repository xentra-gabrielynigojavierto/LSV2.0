/**
 * tenant-fetch.ts — Per-request memoised tenant fetcher.
 *
 * React's `cache()` deduplicates calls within the same server render tree,
 * so layout.tsx and page.tsx both calling `getCachedTenantById(id)` result
 * in exactly one outbound HTTP request to the Tenant service.
 *
 * Server-only: import only from Server Components, Server Actions, or
 * Route Handlers.  Never import into Client Components.
 */

import 'server-only';
import { cache }                     from 'react';
import { controlCenterServerApi }    from '@/lib/control-center-api';

/**
 * Fetch a single tenant by ID, memoised for the lifetime of the current
 * React render (i.e. one request).  Identical calls within layout.tsx and
 * page.tsx are coalesced into a single upstream fetch.
 */
export const getCachedTenantById = cache(
  (id: string) => controlCenterServerApi.tenants.getById(id),
);
