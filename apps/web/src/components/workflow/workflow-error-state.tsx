'use client';

import { ApiError } from '@/lib/api-client';

interface Props {
  error: ApiError | Error;
  onRetry?: () => void;
}

export function WorkflowErrorState({ error, onRetry }: Props) {
  const msg = friendlyMessage(error);
  return (
    <div className="rounded-md bg-red-50 border border-red-100 p-2.5">
      <div className="flex items-start gap-2">
        <i className="ri-error-warning-line text-red-500 text-base shrink-0 mt-0.5" />
        <div className="min-w-0 flex-1">
          <p className="text-xs text-red-700 font-medium">Couldn&apos;t load workflow</p>
          <p className="text-xs text-red-600 mt-0.5 break-words">{msg}</p>
          {onRetry && (
            <button
              type="button"
              onClick={onRetry}
              className="mt-1.5 text-xs font-medium text-red-700 underline hover:text-red-800"
            >
              Try again
            </button>
          )}
        </div>
      </div>
    </div>
  );
}

function friendlyMessage(err: ApiError | Error): string {
  if (err instanceof ApiError) {
    if (err.isUnauthorized) return 'Your session expired. Refresh the page to sign in again.';
    if (err.isForbidden)    return "You don't have permission to view this workflow.";
    if (err.isNotFound)     return 'No workflow record was found for this case.';
    if (err.isConflict)     return 'The workflow changed while we were loading it. Try again.';
    if (err.isServerError)  return 'The workflow service is temporarily unavailable.';
  }
  return err.message || 'Unexpected error.';
}
