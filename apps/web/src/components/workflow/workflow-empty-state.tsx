'use client';

interface Props {
  productLabel: string;
  canStart: boolean;
  onStart?: () => void;
}

export function WorkflowEmptyState({ productLabel, canStart, onStart }: Props) {
  return (
    <div className="text-center py-3">
      <i className="ri-flow-chart text-2xl text-gray-300" />
      <p className="text-xs text-gray-500 mt-1">No {productLabel} started for this case yet.</p>
      {canStart && onStart && (
        <button
          type="button"
          onClick={onStart}
          className="mt-2 inline-flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium rounded-md bg-primary text-white hover:bg-primary/90 transition-colors"
        >
          <i className="ri-play-line text-sm" />
          Start workflow
        </button>
      )}
    </div>
  );
}
