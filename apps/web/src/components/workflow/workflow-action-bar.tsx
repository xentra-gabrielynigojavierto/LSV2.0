'use client';

/**
 * E8.4 — orchestrates the progression actions (Advance / Complete) for an
 * active workflow row. Product-agnostic: rendered by `WorkflowPanel` once
 * the parent has decided the user can act and the workflow is non-terminal.
 *
 * Returns `null` for terminal statuses so callers can drop it in
 * unconditionally without re-checking status — keeping the panel layout
 * code short.
 */
import {
  isTerminal,
  type ProductWorkflowRow,
  type WorkflowApiAdapter,
  type WorkflowInstanceDetail,
} from '@/lib/workflow';
import { AdvanceWorkflowAction } from './advance-workflow-action';
import { CompleteWorkflowAction } from './complete-workflow-action';

interface Props {
  caseId: string;
  row: ProductWorkflowRow;
  detail?: WorkflowInstanceDetail | null;
  canAdvance: boolean;
  canComplete: boolean;
  /** Called after either action succeeds; typically refreshes the panel. */
  onMutated?: (detail: WorkflowInstanceDetail) => void | Promise<void>;
  api?: WorkflowApiAdapter;
}

export function WorkflowActionBar({
  caseId,
  row,
  detail,
  canAdvance,
  canComplete,
  onMutated,
  api,
}: Props) {
  // Terminal workflows expose no progression actions.
  if (isTerminal(row.status)) return null;
  // No workflow-instance id ⇒ row hasn't materialised an instance yet, so
  // there's nothing the execution endpoints can act on.
  const instanceId = row.workflowInstanceId ?? null;
  if (!instanceId) return null;

  const showAdvance  = canAdvance;
  const showComplete = canComplete;
  if (!showAdvance && !showComplete) return null;

  const currentStepKey = detail?.currentStepKey ?? null;

  return (
    <div className="space-y-2">
      {showAdvance && (
        <AdvanceWorkflowAction
          caseId={caseId}
          workflowInstanceId={instanceId}
          currentStepKey={currentStepKey}
          onAdvanced={onMutated}
          api={api}
        />
      )}
      {showComplete && (
        <CompleteWorkflowAction
          caseId={caseId}
          workflowInstanceId={instanceId}
          currentStepKey={currentStepKey}
          onCompleted={onMutated}
          api={api}
        />
      )}
    </div>
  );
}
