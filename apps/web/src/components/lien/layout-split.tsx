'use client';

import { useState, type ReactNode } from 'react';

export type PanelMode = 'split' | 'left-expanded' | 'right-expanded';

interface LayoutSplitProps {
  left: ReactNode;
  right: ReactNode;
  defaultMode?: 'split' | 'left' | 'right';
  mode?: PanelMode;
  onModeChange?: (mode: PanelMode) => void;
  className?: string;
}

const MODE_MAP: Record<string, PanelMode> = {
  split: 'split',
  left: 'left-expanded',
  right: 'right-expanded',
};

export function LayoutSplit({ left, right, defaultMode = 'split', mode: controlledMode, onModeChange, className }: LayoutSplitProps) {
  const [internalMode, setInternalMode] = useState<PanelMode>(MODE_MAP[defaultMode] ?? 'split');
  const mode = controlledMode ?? internalMode;
  const setMode = (m: PanelMode) => {
    if (onModeChange) onModeChange(m);
    else setInternalMode(m);
  };

  const showLeft = mode !== 'right-expanded';
  const showRight = mode !== 'left-expanded';

  const gridClass =
    mode === 'split'
      ? 'grid-cols-[1fr_auto_minmax(0,0.42fr)]'
      : mode === 'left-expanded'
        ? 'grid-cols-[1fr_auto]'
        : 'grid-cols-[auto_1fr]';

  const handleLeftExpand = () => {
    setMode(mode === 'split' ? 'left-expanded' : 'split');
  };

  const handleRightExpand = () => {
    setMode(mode === 'split' ? 'right-expanded' : 'split');
  };

  const leftBtnIcon = mode === 'left-expanded' ? 'right' : 'left';
  const rightBtnIcon = mode === 'right-expanded' ? 'left' : 'right';

  return (
    <div className={`grid ${gridClass} gap-0 items-start ${className ?? ''}`}>
      {showLeft && <div className="min-w-0">{left}</div>}

      <div className="flex flex-col items-center justify-start pt-1 gap-1 self-stretch shrink-0 mx-1">
        <button
          onClick={handleLeftExpand}
          title={mode === 'split' ? 'Expand left panel' : 'Restore split view'}
          className={`w-7 h-7 flex items-center justify-center rounded-md border transition-colors ${
            mode === 'left-expanded'
              ? 'border-primary bg-primary/10 text-primary'
              : 'border-gray-200 bg-white text-gray-400 hover:text-gray-600 hover:border-gray-300 hover:bg-gray-50'
          }`}
        >
          <i className={`ri-arrow-${leftBtnIcon}-s-line text-sm`} />
        </button>
        <div className="w-px h-4 bg-gray-200" />
        <button
          onClick={handleRightExpand}
          title={mode === 'split' ? 'Expand right panel' : 'Restore split view'}
          className={`w-7 h-7 flex items-center justify-center rounded-md border transition-colors ${
            mode === 'right-expanded'
              ? 'border-primary bg-primary/10 text-primary'
              : 'border-gray-200 bg-white text-gray-400 hover:text-gray-600 hover:border-gray-300 hover:bg-gray-50'
          }`}
        >
          <i className={`ri-arrow-${rightBtnIcon}-s-line text-sm`} />
        </button>
      </div>

      {showRight && <div className="min-w-0">{right}</div>}
    </div>
  );
}
