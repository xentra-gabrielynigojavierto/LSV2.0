'use client';

export function WorkflowLoadingState() {
  return (
    <div className="space-y-2 py-1" aria-busy="true" aria-live="polite">
      <div className="h-3 w-2/3 bg-gray-100 rounded animate-pulse" />
      <div className="h-3 w-1/2 bg-gray-100 rounded animate-pulse" />
      <div className="h-3 w-3/4 bg-gray-100 rounded animate-pulse" />
    </div>
  );
}
