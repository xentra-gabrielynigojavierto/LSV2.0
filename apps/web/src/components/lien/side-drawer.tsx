'use client';

import { useEffect, useRef, type ReactNode } from 'react';

interface SideDrawerProps {
  open: boolean;
  onClose: () => void;
  title: string;
  subtitle?: string;
  children: ReactNode;
  footer?: ReactNode;
  width?: 'sm' | 'md' | 'lg';
}

const WIDTH_MAP = { sm: 'max-w-sm', md: 'max-w-md', lg: 'max-w-lg' };

export function SideDrawer({ open, onClose, title, subtitle, children, footer, width = 'md' }: SideDrawerProps) {
  const overlayRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!open) return;
    const handler = (e: KeyboardEvent) => { if (e.key === 'Escape') onClose(); };
    document.addEventListener('keydown', handler);
    document.body.style.overflow = 'hidden';
    return () => { document.removeEventListener('keydown', handler); document.body.style.overflow = ''; };
  }, [open, onClose]);

  if (!open) return null;

  return (
    <div ref={overlayRef} className="fixed inset-0 z-50" role="dialog" aria-modal="true" aria-labelledby="drawer-title" onClick={(e) => { if (e.target === overlayRef.current) onClose(); }}>
      <div className="fixed inset-0 bg-black/30" aria-hidden="true" />
      <div className={`fixed top-0 right-0 h-full w-full ${WIDTH_MAP[width]} bg-white shadow-xl flex flex-col animate-in slide-in-from-right duration-200`}>
        <div className="flex items-center justify-between px-5 py-4 border-b border-gray-100">
          <div>
            <h2 id="drawer-title" className="text-base font-semibold text-gray-900">{title}</h2>
            {subtitle && <p className="text-xs text-gray-500 mt-0.5">{subtitle}</p>}
          </div>
          <button onClick={onClose} aria-label="Close drawer" className="p-1 rounded-lg hover:bg-gray-100 text-gray-400 hover:text-gray-600 transition-colors">
            <i className="ri-close-line text-xl" />
          </button>
        </div>
        <div className="flex-1 overflow-y-auto px-5 py-4">{children}</div>
        {footer && <div className="px-5 py-3 border-t border-gray-100 flex items-center justify-end gap-2">{footer}</div>}
      </div>
    </div>
  );
}
