'use client';

/**
 * E8.4 — compact two-step confirmation to complete the active workflow.
 *
 * No payload is required by the backend; the component surfaces a small
 * confirmation row (Confirm / Cancel) and an inline error on failure.
 */
import { useState } from 'react';
import { useCompleteCaseWorkflow } from '@/hooks/use-case-workflows';
import {
  workflowApi,
  type WorkflowApiAdapter,
  type WorkflowInstanceDetail,
} from '@/lib/workflow';
import { WorkflowActionError } from './workflow-action-error';

interface Props {
  caseId: string;
  workflowInstanceId: string;
  /** Current step shown in the confirmation copy. */
  currentStepKey?: string | null;
  /** Called after a successful completion with the updated detail. */
  onCompleted?: (detail: WorkflowInstanceDetail) => void | Promise<void>;
  api?: WorkflowApiAdapter;
}

export function CompleteWorkflowAction({
  caseId,
  workflowInstanceId,
  currentStepKey,
  onCompleted,
  api = workflowApi,
}: Props) {
  const [confirming, setConfirming] = useState(false);

  const { submitting, error, submit, reset } = useCompleteCaseWorkflow(
    caseId,
    workflowInstanceId,
    onCompleted,
    api,
  );

  if (!confirming) {
    return (
      <button
        type="button"
        onClick={() => setConfirming(true)}
        className="inline-flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium rounded-md border border-emerald-300 text-emerald-700 hover:bg-emerald-50 transition-colors"
      >
        <i className="ri-check-double-line text-sm" />
        Complete workflow
      </button>
    );
  }

  return (
    <div className="space-y-2 border border-emerald-200 rounded-md p-2.5 bg-emerald-50/50">
      <p className="text-xs text-gray-700">
        Mark this workflow as <span className="font-medium text-emerald-700">Completed</span>?
        {currentStepKey && (
          <> Current step: <span className="font-mono text-gray-900">{currentStepKey}</span>.</>
        )}
      </p>

      {error && <WorkflowActionError error={error} action="complete" onDismiss={reset} />}

      <div className="flex items-center gap-2">
        <button
          type="button"
          disabled={submitting}
          onClick={async () => {
            const detail = await submit();
            if (detail) setConfirming(false);
          }}
          className="inline-flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium rounded-md bg-emerald-600 text-white hover:bg-emerald-700 transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
        >
          {submitting ? (<><i className="ri-loader-4-line animate-spin" />Completing…</>) : 'Confirm complete'}
        </button>
        <button
          type="button"
          onClick={() => { setConfirming(false); reset(); }}
          className="px-3 py-1.5 text-xs font-medium rounded-md text-gray-600 hover:bg-gray-100 transition-colors"
        >
          Cancel
        </button>
      </div>
    </div>
  );
}
