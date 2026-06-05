/**
 * Tenant Users — loading skeleton.
 *
 * Shown by Next.js App Router while the Tenant Users Server Component page
 * is suspended (waiting for session + data fetches to resolve).
 * Matches the visual structure of /tenant-users page.
 */

import {
  LoadingShell,
  HeaderSkeleton,
  FilterBarSkeleton,
  TableSkeleton,
} from '@/components/ui/loading-shell';

export default function TenantUsersLoading() {
  return (
    <LoadingShell>
      <div className="space-y-4">
        <HeaderSkeleton />
        <FilterBarSkeleton inputs={1} />
        <TableSkeleton rows={10} cols={6} />
      </div>
    </LoadingShell>
  );
}
