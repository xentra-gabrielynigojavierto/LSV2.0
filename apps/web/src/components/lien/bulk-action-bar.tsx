'use client';

import type { BulkActionConfig } from '@/lib/bulk-operations';

interface BulkActionBarProps {
  count: number;
  actions: BulkActionConfig[];
  onAction: (actionKey: string) => void;
  onClear: () => void;
}

export function BulkActionBar({ count, actions, onAction, onClear }: BulkActionBarProps) {
  if (count === 0) return null;

  return (
    <div className="fixed bottom-6 left-1/2 -translate-x-1/2 z-40 flex items-center gap-3 bg-gray-900 text-white rounded-xl shadow-2xl px-5 py-3 animate-in slide-in-from-bottom-4 duration-200">
      <span className="text-sm font-medium tabular-nums">
        {count} selected
      </span>

      <div className="w-px h-5 bg-gray-600" />

      {actions.map((action) => (
        <button
          key={action.key}
          onClick={() => onAction(action.key)}
          className={`flex items-center gap-1.5 text-sm font-medium px-3 py-1.5 rounded-lg transition-colors ${
            action.variant === 'danger'
              ? 'bg-red-600 hover:bg-red-700 text-white'
              : 'bg-white/10 hover:bg-white/20 text-white'
          }`}
        >
          <i className={`${action.icon} text-base`} />
          {action.label}
        </button>
      ))}

      <div className="w-px h-5 bg-gray-600" />

      <button
        onClick={onClear}
        className="text-sm text-gray-400 hover:text-white transition-colors px-2 py-1"
      >
        Clear
      </button>
    </div>
  );
}
