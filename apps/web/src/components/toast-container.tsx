'use client';

import { useToastState } from '@/lib/toast-context';

const ICON: Record<string, string> = {
  success: 'ri-checkbox-circle-fill',
  error:   'ri-error-warning-fill',
  info:    'ri-information-fill',
};

const COLOR: Record<string, string> = {
  success: 'bg-green-50 border-green-200 text-green-800',
  error:   'bg-red-50 border-red-200 text-red-800',
  info:    'bg-blue-50 border-blue-200 text-blue-800',
};

const ICON_COLOR: Record<string, string> = {
  success: 'text-green-500',
  error:   'text-red-500',
  info:    'text-blue-500',
};

export function ToastContainer() {
  const { toasts, dismiss } = useToastState();

  if (toasts.length === 0) return null;

  return (
    <div
      aria-live="polite"
      aria-atomic="false"
      className="fixed bottom-5 right-5 z-[200] flex flex-col gap-2 max-w-sm w-full"
    >
      {toasts.map(toast => (
        <div
          key={toast.id}
          role="alert"
          className={`flex items-start gap-3 rounded-lg border px-4 py-3 shadow-lg animate-slide-in ${COLOR[toast.variant]}`}
        >
          <span className={`${ICON[toast.variant]} text-base mt-0.5 shrink-0 ${ICON_COLOR[toast.variant]}`} />
          <p className="flex-1 text-sm leading-snug">{toast.message}</p>
          <button
            onClick={() => dismiss(toast.id)}
            aria-label="Dismiss"
            className="shrink-0 opacity-60 hover:opacity-100 transition-opacity ml-1"
          >
            <span className="ri-close-line text-base" />
          </button>
        </div>
      ))}
    </div>
  );
}
