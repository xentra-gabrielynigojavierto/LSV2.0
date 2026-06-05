/**
 * Roles — loading skeleton.
 *
 * Shown by Next.js App Router while the Roles Server Component page
 * is suspended (waiting for session + data fetches to resolve).
 * Matches the visual structure of /roles page.
 */

import {
  LoadingShell,
  HeaderSkeleton,
  TableSkeleton,
} from '@/components/ui/loading-shell';

export default function RolesLoading() {
  return (
    <LoadingShell>
      <div className="space-y-4">
        <HeaderSkeleton />
        <TableSkeleton rows={6} cols={4} />
      </div>
    </LoadingShell>
  );
}
