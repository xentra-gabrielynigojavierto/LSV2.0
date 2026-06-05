"use client";

import { useCallback, useEffect, useState } from "react";
import type { TaskItemStatus, TaskResponse } from "@/types/task";
import { buildTransitionActions } from "@/lib/transitionLabels";
import type { TransitionAction } from "@/lib/transitionLabels";
import { validateTransition, requiresConfirmation, getConfirmMessage, getRuleHintLabels } from "@/lib/transitionRules";
import { ConfirmDialog } from "@/components/ui/ConfirmDialog";

interface TransitionActionsProps {
  task: TaskResponse;
  allowedNextStatuses: TaskItemStatus[];
  workflowTransitionNames?: Map<TaskItemStatus, string>;
  onTransition: (status: TaskItemStatus) => Promise<void>;
  disabled?: boolean;
  error?: string | null;
  onErrorClear?: () => void;
}

const VARIANT_STYLES: Record<TransitionAction["variant"], string> = {
  primary: "bg-blue-600 text-white hover:bg-blue-700 disabled:bg-blue-300",
  success: "bg-emerald-600 text-white hover:bg-emerald-700 disabled:bg-emerald-300",
  warning: "bg-amber-500 text-white hover:bg-amber-600 disabled:bg-amber-300",
  danger: "bg-red-500 text-white hover:bg-red-600 disabled:bg-red-300",
  neutral: "bg-gray-100 text-gray-700 hover:bg-gray-200 disabled:bg-gray-50 disabled:text-gray-400",
};

export function TransitionActions({
  task,
  allowedNextStatuses,
  workflowTransitionNames,
  onTransition,
  disabled,
  error,
  onErrorClear,
}: TransitionActionsProps) {
  const [activeTransition, setActiveTransition] = useState<TaskItemStatus | null>(null);
  const [validationErrors, setValidationErrors] = useState<string[]>([]);
  const [confirmTarget, setConfirmTarget] = useState<TaskItemStatus | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);

  const actions = buildTransitionActions(task.status, allowedNextStatuses, workflowTransitionNames);

  useEffect(() => {
    if (!successMessage) return;
    const timer = setTimeout(() => setSuccessMessage(null), 3000);
    return () => clearTimeout(timer);
  }, [successMessage]);

  useEffect(() => {
    if (validationErrors.length === 0) return;
    const timer = setTimeout(() => setValidationErrors([]), 5000);
    return () => clearTimeout(timer);
  }, [validationErrors]);

  const attemptTransition = useCallback(async (targetStatus: TaskItemStatus) => {
    setValidationErrors([]);
    onErrorClear?.();

    const validation = validateTransition(task, targetStatus);
    if (!validation.valid) {
      setValidationErrors(validation.errors);
      return;
    }

    if (requiresConfirmation(targetStatus, task.allowedTransitionRules)) {
      setConfirmTarget(targetStatus);
      return;
    }

    await executeTransition(targetStatus);
  }, [task, onErrorClear]);

  const executeTransition = async (targetStatus: TaskItemStatus) => {
    setActiveTransition(targetStatus);
    setConfirmTarget(null);
    try {
      await onTransition(targetStatus);
      const action = actions.find((a) => a.targetStatus === targetStatus);
      setSuccessMessage(action?.label ? `${action.label} — done` : "Transition complete");
    } catch {
      // error handled by parent via error prop
    } finally {
      setActiveTransition(null);
    }
  };

  if (actions.length === 0) {
    return (
      <div>
        <p className="text-sm text-gray-400 italic">No transitions available</p>
      </div>
    );
  }

  return (
    <div>
      {error && (
        <div className="mb-2 flex items-start gap-2 rounded-lg bg-red-50 border border-red-200 px-3 py-2">
          <svg className="h-4 w-4 text-red-500 mt-0.5 flex-shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24" strokeWidth={2}>
            <path strokeLinecap="round" strokeLinejoin="round" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-2.5L13.732 4c-.77-.833-1.964-.833-2.732 0L4.082 16.5c-.77.833.192 2.5 1.732 2.5z" />
          </svg>
          <p className="text-sm text-red-700">{error}</p>
        </div>
      )}

      {validationErrors.length > 0 && (
        <div className="mb-2 rounded-lg bg-amber-50 border border-amber-200 px-3 py-2">
          <div className="flex items-start gap-2">
            <svg className="h-4 w-4 text-amber-500 mt-0.5 flex-shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24" strokeWidth={2}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-2.5L13.732 4c-.77-.833-1.964-.833-2.732 0L4.082 16.5c-.77.833.192 2.5 1.732 2.5z" />
            </svg>
            <div>
              {validationErrors.map((err, i) => (
                <p key={i} className="text-sm text-amber-700">{err}</p>
              ))}
            </div>
          </div>
        </div>
      )}

      {successMessage && (
        <div className="mb-2 flex items-center gap-2 rounded-lg bg-emerald-50 border border-emerald-200 px-3 py-2">
          <svg className="h-4 w-4 text-emerald-500 flex-shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24" strokeWidth={2}>
            <path strokeLinecap="round" strokeLinejoin="round" d="M5 13l4 4L19 7" />
          </svg>
          <p className="text-sm text-emerald-700">{successMessage}</p>
        </div>
      )}

      <div className="flex flex-wrap gap-2">
        {actions.map((action) => {
          const hints = getRuleHintLabels(action.targetStatus, task.allowedTransitionRules);
          return (
            <div key={action.targetStatus} className="flex flex-col items-start">
              <button
                onClick={() => attemptTransition(action.targetStatus)}
                disabled={disabled || activeTransition !== null}
                className={`inline-flex items-center gap-1.5 rounded-lg px-3 py-2 text-sm font-medium transition-colors ${VARIANT_STYLES[action.variant]}`}
              >
                {activeTransition === action.targetStatus ? (
                  <div className="h-3.5 w-3.5 animate-spin rounded-full border-2 border-current border-t-transparent" />
                ) : (
                  <svg className="h-3.5 w-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24" strokeWidth={2}>
                    <path strokeLinecap="round" strokeLinejoin="round" d={action.icon} />
                  </svg>
                )}
                {action.label}
              </button>
              {hints.length > 0 && (
                <p className="mt-0.5 text-[10px] text-gray-400 leading-tight px-1">
                  {hints.join(" · ")}
                </p>
              )}
            </div>
          );
        })}
      </div>

      {activeTransition && (
        <p className="mt-1.5 text-xs text-gray-400">Processing transition...</p>
      )}

      {confirmTarget && (
        <ConfirmDialog
          open
          title="Confirm transition"
          message={getConfirmMessage(confirmTarget, task.allowedTransitionRules)}
          confirmLabel={actions.find((a) => a.targetStatus === confirmTarget)?.label ?? "Confirm"}
          variant={confirmTarget === "Cancelled" ? "danger" : "default"}
          loading={activeTransition !== null}
          onConfirm={() => executeTransition(confirmTarget)}
          onCancel={() => setConfirmTarget(null)}
        />
      )}
    </div>
  );
}
