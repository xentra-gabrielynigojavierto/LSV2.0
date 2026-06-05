'use client';

/**
 * ConfirmDialog — accessible modal confirmation dialog.
 *
 * Used for destructive or irreversible actions (suspend, deactivate, lock)
 * to give the admin a chance to confirm before the action is executed.
 *
 * ── Accessibility ─────────────────────────────────────────────────────────────
 *
 *   - role="dialog" + aria-modal="true" announces the dialog to screen readers
 *   - aria-labelledby points to the title element
 *   - aria-describedby points to the description element (when present)
 *   - Cancel button receives focus on mount — safer default for destructive actions
 *     (the admin must actively move to Confirm, reducing accidental confirmations)
 *   - Escape key closes the dialog (unless an action is pending)
 *   - Backdrop click closes the dialog (unless an action is pending)
 *   - All interactive elements have visible focus-visible rings
 *
 * ── Usage ─────────────────────────────────────────────────────────────────────
 *
 *   const [confirming, setConfirming] = useState(false);
 *
 *   {confirming && (
 *     <ConfirmDialog
 *       title="Suspend tenant?"
 *       description="The tenant will lose access immediately."
 *       confirmLabel="Suspend"
 *       variant="danger"
 *       onConfirm={() => { handleSuspend(); setConfirming(false); }}
 *       onCancel={() => setConfirming(false)}
 *     />
 *   )}
 */

import { useEffect, useRef, useId } from 'react';

export interface ConfirmDialogProps {
  /** Short, imperative title. E.g. "Suspend tenant?" */
  title:         string;
  /** Optional longer explanation shown below the title. */
  description?:  string;
  /** Label on the confirm button. Default: "Confirm" */
  confirmLabel?: string;
  /** Label on the cancel button. Default: "Cancel" */
  cancelLabel?:  string;
  /**
   * Controls the colour of the confirm button:
   *   danger  — red  (destructive: suspend, delete, lock)
   *   warning — amber (caution: deactivate, reset)
   *   neutral — indigo (non-destructive confirm)
   * Default: 'neutral'
   */
  variant?:     'danger' | 'warning' | 'neutral';
  /** When true, both buttons are disabled and the confirm button shows a spinner. */
  isPending?:   boolean;
  /** Called when the admin clicks Confirm (or presses Enter while focused on it). */
  onConfirm:    () => void;
  /** Called when the admin clicks Cancel, clicks the backdrop, or presses Escape. */
  onCancel:     () => void;
}

export function ConfirmDialog({
  title,
  description,
  confirmLabel = 'Confirm',
  cancelLabel  = 'Cancel',
  variant      = 'neutral',
  isPending    = false,
  onConfirm,
  onCancel,
}: ConfirmDialogProps) {
  const titleId       = useId();
  const descId        = useId();
  const cancelRef     = useRef<HTMLButtonElement>(null);

  // Auto-focus the cancel button on mount.
  // This ensures the admin must deliberately move focus to Confirm,
  // reducing accidental confirmations for destructive actions.
  useEffect(() => {
    cancelRef.current?.focus();
  }, []);

  // Close on Escape (guarded: don't close while action is in flight)
  useEffect(() => {
    function handleKey(e: KeyboardEvent) {
      if (e.key === 'Escape' && !isPending) onCancel();
    }
    document.addEventListener('keydown', handleKey);
    return () => document.removeEventListener('keydown', handleKey);
  }, [onCancel, isPending]);

  const confirmBtnStyles: Record<'danger' | 'warning' | 'neutral', string> = {
    danger:  'bg-red-600 text-white hover:bg-red-700 focus-visible:ring-red-500',
    warning: 'bg-amber-600 text-white hover:bg-amber-700 focus-visible:ring-amber-500',
    neutral: 'bg-indigo-600 text-white hover:bg-indigo-700 focus-visible:ring-indigo-500',
  };

  return (
    /* Full-screen overlay */
    <div
      className="fixed inset-0 z-50 flex items-center justify-center"
      aria-hidden={false}
    >
      {/* Backdrop */}
      <div
        className="absolute inset-0 bg-black/40 backdrop-blur-[2px] transition-opacity"
        onClick={() => !isPending && onCancel()}
        aria-hidden="true"
      />

      {/* Dialog panel */}
      <div
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        aria-describedby={description ? descId : undefined}
        className="relative z-10 w-full max-w-sm mx-4 bg-white rounded-xl shadow-xl border border-gray-200 p-6"
      >
        {/* Title */}
        <h2
          id={titleId}
          className="text-sm font-semibold text-gray-900 leading-snug"
        >
          {title}
        </h2>

        {/* Description */}
        {description && (
          <p
            id={descId}
            className="mt-1.5 text-xs text-gray-500 leading-relaxed"
          >
            {description}
          </p>
        )}

        {/* Buttons */}
        <div className="mt-5 flex items-center justify-end gap-2">

          {/* Cancel */}
          <button
            ref={cancelRef}
            type="button"
            disabled={isPending}
            onClick={onCancel}
            className={[
              'px-3 py-1.5 text-sm font-medium rounded-md transition-colors',
              'text-gray-700 bg-white border border-gray-300 hover:bg-gray-50',
              'focus:outline-none focus-visible:ring-2 focus-visible:ring-gray-400 focus-visible:ring-offset-1',
              'disabled:opacity-50 disabled:cursor-not-allowed',
            ].join(' ')}
          >
            {cancelLabel}
          </button>

          {/* Confirm */}
          <button
            type="button"
            disabled={isPending}
            onClick={onConfirm}
            className={[
              'px-3 py-1.5 text-sm font-medium rounded-md transition-colors',
              'focus:outline-none focus-visible:ring-2 focus-visible:ring-offset-1',
              'disabled:opacity-50 disabled:cursor-not-allowed',
              confirmBtnStyles[variant],
            ].join(' ')}
          >
            {isPending ? (
              <span className="flex items-center gap-1.5">
                <span
                  aria-hidden="true"
                  className="h-3.5 w-3.5 rounded-full border-2 border-white/60 border-t-transparent animate-spin"
                />
                Processing…
              </span>
            ) : (
              confirmLabel
            )}
          </button>

        </div>
      </div>
    </div>
  );
}
