'use client';

/**
 * AnalyticsProvider — automatic page view tracking for the Control Center.
 *
 * Place this component once in the root layout, wrapping the page children.
 * It fires a 'page.view' analytics event every time the Next.js pathname
 * changes (i.e. every client-side navigation in the App Router).
 *
 * This is a client component because it uses usePathname() and useEffect()
 * to detect navigations. The analytics.track() call is a no-op server-side.
 *
 * Usage in layout.tsx:
 *   import { AnalyticsProvider } from '@/components/analytics/analytics-provider';
 *   <body>
 *     <AnalyticsProvider>{children}</AnalyticsProvider>
 *   </body>
 *
 * TODO: call identifyUser(userId, email) here after confirming the session
 *       is available on the client (e.g. read from a NEXT_PUBLIC cookie or
 *       a lightweight /api/me endpoint).
 */

import { useEffect }   from 'react';
import { usePathname } from 'next/navigation';
import { track }       from '@/lib/analytics';

interface AnalyticsProviderProps {
  children: React.ReactNode;
}

export function AnalyticsProvider({ children }: AnalyticsProviderProps) {
  const pathname = usePathname();

  useEffect(() => {
    track('page.view', { path: pathname });
  }, [pathname]);

  return <>{children}</>;
}
