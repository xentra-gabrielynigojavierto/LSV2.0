/**
 * Tenants list — loading skeleton.
 *
 * Shown by Next.js App Router while the Tenants Server Component page
 * is suspended (waiting for session + data fetches to resolve).
 * Matches the visual structure of /tenants page.
 */

import {
  LoadingShell,
  HeaderSkeleton,
  FilterBarSkeleton,
  TableSkeleton,
} from '@/components/ui/loading-shell';

export default function TenantsLoading() {
  return (
    <LoadingShell>
      <div className="space-y-4">
        {/* Header row: title + create button */}
        <div className="flex items-center justify-between animate-pulse">
          <HeaderSkeleton hasSubtitle={false} />
          <div className="h-9 w-32 bg-indigo-200 rounded-md" />
        </div>

        {/* Search bar */}
        <FilterBarSkeleton inputs={1} />

        {/* Table: 6 columns, 10 rows */}
        <TableSkeleton rows={10} cols={6} />
      </div>
    </LoadingShell>
  );
}
