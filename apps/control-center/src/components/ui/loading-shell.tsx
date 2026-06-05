/**
 * LoadingShell — skeleton placeholder for the Control Center shell.
 *
 * Rendered by each route's loading.tsx file while the Server Component
 * page is fetching data. Matches the visual structure of CCShell so the
 * layout does not shift when the real page mounts:
 *
 *   ┌────────────────────────────────────────────────────────┐
 *   │  Top bar (h-14, white, border-b)                       │
 *   ├──────────┬─────────────────────────────────────────────┤
 *   │ Sidebar  │  Main content                               │
 *   │  w-56    │  (slot for page-specific skeleton)          │
 *   │          │                                             │
 *   └──────────┴─────────────────────────────────────────────┘
 *
 * Usage:
 *   // src/app/tenants/loading.tsx
 *   import { LoadingShell } from '@/components/ui/loading-shell';
 *   export default function Loading() {
 *     return (
 *       <LoadingShell>
 *         <TenantsSkeleton />
 *       </LoadingShell>
 *     );
 *   }
 *
 * Accessibility:
 *   - aria-busy="true" on the main content area
 *   - aria-label="Loading, please wait" on the loading container
 */

import type { ReactNode } from 'react';

interface LoadingShellProps {
  children?: ReactNode;
}

export function LoadingShell({ children }: LoadingShellProps) {
  return (
    <div
      className="flex flex-col h-screen bg-gray-50"
      aria-label="Loading, please wait"
      aria-busy="true"
    >
      {/* Top bar skeleton */}
      <header className="h-14 border-b border-gray-200 bg-white flex items-center px-4 gap-4 shrink-0">
        {/* Logo placeholder */}
        <div className="h-4 w-20 bg-gray-200 rounded animate-pulse" />
        <div className="w-px h-6 bg-gray-200" />
        {/* Badge placeholder */}
        <div className="h-6 w-28 bg-indigo-50 border border-indigo-100 rounded-md animate-pulse" />
        <div className="flex-1" />
        {/* User area placeholder */}
        <div className="flex items-center gap-3">
          <div className="h-4 w-32 bg-gray-200 rounded animate-pulse" />
          <div className="h-7 w-20 bg-gray-100 rounded-md animate-pulse" />
        </div>
      </header>

      {/* Body */}
      <div className="flex flex-1 overflow-hidden">

        {/* Sidebar skeleton */}
        <aside className="w-56 shrink-0 border-r border-gray-200 bg-white flex flex-col h-full overflow-y-auto py-4 px-2 space-y-6">
          {/* Nav group 1 */}
          <div className="space-y-1">
            <div className="h-3 w-16 bg-gray-200 rounded mx-3 mb-2 animate-pulse" />
            {[100, 88, 92, 80].map(w => (
              <div key={w} className="h-8 rounded-md bg-gray-100 animate-pulse mx-0.5" style={{ width: `${w}%` }} />
            ))}
          </div>
          {/* Nav group 2 */}
          <div className="space-y-1">
            <div className="h-3 w-14 bg-gray-200 rounded mx-3 mb-2 animate-pulse" />
            {[96, 84].map(w => (
              <div key={w} className="h-8 rounded-md bg-gray-100 animate-pulse mx-0.5" style={{ width: `${w}%` }} />
            ))}
          </div>
        </aside>

        {/* Main content — page-specific skeleton injected here */}
        <main
          className="flex-1 overflow-y-auto p-6"
          aria-live="polite"
          aria-label="Page content loading"
        >
          {children ?? <DefaultContentSkeleton />}
        </main>

      </div>
    </div>
  );
}

// ── Default skeleton (used when no specific content is provided) ──────────────

function DefaultContentSkeleton() {
  return (
    <div className="space-y-4 animate-pulse">
      {/* Page title */}
      <div className="h-7 w-40 bg-gray-200 rounded" />
      {/* Sub-title */}
      <div className="h-4 w-64 bg-gray-200 rounded" />
      {/* Card */}
      <div className="h-64 bg-white border border-gray-200 rounded-lg" />
    </div>
  );
}

// ── Reusable skeleton primitives ─────────────────────────────────────────────

/** Skeleton for a standard page header (title + optional subtitle). */
export function HeaderSkeleton({ hasSubtitle = true }: { hasSubtitle?: boolean }) {
  return (
    <div className="space-y-2 animate-pulse">
      <div className="h-7 w-36 bg-gray-200 rounded" />
      {hasSubtitle && <div className="h-4 w-56 bg-gray-100 rounded" />}
    </div>
  );
}

/** Skeleton for a filter/search form bar. */
export function FilterBarSkeleton({ inputs = 2 }: { inputs?: number }) {
  return (
    <div className="flex items-center gap-2 animate-pulse">
      {Array.from({ length: inputs }).map((_, i) => (
        <div
          key={i}
          className={`h-8 bg-gray-200 rounded-md ${i === 0 ? 'w-56' : 'w-36'}`}
        />
      ))}
      <div className="h-8 w-20 bg-indigo-200 rounded-md" />
    </div>
  );
}

/** Skeleton for a data table (header + N rows of M columns). */
export function TableSkeleton({ rows = 8, cols = 5 }: { rows?: number; cols?: number }) {
  return (
    <div className="bg-white border border-gray-200 rounded-lg overflow-hidden animate-pulse">
      {/* Header row */}
      <div className="flex items-center gap-4 px-4 py-3 border-b border-gray-100 bg-gray-50">
        {Array.from({ length: cols }).map((_, i) => (
          <div key={i} className="h-3 bg-gray-300 rounded flex-1" style={{ maxWidth: `${60 + i * 15}px` }} />
        ))}
      </div>
      {/* Data rows */}
      {Array.from({ length: rows }).map((_, r) => (
        <div key={r} className="flex items-center gap-4 px-4 py-3.5 border-b border-gray-50 last:border-0">
          {Array.from({ length: cols }).map((_, c) => (
            <div
              key={c}
              className="h-4 bg-gray-100 rounded flex-1"
              style={{ maxWidth: `${50 + (c + r) * 12}px`, opacity: 1 - r * 0.08 }}
            />
          ))}
        </div>
      ))}
    </div>
  );
}
