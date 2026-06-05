'use client';

import { createContext, useCallback, useContext, useId, useRef, useState } from 'react';

export type ToastVariant = 'success' | 'error' | 'info';

export interface Toast {
  id:      string;
  message: string;
  variant: ToastVariant;
}

interface ToastContextValue {
  toasts:  Toast[];
  show:    (message: string, variant?: ToastVariant) => void;
  dismiss: (id: string) => void;
}

const ToastContext = createContext<ToastContextValue | null>(null);

export function ToastProvider({ children }: { children: React.ReactNode }) {
  const [toasts, setToasts] = useState<Toast[]>([]);
  const timers = useRef<Map<string, ReturnType<typeof setTimeout>>>(new Map());

  const dismiss = useCallback((id: string) => {
    clearTimeout(timers.current.get(id));
    timers.current.delete(id);
    setToasts(prev => prev.filter(t => t.id !== id));
  }, []);

  const show = useCallback((message: string, variant: ToastVariant = 'success') => {
    const id = `${Date.now()}-${Math.random()}`;
    setToasts(prev => [...prev, { id, message, variant }]);
    const timer = setTimeout(() => dismiss(id), 4000);
    timers.current.set(id, timer);
  }, [dismiss]);

  return (
    <ToastContext.Provider value={{ toasts, show, dismiss }}>
      {children}
    </ToastContext.Provider>
  );
}

export function useToast(): Pick<ToastContextValue, 'show'> {
  const ctx = useContext(ToastContext);
  if (!ctx) throw new Error('useToast must be used inside <ToastProvider>');
  return { show: ctx.show };
}

export function useToastState(): Omit<ToastContextValue, 'show'> {
  const ctx = useContext(ToastContext);
  if (!ctx) throw new Error('useToastState must be used inside <ToastProvider>');
  return { toasts: ctx.toasts, dismiss: ctx.dismiss };
}
