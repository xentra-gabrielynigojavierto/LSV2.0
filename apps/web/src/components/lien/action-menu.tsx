'use client';

import { useState, useRef, useEffect } from 'react';

export interface ActionMenuItem {
  label: string;
  icon?: string;
  onClick: () => void;
  variant?: 'default' | 'danger';
  disabled?: boolean;
  /** LS-ID-TNT-015-004: Short explanation shown below a disabled item. */
  disabledReason?: string;
  divider?: boolean;
}

interface ActionMenuProps {
  items: ActionMenuItem[];
  triggerIcon?: string;
}

export function ActionMenu({ items, triggerIcon = 'ri-more-2-fill' }: ActionMenuProps) {
  const [open, setOpen] = useState(false);
  const ref = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!open) return;
    const handler = (e: MouseEvent) => { if (ref.current && !ref.current.contains(e.target as Node)) setOpen(false); };
    document.addEventListener('mousedown', handler);
    return () => document.removeEventListener('mousedown', handler);
  }, [open]);

  return (
    <div ref={ref} className="relative">
      <button onClick={(e) => { e.stopPropagation(); setOpen(!open); }} aria-label="Actions menu" aria-haspopup="true" aria-expanded={open} className="p-1.5 rounded-lg hover:bg-gray-100 text-gray-400 hover:text-gray-600 transition-colors">
        <i className={`${triggerIcon} text-base`} />
      </button>
      {open && (
        <div role="menu" className="absolute right-0 top-full mt-1 w-48 bg-white border border-gray-200 rounded-xl shadow-lg z-20 py-1 animate-in fade-in zoom-in-95 duration-150">
          {items.map((item, i) => (
            <div key={i}>
              {item.divider && i > 0 && <div className="border-t border-gray-100 my-1" />}
              <button
                role="menuitem"
                onClick={(e) => { e.stopPropagation(); if (!item.disabled) { item.onClick(); setOpen(false); } }}
                disabled={item.disabled}
                aria-disabled={item.disabled}
                className={`w-full text-left px-3 py-2 text-sm flex items-start gap-2 transition-colors disabled:opacity-40 disabled:cursor-not-allowed ${
                  item.variant === 'danger' ? 'text-red-600 hover:bg-red-50' : 'text-gray-700 hover:bg-gray-50'
                }`}
              >
                {item.icon && <i className={`${item.icon} text-base mt-px shrink-0`} />}
                <span className="flex flex-col gap-0.5 text-left">
                  <span>{item.label}</span>
                  {/* LS-ID-TNT-015-004: Inline reason hint for disabled items */}
                  {item.disabled && item.disabledReason && (
                    <span className="text-xs text-gray-400 font-normal leading-tight">
                      {item.disabledReason}
                    </span>
                  )}
                </span>
              </button>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
