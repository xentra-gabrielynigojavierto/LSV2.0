/**
 * Audit logs — loading skeleton.
 *
 * Shown by Next.js App Router while the Audit Logs Server Component page
 * is suspended (waiting for session + data fetches to resolve).
 * Matches the visual structure of /audit-logs page.
 */

import {
  LoadingShell,
  HeaderSkeleton,
  FilterBarSkeleton,
  TableSkeleton,
} from '@/components/ui/loading-shell';

export default function AuditLogsLoading() {
  return (
    <LoadingShell>
      <div className="space-y-4">
        {/* Page header + subtitle */}
        <HeaderSkeleton />

        {/* Filter bar: search + entity type + actor + submit */}
        <FilterBarSkeleton inputs={3} />

        {/* Result count skeleton */}
        <div className="h-4 w-48 bg-gray-200 rounded animate-pulse" />

        {/* Table: 5 columns, 12 rows (audit tables tend to be denser) */}
        <TableSkeleton rows={12} cols={5} />
      </div>
    </LoadingShell>
  );
}
