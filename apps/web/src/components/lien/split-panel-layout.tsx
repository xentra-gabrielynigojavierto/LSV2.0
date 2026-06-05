'use client';

import { useState, type ReactNode } from 'react';

export type PanelMode = 'split' | 'left-expanded' | 'right-expanded';

interface SplitPanelLayoutProps {
  left: ReactNode;
  right: ReactNode;
  leftLabel?: string;
  rightLabel?: string;
  defaultMode?: PanelMode;
}

export function SplitPanelLayout({
  left,
  right,
  leftLabel = 'Main',
  rightLabel = 'Details',
  defaultMode = 'split',
}: SplitPanelLayoutProps) {
  const [mode, setMode] = useState<PanelMode>(defaultMode);

  return (
    <div className="flex gap-4 h-full min-h-[400px]">
      {mode === 'right-expanded' ? (
        <CollapsedStrip
          label={leftLabel}
          side="left"
          onExpand={() => setMode('split')}
        />
      ) : (
        <div
          className={`bg-white border border-gray-200 rounded-xl overflow-auto transition-all duration-200 ${
            mode === 'left-expanded' ? 'flex-1 min-w-0' : 'flex-[7] min-w-0'
          }`}
        >
          {left}
        </div>
      )}

      <PanelDivider mode={mode} setMode={setMode} />

      {mode === 'left-expanded' ? (
        <CollapsedStrip
          label={rightLabel}
          side="right"
          onExpand={() => setMode('split')}
        />
      ) : (
        <div
          className={`bg-white border border-gray-200 rounded-xl overflow-auto transition-all duration-200 ${
            mode === 'right-expanded' ? 'flex-1 min-w-0' : 'flex-[3] min-w-0'
          }`}
        >
          {right}
        </div>
      )}
    </div>
  );
}

function PanelDivider({
  mode,
  setMode,
}: {
  mode: PanelMode;
  setMode: (m: PanelMode) => void;
}) {
  if (mode !== 'split') return null;

  return (
    <div className="flex flex-col items-center justify-center gap-1.5 shrink-0">
      <button
        onClick={() => setMode('left-expanded')}
        title="Expand left panel"
        className="w-6 h-6 flex items-center justify-center rounded hover:bg-gray-100 text-gray-400 hover:text-gray-600 transition-colors"
      >
        <i className="ri-arrow-left-s-line text-sm" />
      </button>
      <div className="w-px h-8 bg-gray-200" />
      <button
        onClick={() => setMode('right-expanded')}
        title="Expand right panel"
        className="w-6 h-6 flex items-center justify-center rounded hover:bg-gray-100 text-gray-400 hover:text-gray-600 transition-colors"
      >
        <i className="ri-arrow-right-s-line text-sm" />
      </button>
    </div>
  );
}

function CollapsedStrip({
  label,
  side,
  onExpand,
}: {
  label: string;
  side: 'left' | 'right';
  onExpand: () => void;
}) {
  return (
    <div className="w-10 shrink-0 bg-white border border-gray-200 rounded-xl flex flex-col items-center pt-3 gap-2 self-stretch">
      <button
        onClick={onExpand}
        title={`Restore ${label}`}
        className="w-7 h-7 flex items-center justify-center rounded-lg hover:bg-gray-100 text-gray-400 hover:text-gray-600 transition-colors"
      >
        <i
          className={`${
            side === 'left'
              ? 'ri-arrow-right-double-line'
              : 'ri-arrow-left-double-line'
          } text-base`}
        />
      </button>
      <button
        onClick={onExpand}
        title="Restore split view"
        className="w-7 h-7 flex items-center justify-center rounded-lg hover:bg-gray-100 text-gray-400 hover:text-gray-600 transition-colors"
      >
        <i className="ri-layout-column-line text-base" />
      </button>
      <span
        className="text-[11px] font-semibold text-primary tracking-wide mt-1"
        style={{ writingMode: 'vertical-rl', textOrientation: 'mixed' }}
      >
        {label}
      </span>
    </div>
  );
}

export function SectionCard({
  title,
  icon,
  iconBg,
  iconColor,
  actions,
  children,
  className = '',
}: {
  title: string;
  icon: string;
  iconBg?: string;
  iconColor?: string;
  actions?: ReactNode;
  children: ReactNode;
  className?: string;
}) {
  return (
    <div className={`border border-gray-100 rounded-lg bg-white ${className}`}>
      <div className="flex items-center justify-between px-4 py-3 border-b border-gray-100">
        <h3 className="text-sm font-semibold text-gray-800 flex items-center gap-2">
          <span
            className={`w-6 h-6 rounded-md flex items-center justify-center ${iconBg || 'bg-gray-50'}`}
          >
            <i className={`${icon} text-xs ${iconColor || 'text-gray-600'}`} />
          </span>
          {title}
        </h3>
        {actions && <div className="flex items-center gap-1">{actions}</div>}
      </div>
      <div className="px-4 py-3">{children}</div>
    </div>
  );
}

export function MetadataGrid({ children }: { children: ReactNode }) {
  return (
    <dl className="grid grid-cols-2 sm:grid-cols-3 gap-x-6 gap-y-3">
      {children}
    </dl>
  );
}

const COL_SPAN_MAP: Record<number, string> = {
  1: 'sm:col-span-1',
  2: 'sm:col-span-2',
  3: 'sm:col-span-3',
};

export function MetadataItem({
  label,
  value,
  colSpan,
}: {
  label: string;
  value: string;
  colSpan?: number;
}) {
  return (
    <div className={colSpan ? COL_SPAN_MAP[colSpan] || '' : ''}>
      <dt className="text-[11px] font-medium text-gray-400 uppercase tracking-wide">
        {label}
      </dt>
      <dd className="text-sm text-gray-700 mt-0.5">{value || '---'}</dd>
    </div>
  );
}
