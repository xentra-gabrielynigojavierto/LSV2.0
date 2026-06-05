'use client';

/**
 * E8.4 — inline expander to advance the active workflow one step.
 *
 * Submits the minimum payload required by the backend
 * (`expectedCurrentStepKey` + optional `toStepKey`). The
 * `expectedCurrentStepKey` value is sourced from the parent's loaded
 * `WorkflowInstanceDetail`; if not yet known the action is disabled.
 */
import { useState } from 'react';
import { useAdvanceCaseWorkflow } from '@/hooks/use-case-workflows';
import {
  workflowApi,
  type WorkflowApiAdapter,
  type WorkflowInstanceDetail,
} from '@/lib/workflow';
import { WorkflowActionError } from './workflow-action-error';

interface Props {
  caseId: string;
  workflowInstanceId: string;
  /** Current step key from the loaded detail; null disables the action. */
  currentStepKey: string | null;
  /** Called after a successful advance with the updated detail. */
  onAdvanced?: (detail: WorkflowInstanceDetail) => void | Promise<void>;
  api?: WorkflowApiAdapter;
}

export function AdvanceWorkflowAction({
  caseId,
  workflowInstanceId,
  currentStepKey,
  onAdvanced,
  api = workflowApi,
}: Props) {
  const [open, setOpen] = useState(false);
  const [toStepKey, setToStepKey] = useState('');

  const { submitting, error, submit, reset } = useAdvanceCaseWorkflow(
    caseId,
    workflowInstanceId,
    onAdvanced,
    api,
  );

  const stepKnown = !!currentStepKey;

  if (!open) {
    return (
      <button
        type="button"
        onClick={() => setOpen(true)}
        disabled={!stepKnown}
        title={stepKnown ? undefined : 'Loading current step…'}
        className="inline-flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium rounded-md bg-primary text-white hover:bg-primary/90 transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
      >
        <i className="ri-arrow-right-line text-sm" />
        Advance step
      </button>
    );
  }

  const canSubmit = stepKnown && !submitting;

  return (
    <form
      className="space-y-2 border border-gray-200 rounded-md p-2.5 bg-gray-50"
      onSubmit={async (e) => {
        e.preventDefault();
        if (!canSubmit || !currentStepKey) return;
        const detail = await submit({
          expectedCurrentStepKey: currentStepKey,
          toStepKey: toStepKey.trim() ? toStepKey.trim() : undefined,
        });
        if (detail) {
          setOpen(false);
          setToStepKey('');
        }
      }}
    >
      <div className="text-xs text-gray-700">
        Advance from{' '}
        <span className="font-mono text-gray-900">{currentStepKey ?? '—'}</span>
        {' '}to the next step.
      </div>

      <div>
        <label className="block text-xs font-medium text-gray-600 mb-1">
          Target step (optional)
        </label>
        <input
          type="text"
          value={toStepKey}
          onChange={(e) => setToStepKey(e.target.value)}
          placeholder="Leave blank to follow the workflow definition"
          maxLength={120}
          className="w-full text-xs border border-gray-200 rounded px-2 py-1.5 bg-white"
        />
      </div>

      {error && <WorkflowActionError error={error} action="advance" onDismiss={reset} />}

      <div className="flex items-center gap-2 pt-1">
        <button
          type="submit"
          disabled={!canSubmit}
          className="inline-flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium rounded-md bg-primary text-white hover:bg-primary/90 transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
        >
          {submitting ? (<><i className="ri-loader-4-line animate-spin" />Advancing…</>) : 'Confirm advance'}
        </button>
        <button
          type="button"
          onClick={() => { setOpen(false); reset(); setToStepKey(''); }}
          className="px-3 py-1.5 text-xs font-medium rounded-md text-gray-600 hover:bg-gray-100 transition-colors"
        >
          Cancel
        </button>
      </div>
    </form>
  );
}
