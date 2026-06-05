/**
 * Support cases — loading skeleton.
 *
 * Shown by Next.js App Router while the Support Server Component page
 * is suspended (waiting for session + data fetches to resolve).
 * Matches the visual structure of /support page.
 */

import {
  LoadingShell,
  HeaderSkeleton,
  FilterBarSkeleton,
  TableSkeleton,
} from '@/components/ui/loading-shell';

export default function SupportLoading() {
  return (
    <LoadingShell>
      <div className="space-y-4">
        {/* Page header */}
        <HeaderSkeleton />

        {/* Filter bar: search + status + priority + submit */}
        <FilterBarSkeleton inputs={3} />

        {/* Table: 6 columns, 8 rows */}
        <TableSkeleton rows={8} cols={6} />
      </div>
    </LoadingShell>
  );
}
