'use client';

import { useLienStore } from '@/stores/lien-store';

const ICONS: Record<string, string> = {
  success: 'ri-checkbox-circle-fill',
  error: 'ri-error-warning-fill',
  warning: 'ri-alert-fill',
  info: 'ri-information-fill',
};

const COLORS: Record<string, string> = {
  success: 'text-green-500',
  error: 'text-red-500',
  warning: 'text-amber-500',
  info: 'text-blue-500',
};

const BG: Record<string, string> = {
  success: 'border-green-200 bg-green-50',
  error: 'border-red-200 bg-red-50',
  warning: 'border-amber-200 bg-amber-50',
  info: 'border-blue-200 bg-blue-50',
};

export function ToastContainer() {
  const toasts = useLienStore((s) => s.toasts);
  const removeToast = useLienStore((s) => s.removeToast);

  if (toasts.length === 0) return null;

  return (
    <div className="fixed bottom-4 right-4 z-[60] flex flex-col gap-2 max-w-sm">
      {toasts.map((toast) => (
        <div key={toast.id} className={`flex items-start gap-3 px-4 py-3 border rounded-xl shadow-lg animate-in slide-in-from-right duration-300 ${BG[toast.type]}`}>
          <i className={`${ICONS[toast.type]} text-lg ${COLORS[toast.type]} mt-0.5`} />
          <div className="flex-1 min-w-0">
            <p className="text-sm font-medium text-gray-900">{toast.title}</p>
            {toast.description && <p className="text-xs text-gray-600 mt-0.5">{toast.description}</p>}
          </div>
          <button onClick={() => removeToast(toast.id)} className="text-gray-400 hover:text-gray-600 shrink-0">
            <i className="ri-close-line text-base" />
          </button>
        </div>
      ))}
    </div>
  );
}
