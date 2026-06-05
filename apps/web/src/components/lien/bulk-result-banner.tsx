'use client';

import Link from 'next/link';
import type { BulkOperationResult } from '@/lib/bulk-operations';

interface BulkResultBannerProps {
  result: BulkOperationResult | null;
  onDismiss: () => void;
  entityLabel?: string;
}

export function BulkResultBanner({ result, onDismiss, entityLabel = 'items' }: BulkResultBannerProps) {
  if (!result) return null;

  const allSucceeded = result.failedCount === 0;
  const allFailed = result.succeededCount === 0;
  const partial = !allSucceeded && !allFailed;

  const bgClass = allSucceeded
    ? 'bg-emerald-50 border-emerald-200'
    : allFailed
    ? 'bg-red-50 border-red-200'
    : 'bg-amber-50 border-amber-200';

  const iconClass = allSucceeded
    ? 'ri-checkbox-circle-fill text-emerald-600'
    : allFailed
    ? 'ri-close-circle-fill text-red-600'
    : 'ri-error-warning-fill text-amber-600';

  const failedItems = result.results.filter((r) => !r.success);

  return (
    <div className={`border rounded-lg px-4 py-3 ${bgClass}`}>
      <div className="flex items-start justify-between gap-3">
        <div className="flex items-start gap-2 min-w-0">
          <i className={`${iconClass} text-lg mt-0.5 shrink-0`} />
          <div className="min-w-0">
            <p className="text-sm font-medium text-gray-800">
              {allSucceeded && `${result.succeededCount} ${entityLabel} updated successfully`}
              {allFailed && `Failed to update ${result.failedCount} ${entityLabel}`}
              {partial &&
                `${result.succeededCount} succeeded, ${result.failedCount} failed`}
            </p>

            {failedItems.length > 0 && (
              <ul className="mt-1.5 space-y-0.5">
                {failedItems.slice(0, 5).map((item) => (
                  <li key={item.id} className="text-xs text-gray-600">
                    <span className="font-mono text-gray-500">{item.id.slice(0, 8)}...</span>
                    {' — '}
                    {item.error || 'Unknown error'}
                  </li>
                ))}
                {failedItems.length > 5 && (
                  <li className="text-xs text-gray-500">
                    +{failedItems.length - 5} more failures
                  </li>
                )}
              </ul>
            )}

            <Link
              href="/lien/activity"
              className="inline-block mt-2 text-xs text-blue-600 hover:text-blue-700 hover:underline"
            >
              View Activity Log
            </Link>
          </div>
        </div>

        <button
          onClick={onDismiss}
          className="shrink-0 p-1 rounded hover:bg-black/5 text-gray-400 hover:text-gray-600 transition-colors"
          aria-label="Dismiss"
        >
          <i className="ri-close-line text-lg" />
        </button>
      </div>
    </div>
  );
}
